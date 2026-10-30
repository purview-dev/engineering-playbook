using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Acme.Persistence.ValueGeneration;

/// <summary>Carries the provider's strategy on the context options, once.</summary>
public static class SequentialGuidDbContextOptionsBuilderExtensions
{
	public static DbContextOptionsBuilder UseSequentialGuids<TProvider>(this DbContextOptionsBuilder builder)
		where TProvider : ISequentialGuidProvider
	{
		ArgumentNullException.ThrowIfNull(builder);

		var extension = new SequentialGuidOptionsExtension(
			TProvider.Name,
			typeof(SequentialGuidValueGenerator<TProvider>),
			typeof(SequentialGuidFactory<TProvider>)
		);

		((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(extension);

		return builder;
	}
}
