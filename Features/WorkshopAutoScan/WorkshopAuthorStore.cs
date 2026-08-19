using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using Newtonsoft.Json;

namespace QoLiTea.Features.WorkshopAutoScan;

/// <summary>
/// Persist Workshop AutoScan auto-subscribe author OwnerIds.
/// </summary>
public static class WorkshopAuthorStore
{
    private static readonly object Gate = new object();
    private static WorkshopAuthorsDto _dto;

    public static string StorePath =>
        Path.Combine(Paths.ConfigPath, MyPluginInfo.PLUGIN_GUID, "workshop-autoscan-authors.json");

    public static void LoadFromDisk()
    {
        lock (Gate)
        {
            _dto = null;
            EnsureLoaded();
        }
    }

    public static List<ulong> SnapshotOwnerIds()
    {
        lock (Gate)
        {
            EnsureLoaded();
            return new List<ulong>(_dto.OwnerIds ?? new List<ulong>());
        }
    }

    public static bool Contains(ulong ownerId)
    {
        if (ownerId == 0)
            return false;
        lock (Gate)
        {
            EnsureLoaded();
            return _dto.OwnerIds != null && _dto.OwnerIds.Contains(ownerId);
        }
    }

    public static void AddOwnerAndSave(ulong ownerId)
    {
        if (ownerId == 0)
            return;

        lock (Gate)
        {
            EnsureLoaded();
            _dto.OwnerIds ??= new List<ulong>();
            if (!_dto.OwnerIds.Contains(ownerId))
                _dto.OwnerIds.Add(ownerId);

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
                Plugin.Logger?.LogError($"WorkshopAuthorStore: save failed: {e}");
            }
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
                _dto = new WorkshopAuthorsDto();
                return;
            }

            string json = File.ReadAllText(path, Encoding.UTF8);
            _dto = JsonConvert.DeserializeObject<WorkshopAuthorsDto>(json) ?? new WorkshopAuthorsDto();
            _dto.OwnerIds ??= new List<ulong>();
            Plugin.Logger?.LogInfo(
                $"WorkshopAuthorStore: loaded {_dto.OwnerIds.Count} auto-sub author(s)");
        }
        catch (Exception e)
        {
            Plugin.Logger?.LogError($"WorkshopAuthorStore: load failed: {e}");
            _dto = new WorkshopAuthorsDto();
        }
    }

    private sealed class WorkshopAuthorsDto
    {
        [JsonProperty("version")]
        public int Version { get; set; } = 1;

        [JsonProperty("ownerIds")]
        public List<ulong> OwnerIds { get; set; } = new List<ulong>();
    }
}
