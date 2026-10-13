public class RecordEncroachmentHandler(IApplicationDbContext context, MasterLookup masters, CodeGenerator codes)
  : ICommandHandler<RecordEncroachmentCommand, Result<RecordEncroachmentCommandResult>>
{
  public async Task<Result<RecordEncroachmentCommandResult>> Handle(RecordEncroachmentCommand command, CancellationToken cancellationToken)
  {
    var input = command.Encroachment;
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);

    var status = input.EncroachmentStatusId is { } statusId
      ? await masters.GetAsync<EncroachmentStatus>(statusId, cancellationToken)
      : await masters.GetByCodeAsync<EncroachmentStatus>(SystemMasterCodes.Active, cancellationToken);

    // a notice given with the case moves it to UNDER_NOTICE; a notice date without its number is refused
    var hasNotice = !string.IsNullOrWhiteSpace(input.NoticeNo) || input.NoticeDate is not null;
    var underNotice = hasNotice ? await masters.GetByCodeAsync<EncroachmentStatus>(SystemMasterCodes.UnderNotice, cancellationToken) : null;

    var encroachment = PropertyEncroachment.Record(
      EncroachmentId.New(), property, await codes.NextAsync(CodeSequenceKeys.Encroachment, cancellationToken),
      input.EncroachmentArea, await masters.GetAsync<MeasurementUnit>(input.MeasurementUnitId, cancellationToken), status,
      input.DetectionDate, await input.Details.ToDetailsAsync(context, cancellationToken), input.Points.ToGeoPoints(),
      PropertyWriteGuard.Today(), new PropertyEncroachment.Notice(input.NoticeNo, input.NoticeDate), underNotice);

    await context.Encroachments.AddAsync(encroachment, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RecordEncroachmentCommandResult>.Success(new RecordEncroachmentCommandResult(encroachment.Id.Value, encroachment.EncroachmentNo.Value));
  }
}
