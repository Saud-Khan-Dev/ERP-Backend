using FluentValidation;
using Microsoft.EntityFrameworkCore;

public sealed record PostDetailsInput(
  Guid DesignationId,
  Guid GradeId,
  Guid OrgUnitId,
  Guid? ReportingPostId,
  EmploymentType EmploymentType,
  Guid? LocationId,
  int SanctionedCount,
  string? NotificationRef);

/// A newly sanctioned post. Leave PostCode empty to have the next POST-0001 style code issued.
public sealed record CreatePostCommand(string? PostCode, PostDetailsInput Details, DateOnly EffectiveFrom) : ICommand<Result<CreatePostCommandResult>>;

public sealed record CreatePostCommandResult(Guid Id, string PostCode);

/// A change from a date: upgrade, move, more or fewer seats, another reporting post. The current version closes the day before.
public sealed record RevisePostCommand(Guid Id, PostDetailsInput Details, DateOnly EffectiveFrom) : ICommand<Result<CreatedResult>>;

/// Fixes a mistake in the latest version without starting a new period.
public sealed record CorrectPostCommand(Guid Id, PostDetailsInput Details) : ICommand<Result<UpdatedResult>>;

public enum PostLifecycleAction
{
  Freeze,
  Unfreeze,
  Abolish
}

public sealed record ChangePostLifecycleCommand(Guid Id, PostLifecycleAction Action, DateOnly EffectiveFrom, string? NotificationRef) : ICommand<Result<CreatedResult>>;

public class PostDetailsInputValidator : AbstractValidator<PostDetailsInput>
{
  public PostDetailsInputValidator()
  {
    RuleFor(x => x.DesignationId).NotEmpty();
    RuleFor(x => x.GradeId).NotEmpty();
    RuleFor(x => x.OrgUnitId).NotEmpty();
    RuleFor(x => x.EmploymentType).IsInEnum();
    RuleFor(x => x.SanctionedCount).InclusiveBetween(1, 10000);
    RuleFor(x => x.NotificationRef).MaximumLength(200);
  }
}

public class CreatePostCommandValidator : AbstractValidator<CreatePostCommand>
{
  public CreatePostCommandValidator()
  {
    RuleFor(x => x.PostCode).MaximumLength(Post.CodeMaxLength);
    RuleFor(x => x.Details).NotNull().SetValidator(new PostDetailsInputValidator());
  }
}

public class RevisePostCommandValidator : AbstractValidator<RevisePostCommand>
{
  public RevisePostCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Details).NotNull().SetValidator(new PostDetailsInputValidator());
  }
}

public class CorrectPostCommandValidator : AbstractValidator<CorrectPostCommand>
{
  public CorrectPostCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Details).NotNull().SetValidator(new PostDetailsInputValidator());
  }
}

public class ChangePostLifecycleCommandValidator : AbstractValidator<ChangePostLifecycleCommand>
{
  public ChangePostLifecycleCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Action).IsInEnum();
    RuleFor(x => x.NotificationRef).MaximumLength(200);
  }
}

public class PostHandlers(IApplicationDbContext context, CodeIssuer codes, ServiceRecordReader records, ICurrentUser currentUser) :
  ICommandHandler<CreatePostCommand, Result<CreatePostCommandResult>>,
  ICommandHandler<RevisePostCommand, Result<CreatedResult>>,
  ICommandHandler<CorrectPostCommand, Result<UpdatedResult>>,
  ICommandHandler<ChangePostLifecycleCommand, Result<CreatedResult>>
{
  public async Task<Result<CreatePostCommandResult>> Handle(CreatePostCommand command, CancellationToken cancellationToken)
  {
    string code;
    if (string.IsNullOrWhiteSpace(command.PostCode))
    {
      code = await codes.NextPostCodeAsync(cancellationToken);
    }
    else
    {
      code = Guard.Code(command.PostCode, Post.CodeMaxLength, "Post code");
      if (await context.Posts.AnyAsync(p => p.PostCode == code, cancellationToken))
        return Result<CreatePostCommandResult>.Failure($"A post with code {code} already exists.");
    }

    var postId = PostId.New();
    var details = await ResolveAsync(postId, command.Details, command.EffectiveFrom, previous: null, cancellationToken);
    var post = Post.Create(postId, code, details, command.EffectiveFrom, currentUser.UserId);

    context.Posts.Add(post);
    await context.SaveChangesAsync(cancellationToken);
    return Result<CreatePostCommandResult>.Success(new(post.Id.Value, post.PostCode));
  }

  public async Task<Result<CreatedResult>> Handle(RevisePostCommand command, CancellationToken cancellationToken)
  {
    var post = await context.LoadPostAsync(command.Id, cancellationToken);
    var details = await ResolveAsync(post.Id, command.Details, command.EffectiveFrom, post.Latest.Details, cancellationToken);
    var filled = await records.FilledSeatsOnAsync(post.Id, command.EffectiveFrom, cancellationToken);
    var version = post.Revise(details, command.EffectiveFrom, filled, currentUser.UserId);

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(version.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(CorrectPostCommand command, CancellationToken cancellationToken)
  {
    var post = await context.LoadPostAsync(command.Id, cancellationToken);
    var details = await ResolveAsync(post.Id, command.Details, post.Latest.EffectiveFrom, post.Latest.Details, cancellationToken);
    var filled = await records.FilledSeatsOnAsync(post.Id, post.Latest.EffectiveFrom, cancellationToken);
    post.CorrectLatest(details, filled);

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<CreatedResult>> Handle(ChangePostLifecycleCommand command, CancellationToken cancellationToken)
  {
    var post = await context.LoadPostAsync(command.Id, cancellationToken);
    var date = command.EffectiveFrom;

    var version = command.Action switch
    {
      PostLifecycleAction.Freeze => post.Freeze(date, command.NotificationRef, currentUser.UserId),
      PostLifecycleAction.Unfreeze => post.Unfreeze(date, command.NotificationRef, currentUser.UserId),
      _ => post.Abolish(date, command.NotificationRef, await records.FilledSeatsOnAsync(post.Id, date, cancellationToken), currentUser.UserId)
    };

    if (command.Action == PostLifecycleAction.Abolish)
    {
      // a regular holder whose assignment starts later would be left on an abolished post
      var later = await context.PositionAssignments.AnyAsync(a => a.PostId == post.Id && a.Status == RecordStatus.Active
        && a.AssignmentType == AssignmentType.Regular && a.EffectiveFrom > date, cancellationToken);
      if (later)
        return Result<CreatedResult>.Failure($"Post {post.PostCode} has a regular assignment starting after {date:yyyy-MM-dd}. Cancel it first.");

      var heads = await context.OrganizationUnitVersions.AnyAsync(v => v.HeadPostId == post.Id
        && (v.EffectiveTo == null || v.EffectiveTo >= date), cancellationToken);
      if (heads)
        return Result<CreatedResult>.Failure($"Post {post.PostCode} heads an org unit. Give the unit another head post first.");
    }

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(version.Id);
  }

  /// Checks what the details point at, on the date they take effect. Values carried over unchanged are not re-checked.
  private async Task<PostDetails> ResolveAsync(PostId postId, PostDetailsInput input, DateOnly date, PostDetails? previous, CancellationToken cancellationToken)
  {
    var designationId = DesignationId.Of(input.DesignationId);
    if (previous?.DesignationId != designationId)
      (await context.LoadDesignationAsync(input.DesignationId, cancellationToken)).EnsureActive();

    var gradeId = PayScaleGradeId.Of(input.GradeId);
    if (previous?.GradeId != gradeId)
      (await context.LoadGradeAsync(input.GradeId, cancellationToken)).EnsureActive();

    var unit = await context.LoadOrgUnitAsync(input.OrgUnitId, cancellationToken);
    if (!unit.IsActiveOn(date))
      throw new DomainException($"Org unit {unit.Code} is not active on {date:yyyy-MM-dd}.");

    LocationId? locationId = input.LocationId is { } location ? LocationId.Of(location) : null;
    if (locationId is not null && previous?.LocationId != locationId)
      (await context.LoadLocationAsync(input.LocationId!.Value, cancellationToken)).EnsureActive();

    PostId? reportingId = input.ReportingPostId is { } reporting ? PostId.Of(reporting) : null;
    if (reportingId is not null)
    {
      if (reportingId == postId)
        throw new DomainException("A post cannot report to itself.");

      var reportingPost = await context.LoadPostAsync(input.ReportingPostId!.Value, cancellationToken);
      var reportingVersion = reportingPost.VersionOn(date)
        ?? throw new DomainException($"Post {reportingPost.PostCode} does not exist on {date:yyyy-MM-dd}.");
      if (reportingVersion.LifecycleStatus == PostLifecycle.Abolished)
        throw new DomainException($"Post {reportingPost.PostCode} is abolished.");

      // the reporting line may not loop back to this post
      var reportsTo = (await context.PostVersions.AsNoTracking()
          .Where(v => v.EffectiveFrom <= date && (v.EffectiveTo == null || v.EffectiveTo >= date))
          .Select(v => new { v.PostId, v.ReportingPostId })
          .ToListAsync(cancellationToken))
        .ToDictionary(v => v.PostId, v => v.ReportingPostId);
      var seen = new HashSet<PostId>();
      for (var current = reportingId; current is not null && seen.Add(current); current = reportsTo.GetValueOrDefault(current))
      {
        if (current == postId)
          throw new DomainException("That reporting post (directly or further up) reports to this post; the reporting line would loop.");
      }
    }

    return new PostDetails(designationId, gradeId, unit.Id, reportingId, input.EmploymentType, locationId, input.SanctionedCount, input.NotificationRef);
  }
}
