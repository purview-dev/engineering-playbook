using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Acme.Persistence.ValueGeneration;

/// <summary>The shared registration, callable from any context shape.</summary>
public static class AcmeModelConventions
{
	public static void ApplySequentialGuids<TProvider>(ModelConfigurationBuilder configurationBuilder)
		where TProvider : ISequentialGuidProvider
	{
		configurationBuilder.Conventions.Add(_ => new SequentialGuidConvention<TProvider>(property =>
			property.GetContainingIndexes().Any()
		));
	}
}
