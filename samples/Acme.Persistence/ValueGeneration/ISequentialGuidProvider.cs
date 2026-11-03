namespace Acme.Persistence.ValueGeneration;

/// <summary>A store-specific sequential GUID strategy.</summary>
public interface ISequentialGuidProvider
{
	static abstract string Name { get; }

	static abstract Guid NewGuid();

	static abstract Guid NewGuid(DateTime timestampUtc);

	static abstract DateTime GetTimestamp(Guid value);

	static abstract Guid MinValueFor(DateTime timestampUtc);

	static abstract Guid MaxValueFor(DateTime timestampUtc);
}
