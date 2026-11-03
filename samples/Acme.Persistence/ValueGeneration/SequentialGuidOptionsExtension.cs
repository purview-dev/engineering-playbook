using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.Persistence.ValueGeneration;

/// <summary>The strategy choice, carried as data on the context options.</summary>
public sealed class SequentialGuidOptionsExtension(string strategyName, Type valueGeneratorType, Type factoryType)
	: IDbContextOptionsExtension
{
	public string StrategyName { get; } = strategyName;

	public Type ValueGeneratorType { get; } = valueGeneratorType;

	public Type FactoryType { get; } = factoryType;

	public DbContextOptionsExtensionInfo Info => field ??= new ExtensionInfo(this);

	// Registered separately from EF's internal generator so application code can mint the same
	// values for outbox rows, event envelopes and correlation ids.
	public void ApplyServices(IServiceCollection services) =>
		services.AddSingleton(typeof(ISequentialGuidFactory), FactoryType);

	public void Validate(IDbContextOptions options)
	{
		// The strategy participates in EF's service-provider cache key. An external internal
		// provider would bypass that key, so the first provider to build the model would win.
		if (options.FindExtension<CoreOptionsExtension>()?.InternalServiceProvider is not null)
		{
			throw new InvalidOperationException(
				"Sequential GUID generation cannot be combined with UseInternalServiceProvider."
			);
		}
	}

	sealed class ExtensionInfo(SequentialGuidOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
	{
		// DbContextOptionsExtensionInfo.Extension is typed as the interface, so this narrows it.
		public new SequentialGuidOptionsExtension Extension => (SequentialGuidOptionsExtension)base.Extension;

		public override bool IsDatabaseProvider => false;

		public override string LogFragment => $"SequentialGuids={Extension.StrategyName} ";

		public override int GetServiceProviderHashCode() =>
			HashCode.Combine(Extension.ValueGeneratorType, Extension.FactoryType);

		public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) =>
			other is ExtensionInfo otherInfo
			&& otherInfo.Extension.ValueGeneratorType == Extension.ValueGeneratorType
			&& otherInfo.Extension.FactoryType == Extension.FactoryType;

		public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
		{
			debugInfo["SequentialGuid:Strategy"] = Extension.StrategyName;
			debugInfo["SequentialGuid:ValueGenerator"] =
				Extension.ValueGeneratorType.AssemblyQualifiedName ?? string.Empty;
		}
	}
}
