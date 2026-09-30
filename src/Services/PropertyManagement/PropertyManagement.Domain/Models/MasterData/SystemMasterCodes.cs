/// The few master codes the application itself relies on. They are seeded, and admins should
/// deactivate rather than repurpose them. Everything else is compared on whatever code the admin set.
public static class SystemMasterCodes
{
  /// Tenure given to a transferee who owned nothing before, when the transfer does not name one.
  public const string TenureOwned = "OWNED";

  /// Document type used for an owner's CNIC copy (property_owner.cnic_document_id).
  public const string DocumentCnicCopy = "CNIC_COPY";

  /// The base measurement unit; every *_base column is in this unit.
  public const string UnitSquareFeet = "SQFT";
}
