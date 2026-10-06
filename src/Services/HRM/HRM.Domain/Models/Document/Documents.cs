/// Kinds of employee paper (CNIC, Domicile, Appointment Order ...). requires_expiry: the paper runs out (CNIC, medical).
public class DocumentType : Aggregate<DocumentTypeId>
{
  public string Name { get; private set; } = default!;
  public bool RequiresExpiry { get; private set; }
  public bool IsActive { get; private set; }

  public static DocumentType Create(DocumentTypeId id, string name, bool requiresExpiry)
  {
    var type = new DocumentType { Id = id, IsActive = true };
    type.Update(name, requiresExpiry);
    return type;
  }

  public void Update(string name, bool requiresExpiry)
  {
    Name = Guard.RequiredText(name, 100, "Document type name");
    RequiresExpiry = requiresExpiry;
  }

  public void SetActive(bool isActive) => IsActive = isActive;

  public void EnsureActive()
  {
    if (!IsActive)
      throw new DomainException($"Document type '{Name}' is inactive.");
  }
}

public sealed record DocumentDetails(string? DocumentNumber, DateOnly? IssueDate, DateOnly? ExpiryDate);

/// A paper on an employee's file. The file itself is on the GDA file server; file_reference is its relative path.
/// Verification: unverified -> pending (sent for checking) -> verified / rejected. A verified paper is frozen.
public class EmployeeDocument : Aggregate<EmployeeDocumentId>
{
  public EmployeeId EmployeeId { get; private set; } = default!;
  public DocumentTypeId DocumentTypeId { get; private set; } = default!;
  public string? DocumentNumber { get; private set; }
  public DateOnly? IssueDate { get; private set; }
  public DateOnly? ExpiryDate { get; private set; }
  public string FileReference { get; private set; } = default!;
  public VerificationStatus VerificationStatus { get; private set; }
  public Guid? VerifiedBy { get; private set; }
  public DateOnly? VerificationDate { get; private set; }

  public static EmployeeDocument Create(EmployeeDocumentId id, EmployeeId employeeId, DocumentType type, DocumentDetails details, string fileReference)
  {
    ArgumentNullException.ThrowIfNull(employeeId);
    ArgumentNullException.ThrowIfNull(type);
    type.EnsureActive();

    var document = new EmployeeDocument
    {
      Id = id,
      EmployeeId = employeeId,
      DocumentTypeId = type.Id,
      FileReference = Guard.RequiredText(fileReference, 500, "File"),
      VerificationStatus = VerificationStatus.Unverified
    };
    document.Apply(type, details);
    return document;
  }

  public void UpdateDetails(DocumentType type, DocumentDetails details)
  {
    EnsureNotVerified();
    if (type.Id != DocumentTypeId)
      type.EnsureActive();
    Apply(type, details);
  }

  /// A better scan of the same paper. A verified paper cannot be swapped; upload a new document instead.
  public void ReplaceFile(string fileReference)
  {
    EnsureNotVerified();
    FileReference = Guard.RequiredText(fileReference, 500, "File");
    if (VerificationStatus == VerificationStatus.Rejected)
      VerificationStatus = VerificationStatus.Unverified;
  }

  public void SubmitForVerification()
  {
    if (VerificationStatus is not (VerificationStatus.Unverified or VerificationStatus.Rejected))
      throw new DomainException($"The document is already {VerificationStatus.ToString().ToLowerInvariant()}.");

    VerificationStatus = VerificationStatus.Pending;
    VerifiedBy = null;
    VerificationDate = null;
  }

  public void Verify(Guid? verifiedBy, DateOnly verificationDate)
  {
    if (VerificationStatus == VerificationStatus.Verified)
      throw new DomainException("The document is already verified.");

    VerifiedBy = Guard.Actor(verifiedBy, "verify a document");
    VerificationDate = verificationDate;
    VerificationStatus = VerificationStatus.Verified;
  }

  /// Rejected papers record who rejected them and when; the employee uploads a corrected scan.
  public void Reject(Guid? rejectedBy, DateOnly rejectionDate)
  {
    if (VerificationStatus == VerificationStatus.Verified)
      throw new DomainException("A verified document cannot be rejected.");

    VerifiedBy = Guard.Actor(rejectedBy, "reject a document");
    VerificationDate = rejectionDate;
    VerificationStatus = VerificationStatus.Rejected;
  }

  public bool IsExpiredOn(DateOnly date) => ExpiryDate is { } expiry && expiry < date;

  private void EnsureNotVerified()
  {
    if (VerificationStatus == VerificationStatus.Verified)
      throw new DomainException("A verified document cannot be changed. Upload a new one instead.");
  }

  private void Apply(DocumentType type, DocumentDetails details)
  {
    ArgumentNullException.ThrowIfNull(details);

    if (type.RequiresExpiry && details.ExpiryDate is null)
      throw new DomainException($"A {type.Name} needs its expiry date.");

    Guard.DateOrder(details.IssueDate, details.ExpiryDate, "Issue date", "Expiry date");

    DocumentTypeId = type.Id;
    DocumentNumber = Guard.Text(details.DocumentNumber, 100, "Document number");
    IssueDate = details.IssueDate;
    ExpiryDate = details.ExpiryDate;
  }
}

public sealed record EducationDetails(
  QualificationLevel QualificationLevel,
  string DegreeTitle,
  string InstitutionName,
  string? BoardOrUniversity,
  int? PassingYear,
  string? GradeOrCgpa,
  bool IsHighestQualification);

/// A qualification of an employee. At most one is marked as the highest.
public class EmployeeEducation : Aggregate<EducationId>
{
  public EmployeeId EmployeeId { get; private set; } = default!;
  public QualificationLevel QualificationLevel { get; private set; }
  public string DegreeTitle { get; private set; } = default!;
  public string InstitutionName { get; private set; } = default!;
  public string? BoardOrUniversity { get; private set; }
  public int? PassingYear { get; private set; }
  public string? GradeOrCgpa { get; private set; }
  public bool IsHighestQualification { get; private set; }
  public string? FileReference { get; private set; }

  /// `others` = the employee's other education records, so the highest flag can move here.
  public static EmployeeEducation Create(EducationId id, EmployeeId employeeId, EducationDetails details, IReadOnlyCollection<EmployeeEducation> others, int currentYear)
  {
    ArgumentNullException.ThrowIfNull(employeeId);

    var education = new EmployeeEducation { Id = id, EmployeeId = employeeId };
    education.Apply(details, others, currentYear);
    return education;
  }

  public void Update(EducationDetails details, IReadOnlyCollection<EmployeeEducation> others, int currentYear) =>
      Apply(details, others, currentYear);

  public void AttachFile(string? fileReference) => FileReference = Guard.Text(fileReference, 500, "File");

  private void Apply(EducationDetails details, IReadOnlyCollection<EmployeeEducation> others, int currentYear)
  {
    ArgumentNullException.ThrowIfNull(details);

    QualificationLevel = details.QualificationLevel;
    DegreeTitle = Guard.RequiredText(details.DegreeTitle, 200, "Degree title");
    InstitutionName = Guard.RequiredText(details.InstitutionName, 200, "Institution");
    BoardOrUniversity = Guard.Text(details.BoardOrUniversity, 200, "Board / university");
    GradeOrCgpa = Guard.Text(details.GradeOrCgpa, 30, "Grade / CGPA");

    if (details.PassingYear is { } year && (year < 1950 || year > Math.Min(2100, currentYear + 1)))
      throw new DomainException($"Passing year must be between 1950 and {Math.Min(2100, currentYear + 1)}.");
    PassingYear = details.PassingYear;

    if (details.IsHighestQualification)
    {
      foreach (var other in others.Where(o => o.Id != Id && o.EmployeeId == EmployeeId && o.IsHighestQualification))
        other.IsHighestQualification = false;
    }

    IsHighestQualification = details.IsHighestQualification;
  }
}

/// How an employee was recruited: Project Regularization / Board of Authority / Public Service Commission ...
public class RecruitmentMethod : Aggregate<RecruitmentMethodId>
{
  public string Name { get; private set; } = default!;
  public bool IsActive { get; private set; }

  public static RecruitmentMethod Create(RecruitmentMethodId id, string name) =>
      new() { Id = id, Name = Guard.RequiredText(name, 150, "Recruitment method"), IsActive = true };

  public void Update(string name) => Name = Guard.RequiredText(name, 150, "Recruitment method");

  public void SetActive(bool isActive) => IsActive = isActive;

  public void EnsureActive()
  {
    if (!IsActive)
      throw new DomainException($"Recruitment method '{Name}' is inactive.");
  }
}
