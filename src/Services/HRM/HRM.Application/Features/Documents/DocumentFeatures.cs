using FluentValidation;
using Microsoft.EntityFrameworkCore;

public sealed record DocumentDetailsInput(Guid DocumentTypeId, string? DocumentNumber, DateOnly? IssueDate, DateOnly? ExpiryDate)
{
  public DocumentDetails ToDetails() => new(DocumentNumber, IssueDate, ExpiryDate);
}

public sealed record GetEmployeeDocumentsQueryResult(IReadOnlyList<EmployeeDocumentDto> Documents);

public sealed record GetEmployeeDocumentsQuery(Guid EmployeeId) : IQuery<Result<GetEmployeeDocumentsQueryResult>>;

public sealed record ExpiringDocumentDto(EmployeeDocumentDto Document, string EmployeeNumber, string EmployeeName, int DaysLeft);

public sealed record GetExpiringDocumentsQueryResult(DateOnly AsOf, IReadOnlyList<ExpiringDocumentDto> Documents);

/// Papers that expire within the window (and, when asked, those already expired), soonest first.
public sealed record GetExpiringDocumentsQuery(int WithinDays, bool IncludeExpired, Guid? DocumentTypeId) : IQuery<Result<GetExpiringDocumentsQueryResult>>;

/// Papers waiting for verification (not yet verified, or sent for verification) of people in service, oldest first.
public sealed record GetDocumentsToVerifyQuery(Guid? DocumentTypeId, string? Search) : IQuery<Result<GetExpiringDocumentsQueryResult>>;

/// OnlyForEmployee: self-service may open only its own documents.
/// Download: saved to the person's computer (audit "Downloaded"); otherwise opened in the viewer ("Viewed").
public sealed record GetDocumentContentQuery(Guid Id, Guid? OnlyForEmployee = null, bool Download = true) : IQuery<Result<StoredFile>>;

public sealed record UploadEmployeeDocumentCommand(Guid EmployeeId, DocumentDetailsInput Details, UploadedFile File) : ICommand<Result<CreatedResult>>;

public sealed record UpdateDocumentDetailsCommand(Guid Id, DocumentDetailsInput Details) : ICommand<Result<UpdatedResult>>;

/// A better scan of the same paper (not once verified).
public sealed record ReplaceDocumentFileCommand(Guid Id, UploadedFile File) : ICommand<Result<UpdatedResult>>;

public enum DocumentReview
{
  Submit,
  Verify,
  Reject
}

public sealed record ReviewDocumentCommand(Guid Id, DocumentReview Action) : ICommand<Result<UpdatedResult>>;

/// Removes a paper uploaded by mistake: not verified and not referred to by any service record or request.
public sealed record DeleteDocumentCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

public class DocumentDetailsInputValidator : AbstractValidator<DocumentDetailsInput>
{
  public DocumentDetailsInputValidator()
  {
    RuleFor(x => x.DocumentTypeId).NotEmpty();
    RuleFor(x => x.DocumentNumber).MaximumLength(100);
    RuleFor(x => x.ExpiryDate).GreaterThanOrEqualTo(x => x.IssueDate).When(x => x.IssueDate.HasValue && x.ExpiryDate.HasValue)
      .WithMessage("The expiry date cannot be before the issue date.");
  }
}

public class UploadEmployeeDocumentCommandValidator : AbstractValidator<UploadEmployeeDocumentCommand>
{
  public UploadEmployeeDocumentCommandValidator()
  {
    RuleFor(x => x.EmployeeId).NotEmpty();
    RuleFor(x => x.Details).NotNull().SetValidator(new DocumentDetailsInputValidator());
    RuleFor(x => x.File.FileName).NotEmpty().MaximumLength(255);
  }
}

public class UpdateDocumentDetailsCommandValidator : AbstractValidator<UpdateDocumentDetailsCommand>
{
  public UpdateDocumentDetailsCommandValidator() => RuleFor(x => x.Details).NotNull().SetValidator(new DocumentDetailsInputValidator());
}

public class GetExpiringDocumentsQueryValidator : AbstractValidator<GetExpiringDocumentsQuery>
{
  public GetExpiringDocumentsQueryValidator() => RuleFor(x => x.WithinDays).InclusiveBetween(0, 3650);
}

public class DocumentHandlers(IApplicationDbContext context, FileUploads files, HrLookup lookup, ICurrentUser currentUser, IClock clock, DutyGuard duty, IAuditTrail audit) :
  IQueryHandler<GetEmployeeDocumentsQuery, Result<GetEmployeeDocumentsQueryResult>>,
  IQueryHandler<GetExpiringDocumentsQuery, Result<GetExpiringDocumentsQueryResult>>,
  IQueryHandler<GetDocumentsToVerifyQuery, Result<GetExpiringDocumentsQueryResult>>,
  IQueryHandler<GetDocumentContentQuery, Result<StoredFile>>,
  ICommandHandler<UploadEmployeeDocumentCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateDocumentDetailsCommand, Result<UpdatedResult>>,
  ICommandHandler<ReplaceDocumentFileCommand, Result<UpdatedResult>>,
  ICommandHandler<ReviewDocumentCommand, Result<UpdatedResult>>,
  ICommandHandler<DeleteDocumentCommand, Result<UpdatedResult>>
{
  public async Task<Result<GetEmployeeDocumentsQueryResult>> Handle(GetEmployeeDocumentsQuery query, CancellationToken cancellationToken)
  {
    var employeeId = EmployeeId.Of(query.EmployeeId);
    if (!await context.Employees.AnyAsync(e => e.Id == employeeId, cancellationToken))
      throw new EmployeeNotFoundException($"Employee {query.EmployeeId} was not found.");

    var documents = await context.Documents.AsNoTracking().Where(d => d.EmployeeId == employeeId).OrderByDescending(d => d.CreatedAt).ToListAsync(cancellationToken);
    var types = await TypeNamesAsync(cancellationToken);
    var today = clock.Today;
    return Result<GetEmployeeDocumentsQueryResult>.Success(new(documents.Select(d => d.ToDto(types.GetValueOrDefault(d.DocumentTypeId), today)).ToList()));
  }

  public async Task<Result<GetExpiringDocumentsQueryResult>> Handle(GetExpiringDocumentsQuery query, CancellationToken cancellationToken)
  {
    var today = clock.Today;
    var until = today.AddDays(query.WithinDays);
    var documents = context.Documents.AsNoTracking().Where(d => d.ExpiryDate != null && d.ExpiryDate <= until && d.VerificationStatus != VerificationStatus.Rejected);
    if (!query.IncludeExpired)
      documents = documents.Where(d => d.ExpiryDate >= today);
    if (query.DocumentTypeId is { } type)
    {
      var typeId = DocumentTypeId.Of(type);
      documents = documents.Where(d => d.DocumentTypeId == typeId);
    }

    // only people still on the books, and only the latest paper of a kind (a renewed one replaces the old)
    var inService = EmploymentStatuses.InService;
    documents = documents.Where(d => context.Employees.Any(e => e.Id == d.EmployeeId && e.ProfileStatus == RecordStatus.Active
      && inService.Contains(e.EmploymentStatus)));
    documents = documents.Where(d => !context.Documents.Any(n => n.EmployeeId == d.EmployeeId && n.DocumentTypeId == d.DocumentTypeId && n.Id != d.Id
      && n.VerificationStatus != VerificationStatus.Rejected && n.ExpiryDate != null && n.ExpiryDate > d.ExpiryDate));

    var rows = await documents.OrderBy(d => d.ExpiryDate).Take(5000).ToListAsync(cancellationToken);
    var types = await TypeNamesAsync(cancellationToken);
    var people = await lookup.EmployeesAsync(rows.Select(d => (EmployeeId?)d.EmployeeId), cancellationToken);

    var data = rows.Select(d => new ExpiringDocumentDto(
      d.ToDto(types.GetValueOrDefault(d.DocumentTypeId), today),
      people.GetValueOrDefault(d.EmployeeId.Value)?.EmployeeNumber ?? "", people.GetValueOrDefault(d.EmployeeId.Value)?.FullName ?? "",
      d.ExpiryDate!.Value.DayNumber - today.DayNumber)).ToList();
    return Result<GetExpiringDocumentsQueryResult>.Success(new(today, data));
  }

  public async Task<Result<StoredFile>> Handle(GetDocumentContentQuery query, CancellationToken cancellationToken)
  {
    var document = await context.LoadDocumentAsync(query.Id, cancellationToken);
    if (query.OnlyForEmployee is { } own && document.EmployeeId.Value != own)
      throw new EmployeeDocumentNotFoundException($"Document {query.Id} was not found.");
    var type = await context.DocumentTypes.AsNoTracking().Where(t => t.Id == document.DocumentTypeId).Select(t => t.Name).FirstAsync(cancellationToken);
    var number = await context.Employees.AsNoTracking().Where(e => e.Id == document.EmployeeId).Select(e => e.EmployeeNumber).FirstAsync(cancellationToken);
    // a scanned paper (CNIC, degree, order) leaving the system is kept in the trail
    audit.Record(query.Download ? AuditActions.Downloaded : AuditActions.Viewed, document, query.OnlyForEmployee is null ? $"{type} scan" : $"{type} scan (own, through the portal)");
    await audit.SaveAsync(cancellationToken);
    return Result<StoredFile>.Success(await files.OpenAsync(document.FileReference, $"{number}-{type}{(document.DocumentNumber is null ? "" : "-" + document.DocumentNumber)}", cancellationToken));
  }

  public async Task<Result<CreatedResult>> Handle(UploadEmployeeDocumentCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    employee.EnsureProfileActive();
    var type = await context.LoadDocumentTypeAsync(command.Details.DocumentTypeId, cancellationToken);

    // check the details before the file is written
    var probe = EmployeeDocument.Create(EmployeeDocumentId.New(), employee.Id, type, command.Details.ToDetails(), "pending");
    var path = await files.SaveDocumentAsync(command.File, employee.EmployeeNumber, "documents", cancellationToken);
    probe.ReplaceFile(path);

    context.Documents.Add(probe);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(probe.Id);
  }

  public async Task<Result<GetExpiringDocumentsQueryResult>> Handle(GetDocumentsToVerifyQuery query, CancellationToken cancellationToken)
  {
    var today = clock.Today;
    var inService = EmploymentStatuses.InService;
    var documents = context.Documents.AsNoTracking().Where(d => (d.VerificationStatus == VerificationStatus.Unverified || d.VerificationStatus == VerificationStatus.Pending)
      && context.Employees.Any(e => e.Id == d.EmployeeId && e.ProfileStatus == RecordStatus.Active && inService.Contains(e.EmploymentStatus)));
    if (query.DocumentTypeId is { } type)
    {
      var typeId = DocumentTypeId.Of(type);
      documents = documents.Where(d => d.DocumentTypeId == typeId);
    }
    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var pattern = SearchPattern.Contains(query.Search);
      documents = documents.Where(d => context.Employees.Any(e => e.Id == d.EmployeeId && (EF.Functions.Like(e.EmployeeNumber.ToLower(), pattern, SearchPattern.Escape)
        || EF.Functions.Like(e.FullName!.ToLower(), pattern, SearchPattern.Escape))));
    }

    var rows = await documents.OrderBy(d => d.CreatedAt).Take(5000).ToListAsync(cancellationToken);
    var types = await TypeNamesAsync(cancellationToken);
    var people = await lookup.EmployeesAsync(rows.Select(d => (EmployeeId?)d.EmployeeId), cancellationToken);
    var data = rows.Select(d => new ExpiringDocumentDto(
      d.ToDto(types.GetValueOrDefault(d.DocumentTypeId), today),
      people.GetValueOrDefault(d.EmployeeId.Value)?.EmployeeNumber ?? "", people.GetValueOrDefault(d.EmployeeId.Value)?.FullName ?? "",
      d.ExpiryDate is { } expiry ? expiry.DayNumber - today.DayNumber : 0)).ToList();
    return Result<GetExpiringDocumentsQueryResult>.Success(new(today, data));
  }

  /// A deactivated profile is read-only: its papers do not change either.
  private async Task EnsureProfileActiveAsync(EmployeeId employeeId, CancellationToken cancellationToken)
  {
    var employee = await context.Employees.AsNoTracking().FirstAsync(e => e.Id == employeeId, cancellationToken);
    employee.EnsureProfileActive();
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateDocumentDetailsCommand command, CancellationToken cancellationToken)
  {
    var document = await context.LoadDocumentAsync(command.Id, cancellationToken);
    await EnsureProfileActiveAsync(document.EmployeeId, cancellationToken);
    var type = await context.LoadDocumentTypeAsync(command.Details.DocumentTypeId, cancellationToken);
    document.UpdateDetails(type, command.Details.ToDetails());
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(ReplaceDocumentFileCommand command, CancellationToken cancellationToken)
  {
    var document = await context.LoadDocumentAsync(command.Id, cancellationToken);
    await EnsureProfileActiveAsync(document.EmployeeId, cancellationToken);
    if (document.VerificationStatus == VerificationStatus.Verified)
      return Result<UpdatedResult>.Failure("A verified document cannot be changed. Upload a new one instead.");

    var number = await context.Employees.Where(e => e.Id == document.EmployeeId).Select(e => e.EmployeeNumber).FirstAsync(cancellationToken);
    document.ReplaceFile(await files.SaveDocumentAsync(command.File, number, "documents", cancellationToken));
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(ReviewDocumentCommand command, CancellationToken cancellationToken)
  {
    var document = await context.LoadDocumentAsync(command.Id, cancellationToken);
    await EnsureProfileActiveAsync(document.EmployeeId, cancellationToken);
    if (command.Action != DocumentReview.Submit)
      await duty.EnsureNotOwnRecordAsync(document.EmployeeId, "verify or reject a document", document, cancellationToken);
    switch (command.Action)
    {
      case DocumentReview.Submit:
        document.SubmitForVerification();
        audit.Record(HrmVerbs.Submitted, document);
        break;
      case DocumentReview.Verify:
        document.Verify(currentUser.UserId, clock.Today);
        audit.Record(HrmVerbs.Verified, document);
        break;
      case DocumentReview.Reject:
        document.Reject(currentUser.UserId, clock.Today);
        audit.Record(HrmVerbs.Rejected, document);
        break;
    }

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(DeleteDocumentCommand command, CancellationToken cancellationToken)
  {
    var document = await context.LoadDocumentAsync(command.Id, cancellationToken);
    await EnsureProfileActiveAsync(document.EmployeeId, cancellationToken);
    if (document.VerificationStatus == VerificationStatus.Verified)
      return Result<UpdatedResult>.Failure("A verified document stays on the file.");

    var referenced = await context.ServiceHistory.AnyAsync(h => h.SupportingDocumentId == document.Id, cancellationToken)
      || await context.PositionAssignments.AnyAsync(a => a.OrderDocumentId == document.Id, cancellationToken)
      || await context.HrActions.AnyAsync(a => a.SupportingDocumentId == document.Id, cancellationToken)
      || await context.Requests.AnyAsync(r => r.SupportingDocumentId == document.Id, cancellationToken)
      || await context.LeaveApplications.AnyAsync(a => a.SupportingDocumentId == document.Id, cancellationToken)
      || await context.ProfileChanges.AnyAsync(c => c.SupportingDocumentId == document.Id, cancellationToken);
    if (referenced)
      return Result<UpdatedResult>.Failure("This document supports a service record, an HR action, a request, a leave application or a profile change, so it stays on the file.");

    context.Documents.Remove(document);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  private async Task<Dictionary<DocumentTypeId, string>> TypeNamesAsync(CancellationToken cancellationToken) =>
      await context.DocumentTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);
}

// ---- education ----

public sealed record EducationInput(
  Guid QualificationLevelId,
  string DegreeTitle,
  string InstitutionName,
  string? BoardOrUniversity,
  int? PassingYear,
  string? GradeOrCgpa,
  bool IsHighestQualification)
{
  public EducationDetails ToDetails() => new(global::QualificationLevelId.Of(QualificationLevelId), DegreeTitle, InstitutionName, BoardOrUniversity, PassingYear, GradeOrCgpa, IsHighestQualification);
}

public sealed record GetEducationQueryResult(IReadOnlyList<EducationDto> Education);

public sealed record GetEducationQuery(Guid EmployeeId) : IQuery<Result<GetEducationQueryResult>>;

public sealed record AddEducationCommand(Guid EmployeeId, EducationInput Education) : ICommand<Result<CreatedResult>>;

public sealed record UpdateEducationCommand(Guid Id, EducationInput Education) : ICommand<Result<UpdatedResult>>;

public sealed record RemoveEducationCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

/// The scanned degree / certificate.
public sealed record AttachEducationFileCommand(Guid Id, UploadedFile File) : ICommand<Result<UpdatedResult>>;

public sealed record GetEducationContentQuery(Guid Id, bool Download = true) : IQuery<Result<StoredFile>>;

public class EducationInputValidator : AbstractValidator<EducationInput>
{
  public EducationInputValidator()
  {
    RuleFor(x => x.QualificationLevelId).NotEmpty();
    RuleFor(x => x.DegreeTitle).NotEmpty().MaximumLength(200);
    RuleFor(x => x.InstitutionName).NotEmpty().MaximumLength(200);
    RuleFor(x => x.BoardOrUniversity).MaximumLength(200);
    RuleFor(x => x.GradeOrCgpa).MaximumLength(30);
  }
}

public class AddEducationCommandValidator : AbstractValidator<AddEducationCommand>
{
  public AddEducationCommandValidator() => RuleFor(x => x.Education).NotNull().SetValidator(new EducationInputValidator());
}

public class UpdateEducationCommandValidator : AbstractValidator<UpdateEducationCommand>
{
  public UpdateEducationCommandValidator() => RuleFor(x => x.Education).NotNull().SetValidator(new EducationInputValidator());
}

public class EducationHandlers(IApplicationDbContext context, FileUploads files, IClock clock, IAuditTrail audit, DutyGuard duty) :
  IQueryHandler<GetEducationQuery, Result<GetEducationQueryResult>>,
  IQueryHandler<GetEducationContentQuery, Result<StoredFile>>,
  ICommandHandler<AddEducationCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateEducationCommand, Result<UpdatedResult>>,
  ICommandHandler<RemoveEducationCommand, Result<UpdatedResult>>,
  ICommandHandler<AttachEducationFileCommand, Result<UpdatedResult>>
{
  public async Task<Result<GetEducationQueryResult>> Handle(GetEducationQuery query, CancellationToken cancellationToken)
  {
    var employeeId = EmployeeId.Of(query.EmployeeId);
    if (!await context.Employees.AnyAsync(e => e.Id == employeeId, cancellationToken))
      throw new EmployeeNotFoundException($"Employee {query.EmployeeId} was not found.");

    var rows = await context.Educations.AsNoTracking().Where(e => e.EmployeeId == employeeId)
      .OrderByDescending(e => e.PassingYear).ToListAsync(cancellationToken);
    var levels = await context.QualificationLevels.AsNoTracking().ToDictionaryAsync(l => l.Id, cancellationToken);
    return Result<GetEducationQueryResult>.Success(new(rows
      .OrderByDescending(r => r.PassingYear).ThenByDescending(r => levels.GetValueOrDefault(r.QualificationLevelId)?.Rank ?? 0)
      .Select(r => r.ToDto(levels.ToDictionary(l => l.Key, l => l.Value.Name))).ToList()));
  }

  public async Task<Result<StoredFile>> Handle(GetEducationContentQuery query, CancellationToken cancellationToken)
  {
    var education = await LoadAsync(query.Id, cancellationToken);
    if (education.FileReference is null)
      throw new StoredFileNotFoundException("No scan is attached to this qualification.");
    var number = await context.Employees.AsNoTracking().Where(e => e.Id == education.EmployeeId).Select(e => e.EmployeeNumber).FirstAsync(cancellationToken);
    audit.Record(query.Download ? AuditActions.Downloaded : AuditActions.Viewed, education, "Qualification scan");
    await audit.SaveAsync(cancellationToken);
    return Result<StoredFile>.Success(await files.OpenAsync(education.FileReference, $"{number}-{education.DegreeTitle}", cancellationToken));
  }

  public async Task<Result<CreatedResult>> Handle(AddEducationCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    employee.EnsureProfileActive();
    await duty.EnsureNotOwnRecordAsync(employee.Id, "change the qualifications", employee, cancellationToken);
    (await context.LoadQualificationLevelAsync(command.Education.QualificationLevelId, cancellationToken)).EnsureActive();
    var others = await context.Educations.Where(e => e.EmployeeId == employee.Id).ToListAsync(cancellationToken);
    var education = EmployeeEducation.Create(EducationId.New(), employee.Id, command.Education.ToDetails(), others, clock.Today.Year);
    context.Educations.Add(education);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(education.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateEducationCommand command, CancellationToken cancellationToken)
  {
    var education = await LoadAsync(command.Id, cancellationToken);
    await duty.EnsureNotOwnRecordAsync(education.EmployeeId, "change the qualifications", education, cancellationToken);
    var level = await context.LoadQualificationLevelAsync(command.Education.QualificationLevelId, cancellationToken);
    if (level.Id != education.QualificationLevelId)
      level.EnsureActive();
    var others = await context.Educations.Where(e => e.EmployeeId == education.EmployeeId && e.Id != education.Id).ToListAsync(cancellationToken);
    education.Update(command.Education.ToDetails(), others, clock.Today.Year);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(RemoveEducationCommand command, CancellationToken cancellationToken)
  {
    var education = await LoadAsync(command.Id, cancellationToken);
    await duty.EnsureNotOwnRecordAsync(education.EmployeeId, "change the qualifications", education, cancellationToken);
    context.Educations.Remove(education);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(AttachEducationFileCommand command, CancellationToken cancellationToken)
  {
    var education = await LoadAsync(command.Id, cancellationToken);
    var number = await context.Employees.Where(e => e.Id == education.EmployeeId).Select(e => e.EmployeeNumber).FirstAsync(cancellationToken);
    education.AttachFile(await files.SaveDocumentAsync(command.File, number, "education", cancellationToken));
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  private async Task<EmployeeEducation> LoadAsync(Guid id, CancellationToken cancellationToken)
  {
    var educationId = EducationId.Of(id);
    return await context.Educations.FirstOrDefaultAsync(e => e.Id == educationId, cancellationToken)
      ?? throw new EducationNotFoundException($"Education record {id} was not found.");
  }
}
