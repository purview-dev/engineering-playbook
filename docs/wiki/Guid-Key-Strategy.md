# GUID Key Strategy

## The rule

A GUID key needs one strategy per store, chosen once, and applied everywhere without being repeated on
each property.

1. **Pick one strategy per store, and carry it as data.** The provider is known where the context is
   registered, so that is where the choice belongs. Do not branch on `Database.ProviderName` at model
   build time; the model is built once, and the branch is a runtime lookup pretending to be
   configuration.
2. **When the identifier must exist before `SaveChanges`, the application generates it.** The store is
   not the generator for outbox rows, correlation values, event envelopes or an object graph built
   before the commit.
3. **Apply the strategy by convention to every GUID key in the model.** "Every key" includes keys
   declared by a framework base context such as ASP.NET Identity, and keys modelled as scalar value
   objects. A property added next month must be covered without anybody remembering.
4. **The generator must ascend in that store's comparison order.** Byte order and SQL Server's
   `uniqueidentifier` order are not the same order. A value that is sequential in bytes can be
   effectively random to a SQL Server index.
5. **Explicit configuration wins.** `ValueGeneratedNever()`, a different generator, a store default and
   a computed column all override the convention. The convention writes at the lowest precedence so
   that stating an exception is enough.

## Why

| Failure | Cause |
| -- | -- |
| Page splits and fragmentation on a clustered SQL Server index | Random GUIDs insert into the middle of the index instead of its end. |
| A store-ordered value that still fragments SQL Server | UUIDv7 is ordered in bytes, but `uniqueidentifier` weights the trailing six bytes most significantly. |
| One "portable" generator that helps neither store | PostgreSQL compares `uuid` in byte order; SQL Server does not, so the two providers cannot share an implementation. |
| A new entity silently gets a random key | The strategy was configured per property, and the new property had no configuration class. |
| A value-object key is assigned a raw `Guid` | The generator returned a `Guid`, but EF passes the generator's value to the property as-is; the property type is `OrderId`. |
| The first provider to build the model wins for the process | The model is cached per context type, and an external internal service provider bypasses the strategy's cache key. |

## The samples

The complete implementation is `samples/Acme.Persistence` at the repository root; `just samples` builds
and runs it with all three strategies. The page shows the parts that carry the decision, and the sample
is the copyable whole. It lives outside `src/` because the reference implementation there is in-memory
by design, and adding EF Core, a persistence project and a database would contradict [Worked
Examples](Worked-Examples.md).

### 1. The strategy as a pure witness

```csharp
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
```

Static abstract members make each implementation a pure witness. It is never instantiated, so there is
no state, no lifetime and no dependency-injection registration to get wrong. It can be a type argument
to both EF's model pipeline and ordinary application code, and the compiler resolves every call to the
one implementation named in the type argument. An instance-based strategy would need a registration,
and a wrong registration would silently produce the wrong store's ordering.

### 2. Three providers

`FastGuid` below is the `FastGuid` NuGet package (`SecurityDriven.FastGuid`). Its values are ordered for
a specific store, not merely in byte order.

```csharp
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
		var milliseconds = new DateTimeOffset(UtcTimestamp.EnsureUtc(timestampUtc))
			.ToUnixTimeMilliseconds();
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
				nameof(timestampUtc))
			: timestampUtc.ToUniversalTime();
}
```

- **SQL Server weights the trailing bytes most.** It compares `uniqueidentifier` by its last six bytes,
  then the preceding groups. `FastGuid.NewSqlServerGuid()` puts the increasing part where SQL Server
  sorts it. `Guid.CreateVersion7()` does not, so it behaves like a random value on a clustered index.
- **PostgreSQL compares `uuid` in plain big-endian byte order.** `FastGuid.NewPostgreSqlGuid()` and
  `Guid.CreateVersion7()` both work there. Npgsql converts the value to the big-endian wire form.
- **UUIDv7 is RFC 9562, and it is the fallback.** Use it when no provider strategy is registered, or on
  PostgreSQL. It is genuinely sequential on PostgreSQL and not sequential on SQL Server. Choosing it
  everywhere is the mistake this page exists to prevent.
- **`EnsureUtc` rejects `DateTimeKind.Unspecified`.** An unspecified timestamp is interpreted in the
  machine's local zone, so the same code orders differently on two hosts. That is a silent ordering
  bug, not a convenience.
- **`FastGuid` values are not RFC 9562 UUIDs.** They are ordinary GUIDs with a store-specific layout,
  so PostgreSQL's `uuid_extract_timestamp()` cannot read them. `GetTimestamp` is the only supported way
  to recover the timestamp.

### 3. The options extension is the decision, made once

```csharp
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
				"Sequential GUID generation cannot be combined with UseInternalServiceProvider.");
		}
	}

	sealed class ExtensionInfo(SequentialGuidOptionsExtension extension)
		: DbContextOptionsExtensionInfo(extension)
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
```

The two comments are the design. `ApplyServices` registers the application-facing factory, and the
`Validate` guard refuses a combination that would make the strategy's cache key irrelevant. The
`ExtensionInfo` hash and `ShouldUseSameServiceProvider` must agree with the fields: if either ignores
the value generator or the factory, two strategies collapse to one cached service provider.

### 4. The generator, with value-object conversion

The subtle part is the converter. EF passes the generator's return value to the property as-is, so a key
modelled as a `readonly record struct OrderId` needs the `Guid` converted. The generator declares
`GeneratesTemporaryValues => false`, because a temporary key would make EF rewrite the value after the
insert and break every reference built before the commit.

```csharp
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
	public override bool GeneratesTemporaryValues => false;

	public override Guid Next(EntityEntry entry) => TProvider.NewGuid();
}
```

`ValueGenerator.WithConverter` wraps the generator so the value is converted to the model type before
EF assigns it. Without it, a `OrderId` key receives a boxed `Guid` and the insert fails or the value is
silently unusable.

### 5. The convention, applied to every key

```csharp
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
		IConventionContext<IConventionModelBuilder> context)
	{
		GuardFactory();

		foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
		{
			foreach (var property in entityType.GetProperties())
			{
				if (!ShouldGenerate(property))
					continue;

				var builder = property.Builder
					?? throw new InvalidOperationException(
						$"'{property.Name}' is read-only during model finalisation.");
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
		if (property.IsForeignKey()
			|| property.GetDefaultValueSql() is not null
			|| property.GetComputedColumnSql() is not null)
		{
			return false;
		}

		return property.IsKey() || (alsoGenerate?.Invoke(property) ?? false);
	}

	static bool IsGuid(Type clrType) =>
		clrType == typeof(Guid)
		|| clrType == typeof(Guid?)
		|| typeof(IComparable<Guid>).IsAssignableFrom(clrType);

	static void GuardFactory()
	{
		if (!typeof(ValueGeneratorFactory).IsAssignableFrom(FactoryType)
			|| FactoryType.GetConstructor(Type.EmptyTypes) is null)
		{
			throw new InvalidOperationException(
				$"'{FactoryType}' must derive from ValueGeneratorFactory and have a parameterless constructor.");
		}
	}
}
```

Every branch exists for a reason:

- **`IModelFinalizingConvention`, not a loop at the end of `OnModelCreating`.** The convention sees
  entity types discovered late through navigations, complex types, and configuration classes applied
  in an order the context does not control.
- **The convention configuration source is the lowest precedence.** Explicit configuration therefore
  wins, and `property.Builder` is non-null for a property in a finalising model; the guard makes that
  assumption explicit rather than dereferencing silently.
- **`Guid`, `Guid?`, and any `IComparable<Guid>`.** The third form is how a scalar value-object
  identifier models its key, and it is the case a `Guid`-only check misses.
- **Foreign keys are skipped.** A foreign key takes its value from the principal that owns it.
- **Default and computed columns are skipped.** The store owns those values.
- **Keys are always included.** `alsoGenerate` widens the set to indexed non-key GUID columns, which
  pay the same fragmentation cost. In `ConfigureConventions`, where `configurationBuilder` is the
  `ModelConfigurationBuilder`:

  ```csharp
  configurationBuilder.Conventions.Add(
  	_ => new SequentialGuidConvention<SqlServerSequentialGuidProvider>(
  		property => property.GetContainingIndexes().Any()));
  ```

- **The factory is guarded.** `HasValueGeneratorFactory` takes a type, so a compile error cannot catch
  a type that does not derive from `ValueGeneratorFactory` or lacks a parameterless constructor. The
  guard turns that into a model-build failure with a name in it.

### 6. Wiring: one call per provider

The options builder extension carries the strategy's three facts — its name, its generator and its
application-facing factory — as one extension:

```csharp
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
			typeof(SequentialGuidFactory<TProvider>));

		((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(extension);

		return builder;
	}
}

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
```

The host registers the context, and the application-facing factory, with the same provider:

```csharp
builder.Services.AddDbContext<AcmeDbContext>((services, options) =>
{
	options
		.UseSqlServer(connectionString)
		.UseSequentialGuids<SqlServerSequentialGuidProvider>()
		.ReplaceService<IModelCacheKeyFactory, AcmeModelCacheKeyFactory>()
		.ReplaceService<IModelCustomizer, SqlServerAcmeModelCustomizer>();
});

builder.Services.AddSingleton<ISequentialGuidFactory, SequentialGuidFactory<SqlServerSequentialGuidProvider>>();
```

The convention itself is registered where the context configures its conventions, so the same static is
reachable from a context that cannot derive from the application base context:

```csharp
using Microsoft.EntityFrameworkCore;

public sealed class AcmeDbContext(DbContextOptions<AcmeDbContext> options) : DbContext(options)
{
	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
	{
		base.ConfigureConventions(configurationBuilder);
		AcmeModelConventions.ApplySequentialGuids<SqlServerSequentialGuidProvider>(configurationBuilder);
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
		// ... entity configuration ...
	}
}

public static class AcmeModelConventions
{
	public static void ApplySequentialGuids<TProvider>(ModelConfigurationBuilder configurationBuilder)
		where TProvider : ISequentialGuidProvider
	{
		configurationBuilder.Conventions.Add(
			_ => new SequentialGuidConvention<TProvider>(
				property => property.GetContainingIndexes().Any()));
	}
}
```

Four wiring obligations are easy to miss:

- **The design-time options builder must do the same three things.** If it omits `UseSequentialGuids`
  or either `ReplaceService`, the migration snapshot and the runtime model disagree, and the next
  migration will contain a change nobody intended.
- **A context that cannot derive from the application base context calls the shared static.** ASP.NET
  Identity's context is the usual case: it must call `AcmeModelConventions.ApplySequentialGuids` from
  its own `ConfigureConventions`, and its own `base.OnModelCreating` first. Otherwise the Identity
  user key is random while every other key is sequential.
- **Register the application-facing factory separately.** EF's internal registration is not reachable
  from application code. Outbox rows, event envelopes and correlation values need the same values, and
  a second implementation is a second ordering.
- **Replace `IModelCacheKeyFactory` when the model varies by design-time state or tenant.** The service
  provider hash separates strategies, but tenant-specific model configuration is not in the options.
  The cache key must include it, or the first tenant's model is served to the next.

The sample at `samples/Acme.Persistence` closes `AcmeDbContext` over the strategy so one executable can
demonstrate all three providers. A single-provider application can make the type parameter concrete
instead, or hard-code the provider in `ConfigureConventions` as above; the convention and the wiring are
otherwise unchanged.

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

/// <summary>Separates the model cache by context type, design time and strategy.</summary>
public sealed class AcmeModelCacheKeyFactory : IModelCacheKeyFactory
{
	public object Create(DbContext context, bool designTime) =>
		(
			context.GetType(),
			designTime,
			context.GetService<IDbContextOptions>()
				.FindExtension<SequentialGuidOptionsExtension>()
				?.StrategyName
		);
}
```

### 7. Where UUIDv5 and sequential keys meet

- A UUIDv5 identifier is explicitly keyed with `ValueGeneratedNever()`, so this convention leaves it
  alone. That is the documented exception, and a test must prove it: [Well-Known
  Identifiers](Well-Known-Identifiers.md) publishes the check.
- Never apply the sequential convention to a table whose key is derived. The derived value is the
  point; a "sequential" derived key is a contradiction.
- Conversely, do not use UUIDv5 to make ordinary rows deterministic. It is not sequential, and a
  high-write table pays for that at the index. Derive only the known identities.

## How it is enforced

The wiki cannot enforce this. A suite attached to the adopting repository does, and these are the
checks to write. They are tests in that repository, not in this one.

| Check | Shape |
| -- | -- |
| Store-order monotonicity | 10,000 values; `SqlGuid.CompareTo` on SQL Server; big-endian `SequenceCompareTo` on PostgreSQL. |
| UUIDv7 is not SQL Server monotonic | Assert inversions exceed a threshold. The test documents the trap rather than pretending it is absent. |
| Timestamp round-trip | Extract within 1 ms of the requested UTC timestamp, for every strategy. |
| Range bounds | `MinValueFor(t) <= NewGuid(t) <= MaxValueFor(t)` under the store's comparison. |
| Every key covered | For each entity type and each GUID key that is not a foreign key: `ValueGenerated.OnAdd` and the provider's factory. |
| Foreign keys excluded | A foreign-key GUID is never `OnAdd` from this convention. |
| Explicit configuration wins | A `ValueGeneratedNever()` property stays `Never`. |
| Framework keys included | The Identity user's key uses the configured generator. |
| Two providers, one context type | Different strategies produce different service providers and different models. |
| Integration | Insert N rows; keys ascend in the store's order; a repeat run leaves no duplicates. |

## At your scale

- **One provider, low write volume.** `Guid.NewGuid()`. Do not build any of this. Fragmentation never
  matters at tens of inserts a minute, and a random GUID is the simpler, more private choice.
- **One provider, high write volume, value needed after insert.** `NEWSEQUENTIALID()` on SQL Server,
  `uuidv7()` on PostgreSQL 18 and later. Both are store capabilities and both are sufficient.
- **One provider, value needed before insert.** `Guid.CreateVersion7()` with `ValueGeneratedNever()` on
  PostgreSQL; EF Core's `SequentialGuidValueGenerator` on SQL Server. Still no abstraction.
- **Two providers, or identifiers needed before insert on both.** The strategy and convention above.
  The abstraction is earned by the second provider, not by anticipation.

## See also

- [Well-Known Identifiers](Well-Known-Identifiers.md) — the derived keys this convention must skip
- [Contracts and Data Exchange](Contracts-And-Data-Exchange.md) — value-object identifiers and their
  converters
- [Worked Examples](Worked-Examples.md) — why `src/` has no persistence to compile these against
