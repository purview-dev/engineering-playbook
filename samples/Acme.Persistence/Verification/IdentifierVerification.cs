using System.Data.SqlTypes;
using Acme.Persistence.ValueGeneration;

namespace Acme.Verification;

/// <summary>
/// The checks the wiki publishes as the enforcement table: RFC vectors, store-order monotonicity,
/// the UUIDv7 trap, timestamp round-trips and range bounds.
/// </summary>
public static class IdentifierVerification
{
	public static void Run()
	{
		VerifyUuid5();
		VerifyMonotonic<SqlServerSequentialGuidProvider>(CompareSqlServer, "sql server");
		VerifyMonotonic<PostgreSqlSequentialGuidProvider>(CompareBigEndian, "postgresql");
		VerifyUuidV7();
		VerifyTimestampAndRange<SqlServerSequentialGuidProvider>(CompareSqlServer, "sql server");
		VerifyTimestampAndRange<PostgreSqlSequentialGuidProvider>(CompareBigEndian, "postgresql");
		VerifyTimestampAndRange<UuidV7SequentialGuidProvider>(CompareBigEndian, "uuidv7");
	}

	static void VerifyUuid5()
	{
		Check(
			Uuid5.Create(Uuid5.NamespaceDns, "example.com") == Guid.Parse("cfbff0d1-9375-5685-968c-48ce8b15ae17"),
			"uuid5 DNS vector failed"
		);
		Check(
			Uuid5.Create(Uuid5.NamespaceUrl, "www.example.com") == Guid.Parse("b63cdfa4-3df9-568e-97ae-006c5b8fd652"),
			"uuid5 URL vector failed"
		);
		Check(
			Uuid5.Create(Uuid5.NamespaceOid, "1.2.3.4") == Guid.Parse("67448b45-6d15-536b-bef7-8a78c3d10ac6"),
			"uuid5 OID vector failed"
		);
		Check(
			Uuid5.Create(Uuid5.NamespaceX500, "CN=Bob") == Guid.Parse("0840e90d-9ca0-5f14-970e-26294d44cb1d"),
			"uuid5 X500 vector failed"
		);
		Check(
			Uuid5.Create(Uuid5.NamespaceDns, string.Empty) == Guid.Parse("4ebd0208-8328-5d69-8c44-ec50939c0967"),
			"uuid5 empty-name vector failed"
		);

		var value = Uuid5.Create(Uuid5.NamespaceDns, "example.com");
		Span<byte> bytes = stackalloc byte[16];
		value.TryWriteBytes(bytes, bigEndian: true, out _);
		Check(value == Uuid5.Create(Uuid5.NamespaceDns, "example.com"), "uuid5 is not deterministic");
		Check(value != Uuid5.Create(Uuid5.NamespaceDns, "example.org"), "uuid5 ignores the name");
		Check((bytes[6] & 0xF0) == 0x50, "uuid5 version bits are wrong");
		Check((bytes[8] & 0xC0) == 0x80, "uuid5 variant bits are wrong");

		var wellKnown = new Installation.WellKnownIdentifiers(
			new Domain.InstallationId(Guid.Parse("11111111-1111-4111-8111-111111111111"))
		);
		Check(wellKnown.SystemTenantId != wellKnown.SystemPrincipalId, "catalogue ids collide");
	}

	static void VerifyMonotonic<TProvider>(Func<Guid, Guid, int> compare, string name)
		where TProvider : ISequentialGuidProvider
	{
		var start = DateTime.UtcNow.AddMinutes(-1);
		var values = new Guid[10_000];
		for (var index = 0; index < values.Length; index++)
		{
			values[index] = TProvider.NewGuid(start.AddTicks(index * TimeSpan.TicksPerMillisecond));
		}

		Check(Inversions(values, compare) == 0, $"{name} provider is not monotonic in store order");
	}

	static void VerifyUuidV7()
	{
		var start = DateTime.UtcNow.AddMinutes(-1);
		var values = new Guid[10_000];
		for (var index = 0; index < values.Length; index++)
			values[index] = UuidV7SequentialGuidProvider.NewGuid(start.AddMilliseconds(index));

		Check(Inversions(values, CompareSqlServer) > 1_000, "uuidv7 is unexpectedly monotonic on SQL Server");
		Check(Inversions(values, CompareBigEndian) == 0, "uuidv7 is not monotonic on PostgreSQL");
	}

	static void VerifyTimestampAndRange<TProvider>(Func<Guid, Guid, int> compare, string name)
		where TProvider : ISequentialGuidProvider
	{
		var requested = DateTime.UtcNow.AddMinutes(-1).AddTicks(12_345);
		var value = TProvider.NewGuid(requested);
		var actual = TProvider.GetTimestamp(value);

		Check(
			Math.Abs((actual - requested).Ticks) <= TimeSpan.TicksPerMillisecond,
			$"{name} timestamp round-trip exceeded 1 ms"
		);
		Check(compare(TProvider.MinValueFor(requested), value) <= 0, $"{name} lower bound failed");
		Check(compare(value, TProvider.MaxValueFor(requested)) <= 0, $"{name} upper bound failed");

		var threw = false;
		try
		{
			TProvider.NewGuid(DateTime.SpecifyKind(requested, DateTimeKind.Unspecified));
		}
		catch (ArgumentException)
		{
			threw = true;
		}

		Check(threw, $"{name} accepted an unspecified timestamp");
	}

	static int CompareSqlServer(Guid left, Guid right) => new SqlGuid(left).CompareTo(new SqlGuid(right));

	static int CompareBigEndian(Guid left, Guid right)
	{
		Span<byte> leftBytes = stackalloc byte[16];
		Span<byte> rightBytes = stackalloc byte[16];
		left.TryWriteBytes(leftBytes, bigEndian: true, out _);
		right.TryWriteBytes(rightBytes, bigEndian: true, out _);
		return leftBytes.SequenceCompareTo(rightBytes);
	}

	static int Inversions(Guid[] values, Func<Guid, Guid, int> compare)
	{
		var inversions = 0;
		for (var index = 1; index < values.Length; index++)
		{
			if (compare(values[index - 1], values[index]) > 0)
				inversions++;
		}

		return inversions;
	}

	static void Check(bool condition, string message)
	{
		if (!condition)
			throw new InvalidOperationException(message);
	}
}
