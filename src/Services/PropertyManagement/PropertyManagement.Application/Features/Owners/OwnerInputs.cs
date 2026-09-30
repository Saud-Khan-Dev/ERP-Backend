using FluentValidation;
using Microsoft.EntityFrameworkCore;

public sealed record OwnerInput(
  Guid OwnerTypeId,
  string OwnerName,
  string? FatherHusbandName = null,
  string? Cnic = null,
  string? Ntn = null,
  string? RegistrationNo = null,
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
    RuleFor(x => x.FatherHusbandName).MaximumLength(200);
    RuleFor(x => x.Ntn).MaximumLength(PropertyOwner.NtnMaxLength);
    RuleFor(x => x.RegistrationNo).MaximumLength(50);
    RuleFor(x => x.Email).MaximumLength(EmailAddress.MaxLength);
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
    RuleFor(x => x.PostalCode).MaximumLength(20);
  }
}

/// "Look it up by cnic or ntn before creating a new one to avoid duplicates" (schema guide).
public static class OwnerDuplicates
{
  public static async Task<string?> FindAsync(IApplicationDbContext context, Cnic? cnic, string? ntn, OwnerId? self, CancellationToken cancellationToken)
  {
    var owners = context.Owners.AsNoTracking();
    if (self is not null)
      owners = owners.Where(o => o.Id != self);

    if (cnic is not null)
    {
      var match = await owners.Where(o => o.Cnic == cnic).Select(o => o.OwnerCode).FirstOrDefaultAsync(cancellationToken);
      if (match is not null)
        return $"CNIC {cnic.Value} is already registered to owner {match.Value}.";
    }

    if (!string.IsNullOrWhiteSpace(ntn))
    {
      var normalized = ntn.Trim().ToUpperInvariant();
      var match = await owners.Where(o => o.Ntn == normalized).Select(o => o.OwnerCode).FirstOrDefaultAsync(cancellationToken);
      if (match is not null)
        return $"NTN {normalized} is already registered to owner {match.Value}.";
    }

    return null;
  }
}
