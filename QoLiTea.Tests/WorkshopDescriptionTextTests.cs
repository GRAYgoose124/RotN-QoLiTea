using QoLiTea.Features.WorkshopAutoScan;
using Xunit;

namespace QoLiTea.Tests;

public class WorkshopDescriptionTextTests
{
    [Fact]
    public void Strip_bbcode_removes_common_tags_keeps_link_text()
    {
        Assert.Equal(
            "hello world link",
            WorkshopDescriptionText.StripBbCode("hello [b]world[/b] [url=https://x]link[/url]"));
    }

    [Fact]
    public void Strip_bbcode_null_or_empty_returns_empty()
    {
        Assert.Equal(string.Empty, WorkshopDescriptionText.StripBbCode(null));
        Assert.Equal(string.Empty, WorkshopDescriptionText.StripBbCode(""));
    }
}
