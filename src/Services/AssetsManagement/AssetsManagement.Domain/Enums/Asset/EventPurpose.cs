/// What a SYSTEM lifecycle event type stands for. The acts (transfer, loan, return, status change, disposal …) write
/// the event type of their purpose - never one picked by code - so renaming a type never breaks an act. Hand-recorded
/// types (an inspection, a repair) have no purpose.
public enum EventPurpose
{
  Registered,
  Transfer,
  Loan,
  Return,
  StatusChange,
  Disposal,
  DisposalCancelled,
  Valuation,
  DetailsChanged,
  DepreciationRevised,
  DocumentRemoved
}
