using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using Newtonsoft.Json;
using Shared.TrackData;
using Shared.UGC.Local;
using UnityEngine;

namespace QoLiTea.Features.LazyCustomTracks;

/// <summary>
/// Memory + disk display cache for Custom Music track lists.
/// </summary>
public static class TrackListCache
{
    private static List<ITrackMetadata> _tracks;
    private static string _fingerprint;
    private static readonly object Gate = new object();

    public static string CachePath =>
        Path.Combine(Paths.ConfigPath, MyPluginInfo.PLUGIN_GUID, "track-list-cache.json");

    public static bool HasTracks
    {
        get
        {
            lock (Gate)
                return _tracks != null && _tracks.Count > 0;
        }
    }

    public static void LoadFromDisk()
    {
        lock (Gate)
        {
            try
            {
                string path = CachePath;
                if (!File.Exists(path))
                {
                    Plugin.Logger?.LogInfo("TrackListCache: no disk cache yet");
                    return;
                }

                string json = File.ReadAllText(path, Encoding.UTF8);
                var dtos = JsonConvert.DeserializeObject<List<CachedTrackDto>>(json);
                if (dtos == null)
                {
                    Plugin.Logger?.LogWarning("TrackListCache: disk cache empty/null");
                    return;
                }

                _tracks = new List<ITrackMetadata>(dtos.Count);
                foreach (var dto in dtos)
                {
                    if (dto == null || string.IsNullOrEmpty(dto.LevelId))
                        continue;
                    _tracks.Add(new CachedTrackMetadata(dto));
                }

                _fingerprint = ComputeLocalFingerprint();
                Plugin.Logger?.LogInfo($"TrackListCache: loaded {_tracks.Count} tracks from disk");
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogError($"TrackListCache: failed to load disk cache: {e}");
                _tracks = null;
            }
        }
    }

    public static void SaveToDisk()
    {
        lock (Gate)
        {
            try
            {
                if (_tracks == null)
                    return;

                string path = CachePath;
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var dtos = new List<CachedTrackDto>(_tracks.Count);
                foreach (var track in _tracks)
                {
                    if (track == null || string.IsNullOrEmpty(track.LevelId))
                        continue;
                    if (track is CachedTrackMetadata stub)
                        dtos.Add(stub.Dto);
                    else
                        dtos.Add(CachedTrackDto.FromMetadata(track));
                }

                string json = JsonConvert.SerializeObject(dtos, Formatting.Indented);
                File.WriteAllText(path, json, Encoding.UTF8);
                Plugin.Logger?.LogInfo($"TrackListCache: saved {dtos.Count} tracks to disk");
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogError($"TrackListCache: failed to save disk cache: {e}");
            }
        }
    }

    public static bool TryGet(out List<ITrackMetadata> tracks)
    {
        lock (Gate)
        {
            if (_tracks == null)
            {
                tracks = null;
                return false;
            }

            tracks = new List<ITrackMetadata>(_tracks);
            return true;
        }
    }

    public static List<ITrackMetadata> Snapshot()
    {
        lock (Gate)
            return _tracks != null ? new List<ITrackMetadata>(_tracks) : new List<ITrackMetadata>();
    }

    public static void Store(List<ITrackMetadata> tracks, bool saveDisk = true)
    {
        lock (Gate)
        {
            _tracks = tracks != null
                ? new List<ITrackMetadata>(tracks)
                : new List<ITrackMetadata>();
            _fingerprint = ComputeLocalFingerprint();
            Plugin.Logger?.LogInfo($"TrackListCache: stored {_tracks.Count} tracks (fp={_fingerprint})");
        }

        if (saveDisk)
            SaveToDisk();
    }

    public static void Upsert(ITrackMetadata track, bool saveDisk = true)
    {
        if (track == null || string.IsNullOrEmpty(track.LevelId))
            return;

        lock (Gate)
        {
            if (_tracks == null)
                _tracks = new List<ITrackMetadata>();

            int idx = _tracks.FindIndex(t => t != null && t.LevelId == track.LevelId);
            if (idx >= 0)
                _tracks[idx] = track;
            else
                _tracks.Add(track);

            _fingerprint = ComputeLocalFingerprint();
        }

        if (saveDisk)
            SaveToDisk();
    }

    public static bool Remove(string levelId, bool saveDisk = true)
    {
        if (string.IsNullOrEmpty(levelId))
            return false;

        bool removed;
        lock (Gate)
        {
            if (_tracks == null)
                return false;

            removed = _tracks.RemoveAll(t => t != null && t.LevelId == levelId) > 0;
            if (removed)
                _fingerprint = ComputeLocalFingerprint();
        }

        if (removed && saveDisk)
            SaveToDisk();
        return removed;
    }

    public static bool DivergesFrom(List<ITrackMetadata> other)
    {
        lock (Gate)
        {
            var a = Signature(_tracks);
            var b = Signature(other);
            return !string.Equals(a, b, StringComparison.Ordinal);
        }
    }

    private static string Signature(List<ITrackMetadata> tracks)
    {
        if (tracks == null || tracks.Count == 0)
            return "";

        return string.Join("\n", tracks
            .Where(t => t != null && !string.IsNullOrEmpty(t.LevelId))
            .OrderBy(t => t.LevelId, StringComparer.Ordinal)
            .Select(t => $"{t.LevelId}|{t.TrackName}|{t.TimeAdded}"));
    }

    public static string ComputeLocalFingerprint()
    {
        string basePath = null;
        try
        {
            basePath = LocalUgcTrackProvider.BasePath;
        }
        catch
        {
            // Provider may not exist yet during early boot.
        }

        if (string.IsNullOrEmpty(basePath))
            basePath = Path.Combine(Application.persistentDataPath, "CustomTracks");

        if (!Directory.Exists(basePath))
            return "missing:0:0";

        var dirs = Directory.GetDirectories(basePath);
        long maxTicks = 0;
        foreach (string dir in dirs)
        {
            try
            {
                long ticks = Directory.GetLastWriteTimeUtc(dir).Ticks;
                if (ticks > maxTicks)
                    maxTicks = ticks;
            }
            catch
            {
                // ignore unreadable dirs
            }
        }

        long rootTicks = Directory.GetLastWriteTimeUtc(basePath).Ticks;
        if (rootTicks > maxTicks)
            maxTicks = rootTicks;

        return $"{basePath}:{dirs.Length}:{maxTicks}";
    }

    public static string CurrentLocalFingerprint
    {
        get
        {
            lock (Gate)
                return _fingerprint;
        }
    }

    /// <summary>Wipe memory cache and delete the on-disk JSON if present.</summary>
    public static void Clear()
    {
        lock (Gate)
        {
            _tracks = new List<ITrackMetadata>();
            _fingerprint = ComputeLocalFingerprint();
        }

        try
        {
            string path = CachePath;
            if (File.Exists(path))
            {
                File.Delete(path);
                Plugin.Logger?.LogInfo("TrackListCache: deleted disk cache");
            }
        }
        catch (Exception e)
        {
            Plugin.Logger?.LogError($"TrackListCache: failed to delete disk cache: {e}");
        }
    }
}
