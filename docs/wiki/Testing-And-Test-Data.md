# Testing and Test Data

## The rule

| Concern | The rule |
| -- | -- |
| Framework | TUnit. Not xUnit, NUnit or MSTest. |
| Substitutes | TUnit.Mocks. Not NSubstitute or Moq. |
| Test data | Bogus, reached through `TestBuilder`. |
| Structure | Explicit `// Arrange`, `// Act`, `// Assert`. |
| Naming | `{SubjectUnderTest}_{Scenario}_{Expectation}`, and `async Task`. |
| Placement | One `{Class}Tests` class per class under test, **in the namespace of the class under test**. |
| Construction | Dependencies and the system under test come from `CreateXXX` helpers with optional parameters. |
| Cancellation | Pass a `CancellationToken` wherever a downstream method accepts one. |
| Domain content | Built through `TestBuilder`. Never inline. |

## Why

**One framework.** The SDK wires TUnit into any project whose name ends in a test suffix, and that wiring
is the reason a test project file is empty. A second framework in the same repository means every project
file grows a conditional, and the pipeline grows a second discovery path.

**Same namespace.** A test in the namespace of the class it tests reads as part of the thing it describes,
needs no `using` to reach the type, and cannot silently test a different type with the same name. It also
makes the pairing obvious in a file list.

**`CreateXXX` helpers.** A test body that constructs its own dependencies buries the assertion under
setup. A helper with optional parameters says what the test changed and defaults everything else:

```csharp
static ConventionCatalogue CreateCatalogue(IConventionRepository? repository = null) =>
	new(repository ?? CreateRepository().Object);
```

**`TestBuilder`.** Inline construction encodes the shape of a domain type into every test that touches it,
so one constructor change becomes a repository-wide edit. It also destroys the reader's signal about which
values the test actually depends on, because every literal looks deliberate. The builder is the single
means by which a test creates domain content — see [Test data](#test-data) below.

## Writing a test

```csharp
using Acme.Builders;

namespace Acme.Domain;

public sealed class CustomerIdTests
{
	[Test]
	public async Task Create_GivenLowercaseSlug_ReturnsTheValue()
	{
		// Arrange
		var id = TestBuilder.CustomerId("build-enforced");

		// Act
		var value = id.Value;

		// Assert
		await Assert.That(value).IsEqualTo("build-enforced");
	}
}
```

Two things are deliberate. The `// Arrange` block is often empty in a one-line test, and it stays: the
comments are what makes a three-line test readable next to a thirty-line one, and a convention that is
applied "when needed" is applied inconsistently.

And the assertions are awaited. `Assert.That(...)` is terminal and returns a value, so
`await Assert.That(result).IsEqualTo(expected)` is the form — an un-awaited assertion is a test that
cannot fail.

## Assertions

The assertion surface is small and reads as prose:

| Form | Use |
| -- | -- |
| `await Assert.That(x).IsEqualTo(y)` | Value equality |
| `await Assert.That(x).IsNotEqualTo(y)` | Inequality |
| `await Assert.That(x).IsTrue()` / `.IsFalse()` | Booleans |
| `await Assert.That(x).IsEmpty()` | Collections — the workhorse of architecture rules |
| `await Assert.That(x).Count().IsEqualTo(2)` | Collection size |
| `await Assert.That(x).Contains(y)` / `.DoesNotContain(y)` | Membership |
| `await Assert.That(act).Throws<TException>()` | A synchronous delegate that should throw |
| `await Assert.That(async () => await f()).Throws<TException>()` | An async delegate that should throw |
| `await Assert.That(x).IsTypeOf<T>()` | Exact runtime type |
| `await Assert.That(x).IsEquivalentTo(sequence)` | Sequence equality, order-independent |

For an exception test, `Action` and `Func<Task>` are the two shapes; the compiler will not infer either, so
the explicit type is required and is not a style violation:

```csharp
Action act = () => CustomerId.Create("");

await Assert.That(act).Throws<ArgumentException>();
```

## Parameterised tests

`[Arguments]` puts the interesting values in the attribute rather than in a loop, so a failure names the
case:

```csharp
[Test]
[Arguments("")]
[Arguments("   ")]
[Arguments("Has Spaces")]
[Arguments("has_underscore")]
public async Task Create_GivenSlugThatIsNotALowercaseSlug_Throws(string value)
{
	// Arrange
	// Act
	Action act = () => CustomerId.Create(value);

	// Assert
	await Assert.That(act).Throws<ArgumentException>();
}
```

## Substitutes

TUnit.Mocks is globally imported by the SDK, so a test file needs no `using`. The idiom is to set up the
call the code under test will make, then assert on the interaction:

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

- `Mock.Of<T>()` produces a `Mock<T>`; `.Object` is the instance to pass in.
- `Any()` matches any argument; `Is(value)` matches a specific one; `AnyArgs()` matches the whole call.
- `.Returns(value)` sets the result. For a `Task<T>`-returning method, return the `T` — the task is
  unwrapped for you.
- `.WasCalled(Times.Once)` and `.WasNeverCalled()` are the assertions.

The second `// Assert` after the `Throws` is not a mistake: the point of that test is that the exception
was thrown *and* that nothing was written, and both are assertions about the same act.

## Test data

`TestBuilder` lives in the shared testing project and is the single means by which a test creates domain
content:

```csharp
public static class TestBuilder
{
	public const int DefaultCount = 3;

	internal static Faker Faker { get; } = new();

	public static Domain.ConventionId ConventionId(string? value = null) =>
		Domain.ConventionId.Create(value ?? $"{Slug()}-{Faker.Random.String2(6, LowercaseAlphabet)}");

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
}
```

Four rules make it work:

- **Omitted arguments are generated; supplied arguments are used exactly.** So a test overrides only the
  value it is proving, and every other value is realistic without the test saying so.
- **A collection factory comes with a companion.** `Conventions(int count = DefaultCount)` generates a
  distinct set, so a test that needs "some" gets "some" without a loop of its own.
- **Bogus is a global `using`.** `Faker` is available with no import — which is also why an explicit
  `using Bogus;` in a shared testing project is reported as unnecessary.
- **A value-object test is the one exception.** A test proving what `Create` and `Hydrate` accept and
  reject has to name the rejected input itself, so it constructs the invalid value directly. Everything
  valid still goes through the builder.

## Two constraints that shaped the builder

Both were found by building it, and both are consequences of SDK rules rather than choices.

**The factories are flat, not grouped.** The natural shape is a nested class per feature area
(`TestBuilder.Catalogue.Convention(...)`). That cannot compile here: the SDK ships
`dotnet_diagnostic.CA1034.severity = error`, so a publicly visible nested type is a build failure, and
`PRSGD0008` refuses to let a repository add `CA1034` to `NoWarn`. Making the group `internal` does not
help either, because the SDK's `InternalsVisibleTo` target runs on non-test projects and grants access *to*
test and shared-testing projects rather than *from* them — a shared testing project's internals are not
visible to the tests beside it. An area that outgrows a flat builder gets its own top-level builder class
instead.

**`var` is the house style, with one carve-out.** The SDK's `.editorconfig` sets
`csharp_style_var_elsewhere = true:warning`, so an explicit type on a declaration initialised from a method
call is a warning (`IDE0007`). The carve-out is object creation: the SDK's `PDS0003` analyzer prefers an
explicit type with target-typed `new()` over `var`. Together those give:

```csharp
var id = TestBuilder.ConventionId("build-enforced");   // method call  -> var
string title = new('a', Convention.MaxTitleLength);    // new()        -> explicit type
```

## How it is enforced

| Rule | Enforced by |
| -- | -- |
| TUnit is the only framework | The build — the SDK adds it; a second framework has to be added by hand |
| Test projects are discovered and categorised | The build (name-suffix detection) |
| A shared testing project is referenced, not executed | The build (`IsSharedTestingProject` handling) |
| Style, including the `var` carve-out | The build (analyzers, at `warning` or above) |
| AAA structure, naming, `CreateXXX` helpers, `TestBuilder` usage | Review |
| A `CancellationToken` is passed through | Review |

## At your scale

- **One test project.** TUnit, AAA, the naming convention, and `TestBuilder`. That is the whole of it.
- **A shared fixture.** Move it into `SharedTestingInfra` and make the class public — a shared testing
  project is a library, so its fixtures are legitimately public API and `CA1515` does not apply.
- **A database or a container.** [`Purview.Containers`](https://purview.dev/docs/containers/) is the
  organisation's answer: throwaway Linux containers behind one API, over either Microsoft WSL Containers
  (no Docker installation on Windows) or Docker, with typed modules for SQL Server, PostgreSQL, Redis,
  MySQL, RabbitMQ, Azurite and NATS. Give the suite its own test project so the tier is expressed by the
  name, and make it skip itself when no backend is reachable.

  It is **experimental and prerelease** — the API, defaults and packaging rules can change between
  releases — so treat it as the direction of travel rather than a stability guarantee, and pin the
  version you adopt.
- **A package with a documented contract.** Add the consumer-verification script described in
  [Engineering Principles](Engineering-Principles.md).

## See also

- [Verification Model](Verification-Model.md)
- [Architecture as Tests](Architecture-As-Tests.md)
- [Worked Examples](Worked-Examples.md)
- [build-sdk: Testing
  Wiring](https://github.com/purview-dev/build-sdk/blob/main/docs/wiki/Testing-Wiring.md)
