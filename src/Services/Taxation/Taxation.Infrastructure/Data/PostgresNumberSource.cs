using Microsoft.EntityFrameworkCore;

/// Running numbers from the schema's sequences: never repeated, even when two requests ask at the same moment.
public sealed class PostgresNumberSource(ApplicationDbContext context) : INumberSource
{
  public async Task<long> NextAsync(NumberSequence sequence, CancellationToken cancellationToken)
  {
    // the sequence name comes from ApplicationDbContext.Sequences, never from a request
    var sql = "SELECT nextval('" + ApplicationDbContext.Schema + "." + ApplicationDbContext.Sequences[sequence] + "') AS \"Value\"";
    return await context.Database.SqlQueryRaw<long>(sql).SingleAsync(cancellationToken);
  }
}
