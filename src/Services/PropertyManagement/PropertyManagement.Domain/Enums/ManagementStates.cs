// Fixed values the schema documents for management and compliance records (stored as UPPER_SNAKE,
// except AppealOrderSource which stores the source table name).

/// property_lease.amount_frequency / property_outsourcing.amount_frequency
public enum AmountFrequency { Annual, Monthly, OneTime }

/// property_rental.rent_frequency
public enum RentFrequency { Monthly, Quarterly, Annual }

/// agreement_violation.fine_status — s.28(2): unpaid fines are recovered as arrears of land revenue.
public enum FineStatus { Imposed, Paid, Waived, RecoveryAsArrears }

/// agreement_violation.violation_status
public enum ViolationStatus { Open, Rectified, Fined, Cancelled, Appealed }

/// property_boundary.boundary_type
public enum BoundaryType { Original, Revised, Regularized }

/// property_encroachment.resolution_type
public enum EncroachmentResolution { Removed, Regularized, Litigated, Other }

/// property_litigation.gda_role
public enum GdaRole { Plaintiff, Defendant, Complainant, Respondent }

/// litigation_party.party_role
public enum LitigationPartyRole { Plaintiff, Defendant, Petitioner, Respondent, Intervener }

/// property_appeal.decision_outcome
public enum AppealOutcome { Allowed, Dismissed, Remanded, Modified }

/// property_appeal.appeal_status
public enum AppealStatus { Filed, UnderHearing, Decided, Withdrawn }

/// property_appeal.order_source_table — which record holds the order being appealed.
public enum AppealOrderSource
{
  PropertyAllotment,
  AgreementViolation,
  PropertyEncroachment,
  PropertyLease,
  PropertyRental,
  BuildingPlan,
  PropertyTransfer,
  Other
}
