using static Fixture;

public class CnicAndNtnTests
{
  [Theory]
  [InlineData("3520212345671")]
  [InlineData("35202-1234567-1")]
  [InlineData(" 35202 1234567 1 ")]
  public void Cnic_is_stored_in_the_dashed_form(string typed)
  {
    Assert.Equal("35202-1234567-1", Cnic.Of(typed).Value);
  }

  [Theory]
  [InlineData("12345")]
  [InlineData("35202-1234567-12")]
  [InlineData("35202-12345A7-1")]
  public void Cnic_must_have_13_digits(string typed)
  {
    Assert.Throws<DomainException>(() => Cnic.Of(typed));
  }

  [Fact]
  public void Cnic_of_all_zeros_is_refused()
  {
    var error = Assert.Throws<DomainException>(() => Cnic.Of("00000-0000000-0"));
    Assert.Contains("not a real CNIC", error.Message);
  }

  [Fact]
  public void Cnic_starting_with_zero_is_refused()
  {
    var error = Assert.Throws<DomainException>(() => Cnic.Of("05202-1234567-1"));
    Assert.Contains("cannot start with 0", error.Message);
  }

  [Fact]
  public void A_stored_cnic_is_read_back_even_when_it_breaks_the_newer_rules()
  {
    Assert.Equal("05202-1234567-1", Cnic.FromStored("0520212345671").Value);
  }

  [Theory]
  [InlineData("35202", "35202")]
  [InlineData("3520212", "35202-12")]
  [InlineData("35202-1234567", "35202-1234567")]
  [InlineData("3520212345671", "35202-1234567-1")]
  [InlineData("3520", null)]
  [InlineData("Ali", null)]
  public void A_cnic_prefix_is_put_in_the_dashed_form(string typed, string? expected)
  {
    Assert.Equal(expected, Cnic.NormalizePrefix(typed));
  }

  [Theory]
  [InlineData("1234567-8", "1234567-8")]
  [InlineData("12345678", "1234567-8")]
  [InlineData("1234567 8", "1234567-8")]
  [InlineData("3520212345671", "35202-1234567-1")]
  [InlineData("35202-1234567-1", "35202-1234567-1")]
  public void Ntn_is_stored_in_the_dashed_form(string typed, string expected)
  {
    Assert.Equal(expected, Ntn.Of(typed).Value);
  }

  [Theory]
  [InlineData("1234567")]
  [InlineData("123456789")]
  [InlineData("00000000")]
  [InlineData("0520212345671")]
  [InlineData("NTN-1234")]
  public void Ntn_must_be_7_plus_1_or_13_digits(string typed)
  {
    Assert.Throws<DomainException>(() => Ntn.Of(typed));
  }

  [Fact]
  public void Two_ntns_written_differently_have_the_same_digits()
  {
    Assert.Equal(Ntn.Digits(Ntn.Of("1234567-8").Value), Ntn.Digits("12345678"));
  }
}

public class OwnerTypeRuleTests
{
  private static readonly OwnerType Individual = Master<OwnerType>(SystemMasterCodes.OwnerTypeIndividual);

  private static PropertyOwner Register(OwnerType type, string? fatherHusband = null, string? cnic = null, string? ntn = null) =>
      PropertyOwner.Create(OwnerId.New(), BusinessCode.Of("OWN-00001"), type, Name.Of("Someone"), fatherHusband,
        Cnic.OfNullable(cnic), Ntn.OfNullable(ntn), null, null);

  [Fact]
  public void An_individual_needs_a_cnic()
  {
    var error = Assert.Throws<DomainException>(() => Register(Individual, "Father"));
    Assert.Contains("needs a CNIC", error.Message);
  }

  [Fact]
  public void An_individual_with_a_cnic_and_father_name_is_registered()
  {
    var owner = Register(Individual, "Muhammad Akbar", "3520212345671", "1234567-8");

    Assert.Equal(("35202-1234567-1", "Muhammad Akbar", "1234567-8"), (owner.Cnic!.Value, owner.FatherHusbandName, owner.Ntn));
  }

  [Theory]
  [InlineData(SystemMasterCodes.OwnerTypeCompany, "A company has no CNIC - use its NTN.")]
  [InlineData(SystemMasterCodes.OwnerTypeTrust, "A trust has no CNIC - use its NTN.")]
  [InlineData(SystemMasterCodes.OwnerTypeGovernment, "A government body has no CNIC - use its NTN.")]
  [InlineData(SystemMasterCodes.OwnerTypeJointEntity, "A joint entity has no CNIC - use its NTN.")]
  public void An_organization_has_no_cnic(string code, string message)
  {
    var error = Assert.Throws<DomainException>(() => Register(Master<OwnerType>(code), cnic: "3520212345671"));
    Assert.Contains(message, error.Message);
  }

  [Theory]
  [InlineData(SystemMasterCodes.OwnerTypeCompany)]
  [InlineData(SystemMasterCodes.OwnerTypeTrust)]
  [InlineData(SystemMasterCodes.OwnerTypeGovernment)]
  [InlineData(SystemMasterCodes.OwnerTypeJointEntity)]
  public void An_organization_has_no_father_or_husband_name(string code)
  {
    var error = Assert.Throws<DomainException>(() => Register(Master<OwnerType>(code), fatherHusband: "Someone"));
    Assert.Contains("no father / husband name", error.Message);
  }

  [Fact]
  public void A_company_is_registered_with_its_ntn()
  {
    var company = Company(ntn: "12345678");

    Assert.Equal(("1234567-8", (Cnic?)null), (company.Ntn, company.Cnic));
  }

  [Fact]
  public void An_owner_type_an_administrator_added_carries_no_rule()
  {
    var owner = Register(Master<OwnerType>("NGO"), "Someone");

    Assert.Null(owner.Cnic);
  }

  [Fact]
  public void Changing_an_individual_into_a_company_needs_the_cnic_removed()
  {
    var owner = Owner("Ali Khan");

    Assert.Throws<DomainException>(() => owner.Update(Master<OwnerType>(SystemMasterCodes.OwnerTypeCompany), Name.Of("Ali Khan & Co"),
      null, owner.Cnic, null, null, null));

    owner.Update(Master<OwnerType>(SystemMasterCodes.OwnerTypeCompany), Name.Of("Ali Khan & Co"), null, null, Ntn.Of("1234567-8"), null, null);
    Assert.Null(owner.Cnic);
  }
}

public class OwnerDeactivationTests
{
  [Fact]
  public void An_owner_with_nothing_left_is_deactivated()
  {
    var owner = Owner();

    owner.Deactivate(PropertyOwner.Commitments.None);

    Assert.False(owner.IsActive);
  }

  [Fact]
  public void An_owner_holding_a_share_or_a_pending_transfer_or_an_agreement_is_not_deactivated()
  {
    var owner = Owner("Shazia Bibi");

    var error = Assert.Throws<DomainException>(() => owner.Deactivate(new PropertyOwner.Commitments(
      ["PROP-00007 (40%)", "PROP-00009 (25%, disputed)"], ["TRF-00041"], ["ALT-00002", "LSE-00005 (draft)"])));

    Assert.True(owner.IsActive);
    Assert.Contains("PROP-00007 (40%), PROP-00009 (25%, disputed)", error.Message);
    Assert.Contains("pending transfer TRF-00041", error.Message);
    Assert.Contains("ALT-00002, LSE-00005 (draft)", error.Message);
  }

  [Theory]
  [InlineData(true, false, false)]
  [InlineData(false, true, false)]
  [InlineData(false, false, true)]
  public void Any_one_commitment_blocks_deactivation(bool share, bool transfer, bool agreement)
  {
    var owner = Owner();
    var commitments = new PropertyOwner.Commitments(share ? ["PROP-00001 (100%)"] : [], transfer ? ["TRF-00001"] : [], agreement ? ["RNT-00001"] : []);

    Assert.Throws<DomainException>(() => owner.Deactivate(commitments));
  }
}

public class OwnerDocumentTests
{
  private static readonly PropertyDocument.StoredFile File = new("a.pdf", "/owner-documents/OWN-00001/cnic/a.pdf", "application/pdf", 10, new string('a', 64));
  private static readonly PropertyDocument.Details NoDetails = new(null, null, null, null, false);

  private static DocumentType OwnerKind(string code, string folder = "papers") =>
      Master<DocumentType>(code, new MasterExtras(StorageFolder: folder, AppliesTo: DocumentScope.Owner));

  private static readonly DocumentType CnicCopy = OwnerKind(SystemMasterCodes.DocumentCnicCopy, "cnic");
  private static readonly DocumentType NtnCertificate = OwnerKind("NTN_CERTIFICATE", "ntn");
  private static readonly DocumentType Notice = Master<DocumentType>("NOTICE_LETTER");

  private static PropertyDocument OwnerPaper(PropertyOwner owner, DocumentType type, DateTime? uploadedAt = null) =>
      PropertyDocument.Create(DocumentId.New(), null, type, DocumentEntityType.Owner, owner.Id.Value, "paper.pdf", File, NoDetails,
        Guid.NewGuid(), uploadedAt ?? DateTime.UtcNow);

  [Fact]
  public void A_document_type_applies_to_property_unless_told_otherwise()
  {
    Assert.Equal((DocumentScope.Property, DocumentScope.Owner), (Notice.AppliesTo, CnicCopy.AppliesTo));
  }

  [Fact]
  public void The_cnic_copy_type_always_applies_to_owners()
  {
    Assert.Throws<DomainException>(() => Master<DocumentType>(SystemMasterCodes.DocumentCnicCopy, new MasterExtras(StorageFolder: "cnic")));
    Assert.Throws<DomainException>(() => CnicCopy.Update(Name.Of("CNIC Copy"), null, 0, new MasterExtras(AppliesTo: DocumentScope.Property)));
  }

  [Fact]
  public void An_owner_paper_belongs_to_no_property()
  {
    var owner = Owner();
    var document = OwnerPaper(owner, CnicCopy);

    Assert.Null(document.PropertyId);
    Assert.True(document.IsOwnerDocument);
  }

  [Fact]
  public void An_owner_paper_filed_under_a_property_is_refused()
  {
    var owner = Owner();
    var error = Assert.Throws<DomainException>(() => PropertyDocument.Create(DocumentId.New(), Property().Id, CnicCopy, DocumentEntityType.Owner,
      owner.Id.Value, "cnic.pdf", File, NoDetails, Guid.NewGuid(), DateTime.UtcNow));

    Assert.Contains("not filed under a property", error.Message);
  }

  [Fact]
  public void A_property_kind_cannot_be_filed_on_an_owner()
  {
    var error = Assert.Throws<DomainException>(() => OwnerPaper(Owner(), Notice));

    Assert.Contains("property document", error.Message);
  }

  [Fact]
  public void An_owner_kind_cannot_be_filed_on_a_property()
  {
    var property = Property();
    var error = Assert.Throws<DomainException>(() => PropertyDocument.Create(DocumentId.New(), property.Id, CnicCopy, DocumentEntityType.Property,
      property.Id.Value, "cnic.pdf", File, NoDetails, Guid.NewGuid(), DateTime.UtcNow));

    Assert.Contains("owner's document", error.Message);
  }

  [Fact]
  public void The_filing_rules_can_be_checked_before_the_file_is_stored()
  {
    Assert.Throws<DomainException>(() => PropertyDocument.EnsureCanFile(null, Notice, DocumentEntityType.Owner, Guid.NewGuid()));
    PropertyDocument.EnsureCanFile(null, CnicCopy, DocumentEntityType.Owner, Guid.NewGuid());
  }

  [Fact]
  public void A_document_is_re_typed_only_within_its_scope()
  {
    var document = OwnerPaper(Owner(), CnicCopy);

    document.UpdateDetails(NtnCertificate, NoDetails);
    Assert.Equal(NtnCertificate.Id, document.DocumentTypeId);

    Assert.Throws<DomainException>(() => document.UpdateDetails(Notice, NoDetails));
  }

  [Fact]
  public void A_new_version_of_an_owner_paper_stays_without_a_property()
  {
    var document = OwnerPaper(Owner(), CnicCopy);

    var version = document.NewVersion(DocumentId.New(), "cnic-v2.pdf", File, Guid.NewGuid(), DateTime.UtcNow);

    Assert.Equal(((PropertyId?)null, DocumentEntityType.Owner, document.EntityId, 2), (version.PropertyId, version.EntityType, version.EntityId, version.VersionNo));
  }

  [Fact]
  public void Cnic_pointer_follows_the_latest_active_cnic_copy()
  {
    var owner = Owner();
    var first = OwnerPaper(owner, CnicCopy, DateTime.UtcNow.AddDays(-2));
    var ntn = OwnerPaper(owner, NtnCertificate, DateTime.UtcNow);
    var documents = new List<PropertyDocument> { first, ntn };

    // upload
    owner.RefreshCnicDocument(documents, CnicCopy);
    Assert.Equal(first.Id, owner.CnicDocumentId);

    // a new version supersedes the old copy
    var second = first.NewVersion(DocumentId.New(), "cnic-v2.pdf", File, Guid.NewGuid(), DateTime.UtcNow.AddDays(-1));
    documents.Add(second);
    owner.RefreshCnicDocument(documents, CnicCopy);
    Assert.Equal(second.Id, owner.CnicDocumentId);

    // the latest version deactivated: the superseded one does not come back
    second.Deactivate();
    owner.RefreshCnicDocument(documents, CnicCopy);
    Assert.Null(owner.CnicDocumentId);

    // and activated again
    second.Activate();
    owner.RefreshCnicDocument(documents, CnicCopy);
    Assert.Equal(second.Id, owner.CnicDocumentId);

    // re-typed away from CNIC Copy
    second.UpdateDetails(NtnCertificate, NoDetails);
    owner.RefreshCnicDocument(documents, CnicCopy);
    Assert.Null(owner.CnicDocumentId);

    // the NTN certificate re-typed to CNIC Copy
    ntn.UpdateDetails(CnicCopy, NoDetails);
    owner.RefreshCnicDocument(documents, CnicCopy);
    Assert.Equal(ntn.Id, owner.CnicDocumentId);
  }

  [Fact]
  public void Cnic_pointer_ignores_other_owners_papers()
  {
    var owner = Owner();
    var someoneElse = OwnerPaper(Owner(), CnicCopy);

    owner.RefreshCnicDocument([someoneElse], CnicCopy);

    Assert.Null(owner.CnicDocumentId);
  }

  [Fact]
  public void Cnic_pointer_needs_the_cnic_copy_type()
  {
    Assert.Throws<DomainException>(() => Owner().RefreshCnicDocument([], NtnCertificate));
  }
}
