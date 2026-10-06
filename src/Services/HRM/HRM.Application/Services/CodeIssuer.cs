using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

/// Issues the next employee number (EMP-001, the Identity service's style) and post code (POST-0001) when HR leaves
/// them blank. Typed numbers are accepted as they are, so existing GDA personnel numbers keep working. Counting looks
/// at every code with the same prefix, so a number is never handed out twice; two registrations at the same moment
/// collide on the unique index (409) rather than share a number.
public class CodeIssuer(IApplicationDbContext context, IOptions<HrmOptions> options)
{
  public async Task<string> NextEmployeeNumberAsync(CancellationToken cancellationToken)
  {
    var settings = options.Value;
    var prefix = settings.EmployeeNumberPrefix + settings.EmployeeNumberSeparator;
    var taken = await context.Employees.Where(e => e.EmployeeNumber.StartsWith(prefix)).Select(e => e.EmployeeNumber).ToListAsync(cancellationToken);
    return Next(prefix, settings.EmployeeNumberMinimumDigits, taken);
  }

  public async Task<string> NextPostCodeAsync(CancellationToken cancellationToken)
  {
    var settings = options.Value;
    var prefix = settings.PostCodePrefix + "-";
    var taken = await context.Posts.Where(p => p.PostCode.StartsWith(prefix)).Select(p => p.PostCode).ToListAsync(cancellationToken);
    return Next(prefix, settings.PostCodeMinimumDigits, taken);
  }

  private static string Next(string prefix, int minimumDigits, IEnumerable<string> taken)
  {
    var highest = 0L;
    foreach (var code in taken)
    {
      if (long.TryParse(code[prefix.Length..], out var number))
        highest = Math.Max(highest, number);
    }

    return $"{prefix}{(highest + 1).ToString().PadLeft(minimumDigits, '0')}";
  }
}
