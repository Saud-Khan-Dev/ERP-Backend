public sealed record UploadDocumentVersionCommandResult(Guid Id, int VersionNo, Guid SupersedesDocumentId);

/// A new version of a file: a new row with version_no + 1 pointing at the one it replaces.
public sealed record UploadDocumentVersionCommand(Guid DocumentId, DocumentService.Upload File) : ICommand<Result<UploadDocumentVersionCommandResult>>;
