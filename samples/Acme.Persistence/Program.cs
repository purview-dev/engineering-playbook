using Acme.Domain;
using Acme.Installation;
using Acme.Persistence;
using Acme.Persistence.ValueGeneration;
using Acme.Verification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var options = ParseArguments(args);

if (options.ShowHelp)
{
	PrintHelp();
	return 0;
}

if (options.PrintConstants)
{
	PrintConstants(options.InstallationId);
	return 0;
}

IdentifierVerification.Run();
Console.WriteLine("identifier checks   passed");

var services = new ServiceCollection();
var exitCode = options.Provider switch
{
	"sqlserver" => await RunAsync<SqlServerSequentialGuidProvider>(services, options),
	"postgresql" => await RunAsync<PostgreSqlSequentialGuidProvider>(services, options),
	"uuidv7" => await RunAsync<UuidV7SequentialGuidProvider>(services, options),
	_ => throw new ArgumentException($"Unknown provider '{options.Provider}'."),
};

return exitCode;

static async Task<int> RunAsync<TProvider>(IServiceCollection services, SampleOptions options)
	where TProvider : ISequentialGuidProvider
{
	services.AddAcmePersistence<TProvider>(options.ConnectionString);

	await using var provider = services.BuildServiceProvider();
	await using var scope = provider.CreateAsyncScope();

	var dbContext = scope.ServiceProvider.GetRequiredService<AcmeDbContext<TProvider>>();
	var bootstrap = scope.ServiceProvider.GetRequiredService<InstallationBootstrap<TProvider>>();

	var first = await bootstrap.RunAsync(options.InstallationId, CancellationToken.None);
	var firstSnapshot = await Snapshot(dbContext);
	var second = await bootstrap.RunAsync(options.InstallationId, CancellationToken.None);
	var secondSnapshot = await Snapshot(dbContext);

	Require(first.Created, "the first bootstrap should create the installation");
	Require(!second.Created, "the second bootstrap should be a no-op");
	Require(firstSnapshot == secondSnapshot, "the second bootstrap must not change any row");
	Require(first.SystemTenantId == second.SystemTenantId, "the tenant id must be stable");

	var customer = new Customer { Name = "Acme" };
	dbContext.Customers.Add(customer);
	var order = new Order { CustomerId = customer.Id, Reference = "A-1000" };
	dbContext.Orders.Add(order);
	await dbContext.SaveChangesAsync();

	Require(order.Id.Value != Guid.Empty, "the order key was not generated");
	Require(order.ExternalId is not null, "the nullable indexed GUID was not generated");

	var factory = scope.ServiceProvider.GetRequiredService<ISequentialGuidFactory>();
	var factoryValue = factory.NewGuid();
	var timestamp = factory.GetTimestamp(factoryValue);
	Require(Math.Abs((timestamp - DateTime.UtcNow).TotalMinutes) < 5, "the timestamp is not recent");

	Console.WriteLine($"provider            {TProvider.Name}");
	Console.WriteLine($"installation        {options.InstallationId:D}");
	Console.WriteLine($"system tenant       {first.SystemTenantId:D}");
	Console.WriteLine($"system principal    {first.SystemPrincipalId:D}");
	Console.WriteLine("bootstrap           first run created; second run unchanged");
	Console.WriteLine($"customer key        {customer.Id:D}");
	Console.WriteLine($"order key           {order.Id.Value:D}");
	Console.WriteLine($"nullable indexed    {order.ExternalId!.Value:D}");
	Console.WriteLine($"factory value       {factoryValue:D}");
	Console.WriteLine($"factory timestamp   {timestamp:O}");
	Console.WriteLine("all checks          passed");

	return 0;
}

static async Task<Snapshot> Snapshot<TProvider>(AcmeDbContext<TProvider> dbContext)
	where TProvider : ISequentialGuidProvider =>
	new(
		await dbContext.Installations.CountAsync(),
		await dbContext.Tenants.CountAsync(),
		await dbContext.ServicePrincipals.CountAsync(),
		await dbContext.Tenants.Select(tenant => tenant.Id).SingleAsync(),
		await dbContext.ServicePrincipals.Select(principal => principal.Id).SingleAsync()
	);

static void PrintConstants(Guid installationId)
{
	var wellKnown = new WellKnownIdentifiers(new InstallationId(installationId));
	Console.WriteLine($"installation        {installationId:D}");
	Console.WriteLine($"system tenant       {wellKnown.SystemTenantId:D}");
	Console.WriteLine($"system principal    {wellKnown.SystemPrincipalId:D}");
	Console.WriteLine($"role:admin          {wellKnown.ReferenceRow("role", "admin"):D}");
}

static SampleOptions ParseArguments(string[] args)
{
	var provider = "sqlserver";
	var installationId = Guid.Parse("11111111-1111-4111-8111-111111111111");
	string? connectionString = null;
	var printConstants = false;
	var showHelp = false;

	for (var index = 0; index < args.Length; index++)
	{
		switch (args[index])
		{
			case "--provider":
				provider = RequireValue(args, ref index, "--provider");
				break;
			case "--installation":
				installationId = Guid.Parse(RequireValue(args, ref index, "--installation"));
				break;
			case "--connection":
				connectionString = RequireValue(args, ref index, "--connection");
				break;
			case "--print-constants":
				printConstants = true;
				break;
			case "--help" or "-h":
				showHelp = true;
				break;
			default:
				throw new ArgumentException($"Unknown argument '{args[index]}'. Use --help.");
		}
	}

	if (provider is not ("sqlserver" or "postgresql" or "uuidv7"))
	{
		throw new ArgumentException($"Unknown provider '{provider}'. Choose sqlserver, postgresql or uuidv7.");
	}

	return new SampleOptions(provider, installationId, connectionString, printConstants, showHelp);
}

static string RequireValue(string[] args, ref int index, string name)
{
	if (index + 1 >= args.Length)
		throw new ArgumentException($"'{name}' needs a value.");

	index++;
	return args[index];
}

static void Require(bool condition, string message)
{
	if (!condition)
		throw new InvalidOperationException(message);
}

static void PrintHelp() =>
	Console.WriteLine(
		"""
		Usage: dotnet run --project samples/Acme.Persistence -- [options]

		  --provider <sqlserver|postgresql|uuidv7>  Strategy to demonstrate (default: sqlserver)
		  --installation <guid>                     Namespace for well-known identities
		  --connection <connection-string>          Use a real SQL Server or PostgreSQL store
		  --print-constants                         Print derived identifiers for a migration
		  --help                                    Show this text
		"""
	);

sealed record SampleOptions(
	string Provider,
	Guid InstallationId,
	string? ConnectionString,
	bool PrintConstants,
	bool ShowHelp
);

sealed record Snapshot(int Installations, int Tenants, int Principals, Guid TenantId, Guid PrincipalId);
