# Worked Examples

## The rule

`src/` in this repository is a deliberately small layered repository that is built, linted and tested by the
same pipeline every Purview repository uses. It exists so that the rules in this playbook are not just prose:
the dependency-direction rules fail the build when they are broken, and the test-data conventions are shown
in the form they are meant to be copied.

It is small on purpose. There is one aggregate, one port, one adapter and four test projects — enough to
show every mechanism, and little enough that you can read all of it in ten minutes.

## Why

A rule you have not watched fail is a rule you are guessing about. Prose can be read and agreed with; a
reference implementation can be built, run, and broken on purpose to see the failure. It is also the one
place this playbook can prove that its own conventions compile and pass — the pipeline that runs here is the
pipeline every Purview repository uses, so a convention that cannot survive its own reference implementation
is a convention that will not survive contact with a real one.

## The shape

```text
src/
├── EngineeringPlaybook.slnx
├── Directory.Build.props            NamespacePrefix + SDK import + NamespaceRemoveSuffix
├── Directory.Build.targets
├── src/
│   ├── Domain/                      Purview.EngineeringPlaybook.Domain
│   │   ├── ConventionId.cs          a value object: Create validates, Hydrate trusts
│   │   ├── VerificationTier.cs      the tier a convention is proved in
│   │   └── Convention.cs            the aggregate
│   ├── Application/                 Purview.EngineeringPlaybook.Application
│   │   ├── IConventionRepository.cs the port — owned here, not beside its implementation
│   │   └── ConventionCatalogue.cs   the service
│   └── Infrastructure/              Purview.EngineeringPlaybook.Infrastructure
│       └── InMemoryConventionRepository.cs
└── tests/
    ├── SharedTestingInfra/          TestBuilder
    ├── Domain.UnitTests/            value-object behaviour, direct construction
    ├── Application.UnitTests/       the service, against a TUnit.Mocks substitute
    ├── Architecture.UnitTests/      the dependency-direction rules, and the architecture policy
    └── Repository.UnitTests/        the repository itself: its skills and its wiki
```

## The one architectural decision

Everything else follows from this: **the port lives in the layer that needs it.**

```csharp
// Purview.EngineeringPlaybook.Application
public interface IConventionRepository
{
	Task<bool> ExistsAsync(ConventionId id, CancellationToken cancellationToken);
	Task AddAsync(Convention convention, CancellationToken cancellationToken);
	Task<IReadOnlyList<Convention>> ListAsync(CancellationToken cancellationToken);
}
```

The interface is in `Application`, and the class that implements it is in `Infrastructure`. That is what
makes the dependency direction one-way and assertable:

- `Application` references `Domain` only. It names `IConventionRepository`, which is its own type.
- `Infrastructure` references `Application`, because it implements that type.
- `Domain` references nothing of ours, and has no idea a repository exists.

The alternative — the interface beside the implementation, in `Infrastructure` — reverses the dependency:
`Application` would have to reference `Infrastructure` to name the port, and the innermost layer would depend
on the outermost. The architecture tests exist to make that mistake fail rather than be debated.

## The domain

`ConventionId` is the value object, hand-written here so the pattern is visible without a source generator in
the way:

```csharp
public sealed record ConventionId
{
	public const int MaxLength = 64;

	ConventionId(string value) => Value = value;

	public string Value { get; }

	public static ConventionId Create(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new ArgumentException("A convention identifier cannot be empty.", nameof(value));
		}

		// ... length, leading/trailing hyphen, and per-character checks ...

		return new ConventionId(value);
	}

	public static ConventionId Hydrate(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new ArgumentException("A hydrated convention identifier cannot be empty.", nameof(value));
		}

		return new ConventionId(value);
	}

	public override string ToString() => Value;
}
```

Three details are deliberate:

- **The constructor is private.** The only doors are `Create` and `Hydrate`, so there is no path into the
  type that skips the rules.
- **`Hydrate` is not `Create` with a flag.** It rejects only the value that cannot possibly be valid — null
  or whitespace — and trusts everything else. That is the whole contract: a hydrated value already came
  through a trusted boundary, and re-validating it would turn a data problem into a crash somewhere
  unrelated.
- **It is a `record`.** Equality is by value, which is what makes it usable as a dictionary key and in an
  assertion.

`Convention` adds the aggregate rules — a title that is non-empty, trimmed, and no longer than
`MaxTitleLength` — and carries `EnforcedByBuild`, which is the playbook's own distinction between a rule the
build refuses to let you break and a rule a reviewer has to notice.

## The application layer

`ConventionCatalogue` holds the rules and nothing else:

```csharp
public sealed class ConventionCatalogue
{
	readonly IConventionRepository _repository;

	public ConventionCatalogue(IConventionRepository repository) =>
		_repository = repository ?? throw new ArgumentNullException(nameof(repository));

	public async Task RegisterAsync(Convention convention, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(convention);

		var alreadyRegistered = await _repository
			.ExistsAsync(convention.Id, cancellationToken)
			.ConfigureAwait(false);

		if (alreadyRegistered)
		{
			throw new InvalidOperationException($"Convention '{convention.Id}' is already registered.");
		}

		await _repository.AddAsync(convention, cancellationToken).ConfigureAwait(false);
	}
}
```

Note the absence of a database, an ORM, a logger and a configuration object. The class depends on one
interface, which is why its tests need no infrastructure — and why the architecture rules can be strict
without being annoying.

## The adapter

```csharp
public sealed class InMemoryConventionRepository : IConventionRepository
{
	readonly ConcurrentDictionary<string, Convention> _conventions = new(StringComparer.Ordinal);

	public Task<bool> ExistsAsync(ConventionId id, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(id);
		cancellationToken.ThrowIfCancellationRequested();

		return Task.FromResult(_conventions.ContainsKey(id.Value));
	}
	// ...
}
```

It exists so the reference implementation has a real adapter on the far side of the port without dragging in
a database. The point being demonstrated is the shape — an adapter implementing a port owned by the layer
above it — not the storage.

It also demonstrates a detail that is easy to get wrong: **an interface implementation keeps `public`.** The
SDK's style policy is `omit_if_default`, so a private field has no modifier, but a member that satisfies an
interface is explicitly `public` because `public` is not the default there.

## The tests

Four projects, each demonstrating a different thing.

**`Domain.UnitTests`** is the value-object exception to the "build everything through `TestBuilder`" rule. A
test that proves what `Create` rejects has to name the rejected input itself:

```csharp
[Test]
[Arguments("")]
[Arguments("   ")]
[Arguments("Build-Enforced")]
[Arguments("build_enforced")]
[Arguments("-leading-hyphen")]
[Arguments("trailing-hyphen-")]
[Arguments("has a space")]
[Arguments("has.a.dot")]
public async Task Create_GivenSlugThatIsNotALowercaseSlug_Throws(string value)
{
	// Arrange
	// Act
	Action act = () => ConventionId.Create(value);

	// Assert
	await Assert.That(act).Throws<ArgumentException>();
}
```

Valid values still come from the builder. The exception is about *invalid* input, where the literal is the
point of the test.

**`Application.UnitTests`** uses a TUnit.Mocks substitute, and asserts on the interaction rather than on
state:

```csharp
[Test]
public async Task RegisterAsync_GivenAnAlreadyRegisteredConvention_DoesNotStoreIt(CancellationToken cancellationToken)
{
	// Arrange
	var repository = CreateRepository();
	repository.ExistsAsync(Any(), Any()).Returns(true);
	var catalogue = CreateCatalogue(repository.Object);

	// Act
	await Assert
		.That(async () => await catalogue.RegisterAsync(
			TestBuilder.EnforcedConvention(),
			cancellationToken))
		.Throws<InvalidOperationException>();

	// Assert
	repository.AddAsync(Any(), Any()).WasNeverCalled();
}
```

**`Architecture.UnitTests`** runs two kinds of rule. `DependencyDirectionTests` is the four rules from
[Architecture as Tests](Architecture-As-Tests.md), reading the compiled assemblies; it is the reason the
`Application` project file carries a comment explaining that a reference to `Infrastructure` would fail
the build. `ArchitectureTests` derives from `PurviewArchitectureTests` and enforces the policy in
`.config/architecture-policy.json`, so the dependency matrix, naming, sibling-independence, composition,
forbidden-package, cycle, test-reference and waiver rules come from `Purview.Architecture.Testing`
rather than from C# written here. `PolicyDocumentationTests` keeps the wiki's tables in step with that
policy.

**`Repository.UnitTests`** turns the repository itself into the subject. `SkillTests` derives from the
skill-conformance rule set the SDK ships, and `DocumentationTests` asserts that every wiki page is in
the navigation and that every fence declares a language. A convention that has to hold across the whole
repository, and that a reviewer would otherwise have to remember, belongs in a test here.

## The test data builder

`TestBuilder` is flat rather than grouped, and
[Testing and Test Data](Testing-And-Test-Data.md#two-constraints-that-shaped-the-builder) explains why. The
contract is the interesting part:

```csharp
public static Domain.Convention Convention(
	Domain.ConventionId? id = null,
	string? title = null,
	VerificationTier? tier = null,
	bool? enforcedByBuild = null) =>
	Domain.Convention.Create(
		id ?? ConventionId(),
		title ?? Faker.Lorem.Sentence(4),
		tier ?? Faker.Random.Enum<VerificationTier>(),
		enforcedByBuild ?? Faker.Random.Bool());
```

An omitted argument is generated; a supplied argument is used exactly. A test therefore reads as a list of
the values it actually cares about, and everything else is realistic without the test saying so.

## How it is enforced

The reference implementation is built, linted and tested by the same pipeline every Purview repository
uses, so a broken convention fails here first.

```text
just build          # dotnet build (Debug)
just test           # dotnet test
just lint-check     # CSharpier check
just pipeline-pr    # restore, build, lint, test through the shared Purview.Build tool
```

On a clean checkout of this repository those give:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)

Test run summary: Passed!
  total: 71
  failed: 0
  succeeded: 71
```

The zero-warning result is worth noticing, because it took work: the telemetry source generator's public
types, the SDK's `var` preference, and the flat-factory constraint all surfaced as build diagnostics rather
than as documentation. Each is written down on the page where it belongs.

### Break it on purpose

The fastest way to understand a rule is to watch it fail. Three edits, each of which should fail the build:

1. **Add `<ProjectReference Include="../Infrastructure/Infrastructure.csproj" />` to
   `Application.csproj`.** `Application_GivenItsReferences_DependsOnTheDomainAndNotOnInfrastructure` fails.
2. **Rename `src/src/Domain/Domain.csproj` to `DomainAggregate.csproj`.** `PurviewProjectFileNameMismatch`
   fails, because the project filename no longer matches its directory.
3. **Move a `Convention` type into a namespace outside the repository's prefix, such as
   `Acme.Elsewhere`.** The namespace-identity rule fails and names the type.

## What this repository deliberately does not do

- **No package.** It ships no NuGet package, so there is no pack metadata, no pack validation manifest and no
  consumer-verification script. Those mechanisms are documented, and
  [containers](https://github.com/purview-dev/containers) is the reference for them.
- **No persistence.** The adapter is in-memory. There is no migration, no schema, no connection string.
- **No persistence-specific identifier code in `src/`.** [Well-Known Identifiers](Well-Known-Identifiers.md)
  and [GUID Key Strategy](Guid-Key-Strategy.md) are implemented in full by the standalone sample at
  `samples/Acme.Persistence`. `just samples` builds and runs it; it sits outside the canonical `src/`
  solution because the reference implementation here is in-memory by design.
- **No web layer.** The result-to-response mapping in [Contracts and Data
  Exchange](Contracts-And-Data-Exchange.md) is described, not implemented, because
  [results](https://github.com/purview-dev/results) already implements it and a second implementation here
  would be a worse copy.
- **No integration tier.** Every test is deterministic, so the pipeline needs no second job.

## At your scale

This is the **Medium** profile: a few layers, a shared testing project, and architecture tests. A small
repository takes the SDK import, `Directory.Packages.props`, `AGENTS.md` and `TestBuilder` and stops there;
a large one adds pack validation, consumer verification and a release gate. [Adopting This at Your
Scale](Adopting-This-At-Your-Scale.md) is the page that decides.

## See also

- [Getting Started](Getting-Started.md)
- [Architecture as Tests](Architecture-As-Tests.md)
- [Testing and Test Data](Testing-And-Test-Data.md)
- [Contracts and Data Exchange](Contracts-And-Data-Exchange.md)
