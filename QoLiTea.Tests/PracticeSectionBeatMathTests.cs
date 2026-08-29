using QoLiTea.Features.WorstSectionPractice;
using Xunit;

namespace QoLiTea.Tests;

public class PracticeSectionBeatMathTests
{
    [Theory]
    [InlineData(100f, 2f, 92f)]
    [InlineData(5f, 2f, 2f)]
    [InlineData(1f, 2f, 0f)]
    public void WarmStartBeat_matches_stock_warmup(float sectionStart, float fadeIn, float expected)
    {
        float warm = PracticeSectionBeatMath.WarmStartBeat(sectionStart, fadeIn);
        Assert.Equal(expected, warm);
    }
}
