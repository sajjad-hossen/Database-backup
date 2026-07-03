using System.Text;
using Npgsql;

namespace DatabaseBackupAPI.Services;

public class FinancialReportService
{
    private readonly IConfiguration _configuration;

    public FinancialReportService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<string> GenerateHtmlReportAsync()
    {
        var connStr = _configuration.GetConnectionString("NeonConnection");
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        // 1. Get mess name map: Id -> Name
        var messNameMap = await GetMessNameMapAsync(conn);

        // 2. Get all distinct MessIds that have users
        var messIds = await GetDistinctMessIds(conn);

        // 2. Get all Users (with Name for display)
        var allUsers = await QueryRawAsync(conn,
            "SELECT \"Id\", \"Name\", \"MessId\" FROM \"Users\" ORDER BY \"MessId\", \"Name\"");

        // 3. Get all data for the 4 tables
        var allMeals     = await QueryRawAsync(conn, "SELECT * FROM \"Meals\" ORDER BY \"MessId\", \"Date\" DESC");
        var allDeposits  = await QueryRawAsync(conn, "SELECT * FROM \"Deposits\" ORDER BY \"MessId\", \"Date\" DESC");
        var allBazar     = await QueryRawAsync(conn, "SELECT * FROM \"BazarCosts\" ORDER BY \"MessId\", \"Date\" DESC");

        return BuildHtml(messIds, messNameMap, allUsers, allMeals, allDeposits, allBazar);
    }

    private static async Task<Dictionary<string, string>> GetMessNameMapAsync(NpgsqlConnection conn)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await using var cmd = new NpgsqlCommand("SELECT \"Id\", \"Name\" FROM \"Messes\"", conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id   = reader.IsDBNull(0) ? "" : reader.GetValue(0).ToString()!;
                var name = reader.IsDBNull(1) ? id : reader.GetValue(1).ToString()!;
                if (!string.IsNullOrEmpty(id)) map[id] = name;
            }
        }
        catch { /* if Messes table missing, fall back to IDs */ }
        return map;
    }

    private static async Task<List<string>> GetDistinctMessIds(NpgsqlConnection conn)
    {
        var ids = new List<string>();
        await using var cmd = new NpgsqlCommand("SELECT DISTINCT \"MessId\" FROM \"Users\" ORDER BY \"MessId\"", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            ids.Add(reader.IsDBNull(0) ? "Unknown" : reader.GetValue(0).ToString()!);
        return ids;
    }

    private static async Task<(List<string> Cols, List<Dictionary<string, string>> Rows)> QueryRawAsync(
        NpgsqlConnection conn, string sql)
    {
        var cols = new List<string>();
        var rows = new List<Dictionary<string, string>>();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        for (int i = 0; i < reader.FieldCount; i++) cols.Add(reader.GetName(i));
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
                row[cols[i]] = reader.IsDBNull(i) ? "—" : reader.GetValue(i)?.ToString() ?? "—";
            rows.Add(row);
        }
        return (cols, rows);
    }

    // ─── HTML BUILDERS ─────────────────────────────────────────────────────────

    private static string BuildHtml(
        List<string> messIds,
        Dictionary<string, string> messNameMap,
        (List<string> Cols, List<Dictionary<string, string>> Rows) users,
        (List<string> Cols, List<Dictionary<string, string>> Rows) meals,
        (List<string> Cols, List<Dictionary<string, string>> Rows) deposits,
        (List<string> Cols, List<Dictionary<string, string>> Rows) bazar)
    {
        var generatedAt = DateTime.Now.ToString("dddd, MMMM dd, yyyy — hh:mm:ss tt");
        int totalUsers    = users.Rows.Count;
        int totalMeals    = meals.Rows.Count;
        decimal totalDep  = SumColumn(deposits.Rows, "Amount");
        decimal totalBaz  = SumColumn(bazar.Rows, "Amount");

        // Build each mess section
        var messSections = new StringBuilder();
        var messNavLinks = new StringBuilder();

        var palette = new[] { "#6366f1", "#10b981", "#f59e0b", "#ef4444", "#06b6d4", "#a855f7", "#ec4899", "#84cc16" };

        for (int mi = 0; mi < messIds.Count; mi++)
        {
            string messId   = messIds[mi];
            string messName = messNameMap.TryGetValue(messId, out var mn) ? mn : $"Mess {messId}";
            string color    = palette[mi % palette.Length];
            string anchor   = $"mess-{mi}";

            // Filter rows for this mess
            var messUsers    = users.Rows.Where(r => GetVal(r, "MessId") == messId).ToList();
            var userMap      = messUsers.ToDictionary(r => GetVal(r, "Id"), r => GetVal(r, "Name"));

            var messMeals    = meals.Rows.Where(r => GetVal(r, "MessId") == messId).ToList();
            var messDeposits = deposits.Rows.Where(r => GetVal(r, "MessId") == messId).ToList();
            var messBazar    = bazar.Rows.Where(r => GetVal(r, "MessId") == messId).ToList();

            decimal messDepTotal  = SumColumn(messDeposits, "Amount");
            decimal messBazTotal  = SumColumn(messBazar, "Amount");
            decimal totalMealsVal = messMeals.Sum(r => decimal.TryParse(GetVal(r, "MealCount"), out var v) ? v : 0);
            decimal mealRate      = totalMealsVal > 0 && messBazTotal > 0
                                    ? Math.Round(messBazTotal / totalMealsVal, 2) : 0;

            messNavLinks.AppendLine($"<a href=\"#{anchor}\" class=\"nav-link\" style=\"--c:{color}\">{EscapeHtml(messName)}</a>");

            messSections.AppendLine($@"
<section class=""mess-section"" id=""{anchor}"">
  <div class=""mess-header"" style=""--accent:{color}"">
    <div class=""mess-title-row"">
      <span class=""mess-icon"">🏠</span>
      <div>
        <h2 class=""mess-name"">{EscapeHtml(messName)}</h2>
        <p class=""mess-sub"">ID: {EscapeHtml(messId)} &nbsp;·&nbsp; {messUsers.Count} member{(messUsers.Count != 1 ? "s" : "")}</p>
      </div>
    </div>
    <div class=""mess-stats"">
      <div class=""mstat""><span class=""mstat-label"">Members</span><span class=""mstat-val"">{messUsers.Count}</span></div>
      <div class=""mstat""><span class=""mstat-label"">Total Meals</span><span class=""mstat-val"">{totalMealsVal:N1}</span></div>
      <div class=""mstat""><span class=""mstat-label"">Deposits</span><span class=""mstat-val"">৳{messDepTotal:N2}</span></div>
      <div class=""mstat""><span class=""mstat-label"">Bazar Cost</span><span class=""mstat-val"">৳{messBazTotal:N2}</span></div>
      <div class=""mstat""><span class=""mstat-label"">Meal Rate</span><span class=""mstat-val"">৳{mealRate:N2}</span></div>
      <div class=""mstat""><span class=""mstat-label"">Balance</span><span class=""mstat-val {(messDepTotal - messBazTotal >= 0 ? "pos" : "neg")}"">৳{(messDepTotal - messBazTotal):N2}</span></div>
    </div>
  </div>

  {BuildSubTable("👥 Members", color, new[] {"Name","Email","Status","Role","IsCalculationMember"},
      messUsers, userMap)}
  {BuildSubTable("🍽️ Meals", color, new[] {"UserId","Date","MealCount"},
      messMeals, userMap, resolveUserCol:"UserId")}
  {BuildSubTable("💰 Deposits", color, new[] {"UserId","Amount","Date","Note"},
      messDeposits, userMap, resolveUserCol:"UserId")}
  {BuildSubTable("🛒 Bazar Costs", color, new[] {"BuyerUserId","Amount","Date","Description"},
      messBazar, userMap, resolveUserCol:"BuyerUserId")}
</section>");
        }

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>Mess-Wise Financial Report</title>
  <link href=""https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700;800&display=swap"" rel=""stylesheet"">
  <style>
    *, *::before, *::after {{ box-sizing: border-box; margin: 0; padding: 0; }}
    :root {{
      --bg: #0a0a14;
      --surface: #12121f;
      --surface2: #1a1a2e;
      --surface3: #1e1e35;
      --border: #252540;
      --text: #e2e8f0;
      --muted: #7c8db0;
      --radius: 14px;
    }}
    html {{ scroll-behavior: smooth; }}
    body {{ font-family: 'Inter', sans-serif; background: var(--bg); color: var(--text); min-height: 100vh; padding: 0 0 3rem; }}

    /* TOPBAR NAV */
    .topbar {{ position: sticky; top: 0; z-index: 100; background: rgba(10,10,20,.92); backdrop-filter: blur(12px); border-bottom: 1px solid var(--border); padding: .75rem 2rem; display: flex; gap: .5rem; flex-wrap: wrap; align-items: center; }}
    .topbar-title {{ font-weight: 700; font-size: .85rem; color: var(--muted); margin-right: .5rem; white-space: nowrap; }}
    .nav-link {{ padding: .35rem .9rem; border-radius: 999px; border: 1px solid color-mix(in srgb, var(--c) 50%, transparent); color: color-mix(in srgb, var(--c) 90%, #fff); font-size: .78rem; font-weight: 600; text-decoration: none; transition: background .2s; }}
    .nav-link:hover {{ background: color-mix(in srgb, var(--c) 20%, transparent); }}

    /* HERO */
    .hero {{ text-align: center; padding: 3.5rem 2rem 2.5rem; background: linear-gradient(160deg, #1a1a2e 0%, #0a0a14 100%); border-bottom: 1px solid var(--border); position: relative; overflow: hidden; }}
    .hero::before {{ content:''; position:absolute; inset:0; background: radial-gradient(ellipse at 50% 0%, rgba(99,102,241,.2) 0%, transparent 65%); pointer-events:none; }}
    .hero-badge {{ display:inline-block; padding:.35rem 1rem; background:rgba(99,102,241,.12); border:1px solid rgba(99,102,241,.35); border-radius:999px; font-size:.72rem; font-weight:700; letter-spacing:.1em; text-transform:uppercase; color:#a5b4fc; margin-bottom:1rem; }}
    .hero h1 {{ font-size: clamp(1.8rem,4vw,3rem); font-weight:800; background: linear-gradient(135deg,#fff 30%,#a5b4fc 100%); -webkit-background-clip:text; -webkit-text-fill-color:transparent; background-clip:text; line-height:1.15; }}
    .hero-sub {{ margin-top:.5rem; color:var(--muted); font-size:.9rem; }}
    .hero-ts {{ margin-top:1rem; font-size:.75rem; color:#475569; }}

    /* GLOBAL STATS */
    .global-stats {{ display:grid; grid-template-columns:repeat(auto-fit,minmax(160px,1fr)); gap:1rem; padding: 2rem; max-width:1200px; margin:0 auto; }}
    .gs-card {{ background:var(--surface); border:1px solid var(--border); border-radius:var(--radius); padding:1.2rem 1.4rem; position:relative; overflow:hidden; transition:transform .2s,box-shadow .2s; }}
    .gs-card:hover {{ transform:translateY(-2px); box-shadow:0 8px 24px rgba(0,0,0,.4); }}
    .gs-card::after {{ content:''; position:absolute; bottom:0;left:0;right:0; height:3px; background:var(--accent,#6366f1); }}
    .gs-label {{ font-size:.72rem; color:var(--muted); font-weight:600; text-transform:uppercase; letter-spacing:.06em; }}
    .gs-value {{ font-size:1.5rem; font-weight:800; margin-top:.4rem; }}
    .gs-icon {{ font-size:1.3rem; margin-bottom:.3rem; }}

    /* MESS SECTION */
    .mess-section {{ max-width:1200px; margin: 2rem auto 0; padding:0 2rem; }}
    .mess-header {{ background: linear-gradient(135deg, var(--surface2) 0%, var(--surface) 100%); border:1px solid color-mix(in srgb, var(--accent) 30%, var(--border)); border-radius:var(--radius); padding:1.5rem 1.8rem; margin-bottom:1.25rem; position:relative; overflow:hidden; }}
    .mess-header::before {{ content:''; position:absolute; top:0;left:0;bottom:0; width:5px; background:var(--accent); border-radius:var(--radius) 0 0 var(--radius); }}
    .mess-title-row {{ display:flex; align-items:center; gap:1rem; margin-bottom:1.2rem; }}
    .mess-icon {{ font-size:2rem; }}
    .mess-name {{ font-size:1.2rem; font-weight:800; color:var(--text); }}
    .mess-sub {{ font-size:.8rem; color:var(--muted); margin-top:.2rem; }}
    .mess-stats {{ display:flex; flex-wrap:wrap; gap:.75rem; }}
    .mstat {{ background:rgba(255,255,255,.04); border:1px solid var(--border); border-radius:10px; padding:.6rem 1rem; min-width:100px; }}
    .mstat-label {{ font-size:.68rem; color:var(--muted); font-weight:600; text-transform:uppercase; letter-spacing:.05em; display:block; }}
    .mstat-val {{ font-size:1.05rem; font-weight:700; display:block; margin-top:.2rem; }}
    .mstat-val.pos {{ color:#34d399; }}
    .mstat-val.neg {{ color:#f87171; }}

    /* SUB TABLE CARD */
    .sub-card {{ background:var(--surface); border:1px solid var(--border); border-radius:var(--radius); margin-bottom:1rem; overflow:hidden; }}
    .sub-card-header {{ display:flex; align-items:center; gap:.6rem; padding:.9rem 1.2rem; background:var(--surface3); border-bottom:1px solid var(--border); }}
    .sub-card-header h3 {{ font-size:.9rem; font-weight:700; }}
    .sub-card-header .badge {{ margin-left:auto; background:color-mix(in srgb, var(--accent) 15%, transparent); border:1px solid color-mix(in srgb, var(--accent) 30%, transparent); color:color-mix(in srgb, var(--accent) 90%, #fff); font-size:.68rem; font-weight:700; padding:.2rem .6rem; border-radius:999px; }}
    .table-wrap {{ overflow-x:auto; }}
    table {{ width:100%; border-collapse:collapse; font-size:.82rem; }}
    thead tr {{ background:rgba(255,255,255,.02); }}
    th {{ padding:.7rem 1rem; text-align:left; font-size:.68rem; font-weight:700; letter-spacing:.06em; text-transform:uppercase; color:var(--muted); white-space:nowrap; border-bottom:1px solid var(--border); }}
    td {{ padding:.7rem 1rem; border-bottom:1px solid rgba(255,255,255,.04); white-space:nowrap; vertical-align:middle; }}
    tbody tr:last-child td {{ border-bottom:none; }}
    tbody tr:hover td {{ background:rgba(255,255,255,.025); }}
    tbody tr:nth-child(even) td {{ background:rgba(255,255,255,.012); }}
    .empty-row {{ text-align:center; padding:1.5rem !important; color:var(--muted); font-style:italic; }}

    /* DIVIDER */
    .mess-divider {{ max-width:1200px; margin:2rem auto; height:1px; background:linear-gradient(90deg,transparent,var(--border),transparent); padding:0 2rem; }}

    footer {{ text-align:center; padding:2rem; color:#334155; font-size:.78rem; border-top:1px solid var(--border); margin-top:3rem; }}
    footer span {{ color:#6366f1; }}
  </style>
</head>
<body>

  <!-- STICKY NAV -->
  <nav class=""topbar"">
    <span class=""topbar-title"">Jump to mess →</span>
    {messNavLinks}
  </nav>

  <!-- HERO -->
  <div class=""hero"">
    <div class=""hero-badge"">📊 Mess-Wise Financial Report</div>
    <h1>Mess Financial Overview</h1>
    <p class=""hero-sub"">{messIds.Count} Mess{(messIds.Count != 1 ? "es" : "")} · Neon Cloud Database</p>
    <p class=""hero-ts"">Generated: {generatedAt}</p>
  </div>

  <!-- GLOBAL SUMMARY -->
  <div class=""global-stats"">
    <div class=""gs-card"" style=""--accent:#6366f1"">
      <div class=""gs-icon"">🏠</div>
      <div class=""gs-label"">Total Messes</div>
      <div class=""gs-value"">{messIds.Count}</div>
    </div>
    <div class=""gs-card"" style=""--accent:#8b5cf6"">
      <div class=""gs-icon"">👥</div>
      <div class=""gs-label"">Total Members</div>
      <div class=""gs-value"">{totalUsers}</div>
    </div>
    <div class=""gs-card"" style=""--accent:#10b981"">
      <div class=""gs-icon"">🍽️</div>
      <div class=""gs-label"">Meal Records</div>
      <div class=""gs-value"">{totalMeals}</div>
    </div>
    <div class=""gs-card"" style=""--accent:#f59e0b"">
      <div class=""gs-icon"">💰</div>
      <div class=""gs-label"">Total Deposits</div>
      <div class=""gs-value"">৳{totalDep:N0}</div>
    </div>
    <div class=""gs-card"" style=""--accent:#ef4444"">
      <div class=""gs-icon"">🛒</div>
      <div class=""gs-label"">Total Bazar Cost</div>
      <div class=""gs-value"">৳{totalBaz:N0}</div>
    </div>
    <div class=""gs-card"" style=""--accent:{((totalDep - totalBaz) >= 0 ? "#34d399" : "#f87171")}"">
      <div class=""gs-icon"">{((totalDep - totalBaz) >= 0 ? "✅" : "⚠️")}</div>
      <div class=""gs-label"">Net Balance</div>
      <div class=""gs-value"">৳{(totalDep - totalBaz):N0}</div>
    </div>
  </div>

  <!-- PER-MESS SECTIONS -->
  {messSections}

  <footer>
    Generated by <span>DatabaseBackupAPI</span> · Neon Cloud · {generatedAt}
  </footer>

</body>
</html>";
    }

    private static string BuildSubTable(
        string title, string color,
        IEnumerable<string> showCols,
        List<Dictionary<string, string>> rows,
        Dictionary<string, string> userMap,
        string? resolveUserCol = null)
    {
        var colList = showCols.ToList();
        var sb = new StringBuilder();
        sb.AppendLine($@"
<div class=""sub-card"" style=""--accent:{color}"">
  <div class=""sub-card-header"">
    <h3>{title}</h3>
    <span class=""badge"">{rows.Count} record{(rows.Count != 1 ? "s" : "")}</span>
  </div>
  <div class=""table-wrap"">
    <table>
      <thead><tr>");

        foreach (var col in colList)
        {
            var label = col == resolveUserCol ? "Member" : col;
            sb.AppendLine($"<th>{EscapeHtml(label)}</th>");
        }
        sb.AppendLine("</tr></thead><tbody>");

        if (rows.Count == 0)
        {
            sb.AppendLine($"<tr><td colspan=\"{colList.Count}\" class=\"empty-row\">No records</td></tr>");
        }
        else
        {
            foreach (var row in rows)
            {
                sb.AppendLine("<tr>");
                foreach (var col in colList)
                {
                    var raw = GetVal(row, col);
                    string display = raw;
                    if (col == resolveUserCol && userMap.TryGetValue(raw, out var name))
                        display = name;
                    sb.AppendLine($"<td>{EscapeHtml(display)}</td>");
                }
                sb.AppendLine("</tr>");
            }
        }
        sb.AppendLine("</tbody></table></div></div>");
        return sb.ToString();
    }

    private static string GetVal(Dictionary<string, string> row, string key) =>
        row.TryGetValue(key, out var v) ? v : "—";

    private static decimal SumColumn(List<Dictionary<string, string>> rows, string col) =>
        rows.Sum(r => decimal.TryParse(GetVal(r, col), out var v) ? v : 0);

    private static string EscapeHtml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
