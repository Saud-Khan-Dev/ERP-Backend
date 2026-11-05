using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// Stands in for a real adapter: a message's Body is a key into what the test said the gateway will answer.
public sealed class TestGateway : IPaymentGateway
{
  private readonly Dictionary<string, GatewayVerification> _answers = new();

  public string Source => "test-gateway";

  public IReadOnlySet<PaymentChannel> Channels { get; } = new HashSet<PaymentChannel> { PaymentChannel.Onelink };

  public GatewayMessage Answers(GatewayVerification answer)
  {
    var key = Guid.NewGuid().ToString();
    lock (_answers)
      _answers[key] = answer;
    return new GatewayMessage(key, new Dictionary<string, string>());
  }

  public Task<GatewayVerification> VerifyAsync(GatewayMessage message, CancellationToken cancellationToken)
  {
    lock (_answers)
      return Task.FromResult(_answers[message.Body]);
  }
}

/// The caller of a test: a taxpayer (an owner id, as the `own` claim carries it) inside `PaymentDatabase.AsTaxpayer`,
/// otherwise nobody (a background job's view). Held per async flow, so tests running at once do not mix.
public sealed class TestUser : ICurrentUser
{
  public static readonly AsyncLocal<Guid?> Owner = new();

  public bool IsAuthenticated => Owner.Value is not null;
  public Guid? UserId => null;
  public string? Username => null;
  public string? DisplayName => null;
  public string? Email => null;
  public Guid? EmployeeId => null;
  public Guid? OwnerId => Owner.Value;
  public IReadOnlyCollection<string> Permissions => [];
  public IReadOnlyCollection<string> Roles => [];
  public Guid? SessionId => null;
  public long? AccessStamp => null;
  public IReadOnlySet<Guid>? TownScope => null;
  public IReadOnlySet<Guid>? OfficeScope => null;
  public bool IsSuperAdmin => false;
  public bool IsAuthorizedOfficer => false;
  public bool HasPermission(string permissionCode) => false;
  public string AuditName => "Test";
}

/// The real handlers, EF mapping, migrations, seed and audit trail over a throwaway database. TAX_TEST_DB names it, and the
/// tests refuse a database called ERP_DB, because they write rows that cannot be removed (the audit trail is append-only).
public sealed class PaymentDatabase : IAsyncLifetime
{
  private ServiceProvider _services = default!;
  private TaxCircleId _circle = default!;
  private long _next = DateTime.UtcNow.Ticks % 1_000_000;

  public TestGateway Gateway { get; } = new();

  public async Task InitializeAsync()
  {
    var connection = Environment.GetEnvironmentVariable("TAX_TEST_DB")
      ?? throw new InvalidOperationException("Set TAX_TEST_DB to the connection string of a throwaway PostgreSQL database.");
    if (connection.Contains("ERP_DB", StringComparison.OrdinalIgnoreCase))
      throw new InvalidOperationException("These tests write rows that cannot be removed: give them a throwaway database, not ERP_DB.");

    var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
      ["ConnectionStrings:Database"] = connection,
      ["Jwt:Issuer"] = "test",
      ["Jwt:Audience"] = "test",
      ["Jwt:SigningKey"] = new string('k', 64)
    }).Build();

    var services = new ServiceCollection();
    services.AddSingleton<IConfiguration>(configuration);
    services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
    services.AddApplicationServices(configuration);
    services.AddInfrastructureServices(configuration);
    services.AddErpAuthentication(configuration);
    services.AddScoped<IPaymentGateway>(_ => Gateway);
    services.AddScoped<ICurrentUser, TestUser>();
    _services = services.BuildServiceProvider();

    await InScopeAsync(async sp =>
    {
      await sp.GetRequiredService<DatabaseInitializer>().InitialiseAsync(applyMigrations: true);
      var context = sp.GetRequiredService<IApplicationDbContext>();
      _circle = await context.TaxCircles.Select(c => c.Id).FirstAsync();
    });
  }

  public async Task DisposeAsync() => await _services.DisposeAsync();

  public async Task InScopeAsync(Func<IServiceProvider, Task> work)
  {
    await using var scope = _services.CreateAsyncScope();
    await work(scope.ServiceProvider);
  }

  public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
  {
    await using var scope = _services.CreateAsyncScope();
    return await work(scope.ServiceProvider);
  }

  public Task<T> QueryAsync<T>(Func<IApplicationDbContext, Task<T>> read) => InScopeAsync(sp => read(sp.GetRequiredService<IApplicationDbContext>()));

  public Task<GatewayOutcome> ReceiveAsync(GatewayVerification answer) =>
      InScopeAsync(sp => sp.GetRequiredService<GatewayPayments>().ReceiveAsync(Gateway.Answers(answer), default));

  public Task<GatewayOutcome> ReportAsync(GatewayPaymentFacts facts) => ReceiveAsync(new GatewayVerification.Genuine(facts));

  public Task<decimal> DuesAsync(string psid) => InScopeAsync(async sp =>
  {
    var context = sp.GetRequiredService<IApplicationDbContext>();
    return await sp.GetRequiredService<Ledger>().DuesAsync(await context.LoadTaxFileByPsidAsync(psid, default), default);
  });

  /// Runs `work` signed in as the taxpayer whose Property owner record is `owner`.
  public async Task<T> AsTaxpayer<T>(Guid owner, Func<ISender, Task<T>> work)
  {
    TestUser.Owner.Value = owner;
    try
    {
      return await InScopeAsync(sp => work(sp.GetRequiredService<ISender>()));
    }
    finally
    {
      TestUser.Owner.Value = null;
    }
  }

  /// A property owing `dues` from the old register, with its own reference (and, given, the Property owner who pays on
  /// it). Returns the reference.
  public Task<string> FileOwingAsync(decimal dues, Guid? owner = null) => InScopeAsync(async sp =>
  {
    var context = sp.GetRequiredService<IApplicationDbContext>();
    var number = Interlocked.Increment(ref _next);
    var psid = $"6011{number:D8}";
    var town = Guid.NewGuid();
    var snapshot = new PropertySnapshot(Guid.NewGuid(), $"PROP-{number}", "Test property", true, town, "NATHIAGALI", "Nathiagali", Guid.NewGuid(), "RESIDENTIAL", "OCCUPIED",
      "PRIVATE_PROPERTY", "Mall Road, Nathiagali", 3600, 400, "SQYD", null, 2500, CoveredAreaSource.ApprovedPlan, Guid.NewGuid(), Guid.NewGuid(), null, null, null,
      HoldingKind.PrivateRegistered,
      [new PayerSnapshot(owner ?? Guid.NewGuid(), "OWN-00001", PayerRole.Owner, PayerSource.Ownership, Guid.NewGuid(), 100, "Asif Jadoon", null, "35202-1234567-1", "0300-1234567",
        "Chairlift Road, Nathiagali", new DateOnly(2020, 1, 1))],
      [], "v1");
    var file = TaxFile.Open(TaxFileId.New(), $"GDA-TEST-{number}", psid, snapshot, _circle, TaxCategory.Res,
      new TaxFileDetails(null, new DateOnly(2015, 1, 1), null, null, "NA/R/01", "64", null), new DateOnly(2025, 7, 1), DateTime.UtcNow);
    context.TaxFiles.Add(file);
    var head = await context.TaxHeads.FirstAsync(h => h.Code == HeadCodes.LeaseMoney);
    context.ArrearsRows.Add(Arrears.Imported(ArrearsId.New(), file.Id, head, 2024, dues, "Old register", null, Guid.NewGuid(), DateTime.UtcNow));
    await context.SaveChangesAsync(default);
    return psid;
  });
}

[CollectionDefinition(nameof(PaymentDatabase))]
public class PaymentDatabaseCollection : ICollectionFixture<PaymentDatabase>;
