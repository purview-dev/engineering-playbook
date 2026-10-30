# Well-Known Identifiers

## The rule

An identifier whose value is known before storage is **derived**, not generated. The derivation is
UUIDv5 over a namespace and a name, and it carries five obligations:

1. **Derive it.** `Uuid5.Create(namespace, name)` produces an RFC 4122 version-5 (SHA-1) UUID that is
   byte-identical for the same namespace and name in every process, language and restart.
2. **Scope the namespace to the isolation boundary.** The namespace is one installation or one
   deployment — never a global constant shared by every deployment. Two installations that share a
   namespace share identities they must not share.
3. **Fix the name as a canonical constant of the identity kind.** It is not a display name, not user
   data and not a secret. It identifies a role in the system, not a row's contents.
4. **Treat the persisted value as authoritative.** Once written, never recompute it in a later release
   and update the row. If the derivation and the row disagree, the row is what every reference already
   points at.
5. **Key it explicitly.** An entity whose key is derived configures `ValueGeneratedNever()`. It is
   excluded from [GUID Key Strategy](Guid-Key-Strategy.md), whose convention must never generate over
   a value the application already knows.

### The catalogue

The identities worth deriving are few and enumerable. Write them down in one place.

| Identity | Namespace | Name | Derived when | Immutable |
| -- | -- | -- | -- | -- |
| System tenant of an installation | installation id | `system-tenant` | first bootstrap | yes |
| System principal of an installation | installation id | `system-service-principal` | first bootstrap | yes |
| Seeded reference row | installation id | `<entity>:<code>` | seed step | yes |
| Ingested external object (optional) | installation id | `<provider>:<external-id>` | first ingest | yes |

The last row is conditional. Derive it only when the provider guarantees the external key is stable and
never recycled. If the provider can recycle a key, keep the local key sequential and store the external
key as a unique column instead; a recycled external key would otherwise inherit the identity, history
and references of the object that used it before.

### Naming rules

- Lowercase ASCII, hyphen-separated, no whitespace, no locale-dependent casing.
- Never built from a display name, email, URL, title, or anything a user can edit.
- Never built from a secret and never used as a capability token. A name is guessable; the derived
  identifier is guessable too.
- Renaming a name creates a new identity. Renaming a persisted one is a data migration, not a rename.

## Why

Each obligation prevents a specific failure:

| Obligation | Failure it prevents |
| -- | -- |
| Derive, do not generate | A second bootstrap run inserts a duplicate system tenant, or a second replica mints a different id for the same logical row. |
| Scope the namespace | Two installations collide, so one deployment's system tenant appears in another's data. |
| Canonical name | Correcting a display name changes the id, and every foreign key and audit row now points at a row that no longer exists under that id. |
| Persisted value wins | A later release recomputes an id and updates the row, breaking references and history that were built on the old value. |
| Explicit keying | The sequential-GUID convention overwrites a derived key, so "deterministic" quietly becomes random. |

There is one more failure worth naming because it looks correct in review: a namespace generated per
process. `Guid.NewGuid()` at startup is random on every restart, so the "deterministic" identifier is
deterministic only until the process restarts. The namespace must be persisted configuration — the
installation id — not a runtime value.

**The limit: UUIDv5 is not sequential.** Do not use it for a high-write table's primary key. It exists
for a small, known set of identities; every other key takes [GUID Key Strategy](Guid-Key-Strategy.md).

## The samples

The complete implementation is `samples/Acme.Persistence` at the repository root; `just samples` builds
and runs it. The page shows the parts that carry the decision, and the sample is the copyable whole. It
lives outside `src/` because the reference implementation there is in-memory by design, and adding EF
Core, a persistence project and a database would contradict [Worked Examples](Worked-Examples.md).

### 1. The helper

```csharp
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace Acme;

/// <summary>Deterministic RFC 4122 version-5 (SHA-1) UUID generation.</summary>
public static class Uuid5
{
	public static readonly Guid NamespaceDns = new("6ba7b810-9dad-11d1-80b4-00c04fd430c8");
	public static readonly Guid NamespaceUrl = new("6ba7b811-9dad-11d1-80b4-00c04fd430c8");
	public static readonly Guid NamespaceOid = new("6ba7b812-9dad-11d1-80b4-00c04fd430c8");
	public static readonly Guid NamespaceX500 = new("6ba7b814-9dad-11d1-80b4-00c04fd430c8");

	[SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms",
		Justification = "SHA-1 is mandated by RFC 4122 for UUIDv5.")]
	public static Guid Create(Guid namespaceId, string name)
	{
		ArgumentNullException.ThrowIfNull(name);

		// The text form is big-endian. Guid.ToByteArray() and new Guid(byte[]) use a mixed-endian
		// layout and produce the wrong hash for every namespace whose first three groups are not
		// palindromic — which is most of them.
		Span<byte> namespaceBytes = stackalloc byte[16];
		namespaceId.TryWriteBytes(namespaceBytes, bigEndian: true, out _);

		var nameBytes = Encoding.UTF8.GetBytes(name);
		var hashInput = new byte[16 + nameBytes.Length];
		namespaceBytes.CopyTo(hashInput.AsSpan(0, 16));
		nameBytes.CopyTo(hashInput, 16);

		var hash = SHA1.HashData(hashInput);

		var result = hash.AsSpan(0, 16);
		result[6] = (byte)((result[6] & 0x0F) | 0x50); // version 5
		result[8] = (byte)((result[8] & 0x3F) | 0x80); // RFC 4122 variant

		return new Guid(result, bigEndian: true);
	}
}
```

The endianness comment is load-bearing. `Guid.ToByteArray()` writes the first three groups
little-endian, so the same namespace hashes differently through that path. The bug is invisible for
palindromic namespaces and wrong for almost every real installation id.

### 2. The catalogue as code

```csharp
namespace Acme.Installation;

/// <summary>Well-known identities scoped to one installation.</summary>
public sealed record WellKnownIdentifiers(InstallationId Installation)
{
	public Guid SystemTenantId => Uuid5.Create(Installation.Value, "system-tenant");
	public Guid SystemPrincipalId => Uuid5.Create(Installation.Value, "system-service-principal");

	public Guid ReferenceRow(string entity, string code) =>
		Uuid5.Create(Installation.Value, $"{entity}:{code}".ToLowerInvariant());
}
```

`InstallationId` is the installation's strongly-typed identifier over a `Guid`; its `Value` is the
namespace. `ReferenceRow` lowercases the composed name so `Role` and `role` cannot produce two
identities for the same code.

### 3. Idempotent bootstrap

The sequence matters more than the surrounding seeder. Read the persisted identity; if it exists it
wins, and a configured value must match or the operation fails. If it does not exist, insert it. Derive
dependants from the persisted id, save once, and let a second run change nothing.

```csharp
using Acme.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Acme.Installation;

public sealed class InstallationBootstrap(
	AcmeDbContext dbContext,
	IOptions<InstallationOptions> options)
{
	public async Task RunAsync(CancellationToken cancellationToken)
	{
		var configured = new InstallationId(options.Value.InstallationId);
		var persisted = await dbContext
			.Installations.OrderBy(installation => installation.CreatedAtUtc)
			.FirstOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		// The persisted identity is authoritative; a configured one that disagrees is a
		// deployment error, not a reason to rewrite the row.
		if (persisted is not null && persisted.Id != configured)
		{
			throw new InvalidOperationException(
				$"Configured installation id '{configured.Value}' does not match "
					+ $"the persisted id '{persisted.Id.Value}'.");
		}

		var installation = persisted ?? new Installation(configured, DateTime.UtcNow);
		if (persisted is null)
			dbContext.Installations.Add(installation);

		var wellKnown = new WellKnownIdentifiers(installation.Id);
		if (
			!await dbContext
				.Tenants.AnyAsync(tenant => tenant.Id == wellKnown.SystemTenantId, cancellationToken)
				.ConfigureAwait(false)
		)
		{
			dbContext.Tenants.Add(new Tenant(wellKnown.SystemTenantId, "System"));
		}

		if (
			!await dbContext
				.ServicePrincipals.AnyAsync(
					principal => principal.Id == wellKnown.SystemPrincipalId,
					cancellationToken
				)
				.ConfigureAwait(false)
		)
		{
			dbContext.ServicePrincipals.Add(
				new ServicePrincipal(wellKnown.SystemPrincipalId, "System")
			);
		}

		// One commit: either the installation and its system identities exist, or none do.
		await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
	}
}
```

`Installation`, `Tenant` and `ServicePrincipal` are ordinary EF entities. `Installation` holds the
installation id; the other two are keyed by the derived values and configure `ValueGeneratedNever()`.
The sample is an extract: the point is the read-or-insert sequence and the guard, not the shape of the
rows.

### 4. Migrations cannot call the helper

A migration and `HasData` are compiled and replayed, so they need a literal GUID. They cannot call
`Uuid5.Create`, because the namespace is deployment configuration and a migration must produce the same
SQL on every machine that applies it.

The pattern is a small script that prints ready-to-paste constants from the same derivation, and a test
that asserts every printed constant equals `Uuid5.Create(...)`. The literal installation id below is
fixed so the constants are reproducible; a real repository substitutes the deployment's id.

```csharp
// tools/DerivedIdentifiers/Program.cs
using Acme.Installation;

var installation = new InstallationId(Guid.Parse(args[0]));
var wellKnown = new WellKnownIdentifiers(installation);

Console.WriteLine($"tenant:    {wellKnown.SystemTenantId:D}");
Console.WriteLine($"principal: {wellKnown.SystemPrincipalId:D}");
```

For installation `11111111-1111-4111-8111-111111111111` the script prints:

```text
tenant:    598f4dd5-4881-5833-8615-2915ab6f7ac9
principal: 62331408-8c66-5cd0-82e1-d8fc91642fb4
```

Paste the tenant constant into the migration and the test:

```csharp
migrationBuilder.InsertData(
	table: "Tenants",
	columns: ["Id", "Name"],
	values: [Guid.Parse("598f4dd5-4881-5833-8615-2915ab6f7ac9"), "System"]);
```

```csharp
using Acme;

[Test]
public async Task SeededTenant_GivenTheMigrationConstant_MatchesTheDerivation()
{
	var namespaceId = Guid.Parse("11111111-1111-4111-8111-111111111111");
	var seeded = Guid.Parse("598f4dd5-4881-5833-8615-2915ab6f7ac9");

	await Assert.That(seeded).IsEqualTo(Uuid5.Create(namespaceId, "system-tenant"));
}
```

**Changing the namespace or a name after a migration has shipped is a data migration.** Every row
already written carries the old value, and every reference points at it. Plan the backfill; do not
change the constant and hope.

## How it is enforced

The wiki cannot enforce this. A suite attached to the adopting repository does, and these are the
checks to write. They are tests in that repository, not in this one.

| Check | Shape |
| -- | -- |
| RFC 4122 conformance | Fixed vectors for DNS/URL/OID/X500, plus the empty name; assert exact GUIDs. |
| Determinism | The same pair twice is equal; different names differ. |
| Version and variant bits | Byte 6 high nibble is `0x5`; byte 8 high bits are `10`. |
| Cross-language equality | A second implementation (Python `uuid.uuid5` or Node `crypto`) reproduces the vectors. |
| Catalogue uniqueness | The catalogue's constants are distinct. |
| Bootstrap idempotency | Seeding twice changes zero rows; a mismatched configured id throws. |
| Explicit keying | The entity's key is `ValueGenerated.Never`, and the sequential convention leaves it alone. |

The reference vectors below are the contract. They match RFC 4122 and Python's `uuid.uuid5`, and an
implementation that does not reproduce them is not UUIDv5 — most often because of the endianness trap
in the helper.

```text
uuid5(6ba7b810-9dad-11d1-80b4-00c04fd430c8, "example.com")     = cfbff0d1-9375-5685-968c-48ce8b15ae17
uuid5(6ba7b811-9dad-11d1-80b4-00c04fd430c8, "www.example.com") = b63cdfa4-3df9-568e-97ae-006c5b8fd652
uuid5(6ba7b812-9dad-11d1-80b4-00c04fd430c8, "1.2.3.4")         = 67448b45-6d15-536b-bef7-8a78c3d10ac6
uuid5(6ba7b814-9dad-11d1-80b4-00c04fd430c8, "CN=Bob")          = 0840e90d-9ca0-5f14-970e-26294d44cb1d
```

RFC 4122 is the specification for version 5; [RFC 9562](https://www.rfc-editor.org/rfc/rfc9562) is the
current UUID specification and retains the version-5 algorithm. The .NET BCL has no UUIDv5 API, which
is why the helper above exists rather than a package reference.

## At your scale

- **Single deployment, no seed or ingest.** Do not add this. Use [GUID Key
  Strategy](Guid-Key-Strategy.md) for every key; there is no identity whose value is known in advance.
- **One installation, a handful of system rows.** One namespace constant and two or three names. Skip
  the catalogue abstraction until there is a second identity kind.
- **Several installations and provider ingestion.** The catalogue above, the derived-literal script,
  and the uniqueness and idempotency tests. The namespace becomes configuration that is persisted and
  checked at bootstrap, not a constant compiled into the application.

## See also

- [GUID Key Strategy](Guid-Key-Strategy.md) — the sequential keys every other table uses
- [Contracts and Data Exchange](Contracts-And-Data-Exchange.md) — value objects that carry an identifier
- [Worked Examples](Worked-Examples.md) — why `src/` has no persistence
