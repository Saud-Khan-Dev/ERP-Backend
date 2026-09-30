// DTOs for the management (allotment, lease, rental, violation, auction, outsourcing) and compliance
// (boundary, encroachment, litigation, appeal, building plan) records.

public sealed record GeoPointDto(int SequenceNo, decimal Latitude, decimal Longitude);

public sealed record AllotmentDto(
  Guid Id, Guid PropertyId, string AllotmentNo, OwnerRef? Allottee, MasterRef? AllotmentType, MasterRef? Status,
  DateOnly AllotmentDate, DateOnly? EffectiveDate, DateOnly? ExpiryDate, string? AllotmentLetterRef, string? Conditions,
  DateOnly? CancellationDate, string? CancellationReason, string? CancellationOrderRef,
  DateOnly? RestorationDate, string? RestorationOrderRef, string? Remarks, bool IsActive, DateTime? CreatedAt, Guid? CreatedBy);

public sealed record LeaseDto(
  Guid Id, Guid PropertyId, string LeaseNo, OwnerRef? Lessee, MasterRef? LeaseType, MasterRef? Status,
  DateOnly LeaseStartDate, DateOnly LeaseEndDate, int? LeaseTermYears, string? LeasePurpose, decimal? LeaseAmount,
  AmountFrequency? AmountFrequency, decimal? SecurityDeposit, string? AgreementReference, DateOnly? AgreementDate,
  bool IsRenewable, Guid? RenewedFromLeaseId, DateOnly? TerminationDate, string? TerminationReason, string? Remarks,
  DateTime? CreatedAt, Guid? CreatedBy);

public sealed record RentalDto(
  Guid Id, Guid PropertyId, string RentalNo, OwnerRef? Tenant, MasterRef? RentalType, MasterRef? Status,
  DateOnly RentalStartDate, DateOnly? RentalEndDate, decimal RentAmount, RentFrequency RentFrequency, decimal? SecurityDeposit,
  decimal? AnnualIncreasePct, string? AgreementReference, DateOnly? AgreementDate, Guid? RenewedFromRentalId,
  DateOnly? TerminationDate, string? TerminationReason, string? Remarks, DateTime? CreatedAt, Guid? CreatedBy);

public sealed record ViolationDto(
  Guid Id, Guid PropertyId, MasterRef? AgreementType, Guid? LeaseId, Guid? RentalId, Guid? TransferId, OwnerRef? Violator,
  DateOnly ViolationDate, string ViolationDescription, short OccurrenceNo, string? NoticeNo, DateOnly? NoticeDate,
  DateOnly? NoticeDeadline, decimal? FineAmount, Guid? FineImposedBy, FineStatus? FineStatus, bool LedToCancellation,
  DateOnly? CancellationDate, ViolationStatus Status, string? Remarks, DateTime? CreatedAt, Guid? CreatedBy);

public sealed record AuctionBidDto(Guid Id, OwnerRef? Bidder, decimal BidAmount, int? BidRank, decimal? EarnestMoney, bool IsWinning, string? Remarks, DateTime? CreatedAt);

public sealed record AuctionDto(
  Guid Id, Guid PropertyId, string AuctionNo, MasterRef? AuctionType, MasterRef? Status, DateOnly? AnnouncementDate,
  DateOnly? AuctionDate, decimal? BaseReservePrice, decimal? WinningBidAmount, OwnerRef? SuccessfulBidder, DateOnly? AwardDate,
  string? AwardReferenceNo, string? AuctionCommitteeRef, string? Venue, string? ReferenceNo, string? Remarks,
  IReadOnlyList<AuctionBidDto> Bids, DateTime? CreatedAt, Guid? CreatedBy);

public sealed record OutsourcingDto(
  Guid Id, Guid PropertyId, string ContractNo, OwnerRef? Contractor, MasterRef? OutsourcingType, MasterRef? Status,
  DateOnly ContractStartDate, DateOnly? ContractEndDate, string? PurposeService, decimal? ContractAmount,
  AmountFrequency? AmountFrequency, decimal? PerformanceGuarantee, string? ReferenceNo, DateOnly? TerminationDate,
  string? TerminationReason, string? Remarks, DateTime? CreatedAt, Guid? CreatedBy);

public sealed record BoundaryDto(
  Guid Id, Guid PropertyId, BoundaryType BoundaryType, DateOnly? SurveyDate, string? SurveySource,
  decimal? SlopePercentage, bool IsCurrent, string? Remarks, IReadOnlyList<GeoPointDto> Points, DateTime? CreatedAt, Guid? CreatedBy);

public sealed record EncroachmentDto(
  Guid Id, Guid PropertyId, string EncroachmentNo, decimal EncroachmentArea, MasterRef? Unit, decimal EncroachmentAreaBase,
  MasterRef? Status, string? EncroacherName, OwnerRef? EncroacherOwner, DateOnly DetectionDate, DateOnly? EffectiveDate,
  string? NoticeNo, DateOnly? NoticeDate, DateOnly? ResolutionDate, EncroachmentResolution? ResolutionType,
  string? ResolutionReferenceNo, string? Description, string? Remarks, IReadOnlyList<GeoPointDto> Points,
  DateTime? CreatedAt, Guid? CreatedBy);

public sealed record LitigationPartyDto(Guid Id, string PartyName, OwnerRef? PartyOwner, LitigationPartyRole Role, string? CounselName, string? Remarks);

public sealed record LitigationHearingDto(Guid Id, DateOnly HearingDate, string? Proceedings, string? OrderPassed, DateOnly? NextHearingDate, string? AttendedBy);

public sealed record LitigationDto(
  Guid Id, Guid PropertyId, string CaseNo, string CaseTitle, string CourtAuthority, MasterRef? LitigationType, MasterRef? Status,
  DateOnly? FilingDate, GdaRole? GdaRole, Guid? FiledByOfficerId, Guid? RelatedEncroachmentId, Guid? RelatedAllotmentId,
  Guid? RelatedLeaseId, DateOnly? NextHearingDate, DateOnly? DecisionDate, string? DecisionOutcome, string? AppealedTo,
  Guid? ParentLitigationId, string? GdaCounsel, string? Remarks, IReadOnlyList<LitigationPartyDto> Parties,
  IReadOnlyList<LitigationHearingDto> Hearings, DateTime? CreatedAt, Guid? CreatedBy);

public sealed record AppealDto(
  Guid Id, Guid PropertyId, string AppealNo, OwnerRef? Appellant, string AppealedOrderRef, DateOnly AppealedOrderDate,
  DateOnly? OrderReceivedDate, AppealOrderSource? OrderSourceTable, Guid? OrderSourceId, DateOnly AppealDate,
  string AppellateAuthority, string? DelegatedOfficer, DateOnly DecisionDueDate, bool IsOverdue, DateOnly? DecisionDate,
  AppealOutcome? DecisionOutcome, string? DecisionDetails, AppealStatus Status, string? Remarks, DateTime? CreatedAt, Guid? CreatedBy);

public sealed record BuildingPlanDto(
  Guid Id, Guid PropertyId, string PlanNo, int RevisionNo, Guid? SupersedesPlanId, MasterRef? PlanType, MasterRef? Status,
  OwnerRef? Applicant, DateOnly? SubmissionDate, DateOnly? ApprovalDate, string? ApprovedBy, string? ApprovalReferenceNo,
  DateOnly? ValidityEndDate, decimal? CoveredArea, MasterRef? Unit, int? Floors, string? ArchitectName, string? Remarks,
  DateTime? CreatedAt, Guid? CreatedBy);

public static class ComplianceMappings
{
  public static AllotmentDto ToDto(this PropertyAllotment x, MasterRefs refs, IReadOnlyDictionary<OwnerId, OwnerRef> owners) => new(
    x.Id.Value, x.PropertyId.Value, x.AllotmentNo.Value, owners.GetValueOrDefault(x.AllotteeOwnerId), refs[x.AllotmentTypeId], refs[x.AllotmentStatusId],
    x.AllotmentDate, x.EffectiveDate, x.ExpiryDate, x.AllotmentLetterRef, x.Conditions, x.CancellationDate, x.CancellationReason,
    x.CancellationOrderRef, x.RestorationDate, x.RestorationOrderRef, x.Remarks, x.IsActive, x.CreatedAt, x.CreatedBy);

  public static LeaseDto ToDto(this PropertyLease x, MasterRefs refs, IReadOnlyDictionary<OwnerId, OwnerRef> owners) => new(
    x.Id.Value, x.PropertyId.Value, x.LeaseNo.Value, owners.GetValueOrDefault(x.LesseeOwnerId), refs[x.LeaseTypeId], refs[x.LeaseStatusId],
    x.LeaseStartDate, x.LeaseEndDate, x.LeaseTermYears, x.LeasePurpose, x.LeaseAmount, x.AmountFrequency, x.SecurityDeposit,
    x.AgreementReference, x.AgreementDate, x.IsRenewable, x.RenewedFromLeaseId?.Value, x.TerminationDate, x.TerminationReason,
    x.Remarks, x.CreatedAt, x.CreatedBy);

  public static RentalDto ToDto(this PropertyRental x, MasterRefs refs, IReadOnlyDictionary<OwnerId, OwnerRef> owners) => new(
    x.Id.Value, x.PropertyId.Value, x.RentalNo.Value, owners.GetValueOrDefault(x.TenantOwnerId), refs[x.RentalTypeId], refs[x.RentalStatusId],
    x.RentalStartDate, x.RentalEndDate, x.RentAmount, x.RentFrequency, x.SecurityDeposit, x.AnnualIncreasePct,
    x.AgreementReference, x.AgreementDate, x.RenewedFromRentalId?.Value, x.TerminationDate, x.TerminationReason, x.Remarks,
    x.CreatedAt, x.CreatedBy);

  public static ViolationDto ToDto(this AgreementViolation x, MasterRefs refs, IReadOnlyDictionary<OwnerId, OwnerRef> owners) => new(
    x.Id.Value, x.PropertyId.Value, refs[x.AgreementTypeId], x.LeaseId?.Value, x.RentalId?.Value, x.TransferId?.Value,
    owners.GetValueOrDefault(x.ViolatorOwnerId), x.ViolationDate, x.ViolationDescription, x.OccurrenceNo, x.NoticeNo, x.NoticeDate,
    x.NoticeDeadline, x.FineAmount, x.FineImposedBy, x.FineStatus, x.LedToCancellation, x.CancellationDate, x.ViolationStatus,
    x.Remarks, x.CreatedAt, x.CreatedBy);

  public static AuctionDto ToDto(this PropertyAuction x, MasterRefs refs, IReadOnlyDictionary<OwnerId, OwnerRef> owners) => new(
    x.Id.Value, x.PropertyId.Value, x.AuctionNo.Value, refs[x.AuctionTypeId], refs[x.AuctionStatusId], x.AnnouncementDate,
    x.AuctionDate, x.BaseReservePrice, x.WinningBidAmount,
    x.SuccessfulBidderOwnerId is null ? null : owners.GetValueOrDefault(x.SuccessfulBidderOwnerId),
    x.AwardDate, x.AwardReferenceNo, x.AuctionCommitteeRef, x.Venue, x.ReferenceNo, x.Remarks,
    x.Bids.OrderBy(b => b.BidRank).Select(b => new AuctionBidDto(b.Id.Value, owners.GetValueOrDefault(b.BidderOwnerId), b.BidAmount,
      b.BidRank, b.EarnestMoney, b.IsWinning, b.Remarks, b.CreatedAt)).ToList(),
    x.CreatedAt, x.CreatedBy);

  public static OutsourcingDto ToDto(this PropertyOutsourcing x, MasterRefs refs, IReadOnlyDictionary<OwnerId, OwnerRef> owners) => new(
    x.Id.Value, x.PropertyId.Value, x.ContractNo.Value, owners.GetValueOrDefault(x.OutsourcedPartyOwnerId), refs[x.OutsourcingTypeId],
    refs[x.ContractStatusId], x.ContractStartDate, x.ContractEndDate, x.PurposeService, x.ContractAmount, x.AmountFrequency,
    x.PerformanceGuarantee, x.ReferenceNo, x.TerminationDate, x.TerminationReason, x.Remarks, x.CreatedAt, x.CreatedBy);

  public static BoundaryDto ToDto(this PropertyBoundary x) => new(
    x.Id.Value, x.PropertyId.Value, x.BoundaryType, x.SurveyDate, x.SurveySource, x.SlopePercentage, x.IsCurrent, x.Remarks,
    x.Points.Select(p => new GeoPointDto(p.SequenceNo, p.Latitude, p.Longitude)).ToList(), x.CreatedAt, x.CreatedBy);

  public static EncroachmentDto ToDto(this PropertyEncroachment x, MasterRefs refs, IReadOnlyDictionary<OwnerId, OwnerRef> owners) => new(
    x.Id.Value, x.PropertyId.Value, x.EncroachmentNo.Value, x.EncroachmentArea, refs[x.MeasurementUnitId], x.EncroachmentAreaBase,
    refs[x.EncroachmentStatusId], x.EncroacherName, x.EncroacherOwnerId is null ? null : owners.GetValueOrDefault(x.EncroacherOwnerId),
    x.DetectionDate, x.EffectiveDate, x.NoticeNo, x.NoticeDate, x.ResolutionDate, x.ResolutionType, x.ResolutionReferenceNo,
    x.Description, x.Remarks, x.Points.Select(p => new GeoPointDto(p.SequenceNo, p.Latitude, p.Longitude)).ToList(),
    x.CreatedAt, x.CreatedBy);

  public static LitigationDto ToDto(this PropertyLitigation x, MasterRefs refs, IReadOnlyDictionary<OwnerId, OwnerRef> owners) => new(
    x.Id.Value, x.PropertyId.Value, x.CaseNo, x.CaseTitle, x.CourtAuthority, refs[x.LitigationTypeId], refs[x.LitigationStatusId],
    x.FilingDate, x.GdaRole, x.FiledByOfficerId, x.RelatedEncroachmentId?.Value, x.RelatedAllotmentId?.Value, x.RelatedLeaseId?.Value,
    x.NextHearingDate, x.DecisionDate, x.DecisionOutcome, x.AppealedTo, x.ParentLitigationId?.Value, x.GdaCounsel, x.Remarks,
    x.Parties.Select(p => new LitigationPartyDto(p.Id.Value, p.PartyName, p.PartyOwnerId is null ? null : owners.GetValueOrDefault(p.PartyOwnerId),
      p.PartyRole, p.CounselName, p.Remarks)).ToList(),
    x.Hearings.Select(h => new LitigationHearingDto(h.Id.Value, h.HearingDate, h.Proceedings, h.OrderPassed, h.NextHearingDate, h.AttendedBy)).ToList(),
    x.CreatedAt, x.CreatedBy);

  public static AppealDto ToDto(this PropertyAppeal x, IReadOnlyDictionary<OwnerId, OwnerRef> owners, DateOnly today) => new(
    x.Id.Value, x.PropertyId.Value, x.AppealNo.Value, owners.GetValueOrDefault(x.AppellantOwnerId), x.AppealedOrderRef,
    x.AppealedOrderDate, x.OrderReceivedDate, x.OrderSourceTable, x.OrderSourceId, x.AppealDate, x.AppellateAuthority,
    x.DelegatedOfficer, x.DecisionDueDate, x.IsOverdue(today), x.DecisionDate, x.DecisionOutcome, x.DecisionDetails,
    x.AppealStatus, x.Remarks, x.CreatedAt, x.CreatedBy);

  public static BuildingPlanDto ToDto(this BuildingPlan x, MasterRefs refs, IReadOnlyDictionary<OwnerId, OwnerRef> owners) => new(
    x.Id.Value, x.PropertyId.Value, x.PlanNo.Value, x.RevisionNo, x.SupersedesPlanId?.Value, refs[x.BuildingPlanTypeId],
    refs[x.BuildingPlanStatusId], x.ApplicantOwnerId is null ? null : owners.GetValueOrDefault(x.ApplicantOwnerId), x.SubmissionDate,
    x.ApprovalDate, x.ApprovedBy, x.ApprovalReferenceNo, x.ValidityEndDate, x.CoveredArea,
    x.MeasurementUnitId is null ? null : refs[x.MeasurementUnitId], x.Floors, x.ArchitectName, x.Remarks, x.CreatedAt, x.CreatedBy);
}
