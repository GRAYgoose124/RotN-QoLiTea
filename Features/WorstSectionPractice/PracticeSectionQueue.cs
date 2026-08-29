using System;
using System.Collections.Generic;
using QoLiTea.Features.ResultsDivergence;

namespace QoLiTea.Features.WorstSectionPractice;

internal static class PracticeSectionQueue
{
    private static readonly List<PracticeSpan> Spans = new();
    private static readonly List<PracticeSpan> LastSpans = new();
    private static int _index = -1;

    /// <summary>Blocks duplicate seeks while a jump is being applied.</summary>
    internal static bool JumpInProgress { get; private set; }

    internal static bool IsActive => _index >= 0 && _index < Spans.Count;

    internal static bool HasStoredSpans => LastSpans.Count > 0;

    internal static IReadOnlyList<PracticeSpan> StoredSpans => LastSpans;

    internal static void SetQueue(IReadOnlyList<PracticeSpan> spans)
    {
        Spans.Clear();
        LastSpans.Clear();
        if (spans != null && spans.Count > 0)
        {
            Spans.AddRange(spans);
            LastSpans.AddRange(spans);
            _index = 0;
        }
        else
        {
            _index = -1;
        }

        JumpInProgress = false;
    }

    /// <summary>Re-arm the last Auto queue (practice auto-retry / Retry).</summary>
    internal static bool TryRestoreLastQueue()
    {
        if (LastSpans.Count == 0)
            return false;

        Spans.Clear();
        Spans.AddRange(LastSpans);
        _index = 0;
        JumpInProgress = false;
        return true;
    }

    internal static bool TryGetCurrent(out PracticeSpan span)
    {
        if (!IsActive)
        {
            span = default;
            return false;
        }

        span = Spans[_index];
        return true;
    }

    internal static bool HasNextSection()
        => _index >= 0 && _index + 1 < Spans.Count;

    internal static bool TryPeekNext(out PracticeSpan next)
    {
        if (!HasNextSection())
        {
            next = default;
            return false;
        }

        next = Spans[_index + 1];
        return true;
    }

    internal static bool TryAdvanceToNext(out PracticeSpan next)
    {
        if (!TryPeekNext(out next))
            return false;

        _index++;
        return true;
    }

    internal static void MarkJumpInProgress() => JumpInProgress = true;

    internal static void ClearJumpInProgress() => JumpInProgress = false;

    /// <summary>Drop active index but keep LastSpans for the next practice retry.</summary>
    internal static void DeactivateKeepLast()
    {
        Spans.Clear();
        _index = -1;
        JumpInProgress = false;
    }

    internal static void Clear()
    {
        Spans.Clear();
        LastSpans.Clear();
        _index = -1;
        JumpInProgress = false;
    }
}
