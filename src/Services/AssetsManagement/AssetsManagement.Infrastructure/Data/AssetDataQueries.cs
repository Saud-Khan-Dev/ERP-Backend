using Microsoft.EntityFrameworkCore;
using Npgsql;

/// SQL over the assets schema for what LINQ cannot say over the converted columns.
public sealed class AssetDataQueries(ApplicationDbContext context) : IAssetDataQueries
{
  public async Task<IReadOnlyDictionary<string, int>> AssetsHoldingAsync(IReadOnlyCollection<string> fieldCodes, CancellationToken cancellationToken)
  {
    if (fieldCodes.Count == 0)
      return new Dictionary<string, int>();

    var rows = await context.Database.SqlQueryRaw<HoldingRow>(
        """
        SELECT k AS "Code", count(*)::int AS "Count"
        FROM assets.asset a, unnest(@codes) AS k
        WHERE a.deleted_at IS NULL AND jsonb_exists(a.extra_attributes, k)
          AND a.extra_attributes -> k <> 'null'::jsonb AND a.extra_attributes -> k <> '""'::jsonb AND a.extra_attributes -> k <> '[]'::jsonb
        GROUP BY k
        """,
        new NpgsqlParameter("codes", fieldCodes.Distinct().ToArray()))
      .ToListAsync(cancellationToken);

    return rows.ToDictionary(r => r.Code, r => r.Count, StringComparer.Ordinal);
  }

  public async Task<int> AssetsHoldingChoiceAsync(IReadOnlyCollection<string> fieldCodes, string optionCode, CancellationToken cancellationToken)
  {
    if (fieldCodes.Count == 0)
      return 0;

    return await context.Database.SqlQueryRaw<int>(
        """
        SELECT count(*)::int AS "Value"
        FROM assets.asset a
        WHERE a.deleted_at IS NULL
          AND EXISTS (SELECT 1 FROM unnest(@codes) AS k WHERE jsonb_exists(a.extra_attributes -> k, @code))
        """,
        new NpgsqlParameter("codes", fieldCodes.Distinct().ToArray()),
        new NpgsqlParameter("code", optionCode))
      .SingleAsync(cancellationToken);
  }

  public async Task<long> NextTagNumberAsync(CancellationToken cancellationToken) =>
      await context.Database.SqlQueryRaw<long>("""SELECT nextval('assets.asset_tag_seq') AS "Value" """).SingleAsync(cancellationToken);

  public async Task<long> PeekTagNumberAsync(CancellationToken cancellationToken) =>
      await context.Database.SqlQueryRaw<long>(
          """SELECT CASE WHEN is_called THEN last_value + 1 ELSE last_value END AS "Value" FROM assets.asset_tag_seq""")
        .SingleAsync(cancellationToken);

  public async Task<IReadOnlyList<Guid>> AssetIdsWithTagLikeAsync(string text, CancellationToken cancellationToken)
  {
    var escaped = text.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
    return await context.Database.SqlQueryRaw<Guid>(
        """SELECT id AS "Value" FROM assets.asset WHERE asset_code ILIKE '%' || @text || '%' ESCAPE '\'""",
        new NpgsqlParameter("text", escaped))
      .ToListAsync(cancellationToken);
  }

  private sealed class HoldingRow
  {
    public string Code { get; set; } = "";
    public int Count { get; set; }
  }
}
