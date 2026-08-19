using System.Text.RegularExpressions;

namespace QoLiTea.Features.WorkshopAutoScan;

/// <summary>Workshop description display helpers.</summary>
public static class WorkshopDescriptionText
{
    private static readonly Regex BbTag = new Regex(
        @"\[/?[^\]]+\]",
        RegexOptions.Compiled);

    private static readonly Regex MultiSpace = new Regex(
        @"[ \t\f\v]+",
        RegexOptions.Compiled);

    public static string StripBbCode(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        string text = BbTag.Replace(input, string.Empty);
        text = text.Replace('\r', ' ').Replace('\n', ' ');
        text = MultiSpace.Replace(text, " ").Trim();
        return text;
    }
}
