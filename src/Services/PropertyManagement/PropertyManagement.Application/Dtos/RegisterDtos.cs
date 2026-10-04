// DTOs for the cross-property lists (agreements, legal matters, transfers) and the property reports.

/// The property a cross-property row belongs to, with its town resolved.
public sealed record PropertyHeader(Guid Id, string PropertyCode, string PropertyName, MasterRef? Town);

// ---- agreements: allotments, leases, rentals, outsourcing contracts and auctions in one list ----

public enum AgreementKind { Allotment, Lease, Rental, Outsourcing, Auction }

public enum AgreementSort { Ends, Starts, Code, Property, Amount }

/// Party: allottee / lessee / tenant / contractor / successful bidder (null until an auction is awarded).
/// Amount: lease amount / rent / contract amount / winning bid (else the reserve price); none for an allotment.
/// Deposit: security deposit of a lease or rental, performance guarantee of a contract; none otherwise.
/// Status is the record's own master status; InForce is defined per kind in GetAgreementsHandler.
public sealed record AgreementListItemDto(
  AgreementKind Kind,
  Guid Id,
  string Code,
  Guid PropertyId,
  string PropertyCode,
  string PropertyName,
  MasterRef? Town,
  Guid? PartyId,
  string? PartyCode,
  string? PartyName,
  MasterRef? Type,
  MasterRef? Status,
  DateOnly? StartDate,
  DateOnly? EndDate,
  decimal? Amount,
  string? AmountFrequency,
  decimal? Deposit,
  bool InForce);

// ---- legal matters: encroachments, court cases, appeals and agreement violations in one list ----

public enum LegalMatterKind { Encroachment, Case, Appeal, Violation }

public enum LegalMatterSort { Next, Opened, Code, Property }

/// A status as {code, name}: a master status gives its code and name (PENDING / Pending); the fixed appeal and
/// violation statuses give the value the rest of the API uses as code (UnderHearing) and a readable name.
public sealed record StatusRef(string Code, string Name);

/// Code: encroachment no. / case no. / appeal no. / violation notice no. (the violation id when no notice).
/// Title: encroachment description / case title / appealed order ref / violation description.
/// Party: encroacher / the other parties of the case / appellant / violator. Court: cases and appeals only.
/// OpenedOn: detected / filed / appealed / violated on. NextDate: next hearing (case), decision due (appeal),
/// notice deadline (violation); an encroachment has no deadline on record, so none.
/// AreaSqFt: encroachments only; FineAmount and FineStatus: violations only. IsOpen: see OpenMatters.
public sealed record LegalMatterListItemDto(
  LegalMatterKind Kind,
  Guid Id,
  string Code,
  string? Title,
  Guid PropertyId,
  string PropertyCode,
  string PropertyName,
  MasterRef? Town,
  string? Party,
  string? Court,
  StatusRef? Status,
  DateOnly? OpenedOn,
  DateOnly? NextDate,
  decimal? AreaSqFt,
  decimal? FineAmount,
  FineStatus? FineStatus,
  bool IsOpen);

// ---- transfers across properties ----

public enum TransferSort { Date, Code, Property }

public sealed record TransferPartyRowDto(Guid OwnerId, string OwnerCode, string OwnerName, decimal SharePct);

/// Givers: the transferors with the share each gives; receivers: the transferees with the share each receives.
public sealed record TransferListItemDto(
  Guid Id,
  string Code,
  Guid PropertyId,
  string PropertyCode,
  string PropertyName,
  MasterRef? Town,
  MasterRef? Type,
  DateOnly TransferDate,
  TransferStatus Status,
  decimal? Consideration,
  decimal ShareTransferredPct,
  string? ReferenceNo,
  IReadOnlyList<TransferPartyRowDto> Givers,
  IReadOnlyList<TransferPartyRowDto> Receivers);

// ---- reports ----

public sealed record ReportOwnerDto(Guid OwnerId, string OwnerCode, string OwnerName, decimal SharePct);

/// One property in the register report: the register row plus its full area summary (square feet), current
/// owners and custom-field values keyed by the field's code (active fields with a value only).
public sealed record PropertyReportRowDto(
  Guid Id,
  string PropertyCode,
  string PropertyName,
  MasterRef? Town,
  MasterRef? PropertyType,
  MasterRef? Status,
  MasterRef? Classification,
  string? AddressLine,
  string? KhasraSurveyNo,
  string? Description,
  bool IsActive,
  decimal? TotalAreaSqFt,
  decimal? BuiltUpAreaSqFt,
  decimal RegularizedAreaSqFt,
  decimal EncroachedAreaSqFt,
  int CurrentOwnerCount,
  IReadOnlyList<ReportOwnerDto> CurrentOwners,
  IReadOnlyDictionary<string, string> CustomFields,
  DateTime? CreatedAt);

/// TotalAreaSqFt: the rows' current total areas added up (unmeasured properties count as 0).
public sealed record PropertyReportTotalsDto(int Count, decimal TotalAreaSqFt);

/// One owner's share of one property, as it stood on the report date.
public sealed record OwnershipReportRowDto(
  Guid OwnershipId,
  Guid PropertyId,
  string PropertyCode,
  string PropertyName,
  MasterRef? Town,
  Guid OwnerId,
  string OwnerCode,
  string OwnerName,
  MasterRef? OwnerType,
  string? Cnic,
  decimal SharePct,
  MasterRef? Tenure,
  DateOnly EffectiveFrom,
  DateOnly? EffectiveTo,
  MasterRef? AcquiredBy,
  string? ReferenceNo,
  OwnershipStatus Status);
