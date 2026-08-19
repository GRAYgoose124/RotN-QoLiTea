using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using Newtonsoft.Json;

namespace QoLiTea.Features.TrackSets;

public sealed class TrackSetRecord
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("createdUnix")]
    public long CreatedUnix { get; set; }

    [JsonProperty("fileIds")]
    public List<ulong> FileIds { get; set; } = new List<ulong>();
}

/// <summary>Persisted unsub archives for Set Subscriber.</summary>
public static class TrackSetStore
{
    private static readonly object Gate = new object();
    private static TrackSetsDto _dto;

    public static string StorePath =>
        Path.Combine(Paths.ConfigPath, MyPluginInfo.PLUGIN_GUID, "track-sets.json");

    public static void LoadFromDisk()
    {
        lock (Gate)
        {
            _dto = null;
            EnsureLoaded();
        }
    }

    public static List<TrackSetRecord> SnapshotSetsNewestFirst()
    {
        lock (Gate)
        {
            EnsureLoaded();
            return (_dto.Sets ?? new List<TrackSetRecord>())
                .OrderByDescending(s => s.CreatedUnix)
                .ThenByDescending(s => s.Name, StringComparer.Ordinal)
                .ToList();
        }
    }

    public static List<string> SnapshotNames()
    {
        lock (Gate)
        {
            EnsureLoaded();
            return (_dto.Sets ?? new List<TrackSetRecord>())
                .Where(s => s != null && !string.IsNullOrEmpty(s.Name))
                .Select(s => s.Name)
                .ToList();
        }
    }

    public static TrackSetRecord AppendSet(string name, IEnumerable<ulong> fileIds)
    {
        lock (Gate)
        {
            EnsureLoaded();
            _dto.Sets ??= new List<TrackSetRecord>();

            string unique = TrackSetName.EnsureUnique(name, SnapshotNamesUnlocked());
            var ids = new List<ulong>();
            var seen = new HashSet<ulong>();
            if (fileIds != null)
            {
                foreach (ulong id in fileIds)
                {
                    if (id == 0 || !seen.Add(id))
                        continue;
                    ids.Add(id);
                }
            }

            var record = new TrackSetRecord
            {
                Name = unique,
                CreatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                FileIds = ids,
            };
            _dto.Sets.Add(record);
            PersistUnlocked();
            return record;
        }
    }

    private static List<string> SnapshotNamesUnlocked()
    {
        return (_dto.Sets ?? new List<TrackSetRecord>())
            .Where(s => s != null && !string.IsNullOrEmpty(s.Name))
            .Select(s => s.Name)
            .ToList();
    }

    private static void PersistUnlocked()
    {
        try
        {
            string path = StorePath;
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            string json = JsonConvert.SerializeObject(_dto, Formatting.Indented);
            File.WriteAllText(path, json, Encoding.UTF8);
        }
        catch (Exception e)
        {
            Plugin.Logger?.LogError($"TrackSetStore: save failed: {e}");
        }
    }

    private static void EnsureLoaded()
    {
        if (_dto != null)
            return;

        try
        {
            string path = StorePath;
            if (!File.Exists(path))
            {
                _dto = new TrackSetsDto();
                Plugin.Logger?.LogInfo("TrackSetStore: no sets file yet");
                return;
            }

            string json = File.ReadAllText(path, Encoding.UTF8);
            _dto = JsonConvert.DeserializeObject<TrackSetsDto>(json) ?? new TrackSetsDto();
            _dto.Sets ??= new List<TrackSetRecord>();
            Plugin.Logger?.LogInfo($"TrackSetStore: loaded {_dto.Sets.Count} set(s)");
        }
        catch (Exception e)
        {
            Plugin.Logger?.LogError($"TrackSetStore: load failed: {e}");
            _dto = new TrackSetsDto();
        }
    }

    private sealed class TrackSetsDto
    {
        [JsonProperty("version")]
        public int Version { get; set; } = 1;

        [JsonProperty("sets")]
        public List<TrackSetRecord> Sets { get; set; } = new List<TrackSetRecord>();
    }
}
