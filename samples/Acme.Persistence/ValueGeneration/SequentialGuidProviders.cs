using SecurityDriven;

namespace Acme.Persistence.ValueGeneration;

/// <summary>Values ascend in SQL Server's <c>uniqueidentifier</c> comparison order.</summary>
public sealed class SqlServerSequentialGuidProvider : ISequentialGuidProvider
{
	public static string Name => "SqlServer";

	public static Guid NewGuid() => FastGuid.NewSqlServerGuid();

	public static Guid NewGuid(DateTime timestampUtc) =>
		FastGuid.NewSqlServerGuid(UtcTimestamp.EnsureUtc(timestampUtc));

	public static DateTime GetTimestamp(Guid value) => FastGuid.SqlServer.GetTimestamp(value);

	public static Guid MinValueFor(DateTime timestampUtc) =>
		FastGuid.SqlServer.MinGuidForTimestamp(UtcTimestamp.EnsureUtc(timestampUtc));

	public static Guid MaxValueFor(DateTime timestampUtc) =>
		FastGuid.SqlServer.MaxGuidForTimestamp(UtcTimestamp.EnsureUtc(timestampUtc));
}

/// <summary>Values ascend in PostgreSQL's big-endian <c>uuid</c> comparison order.</summary>
public sealed class PostgreSqlSequentialGuidProvider : ISequentialGuidProvider
{
	public static string Name => "PostgreSql";

	public static Guid NewGuid() => FastGuid.NewPostgreSqlGuid();

	public static Guid NewGuid(DateTime timestampUtc) =>
		FastGuid.NewPostgreSqlGuid(UtcTimestamp.EnsureUtc(timestampUtc));

	public static DateTime GetTimestamp(Guid value) => FastGuid.PostgreSql.GetTimestamp(value);

	public static Guid MinValueFor(DateTime timestampUtc) =>
		FastGuid.PostgreSql.MinGuidForTimestamp(UtcTimestamp.EnsureUtc(timestampUtc));

	public static Guid MaxValueFor(DateTime timestampUtc) =>
		FastGuid.PostgreSql.MaxGuidForTimestamp(UtcTimestamp.EnsureUtc(timestampUtc));
}

/// <summary>RFC 9562 UUIDv7. Genuinely sequential on PostgreSQL, not on SQL Server.</summary>
public sealed class UuidV7SequentialGuidProvider : ISequentialGuidProvider
{
	public static string Name => "UuidV7";

	public static Guid NewGuid() => Guid.CreateVersion7();

	public static Guid NewGuid(DateTime timestampUtc) =>
		Guid.CreateVersion7(new DateTimeOffset(UtcTimestamp.EnsureUtc(timestampUtc)));

	public static DateTime GetTimestamp(Guid value) => FastGuid.UUID.ExtractDateTimeV7(value);

	public static Guid MinValueFor(DateTime timestampUtc) => ForTimestamp(timestampUtc, minimum: true);

	public static Guid MaxValueFor(DateTime timestampUtc) => ForTimestamp(timestampUtc, minimum: false);

	static Guid ForTimestamp(DateTime timestampUtc, bool minimum)
	{
		var milliseconds = new DateTimeOffset(UtcTimestamp.EnsureUtc(timestampUtc)).ToUnixTimeMilliseconds();
		Span<byte> bytes = stackalloc byte[16];
		bytes[0] = (byte)(milliseconds >> 40);
		bytes[1] = (byte)(milliseconds >> 32);
		bytes[2] = (byte)(milliseconds >> 24);
		bytes[3] = (byte)(milliseconds >> 16);
		bytes[4] = (byte)(milliseconds >> 8);
		bytes[5] = (byte)milliseconds;
		bytes[6] = 0x70; // version 7
		bytes[8] = 0x80; // RFC 9562 variant

		if (!minimum)
		{
			bytes[6] |= 0x0F;
			bytes[7] = 0xFF;
			bytes[8] |= 0x3F;
			bytes[9..].Fill(0xFF);
		}

		return new Guid(bytes, bigEndian: true);
	}
}

/// <summary>Rejects an ambiguous timestamp before it becomes a silent ordering bug.</summary>
static class UtcTimestamp
{
	public static DateTime EnsureUtc(DateTime timestampUtc) =>
		timestampUtc.Kind == DateTimeKind.Unspecified
			? throw new ArgumentException(
				"A sequential GUID timestamp must be UTC; Unspecified cannot be ordered reliably.",
				nameof(timestampUtc)
			)
			: timestampUtc.ToUniversalTime();
}
