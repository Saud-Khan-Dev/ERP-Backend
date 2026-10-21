/// Reads that need the database itself (JSON inside extra_attributes, sequences, pattern search) and cannot be said
/// in plain LINQ over the converted columns. Implemented in Infrastructure with SQL.
public interface IAssetDataQueries
{
  /// How many assets that are not deleted hold a value for each field code (codes with no holder are left out).
  Task<IReadOnlyDictionary<string, int>> AssetsHoldingAsync(IReadOnlyCollection<string> fieldCodes, CancellationToken cancellationToken);

  /// How many assets that are not deleted hold the choice `optionCode` in any of the fields.
  Task<int> AssetsHoldingChoiceAsync(IReadOnlyCollection<string> fieldCodes, string optionCode, CancellationToken cancellationToken);

  /// The next number of the AST-000001 series (the sequence moves on; never handed out twice).
  Task<long> NextTagNumberAsync(CancellationToken cancellationToken);

  /// The number the next registration would get now - a preview, not a reservation.
  Task<long> PeekTagNumberAsync(CancellationToken cancellationToken);

  /// Ids of assets (deleted ones too) whose tag contains the text, case-insensitive.
  Task<IReadOnlyList<Guid>> AssetIdsWithTagLikeAsync(string text, CancellationToken cancellationToken);
}
