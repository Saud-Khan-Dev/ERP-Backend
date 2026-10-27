using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class UserScopeConfiguration : EntityConfiguration<UserScope, Guid>
{
  public override void Configure(EntityTypeBuilder<UserScope> builder)
  {
    base.Configure(builder);
    builder.ToTable("user_scopes");
    builder.HasKey(x => x.Id);
    builder.Property(x => x.UserId).HasConversion(id => id.Value, dbId => UserId.Of(dbId)).IsRequired();
    builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
    builder.Property(x => x.RefId).IsRequired();
    builder.Property(x => x.Label).HasMaxLength(200).IsRequired();
    builder.HasIndex(x => new { x.UserId, x.Kind, x.RefId }).IsUnique();
    builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
  }
}

public class DutyRuleSwitchConfiguration : EntityConfiguration<DutyRuleSwitch, string>
{
  public override void Configure(EntityTypeBuilder<DutyRuleSwitch> builder)
  {
    base.Configure(builder);
    builder.ToTable("duty_rules");
    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasColumnName("code").HasMaxLength(60);
    builder.Property(x => x.ChangedByName).HasMaxLength(150).IsRequired();
    builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
  }
}

public class AccessReviewConfiguration : EntityConfiguration<AccessReview, Guid>
{
  public override void Configure(EntityTypeBuilder<AccessReview> builder)
  {
    base.Configure(builder);
    builder.ToTable("access_reviews");
    builder.HasKey(x => x.Id);
    builder.Property(x => x.StartedByName).HasMaxLength(150).IsRequired();
    builder.Property(x => x.ClosedByName).HasMaxLength(150);
    builder.Property(x => x.Note).HasMaxLength(1000);
    builder.Ignore(x => x.IsOpen);
    builder.HasIndex(x => x.StartedAt);
  }
}

public class AccessReviewDecisionConfiguration : EntityConfiguration<AccessReviewDecision, Guid>
{
  public override void Configure(EntityTypeBuilder<AccessReviewDecision> builder)
  {
    base.Configure(builder);
    builder.ToTable("access_review_decisions");
    builder.HasKey(x => x.Id);
    builder.Property(x => x.UserId).HasConversion(id => id.Value, dbId => UserId.Of(dbId)).IsRequired();
    builder.Property(x => x.UserName).HasMaxLength(150).IsRequired();
    builder.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(20).IsRequired();
    builder.Property(x => x.AccessText).HasMaxLength(1000).IsRequired();
    builder.Property(x => x.Note).HasMaxLength(500);
    builder.Property(x => x.DecidedByName).HasMaxLength(150).IsRequired();
    builder.HasIndex(x => new { x.ReviewId, x.UserId }).IsUnique();
    builder.HasOne<AccessReview>().WithMany().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Restrict);
  }
}

public class AccountDeletionRequestConfiguration : EntityConfiguration<AccountDeletionRequest, Guid>
{
  public override void Configure(EntityTypeBuilder<AccountDeletionRequest> builder)
  {
    base.Configure(builder);
    builder.ToTable("account_deletion_requests");
    builder.HasKey(x => x.Id);
    builder.Property(x => x.UserId).HasConversion(id => id.Value, dbId => UserId.Of(dbId)).IsRequired();
    builder.Property(x => x.UserName).HasMaxLength(150).IsRequired();
    builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
    builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
    builder.Property(x => x.DecidedByName).HasMaxLength(150);
    builder.Property(x => x.Remarks).HasMaxLength(1000);
    builder.Ignore(x => x.IsPending);
    builder.HasIndex(x => new { x.UserId, x.RequestedAt });
    builder.HasIndex(x => x.Status);
    // one waiting request per person
    builder.HasIndex(x => x.UserId).IsUnique().HasFilter("status = 'Pending'").HasDatabaseName("ux_deletion_request_one_waiting");
    builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
  }
}
