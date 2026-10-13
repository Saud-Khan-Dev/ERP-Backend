using FluentValidation;
using Microsoft.EntityFrameworkCore;

/// Cnic: 13 digits, with or without dashes (required for an INDIVIDUAL, refused for a COMPANY / TRUST / GOVERNMENT /
/// JOINT_ENTITY). Ntn: 1234567-8 or a 13-digit CNIC-based NTN, with or without dashes; stored in the dashed form.
public sealed record OwnerInput(
  Guid OwnerTypeId,
  string OwnerName,
  string? FatherHusbandName = null,
  string? Cnic = null,
  string? Ntn = null,
  string? Email = null,
  string? Remarks = null);

public sealed record ContactInput(Guid ContactTypeId, string ContactNumber, bool IsPrimary = false, string? Remarks = null);

public sealed record AddressInput(
  string FullAddress,
  AddressType AddressType = AddressType.Permanent,
  string? CityTown = null,
  string? District = null,
  string? Province = null,
  string? Country = null,
  string? PostalCode = null,
  bool IsPrimary = false);

public class OwnerInputValidator : AbstractValidator<OwnerInput>
{
  public OwnerInputValidator()
  {
    RuleFor(x => x.OwnerTypeId).NotEmpty();
    RuleFor(x => x.OwnerName).NotEmpty().MaximumLength(PropertyOwner.NameMaxLength);
    RuleFor(x => x.FatherHusbandName).MaximumLength(PropertyOwner.FatherHusbandNameMaxLength);
    RuleFor(x => x.Cnic).MaximumLength(30);
    RuleFor(x => x.Ntn).MaximumLength(PropertyOwner.NtnMaxLength);
    RuleFor(x => x.Email).MaximumLength(EmailAddress.MaxLength);
    RuleFor(x => x.Remarks).MaximumLength(PropertyOwner.RemarksMaxLength);
  }
}

public class ContactInputValidator : AbstractValidator<ContactInput>
{
  public ContactInputValidator()
  {
    RuleFor(x => x.ContactTypeId).NotEmpty();
    RuleFor(x => x.ContactNumber).NotEmpty().MaximumLength(30);
    RuleFor(x => x.Remarks).MaximumLength(200);
  }
}

public class AddressInputValidator : AbstractValidator<AddressInput>
{
  public AddressInputValidator()
  {
    RuleFor(x => x.FullAddress).NotEmpty().MaximumLength(400);
    RuleFor(x => x.AddressType).IsInEnum();
    RuleFor(x => x.CityTown).MaximumLength(100);
    RuleFor(x => x.District).MaximumLength(100);
    RuleFor(x => x.Province).MaximumLength(100);
    RuleFor(x => x.Country).MaximumLength(100);
    RuleFor(x => x.PostalCode).MaximumLength(20);
  }
}

/// "Look it up by cnic or ntn before creating a new one to avoid duplicates" (schema guide). CNICs are compared in
/// their dashed form, NTNs on their digits (so 1234567-8 and 12345678 are the same NTN, also for rows saved
/// before NTNs were normalised). The message names the owner already registered and says when it is inactive.
public static class OwnerDuplicates
{
  public static async Task<string?> FindAsync(IApplicationDbContext context, Cnic? cnic, Ntn? ntn, OwnerId? self, CancellationToken cancellationToken)
  {
    var owners = context.Owners.AsNoTracking();
    if (self is not null)
      owners = owners.Where(o => o.Id != self);

    if (cnic is not null)
    {
      var match = await owners.Where(o => o.Cnic == cnic)
        .Select(o => new { o.OwnerCode, o.OwnerName, o.IsActive })
        .FirstOrDefaultAsync(cancellationToken);
      if (match is not null)
        return Message($"CNIC {cnic.Value}", match.OwnerCode.Value, match.OwnerName.Value, match.IsActive);
    }

    if (ntn is not null)
    {
      var match = await owners.Where(OwnerSearch.NtnIs(ntn.Value))
        .Select(o => new { o.OwnerCode, o.OwnerName, o.IsActive })
        .FirstOrDefaultAsync(cancellationToken);
      if (match is not null)
        return Message($"NTN {ntn.Value}", match.OwnerCode.Value, match.OwnerName.Value, match.IsActive);
    }

    return null;
  }

  private static string Message(string what, string code, string name, bool isActive) => isActive
    ? $"{what} is already registered to {code} ({name}). Use that owner instead of registering them again."
    : $"{what} is already registered to {code} ({name}), which is inactive. Reactivate {code} instead of registering them again.";
}

/// The ways an owner is looked up (register search, duplicate check, cross-property list search).
public static class OwnerSearch
{
  /// NTNs compared on their digits: the column may hold 1234567-8 or (saved before normalising) 12345678.
  public static System.Linq.Expressions.Expression<Func<PropertyOwner, bool>> NtnIs(string ntn)
  {
    var digits = Ntn.Digits(ntn);
    return o => o.Ntn != null && o.Ntn.Replace("-", "").Replace(" ", "") == digits;
  }

  /// Owners matching a free-text term: the exact owner code, a CNIC (13 digits, or its first 5 or more digits),
  /// an NTN (either form), or part of the name.
  public static IQueryable<PropertyOwner> Matching(IQueryable<PropertyOwner> owners, string term)
  {
    term = term.Trim();
    var lower = term.ToLowerInvariant();
    var upper = term.ToUpperInvariant();

    var cnicPrefix = Cnic.NormalizePrefix(term);
    var ntnDigits = Ntn.Normalize(term) is { } ntn ? Ntn.Digits(ntn) : null;

    // codes and CNICs are value-converted: the cast through object lets EF compare the column text
    return owners.Where(o =>
        ((string)(object)o.OwnerCode) == upper
        || (cnicPrefix != null && o.Cnic != null && ((string)(object)o.Cnic).StartsWith(cnicPrefix))
        || (ntnDigits != null && o.Ntn != null && o.Ntn.Replace("-", "").Replace(" ", "") == ntnDigits)
        || o.OwnerName.Value.ToLower().Contains(lower));
  }
}
