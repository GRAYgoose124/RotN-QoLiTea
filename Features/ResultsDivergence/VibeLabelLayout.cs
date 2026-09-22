namespace QoLiTea.Features.ResultsDivergence;

/// <summary>Fullscreen vibe caption Y: always staircase by index, wrap after rowCount.</summary>
public static class VibeLabelLayout
{
    public const float DefaultBaseline = 0.88f;
    public const float DefaultStep = 0.055f;
    public const int DefaultRowCount = 4;

    public static float AnchorY(
        int index,
        float baseline = DefaultBaseline,
        float step = DefaultStep,
        int rowCount = DefaultRowCount)
    {
        if (index < 0)
            index = 0;
        int rows = rowCount < 1 ? 1 : rowCount;
        int row = index % rows;
        return baseline - row * step;
    }
}
