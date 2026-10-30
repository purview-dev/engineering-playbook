using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Acme.Persistence.ValueGeneration;

/// <summary>Applies the strategy to every GUID key at convention precedence.</summary>
public sealed class SequentialGuidConvention<TProvider>(Func<IReadOnlyProperty, bool>? alsoGenerate = null)
	: IModelFinalizingConvention
	where TProvider : ISequentialGuidProvider
{
	static readonly Type FactoryType = typeof(SequentialGuidValueGeneratorFactory<TProvider>);

	public void ProcessModelFinalizing(
		IConventionModelBuilder modelBuilder,
		IConventionContext<IConventionModelBuilder> context
	)
	{
		GuardFactory();

		foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
		{
			foreach (var property in entityType.GetProperties())
			{
				if (!ShouldGenerate(property))
					continue;

				var builder =
					property.Builder
					?? throw new InvalidOperationException(
						$"'{property.Name}' is read-only during model finalisation."
					);
				builder.ValueGenerated(ValueGenerated.OnAdd, fromDataAnnotation: false);
				builder.HasValueGeneratorFactory(FactoryType, fromDataAnnotation: false);
			}
		}
	}

	bool ShouldGenerate(IReadOnlyProperty property)
	{
		if (!IsGuid(property.ClrType))
			return false;

		// A foreign key takes its value from the principal, and a default or computed column is
		// owned by the store. Explicit configuration wins because this writes at convention
		// precedence, the lowest.
		if (
			property.IsForeignKey()
			|| property.GetDefaultValueSql() is not null
			|| property.GetComputedColumnSql() is not null
		)
		{
			return false;
		}

		return property.IsKey() || (alsoGenerate?.Invoke(property) ?? false);
	}

	static bool IsGuid(Type clrType) =>
		clrType == typeof(Guid) || clrType == typeof(Guid?) || typeof(IComparable<Guid>).IsAssignableFrom(clrType);

	static void GuardFactory()
	{
		if (
			!typeof(ValueGeneratorFactory).IsAssignableFrom(FactoryType)
			|| FactoryType.GetConstructor(Type.EmptyTypes) is null
		)
		{
			throw new InvalidOperationException(
				$"'{FactoryType}' must derive from ValueGeneratorFactory and have a parameterless constructor."
			);
		}
	}
}
