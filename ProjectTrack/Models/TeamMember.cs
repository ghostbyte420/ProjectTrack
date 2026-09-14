namespace ProjectTrack.Models;

public enum TeamRole
{
    Developer,
    Cartographer,
    Designer,
    QaTester
}

public enum MemberStatus
{
    Active,
    Inactive,
    OnLeave
}

public class TeamMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Selected { get; set; }
    public HighlightColor Highlight { get; set; } = HighlightColor.None;
    public string Name { get; set; } = string.Empty;
    public TeamRole Role { get; set; } = TeamRole.Developer;
    public MemberStatus Status { get; set; } = MemberStatus.Active;
    public string Notations { get; set; } = string.Empty;
    public string ContactInfo { get; set; } = string.Empty;
    public bool Whitelisted { get; set; }
    public bool Blacklisted { get; set; }
}
