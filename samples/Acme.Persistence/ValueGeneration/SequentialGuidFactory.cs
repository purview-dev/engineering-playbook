namespace Acme.Persistence.ValueGeneration;

/// <summary>The application-facing view of the configured strategy.</summary>
public interface ISequentialGuidFactory
{
	Guid NewGuid();

	Guid NewGuid(DateTime timestampUtc);

	DateTime GetTimestamp(Guid value);

	Guid MinValueFor(DateTime timestampUtc);

	Guid MaxValueFor(DateTime timestampUtc);
}

/// <summary>Forwards to the strategy so application code never resolves the wrong instance.</summary>
public sealed class SequentialGuidFactory<TProvider> : ISequentialGuidFactory
	where TProvider : ISequentialGuidProvider
{
	public Guid NewGuid() => TProvider.NewGuid();

	public Guid NewGuid(DateTime timestampUtc) => TProvider.NewGuid(timestampUtc);

	public DateTime GetTimestamp(Guid value) => TProvider.GetTimestamp(value);

	public Guid MinValueFor(DateTime timestampUtc) => TProvider.MinValueFor(timestampUtc);

	public Guid MaxValueFor(DateTime timestampUtc) => TProvider.MaxValueFor(timestampUtc);
}
