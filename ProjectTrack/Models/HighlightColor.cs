namespace ProjectTrack.Models;

public enum HighlightColor
{
    None,
    Critical,
    Medium,
    Low
}

public static class HighlightColorExtensions
{
    public static string ToCssClass(this HighlightColor color) => color switch
    {
        HighlightColor.Critical => "row-critical",
        HighlightColor.Medium => "row-medium",
        HighlightColor.Low => "row-low",
        _ => string.Empty
    };
}
