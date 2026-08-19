using System.Collections.Generic;
using System.Linq;
using Shared;
using Shared.TrackData;

namespace QoLiTea.Features.LazyCustomTracks;

/// <summary>Display-oriented snapshot of one custom track for disk + stub UI.</summary>
public sealed class CachedTrackDto
{
    public string LevelId { get; set; }
    public string Kind { get; set; } // "workshop" | "local"
    public ulong FileId { get; set; }
    public string BasePath { get; set; }
    public string TrackName { get; set; }
    public string TrackSubtitle { get; set; }
    public string ArtistName { get; set; }
    public string StageCreatorName { get; set; }
    public float? BeatsPerMinute { get; set; }
    public string TrackLength { get; set; }
    public float? BeatCount { get; set; }
    public string AlbumArtUrl { get; set; }
    public string VideoFilePath { get; set; }
    public bool HasLeaderboard { get; set; }
    public long TimeAdded { get; set; }
    public double SortOrder { get; set; }
    public TrackCategory Category { get; set; }
    public List<Difficulty> Difficulties { get; set; } = new List<Difficulty>();
    /// <summary>
    /// Per-difficulty intensity/BPM so stock folder grouping does not dump stubs into Unknowns.
    /// Absent on caches written before this field existed.
    /// </summary>
    public List<CachedDifficultyDto> DifficultyDetails { get; set; } = new List<CachedDifficultyDto>();
    public bool PersistedDifficultyDetails { get; set; }

    public static CachedTrackDto FromMetadata(ITrackMetadata track)
    {
        var dto = new CachedTrackDto
        {
            LevelId = track.LevelId,
            TrackName = track.TrackName,
            TrackSubtitle = track.TrackSubtitle,
            ArtistName = track.ArtistName,
            StageCreatorName = track.StageCreatorName,
            BeatsPerMinute = track.BeatsPerMinute,
            TrackLength = track.TrackLength,
            BeatCount = track.BeatCount,
            AlbumArtUrl = track.AlbumArtUrl,
            VideoFilePath = track.VideoFilePath,
            HasLeaderboard = track.HasLeaderboard,
            TimeAdded = track.TimeAdded,
            SortOrder = track.SortOrder,
            Category = track.Category,
            BasePath = track.BasePath,
            PersistedDifficultyDetails = true,
        };

        if (track.Difficulties != null)
        {
            foreach (var d in track.Difficulties.Distinct())
            {
                dto.Difficulties.Add(d);
                var info = track.GetDifficulty(d);
                dto.DifficultyDetails.Add(new CachedDifficultyDto
                {
                    Difficulty = d,
                    Intensity = info?.Intensity,
                    BeatsPerMinute = info?.BeatsPerMinute ?? track.BeatsPerMinute,
                    BeatCount = info?.BeatCount ?? track.BeatCount,
                });
            }
        }

        if (track is Shared.UGC.Steam.SteamWorkshopUgcTrackMetadata workshop)
        {
            dto.Kind = "workshop";
            dto.FileId = workshop.FileID.m_PublishedFileId;
        }
        else if (dto.Category == TrackCategory.UgcRemote
                 || (dto.LevelId != null && dto.LevelId.StartsWith("workshop", System.StringComparison.OrdinalIgnoreCase)))
        {
            dto.Kind = "workshop";
            var fileId = Shared.UGC.Steam.SteamWorkshopUgcTrackProvider.LevelIdToFileId(dto.LevelId);
            if (fileId.HasValue)
                dto.FileId = fileId.Value.m_PublishedFileId;
        }
        else
        {
            dto.Kind = "local";
        }

        return dto;
    }
}
