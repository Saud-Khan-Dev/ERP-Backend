using FluentValidation;
using Microsoft.EntityFrameworkCore;

// ---- notice templates ----

public sealed record NoticeTemplateInput(string Name, string TitleText, string ParagraphText, string? SourceDocument);

public sealed record GetNoticeTemplatesQueryResult(IReadOnlyList<NoticeTemplateDto> Templates);
public sealed record GetNoticeTemplatesQuery : IQuery<Result<GetNoticeTemplatesQueryResult>>;

/// A saved change of the title or paragraph raises the template's version; issued notices keep theirs.
public sealed record UpdateNoticeTemplateCommand(NoticeStage Stage, NoticeTemplateInput Template) : ICommand<Result<UpdatedResult>>;

public class UpdateNoticeTemplateCommandValidator : AbstractValidator<UpdateNoticeTemplateCommand>
{
  public UpdateNoticeTemplateCommandValidator()
  {
    RuleFor(x => x.Template.Name).NotEmpty().MaximumLength(100);
    RuleFor(x => x.Template.TitleText).NotEmpty().MaximumLength(200);
    RuleFor(x => x.Template.ParagraphText).NotEmpty().MaximumLength(4000);
    RuleFor(x => x.Template.SourceDocument).MaximumLength(255);
  }
}

// ---- letterhead ----

public sealed record GetLetterheadQueryResult(LetterheadDto Current, IReadOnlyList<LetterheadDto> History);
public sealed record GetLetterheadQuery : IQuery<Result<GetLetterheadQueryResult>>;

/// A new letterhead from a date. LogoLeftRef / LogoRightRef: a file uploaded with UploadLogo, "" to remove, null to keep.
public sealed record ReviseLetterheadCommand(LetterheadInput Letterhead, string? LogoLeftRef, string? LogoRightRef, DateOnly? EffectiveFrom) : ICommand<Result<CreatedResult>>;

public sealed record UploadLogoCommand(Stream Content, string FileName, long Length) : ICommand<Result<UploadLogoResult>>;
public sealed record UploadLogoResult(string FileRef);

public sealed record LogoFile(Stream Content, string ContentType, string FileName);
public sealed record GetLogoQuery(Guid LetterheadId, string Side) : IQuery<Result<LogoFile>>;

public class ReviseLetterheadCommandValidator : AbstractValidator<ReviseLetterheadCommand>
{
  public ReviseLetterheadCommandValidator()
  {
    RuleFor(x => x.Letterhead).NotNull();
    RuleFor(x => x.Letterhead.GovernmentLine).NotEmpty().MaximumLength(150);
    RuleFor(x => x.Letterhead.AuthorityLine).NotEmpty().MaximumLength(150);
    RuleFor(x => x.Letterhead.BoardReference).NotEmpty().MaximumLength(100);
  }
}

// ---- notice table layout ----

public sealed record GetTableLayoutQueryResult(TableLayoutDto Current, IReadOnlyList<HeadDto> Heads);
public sealed record GetTableLayoutQuery : IQuery<Result<GetTableLayoutQueryResult>>;

/// A new layout from a date (today when not given); notices already issued keep the table they were printed with.
public sealed record ReviseTableLayoutCommand(TableLayoutInput Layout, DateOnly? EffectiveFrom) : ICommand<Result<CreatedResult>>;

public class ReviseTableLayoutCommandValidator : AbstractValidator<ReviseTableLayoutCommand>
{
  public ReviseTableLayoutCommandValidator()
  {
    RuleFor(x => x.Layout).NotNull();
    RuleFor(x => x.Layout.BoardReference).NotEmpty().MaximumLength(100);
    RuleFor(x => x.Layout.Rows).NotEmpty();
  }
}

public class NoticeSetupHandlers(IApplicationDbContext context, TaxSetup setup, IFileStorage storage, IClock clock) :
  IQueryHandler<GetNoticeTemplatesQuery, Result<GetNoticeTemplatesQueryResult>>,
  ICommandHandler<UpdateNoticeTemplateCommand, Result<UpdatedResult>>,
  IQueryHandler<GetLetterheadQuery, Result<GetLetterheadQueryResult>>,
  ICommandHandler<ReviseLetterheadCommand, Result<CreatedResult>>,
  ICommandHandler<UploadLogoCommand, Result<UploadLogoResult>>,
  IQueryHandler<GetLogoQuery, Result<LogoFile>>,
  IQueryHandler<GetTableLayoutQuery, Result<GetTableLayoutQueryResult>>,
  ICommandHandler<ReviseTableLayoutCommand, Result<CreatedResult>>
{
  private const long MaxLogoBytes = 1024 * 1024;
  private static readonly Dictionary<string, string> LogoTypes = new(StringComparer.OrdinalIgnoreCase)
  {
    [".png"] = "image/png",
    [".jpg"] = "image/jpeg",
    [".jpeg"] = "image/jpeg",
    [".svg"] = "image/svg+xml",
    [".webp"] = "image/webp"
  };

  public async Task<Result<GetNoticeTemplatesQueryResult>> Handle(GetNoticeTemplatesQuery query, CancellationToken cancellationToken)
  {
    var templates = await context.NoticeTemplates.AsNoTracking().OrderBy(t => t.Stage).ToListAsync(cancellationToken);
    return Result<GetNoticeTemplatesQueryResult>.Success(new(templates.Select(t => t.ToDto()).ToList()));
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateNoticeTemplateCommand command, CancellationToken cancellationToken)
  {
    var template = await context.NoticeTemplates.FirstOrDefaultAsync(t => t.Stage == command.Stage, cancellationToken)
      ?? throw new TemplateNotFoundException($"There is no {EnumText.Words(command.Stage)} notice template.");
    var t = command.Template;
    template.Change(t.Name, t.TitleText, t.ParagraphText, t.SourceDocument, clock.Today);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<GetLetterheadQueryResult>> Handle(GetLetterheadQuery query, CancellationToken cancellationToken)
  {
    var current = await setup.LetterheadAsync(clock.Today, cancellationToken);
    var history = await context.Letterheads.AsNoTracking().OrderByDescending(l => l.EffectiveFrom).ThenByDescending(l => l.CreatedAt).ToListAsync(cancellationToken);
    return Result<GetLetterheadQueryResult>.Success(new(current.ToDto(), history.Select(l => l.ToDto()).ToList()));
  }

  public async Task<Result<CreatedResult>> Handle(ReviseLetterheadCommand command, CancellationToken cancellationToken)
  {
    var from = command.EffectiveFrom ?? clock.Today;
    var current = await setup.LetterheadAsync(from, cancellationToken);
    var left = Logo(command.LogoLeftRef, current.LogoLeftRef);
    var right = Logo(command.LogoRightRef, current.LogoRightRef);
    var next = current.Revise(NoticeLetterheadId.New(), command.Letterhead, left, right, from);
    context.Letterheads.Add(next);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(next.Id);
  }

  public async Task<Result<UploadLogoResult>> Handle(UploadLogoCommand command, CancellationToken cancellationToken)
  {
    var extension = Path.GetExtension(command.FileName ?? "");
    if (!LogoTypes.ContainsKey(extension))
      throw new DomainException("A logo is a PNG, JPG, SVG or WEBP image.");
    if (command.Length <= 0 || command.Length > MaxLogoBytes)
      throw new DomainException("A logo can be up to 1 MB.");
    var path = await storage.SaveAsync(command.Content, "letterhead", extension.ToLowerInvariant(), cancellationToken);
    return Result<UploadLogoResult>.Success(new(path));
  }

  public async Task<Result<LogoFile>> Handle(GetLogoQuery query, CancellationToken cancellationToken)
  {
    var id = NoticeLetterheadId.Of(query.LetterheadId);
    var letterhead = await context.Letterheads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, cancellationToken)
      ?? throw new TemplateNotFoundException($"Letterhead {query.LetterheadId} was not found.");
    var path = query.Side.ToLowerInvariant() switch
    {
      "left" => letterhead.LogoLeftRef,
      "right" => letterhead.LogoRightRef,
      _ => throw new DomainException("The side is left or right.")
    } ?? throw new StoredFileNotFoundException("This letterhead has no logo on that side.");
    var stream = await storage.OpenReadAsync(path, cancellationToken) ?? throw new StoredFileNotFoundException("The logo file is missing from storage.");
    var type = LogoTypes.GetValueOrDefault(Path.GetExtension(path), "application/octet-stream");
    return Result<LogoFile>.Success(new(stream, type, Path.GetFileName(path)));
  }

  public async Task<Result<GetTableLayoutQueryResult>> Handle(GetTableLayoutQuery query, CancellationToken cancellationToken)
  {
    var layout = await setup.LayoutAsync(clock.Today, cancellationToken);
    var heads = await setup.HeadsAsync(cancellationToken);
    return Result<GetTableLayoutQueryResult>.Success(new(layout.ToDto(), heads.Select(h => h.ToDto()).ToList()));
  }

  public async Task<Result<CreatedResult>> Handle(ReviseTableLayoutCommand command, CancellationToken cancellationToken)
  {
    var from = command.EffectiveFrom ?? clock.Today;
    var current = await setup.LayoutAsync(from, cancellationToken);
    var (known, required) = await setup.LayoutHeadsAsync(cancellationToken);
    var next = NoticeTableLayout.Create(NoticeTableLayoutId.New(), command.Layout, known, required, from);
    current.EndBefore(from);
    context.TableLayouts.Add(next);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(next.Id);
  }

  /// "" removes the logo, null keeps the current one, anything else must be a file uploaded to the letterhead folder.
  private static string? Logo(string? requested, string? current)
  {
    if (requested is null)
      return current;
    if (requested.Length == 0)
      return null;
    var path = requested.Replace('\\', '/').Trim();
    if (!path.StartsWith("letterhead/", StringComparison.Ordinal) || path.Contains(".."))
      throw new DomainException("Upload the logo first and pass the reference it returned.");
    return path;
  }
}
