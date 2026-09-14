using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectTrack.Models;

namespace ProjectTrack.Services;

public class TrackerData
{
    public List<CodeEntry> CodeEntries { get; set; } = new();
    public List<FacetEntry> FacetEntries { get; set; } = new();
    public List<TeamMember> TeamMembers { get; set; } = new();
}

public class TrackerDataService
{
    private readonly string _dataFilePath;
    private readonly string _webRootPath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public List<CodeEntry> CodeEntries { get; private set; } = new();
    public List<FacetEntry> FacetEntries { get; private set; } = new();
    public List<TeamMember> TeamMembers { get; private set; } = new();

    public event Action? DataChanged;

    public TrackerDataService(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _dataFilePath = Path.Combine(dataDir, "projecttrack-data.json");
        _webRootPath = env.WebRootPath;
        LoadFromDisk();
    }

    private string? GetLogoBase64DataUri()
    {
        try
        {
            var logoPath = Path.Combine(_webRootPath, "images", "logo.png");
            if (!File.Exists(logoPath))
            {
                return null;
            }

            var bytes = File.ReadAllBytes(logoPath);
            return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
        }
        catch
        {
            return null;
        }
    }

    private void LoadFromDisk()
    {
        if (!File.Exists(_dataFilePath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_dataFilePath);
            var data = JsonSerializer.Deserialize<TrackerData>(json, _jsonOptions);
            if (data is not null)
            {
                CodeEntries = data.CodeEntries;
                FacetEntries = data.FacetEntries;
                TeamMembers = data.TeamMembers;
            }
        }
        catch (Exception)
        {
            // If the file is corrupt, start fresh rather than crashing the app.
        }
    }

    public void SaveToDisk()
    {
        var data = new TrackerData
        {
            CodeEntries = CodeEntries,
            FacetEntries = FacetEntries,
            TeamMembers = TeamMembers
        };

        var json = JsonSerializer.Serialize(data, _jsonOptions);
        File.WriteAllText(_dataFilePath, json);
    }

    public string ExportToJson()
    {
        var data = new TrackerData
        {
            CodeEntries = CodeEntries,
            FacetEntries = FacetEntries,
            TeamMembers = TeamMembers
        };

        return JsonSerializer.Serialize(data, _jsonOptions);
    }

    public bool ImportFromJson(string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<TrackerData>(json, _jsonOptions);
            if (data is null)
            {
                return false;
            }

            CodeEntries = data.CodeEntries;
            FacetEntries = data.FacetEntries;
            TeamMembers = data.TeamMembers;
            SaveToDisk();
            NotifyChanged();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void NotifyChanged()
    {
        SaveToDisk();
        DataChanged?.Invoke();
    }

    public static bool IsAssignable(TeamMember member) => member.Whitelisted && !member.Blacklisted;

    public IEnumerable<TeamMember> GetAssignableMembers() => TeamMembers.Where(IsAssignable);

    /// <summary>
    /// Clears the given member's name from any Code/Facet entry assignments.
    /// Used when a member is blacklisted so an admin must explicitly reassign their work.
    /// </summary>
    public void UnassignMember(string memberName)
    {
        if (string.IsNullOrWhiteSpace(memberName))
        {
            return;
        }

        foreach (var entry in CodeEntries.Where(e => e.DeveloperAssigned == memberName))
        {
            entry.DeveloperAssigned = string.Empty;
        }

        foreach (var entry in FacetEntries.Where(e => e.CartographerAssigned == memberName))
        {
            entry.CartographerAssigned = string.Empty;
        }
    }

    /// <summary>
    /// Builds a standalone, self-contained HTML page listing every team member and their
    /// currently assigned Code Tracker / Facet Tracker entries, including due date and time.
    /// </summary>
    public string GenerateRosterHtml()
    {
        var html = new System.Text.StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\"><head><meta charset=\"utf-8\" />");
        html.AppendLine("<title>ProjectTrack - Developer Assignment Roster</title>");
        html.AppendLine("""
        <style>
            body { font-family: Segoe UI, Arial, sans-serif; background: #f4f5f7; margin: 0; padding: 2rem; color: #1b1f2a; }
            .roster-header { text-align: center; margin-bottom: 2rem; }
            .roster-header h1 { margin-bottom: 0.25rem; }
            .roster-header .generated { color: #6b7280; font-size: 0.9rem; }
            .roster-title { display: flex; align-items: center; justify-content: center; gap: 0.75rem; }
            .roster-title h1 { margin: 0; }
            .roster-logo { height: 2.5rem; width: auto; }
            .roster-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(420px, 1fr)); gap: 1.5rem; max-width: 1400px; margin: 0 auto; }
            .member-card { background: #fff; border-radius: 8px; box-shadow: 0 1px 4px rgba(0,0,0,0.12); overflow: hidden; display: flex; flex-direction: column; }
            .member-card-header { background: #1b1f2a; color: #fff; padding: 0.9rem 1.1rem; display: flex; justify-content: space-between; align-items: baseline; }
            .member-card-header h2 { margin: 0; font-size: 1.1rem; }
            .member-card-header .role { font-size: 0.85rem; color: #c9ccd6; }
            .member-meta { padding: 0.6rem 1.1rem; font-size: 0.85rem; color: #4b5563; border-bottom: 1px solid #eee; }
            .assignment-table { width: 100%; border-collapse: collapse; }
            .assignment-table th, .assignment-table td { padding: 0.5rem 0.75rem; font-size: 0.85rem; text-align: left; border-bottom: 1px solid #f0f0f0; }
            .assignment-table th { background: #f7f8fa; color: #374151; font-weight: 600; }
            .assignment-table tr:nth-child(even) td { background: #fafbfc; }
            .assignment-table td.type-code { color: #1d4ed8; font-weight: 600; }
            .assignment-table td.type-facet { color: #047857; font-weight: 600; }
            .no-assignments { padding: 1rem 1.1rem; color: #9ca3af; font-style: italic; font-size: 0.85rem; }
            .status-Active { color: #059669; }
            .status-Inactive { color: #9ca3af; }
            .status-OnLeave { color: #d97706; }
            .item-link { color: #1b1f2a; text-decoration: underline dotted; cursor: pointer; background: none; border: none; font: inherit; padding: 0; text-align: left; }
            .item-link:hover { color: #2563eb; }
            .modal-overlay { display: none; position: fixed; inset: 0; background: rgba(15, 18, 25, 0.55); align-items: center; justify-content: center; z-index: 100; }
            .modal-overlay.open { display: flex; }
            .modal-box { background: #fff; border-radius: 8px; width: 90%; max-width: 480px; box-shadow: 0 8px 30px rgba(0,0,0,0.25); overflow: hidden; }
            .modal-box-header { display: flex; justify-content: space-between; align-items: center; padding: 0.9rem 1.1rem; background: #1b1f2a; color: #fff; }
            .modal-box-header h3 { margin: 0; font-size: 1rem; }
            .modal-close { background: none; border: none; color: #fff; font-size: 1.1rem; cursor: pointer; }
            .modal-box-body { padding: 1rem 1.1rem; font-size: 0.9rem; color: #1b1f2a; }
            .modal-box-body dt { font-weight: 600; color: #4b5563; margin-top: 0.75rem; }
            .modal-box-body dt:first-child { margin-top: 0; }
            .modal-box-body dd { margin: 0.15rem 0 0; white-space: pre-wrap; }
        </style>
        """);

        html.AppendLine("</head><body>");
        html.AppendLine("<div class=\"roster-header\">");
        var logoDataUri = GetLogoBase64DataUri();
        if (logoDataUri is not null)
        {
            html.AppendLine("<div class=\"roster-title\">");
            html.AppendLine($"<img src=\"{logoDataUri}\" alt=\"Logo\" class=\"roster-logo\" />");
            html.AppendLine("<h1>Developer Assignment Roster</h1>");
            html.AppendLine("</div>");
        }
        else
        {
            html.AppendLine("<h1>Developer Assignment Roster</h1>");
        }
        html.AppendLine($"<div class=\"generated\">Generated {DateTime.Now:MMMM d, yyyy h:mm tt}</div>");
        html.AppendLine("</div>");
        html.AppendLine("<div class=\"roster-grid\">");

        foreach (var member in TeamMembers.OrderBy(m => m.Name))
        {
            var codeAssignments = CodeEntries.Where(e => e.DeveloperAssigned == member.Name).ToList();
            var facetAssignments = FacetEntries.Where(e => e.CartographerAssigned == member.Name).ToList();

            html.AppendLine("<div class=\"member-card\">");
            html.AppendLine("<div class=\"member-card-header\">");
            html.AppendLine($"<h2>{System.Net.WebUtility.HtmlEncode(member.Name)}</h2>");
            html.AppendLine($"<span class=\"role\">{member.Role}</span>");
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"member-meta\">");
            html.AppendLine($"Status: <span class=\"status-{member.Status}\">{member.Status}</span> &nbsp;|&nbsp; Contact: {System.Net.WebUtility.HtmlEncode(member.ContactInfo)}");
            html.AppendLine("</div>");

            if (codeAssignments.Count == 0 && facetAssignments.Count == 0)
            {
                html.AppendLine("<div class=\"no-assignments\">No current assignments.</div>");
            }
            else
            {
                html.AppendLine("<table class=\"assignment-table\">");
                html.AppendLine("<thead><tr><th>Type</th><th>Item</th><th>Due Date</th><th>Due Time</th></tr></thead><tbody>");

                foreach (var entry in codeAssignments.OrderBy(e => e.DueDate))
                {
                    var item = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(entry.FileName) ? entry.Description : entry.FileName);
                    var description = System.Net.WebUtility.HtmlEncode(entry.Description);
                    var lineNumbers = System.Net.WebUtility.HtmlEncode(entry.LineNumbers);
                    html.AppendLine("<tr>");
                    html.AppendLine("<td class=\"type-code\">Code</td>");
                    html.AppendLine($"<td><button type=\"button\" class=\"item-link\" data-title=\"{item}\" data-priority=\"{entry.Highlight}\" data-description=\"{description}\" data-linenumbers=\"{lineNumbers}\" onclick=\"showDetails(this)\">{item}</button></td>");
                    html.AppendLine($"<td>{(entry.DueDate.HasValue ? entry.DueDate.Value.ToString("MMM d, yyyy") : "—")}</td>");
                    html.AppendLine($"<td>{(entry.DueTime.HasValue ? DateTime.Today.Add(entry.DueTime.Value).ToString("h:mm tt") : "—")}</td>");
                    html.AppendLine("</tr>");
                }

                foreach (var entry in facetAssignments.OrderBy(e => e.DueDate))
                {
                    var item = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(entry.MapName) ? entry.Description : entry.MapName);
                    var description = System.Net.WebUtility.HtmlEncode(entry.Description);
                    var coordinates = System.Net.WebUtility.HtmlEncode($"X: {entry.CoordinateX}, Y: {entry.CoordinateY}");
                    html.AppendLine("<tr>");
                    html.AppendLine("<td class=\"type-facet\">Facet</td>");
                    html.AppendLine($"<td><button type=\"button\" class=\"item-link\" data-title=\"{item}\" data-priority=\"{entry.Highlight}\" data-description=\"{description}\" data-coordinates=\"{coordinates}\" onclick=\"showDetails(this)\">{item}</button></td>");
                    html.AppendLine($"<td>{(entry.DueDate.HasValue ? entry.DueDate.Value.ToString("MMM d, yyyy") : "—")}</td>");
                    html.AppendLine($"<td>{(entry.DueTime.HasValue ? DateTime.Today.Add(entry.DueTime.Value).ToString("h:mm tt") : "—")}</td>");
                    html.AppendLine("</tr>");
                }

                html.AppendLine("</tbody></table>");
            }

            html.AppendLine("</div>");
        }

        html.AppendLine("</div>");
        html.AppendLine(
            "<div class=\"modal-overlay\" id=\"detailModal\" onclick=\"if(event.target===this) closeDetailModal()\">" +
            "<div class=\"modal-box\">" +
            "<div class=\"modal-box-header\">" +
            "<h3 id=\"detailModalTitle\">Details</h3>" +
            "<button class=\"modal-close\" onclick=\"closeDetailModal()\">\u2715</button>" +
            "</div>" +
            "<div class=\"modal-box-body\">" +
            "<dl>" +
            "<dt>Priority</dt>" +
            "<dd id=\"detailModalPriority\"></dd>" +
            "<dt>Description</dt>" +
            "<dd id=\"detailModalDescription\"></dd>" +
            "<dt id=\"detailModalLineNumbersLabel\" style=\"display:none\">Line Numbers</dt>" +
            "<dd id=\"detailModalLineNumbers\" style=\"display:none\"></dd>" +
            "<dt id=\"detailModalCoordinatesLabel\" style=\"display:none\">Coordinates</dt>" +
            "<dd id=\"detailModalCoordinates\" style=\"display:none\"></dd>" +
            "</dl>" +
            "</div>" +
            "</div>" +
            "</div>");
        html.AppendLine("<script>");
        html.AppendLine("function showDetails(el) {");
        html.AppendLine("    document.getElementById('detailModalTitle').textContent = el.dataset.title || 'Details';");
        html.AppendLine("    document.getElementById('detailModalPriority').textContent = el.dataset.priority || 'None';");
        html.AppendLine("    document.getElementById('detailModalDescription').textContent = el.dataset.description || '(none)';");
        html.AppendLine("    var lineNumbers = el.dataset.linenumbers;");
        html.AppendLine("    var lineLabel = document.getElementById('detailModalLineNumbersLabel');");
        html.AppendLine("    var lineValue = document.getElementById('detailModalLineNumbers');");
        html.AppendLine("    if (lineNumbers) {");
        html.AppendLine("        lineLabel.style.display = '';");
        html.AppendLine("        lineValue.style.display = '';");
        html.AppendLine("        lineValue.textContent = lineNumbers;");
        html.AppendLine("    } else {");
        html.AppendLine("        lineLabel.style.display = 'none';");
        html.AppendLine("        lineValue.style.display = 'none';");
        html.AppendLine("    }");
        html.AppendLine("    var coordinates = el.dataset.coordinates;");
        html.AppendLine("    var coordLabel = document.getElementById('detailModalCoordinatesLabel');");
        html.AppendLine("    var coordValue = document.getElementById('detailModalCoordinates');");
        html.AppendLine("    if (coordinates) {");
        html.AppendLine("        coordLabel.style.display = '';");
        html.AppendLine("        coordValue.style.display = '';");
        html.AppendLine("        coordValue.textContent = coordinates;");
        html.AppendLine("    } else {");
        html.AppendLine("        coordLabel.style.display = 'none';");
        html.AppendLine("        coordValue.style.display = 'none';");
        html.AppendLine("    }");
        html.AppendLine("    document.getElementById('detailModal').classList.add('open');");
        html.AppendLine("}");
        html.AppendLine("function closeDetailModal() {");
        html.AppendLine("    document.getElementById('detailModal').classList.remove('open');");
        html.AppendLine("}");
        html.AppendLine("document.addEventListener('keydown', function (e) {");
        html.AppendLine("    if (e.key === 'Escape') closeDetailModal();");
        html.AppendLine("});");
        html.AppendLine("</script>");
        html.AppendLine("</body></html>");

        return html.ToString();
    }
}
