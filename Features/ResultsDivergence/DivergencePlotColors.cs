using RhythmRift;
using Shared;
using UnityEngine;

namespace QoLiTea.Features.ResultsDivergence;

/// <summary>
/// Dot colors aligned with stock <see cref="AccuracyBar.GetInputColor"/>
/// (Ok white, Good green, Great cyan, Perfect purple). Miss/combo-break are red —
/// stock AccuracyBar maps Miss → Ok, and timeout misses use wasPlayerInput=false.
/// </summary>
internal static class DivergencePlotColors
{
    // Live-probed from results AccuracyBar (2026-08-23)
    internal static readonly Color Perfect = new(0.624f, 0.176f, 1f, 1f);
    internal static readonly Color Great = new(0.055f, 0.8f, 1f, 1f);
    internal static readonly Color Good = new(0.106f, 0.988f, 0f, 1f);
    internal static readonly Color Ok = new(1f, 1f, 1f, 1f);
    internal static readonly Color Miss = new(0.96f, 0.1f, 0.19f, 1f);
    internal static readonly Color ComboBreak = new(1f, 0.45f, 0.5f, 1f);
    internal static readonly Color CenterLine = new(1f, 1f, 1f, 0.55f);
    internal static readonly Color SpanBand = new(0.96f, 0.42f, 0.61f, 0.28f);
    internal static readonly Color Background = new(0.05f, 0.05f, 0.08f, 0.75f);

    internal static Color ForRating(PlotHitRating rating, AccuracyBar stockBar = null)
    {
        if (rating == PlotHitRating.Miss)
            return Miss;
        if (rating == PlotHitRating.ComboBreak)
            return ComboBreak;

        if (stockBar != null)
        {
            var mapped = ToInputRating(rating);
            if (mapped.HasValue)
                return stockBar.GetInputColor(mapped.Value);
        }

        return rating switch
        {
            PlotHitRating.Perfect => Perfect,
            PlotHitRating.Great => Great,
            PlotHitRating.Good => Good,
            PlotHitRating.Ok => Ok,
            _ => Miss,
        };
    }

    internal static PlotHitRating FromInputRating(InputRating rating, bool wasPlayerInput)
    {
        if (rating == InputRating.Miss)
            return wasPlayerInput ? PlotHitRating.Miss : PlotHitRating.ComboBreak;

        return rating switch
        {
            InputRating.Perfect => PlotHitRating.Perfect,
            InputRating.Great => PlotHitRating.Great,
            InputRating.Good => PlotHitRating.Good,
            InputRating.Ok => PlotHitRating.Ok,
            _ => wasPlayerInput ? PlotHitRating.Miss : PlotHitRating.ComboBreak,
        };
    }

    private static InputRating? ToInputRating(PlotHitRating rating)
        => rating switch
        {
            PlotHitRating.Perfect => InputRating.Perfect,
            PlotHitRating.Great => InputRating.Great,
            PlotHitRating.Good => InputRating.Good,
            PlotHitRating.Ok => InputRating.Ok,
            _ => null,
        };
}
