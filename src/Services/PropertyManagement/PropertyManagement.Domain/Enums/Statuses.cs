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

/// How well a date from the old records is known: the day, only the month, only the year, or roughly ("about
/// 1972"). The date itself is stored as the first day of what is known (1972-03-01 for March 1972). Dates entered
/// today are Exact.
public enum DatePrecision { Exact, Month, Year, About }

/// property_owner.old_id_type: what an individual from the old records was identified by instead of a CNIC.
public enum OldIdType { OldNic, Passport, NotRecorded }

/// ownership_history_entry.status. Waiting: entered, a second officer has not decided yet. Registered: approved,
/// the owners and transfers it describes are in the register. Rejected / Withdrawn: closed, nothing changed.
public enum HistoryEntryStatus { Waiting, Registered, Rejected, Withdrawn }

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
  Regularization,
  /// A paper filed with an ownership history entry while it waits; when the entry is registered the paper moves to
  /// the transfer (or first ownership) of its step.
  OwnershipHistory
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
