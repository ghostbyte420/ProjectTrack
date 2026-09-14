namespace ProjectTrack.Models;

public class FacetEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Selected { get; set; }
    public HighlightColor Highlight { get; set; } = HighlightColor.None;
    public string MapName { get; set; } = string.Empty;
    public string MapIndex { get; set; } = string.Empty;
    public string MapRules { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CoordinateX { get; set; } = string.Empty;
    public string CoordinateY { get; set; } = string.Empty;
    public string RawLocation { get; set; } = string.Empty;
    public DateTime DateOfEntry { get; set; } = DateTime.Today;
    public string CartographerAssigned { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public TimeSpan? DueTime { get; set; }

    /// <summary>String proxy for binding TimeSpan? to an &lt;input type="time"&gt; element.</summary>
    public string DueTimeText
    {
        get => DueTime?.ToString(@"hh\:mm") ?? string.Empty;
        set => DueTime = TimeSpan.TryParse(value, out var parsed) ? parsed : null;
    }
}
