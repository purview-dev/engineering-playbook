using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Acme.Persistence.ValueGeneration;

/// <summary>Creates the generator and hands it the property's value converter.</summary>
public sealed class SequentialGuidValueGeneratorFactory<TProvider> : ValueGeneratorFactory
	where TProvider : ISequentialGuidProvider
{
	public override ValueGenerator Create(IProperty property, ITypeBase typeBase)
	{
		ValueGenerator generator = new SequentialGuidValueGenerator<TProvider>();
		var converter = property.GetValueConverter();
		return converter is null ? generator : generator.WithConverter(converter);
	}
}

/// <summary>Generates the strategy's values from the application, not from the store.</summary>
public sealed class SequentialGuidValueGenerator<TProvider> : ValueGenerator<Guid>
	where TProvider : ISequentialGuidProvider
{
	// A temporary value would be written and then rewritten after the insert.
	public override bool GeneratesTemporaryValues => false;

	public override Guid Next(EntityEntry entry) => TProvider.NewGuid();
}
