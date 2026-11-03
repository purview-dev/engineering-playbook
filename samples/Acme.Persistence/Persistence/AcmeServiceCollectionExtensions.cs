using Acme.Installation;
using Acme.Persistence.ValueGeneration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.Persistence;

/// <summary>Composition for the sample: one strategy, one context, one factory.</summary>
public static class AcmeServiceCollectionExtensions
{
	public static IServiceCollection AddAcmePersistence<TProvider>(
		this IServiceCollection services,
		string? connectionString = null
	)
		where TProvider : ISequentialGuidProvider
	{
		services.AddDbContext<AcmeDbContext<TProvider>>(
			(serviceProvider, options) =>
			{
				ConfigureStore<TProvider>(options, connectionString);
				options
					.UseSequentialGuids<TProvider>()
					.ReplaceService<IModelCacheKeyFactory, AcmeModelCacheKeyFactory>()
					.ReplaceService<IModelCustomizer, AcmeModelCustomizer<TProvider>>();
			}
		);

		// The application-facing factory is a host registration, separate from EF's internal one.
		services.AddSingleton<ISequentialGuidFactory, SequentialGuidFactory<TProvider>>();
		services.AddScoped<InstallationBootstrap<TProvider>>();

		return services;
	}

	static void ConfigureStore<TProvider>(DbContextOptionsBuilder options, string? connectionString)
		where TProvider : ISequentialGuidProvider
	{
		if (connectionString is null)
		{
			options.UseInMemoryDatabase($"acme-{TProvider.Name}");
			return;
		}

		if (typeof(TProvider) == typeof(SqlServerSequentialGuidProvider))
		{
			options.UseSqlServer(connectionString);
			return;
		}

		if (typeof(TProvider) == typeof(PostgreSqlSequentialGuidProvider))
		{
			options.UseNpgsql(connectionString);
			return;
		}

		throw new InvalidOperationException(
			"UUIDv7 is the fallback strategy; run it against the in-memory store or choose PostgreSQL."
		);
	}
}
