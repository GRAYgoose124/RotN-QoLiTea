using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using Newtonsoft.Json;

namespace QoLiTea.Features.WorkshopAutoScan;

/// <summary>
/// Disk persistence for Workshop AutoScan seen IDs / first-run flag.
/// </summary>
public static class WorkshopSeenStore
{
    private static readonly object Gate = new object();
    private static WorkshopSeenDto _dto;

    public static string StorePath =>
        Path.Combine(Paths.ConfigPath, MyPluginInfo.PLUGIN_GUID, "workshop-autoscan-seen.json");

    public static bool Initialized
    {
        get
        {
            lock (Gate)
                return _dto != null && _dto.Initialized;
        }
    }

    public static List<ulong> SnapshotSeenFileIds()
    {
        lock (Gate)
        {
            EnsureLoaded();
            return new List<ulong>(_dto.SeenFileIds ?? new List<ulong>());
        }
    }

    public static void LoadFromDisk()
    {
        lock (Gate)
        {
            _dto = null;
            EnsureLoaded();
        }
    }

    public static void Save(
        bool initialized,
        IEnumerable<ulong> seenFileIds)
    {
        lock (Gate)
        {
            var trimmed = WorkshopAutoScanPolicy.TrimSeen(
                seenFileIds,
                WorkshopAutoScanPolicy.MaxSeenIds);

            _dto = new WorkshopSeenDto
            {
                Version = 1,
                Initialized = initialized,
                SeenFileIds = trimmed,
            };

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
                Plugin.Logger?.LogError($"WorkshopSeenStore: save failed: {e}");
            }
        }
    }

    public static void MarkTouchedAndSave(IEnumerable<ulong> touchedFileIds)
    {
        lock (Gate)
        {
            EnsureLoaded();
            var merged = WorkshopAutoScanPolicy.MarkSeen(_dto.SeenFileIds, touchedFileIds);
            var trimmed = WorkshopAutoScanPolicy.TrimSeen(merged, WorkshopAutoScanPolicy.MaxSeenIds);
            _dto.SeenFileIds = trimmed;
            _dto.Initialized = true;
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
                Plugin.Logger?.LogError($"WorkshopSeenStore: mark-save failed: {e}");
            }
        }
    }

    /// <summary>Mark store initialized without adding ids (close with nothing touched).</summary>
    public static void EnsureInitialized()
    {
        lock (Gate)
        {
            EnsureLoaded();
            if (_dto.Initialized)
                return;
            _dto.Initialized = true;
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
                Plugin.Logger?.LogError($"WorkshopSeenStore: ensure-init failed: {e}");
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
                _dto = new WorkshopSeenDto();
                Plugin.Logger?.LogInfo("WorkshopSeenStore: no seen file yet");
                return;
            }

            string json = File.ReadAllText(path, Encoding.UTF8);
            _dto = JsonConvert.DeserializeObject<WorkshopSeenDto>(json) ?? new WorkshopSeenDto();
            _dto.SeenFileIds ??= new List<ulong>();
            Plugin.Logger?.LogInfo(
                $"WorkshopSeenStore: loaded {_dto.SeenFileIds.Count} seen ids (initialized={_dto.Initialized})");
        }
        catch (Exception e)
        {
            Plugin.Logger?.LogError($"WorkshopSeenStore: load failed: {e}");
            _dto = new WorkshopSeenDto();
        }
    }

    private sealed class WorkshopSeenDto
    {
        [JsonProperty("version")]
        public int Version { get; set; } = 1;

        [JsonProperty("initialized")]
        public bool Initialized { get; set; }

        [JsonProperty("seenFileIds")]
        public List<ulong> SeenFileIds { get; set; } = new List<ulong>();
    }
}
