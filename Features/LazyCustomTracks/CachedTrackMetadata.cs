using System;
using System.Collections.Generic;
using System.Linq;
using Shared;
using Shared.TrackData;
using Shared.StorageData;

namespace QoLiTea.Features.LazyCustomTracks;

/// <summary>Stub ITrackMetadata built from a display DTO until sync-hydrated.</summary>
public sealed class CachedTrackMetadata : ITrackMetadata
{
    private readonly Dictionary<Difficulty, CachedTrackDifficulty> _difficulties;

    public CachedTrackMetadata(CachedTrackDto dto)
    {
        Dto = dto ?? throw new ArgumentNullException(nameof(dto));
        _difficulties = new Dictionary<Difficulty, CachedTrackDifficulty>();
        if (dto.Difficulties != null)
        {
            foreach (var d in dto.Difficulties.Distinct())
                _difficulties[d] = new CachedTrackDifficulty(d, dto.BeatsPerMinute, dto.BeatCount);
        }
    }

    public CachedTrackDto Dto { get; }

    public string Version => ITrackMetadata.CurrentVersion;
    public string LevelId => Dto.LevelId;
    public string PreviewLevelID => Dto.LevelId;
    public string TrackName => Dto.TrackName;
    public string TrackSubtitle => Dto.TrackSubtitle;
    public string ArtistName => Dto.ArtistName;
    public string StageCreatorName => Dto.StageCreatorName;
    public float? BeatsPerMinute => Dto.BeatsPerMinute;
    public string TrackLength => Dto.TrackLength;
    public float? BeatCount => Dto.BeatCount;
    public IEnumerable<ITrackAudioChannel> AudioChannels => Array.Empty<ITrackAudioChannel>();
    public string VideoFilePath => Dto.VideoFilePath;
    public string AlbumArtUrl => Dto.AlbumArtUrl;
    public string Counterpart => null;
    public ITrackPortrait PortraitHero => null;
    public ITrackPortrait PortraitCounterpart => null;
    public ITrackVfxConfig VfxConfig => null;
    public string BasePath => Dto.BasePath;
    public IEnumerable<Difficulty> Difficulties => _difficulties.Keys;
    public double DownloadProgress => 1.0;
    public bool HasLeaderboard => Dto.HasLeaderboard;
    public long TimeAdded => Dto.TimeAdded;
    public ITrackDlcInfo Dlc => null;
    public double SortOrder => Dto.SortOrder;
    public TrackCategory Category => Dto.Category;
    public ITrackPreview TrackPreview => null;
    public string TetheredLevelID => null;
    public string OnSubmitEventPath => null;
    public Func<Difficulty, LevelStatsMode, bool> IsNewFunc { get; set; }
    public bool IsFreeDownload => false;

    public ITrackDifficulty GetDifficulty(Difficulty difficulty)
    {
        return _difficulties.TryGetValue(difficulty, out var d) ? d : null;
    }

    public bool IsNew(Difficulty difficulty, LevelStatsMode statsMode)
    {
        return IsNewFunc != null && IsNewFunc(difficulty, statsMode);
    }

    private sealed class CachedTrackDifficulty : ITrackDifficulty
    {
        public CachedTrackDifficulty(Difficulty difficulty, float? bpm, float? beatCount)
        {
            Difficulty = difficulty;
            BeatsPerMinute = bpm;
            BeatCount = beatCount;
        }

        public Difficulty Difficulty { get; }
        public string BeatmapFilePath => null;
        public float? Intensity => null;
        public float? BeatCount { get; }
        public float? BeatsPerMinute { get; }
        public float? FinalInputBeatOverride => null;
        public TrackUnlockCriteria UnlockCriteria => null;
    }
}
