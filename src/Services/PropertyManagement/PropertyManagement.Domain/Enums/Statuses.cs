// System states the application itself acts on. They are fixed values in the schema (varchar with a
// documented list), unlike the admin-editable master tables, and are stored as UPPER_SNAKE text.

/// property_ownership.ownership_status. A DISPUTED share is still held: it counts toward the 100% total
/// and toward the current owners, but cannot be transferred until the dispute is resolved.
public enum OwnershipStatus { Active, Ended, Disputed }

/// property_transfer.transfer_status. Initiated is the only waiting state: approving a transfer completes
/// it in the same step. Approved is kept for rows approved before that change (they are completed with
/// /transfers/{id}/complete). Rejected: the approving officer refused it.
public enum TransferStatus { Initiated, Approved, Completed, Cancelled, Rejected }

/// property_transfer_party.party_role
public enum TransferPartyRole { Transferor, Transferee }

/// property_encumbrance.status
public enum EncumbranceStatus { Active, Released, Enforced }

/// property_area_regularization.regularization_status
public enum RegularizationStatus { Applied, Pending, Regularized, Rejected }

/// owner_address.address_type
public enum AddressType { Permanent, Present, Mailing, Office }

/// property_document.entity_type — which record a document belongs to.
public enum DocumentEntityType
{
  Property,
  Owner,
  Ownership,
  Transfer,
  Allotment,
  Lease,
  Rental,
  Auction,
  Outsourcing,
  Encroachment,
  Litigation,
  Appeal,
  BuildingPlan,
  Encumbrance,
  Violation,
  Regularization
}

/// attribute_definition.data_type — custom fields admins add on top of the schema.
public enum AttributeDataType
{
  Text,
  Number,
  Decimal,
  Date,
  DateTime,
  Boolean,
  Dropdown,
  MultiSelect
}
