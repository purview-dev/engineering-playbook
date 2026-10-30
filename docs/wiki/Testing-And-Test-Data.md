# Testing and Test Data

## The rule

| Concern | The rule |
| -- | -- |
| Framework | TUnit. Not xUnit, NUnit or MSTest. |
| Substitutes | TUnit.Mocks. Not NSubstitute or Moq. |
| Test data | Bogus, reached through `TestBuilder`. |
| Acceptance tests | BDTest over TUnit, through `BDTest.TUnit`. Not Gherkin, SpecFlow or Reqnroll. |
| A real dependency | `Purview.Containers`. Not Testcontainers directly, and never a shared instance. |
| Structure | Explicit `// Arrange`, `// Act`, `// Assert` — Given/When/Then in an acceptance test. |
| Naming | `{SubjectUnderTest}_{Scenario}_{Expectation}`, and `async Task`. |
| Placement | One `{Class}Tests` class per class under test, **in the namespace of the class under test**. |
| Construction | Dependencies and the system under test come from `CreateXXX` helpers with optional parameters. |
| Async | The async overload wherever the API offers one — in the test **and in every helper it calls**. A sync helper that calls such an API is converted, or carries a stated reason it cannot be. |
| Cancellation | If any method the test calls accepts a `CancellationToken`, the test's **last parameter** is one, and it is forwarded to every call that accepts one. |
| Domain content | Built through `TestBuilder`. Never inline. |

## Why

**One framework.** The SDK wires TUnit into any project whose name ends in a test suffix, and that wiring
is the reason a test project file is empty. A second framework in the same repository means every project
file grows a conditional, and the pipeline grows a second discovery path.

That is a rule about the **runner**, not about the style of a test. BDTest is allowed for acceptance tests
precisely because `BDTest.TUnit` is an adapter *over* TUnit rather than a runner of its own: the discovery
path, the filter and the emitted category are unchanged. A BDD framework that brought its own runner would
be the thing this rule exists to refuse.

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

## Async all the way down

**Where an API offers an async overload, a test uses it.** `File.ReadAllTextAsync`, not
`File.ReadAllText`. That much the build catches. The part that needs stating is the second half:

**The rule reaches through test helpers.** A helper that calls an API with an async overload is itself
converted to async — it is not exempt for being a helper, and "it is only a test" is not a reason.

The build enforces the first half and cannot enforce the second, and the gap between them is the whole
point:

- `VSTHRD103` is an `error` in the SDK's `.editorconfig`, and it flags a synchronous call with an
  `Async`-suffixed equivalent **when the call sits inside a `Task`-returning method**. In an `async Task`
  test, `File.ReadAllText` fails the build.
- A **synchronous helper is not a `Task`-returning method**, so the same call inside it is invisible to
  the analyzer. Moving the sync call one level down turns a build error into nothing at all, and the
  suite still looks clean.

That hole is not hypothetical — this repository had it. `PolicyDocumentationTests` read its page through
`static string Documentation => File.ReadAllText(DocumentationPath);`, and five `async Task` tests used it
with a clean build, because none of them contained the offending call.

It compounds with the [cancellation rule](#cancellation), which keys off whether any method in the chain
accepts a `CancellationToken`. A sync helper accepts none, so it **silently removes the trigger**: the
tests above correctly declared no token, and were correctly uncancellable, and nothing said so. The sync
helper is where a chain quietly stops being cancellable.

There is a plainer reason too. TUnit runs tests in parallel, and a blocking read holds a thread-pool
thread that an async read would have returned.

### Converting a helper

Three things change together, and the first two are not optional:

```csharp
// Before — invisible to VSTHRD103, and the chain accepts no token
static string Documentation => File.ReadAllText(DocumentationPath);

// After
static async Task<string> ReadDocumentationAsync(CancellationToken cancellationToken) =>
	await File.ReadAllTextAsync(DocumentationPath, cancellationToken);
```

- **It returns a `Task`**, which is what brings every call inside it back under `VSTHRD103`.
- **It gains the `Async` suffix.** `VSTHRD200` is an `error`, so the rename is enforced rather than
  stylistic.
- **It takes a `CancellationToken` last**, because the chain now accepts one — see
  [Cancellation](#cancellation). Its callers then declare and forward one.

**A property cannot be `async`, so it becomes a method.** That is the conversion, not an exemption from
it; a read-only property hiding IO is the most common form this violation takes.

### When it genuinely cannot be converted

State the reason where the call is, and keep it a fact about the API rather than a preference:

- **No async overload exists.** `File.Exists`, `File.Delete` and `Directory.EnumerateFiles` have none, so
  they stay as they are. In this repository `ArchitecturePolicy.Load()` and `PolicySchemaValidator.Validate()`
  are the same case: `Purview.Architecture.Testing` ships synchronous methods only, which is checkable
  rather than arguable.
- **A base type or interface fixes the signature.** Override what you are given.

What is never a conversion: `Task.Run` around a blocking call, `Task.FromResult` around a sync result, or
`.Result` and `.GetAwaiter().GetResult()` to flatten a task. The last two are `VSTHRD002`, an `error`
here, and the first two move the block rather than remove it.

## Cancellation

The rule has three parts, and the trigger is the call chain rather than the test:

**If any method the test calls accepts a `CancellationToken`, the test declares one.** Not only the method
under test — anything in the chain, including the `File.ReadAllTextAsync` a repository test reaches for and
the delegate handed to `Assert.That`. The moment one such call exists, the test gains the parameter.

**It is the last parameter.** TUnit supplies the value; nothing in the test constructs it:

```csharp
[Test]
public async Task RegisterAsync_GivenAnUnregisteredConvention_StoresIt(CancellationToken cancellationToken)
```

With `[Arguments]`, the data binds to the leading parameters and the token is supplied on top of them, so
the token goes last and the attribute's positional order still matches the signature:

```csharp
[Test]
[Arguments("build-enforced", true)]
[Arguments("review-only", false)]
public async Task ListEnforcedByBuildAsync_GivenAConvention_FiltersIt(
	string id,
	bool enforcedByBuild,
	CancellationToken cancellationToken
)
```

**It is forwarded to every call that accepts one.** This is the part that is actually worth the rule:

```csharp
// Act
var result = await catalogue.ListEnforcedByBuildAsync(cancellationToken);
```

Never `CancellationToken.None`, never `default`, and never a token the test creates for itself. Two
failures follow from doing otherwise:

- **A hung call hangs the run instead of failing one test.** TUnit's token is the *test's* token — it is
  cancelled when the test times out or the run is aborted — so a call given `None` cannot be stopped. One
  method that drops the token is enough: the chain is only as cancellable as its least cooperative hop,
  which is why *partial* forwarding is the trap rather than a lesser version of the rule.
- **Nothing proves the production code threads the token through.** A test that passes `None` exercises
  the signature without exercising the behaviour, and the test is the only place a swallowed token would
  have been noticed.

A test whose chain accepts no token takes no parameter — the parameterised value-object test above is the
shape, and adding an unused token there is noise, not compliance.

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

## Acceptance tests

An acceptance test states a behaviour somebody outside the team asked for, in their words. It is a
different artefact from a unit test, and it gets a different shape: **[BDTest](https://bdtest.tomlonghurst.dev/)
over TUnit, through the `BDTest.TUnit` adapter, in a `<Target>.AcceptanceTests` project.**

Two things make BDTest the choice rather than Gherkin, SpecFlow or Reqnroll:

- **It is code-only.** There is no `.feature` file and no generated test body, so there is no second
  artefact that can drift from the step that implements it. The drift between a feature file and its
  bindings is the failure that makes a BDD suite stop being trusted, and it is a failure this shape cannot
  have.
- **It keeps one runner.** `BDTest.TUnit` is an adapter over TUnit, so the SDK's wiring, the category and
  the `--treenode-filter` all still apply. Nothing in the pipeline learns a second way to find a test.

A scenario is a story, a context, a steps class and a chain:

```csharp
[Story(AsA = "account holder", IWant = "to withdraw money", SoThat = "I can use my savings")]
public class WithdrawalTests : TUnitBDTestBase<AccountContext>
{
	AccountSteps Account => new(Context);

	[Test]
	[ScenarioText("Withdrawing money reduces the balance")]
	public async Task WithdrawMoney() =>
		await Given(() => Account.CreateAnAccount(100m))
			.When(() => Account.Withdraw(25m))
			.Then(() => Account.TheBalanceIs(75m))
			.BDTestAsync();
}

public class AccountSteps(AccountContext context)
{
	[StepText("I have an account with a balance of {0}")]
	public void CreateAnAccount(decimal balance) => context.Balance = balance;

	[StepText("I withdraw {0}")]
	public void Withdraw(decimal amount) => context.Balance -= amount;

	[StepText("the account balance is {0}")]
	public async Task TheBalanceIs(decimal expected) => await Assert.That(context.Balance).IsEqualTo(expected);
}
```

Five rules govern it, and the first is the one that bites:

- **`.BDTestAsync()` is what runs the scenario.** A chain that stops at `.Then(...)` compiles, passes, and
  executes none of its steps — the same failure mode as an un-awaited `Assert.That`, and the reason both
  are called out rather than assumed.
- **Given/When/Then replaces Arrange/Act/Assert, and only here.** The chain already names the three phases,
  so the comments would be noise. Everywhere else the AAA comments stay.
- **`Context` is the only state.** Steps receive it through the constructor and never hold fields of their
  own; BDTest gives each test its own context, which is what keeps a parallel or repeated run honest.
- **`[StepText]` is written for the reader of the report, not the author of the test.** `{0}` is replaced
  with the argument's actual value at run time, so the report states what the scenario did rather than what
  it was called.
- **Domain content still comes from `TestBuilder`.** The step is where the vocabulary changes; the test-data
  rule does not.

Assertions inside a step are ordinary TUnit assertions, awaited as everywhere else. `BDTest.Reporting`
produces the single-file HTML report; publish it as a build artifact from the job that ran the suite, and
treat sharing it with the people who asked for the behaviour as the point of writing the suite this way.

**`src/` does not demonstrate this.** The reference implementation is a catalogue of conventions behind one
service — it has no user-visible behaviour to accept — so adding a BDTest suite would add a package and a
second job to prove nothing this page does not already state. The rule stands on
[the BDTest documentation](https://bdtest.tomlonghurst.dev/) and the example above.

## A test that needs a real dependency

A database, a queue, a blob store or an emulator comes from
[`Purview.Containers`](https://purview.dev/docs/containers/) — a throwaway Linux container per suite, behind
one API over either Microsoft WSL Containers or Docker, with typed modules for SQL Server, PostgreSQL,
Redis, MySQL, RabbitMQ, Azurite and NATS.

The two alternatives are what the rule exists to refuse:

- **A shared instance** — a developer's local SQL Server, a team database — makes tests order-dependent and
  a failure unreproducible, because the state a test found was left by whichever test ran before it, on
  whichever machine.
- **Testcontainers directly** ties the suite to a Docker installation. On Windows that is an install and a
  licensing question for every developer and every runner. The WSL Containers backend removes it, and the
  same test code runs on either backend, so the choice is made per machine rather than per repository.

```csharp
await using var postgres = new PostgreSqlBuilder()
	.WithDatabase("tests")
	.WithUsername("postgres")
	.WithPassword("postgres")
	.Build();

await postgres.StartAsync();

await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
await connection.OpenAsync();
```

`Build()` validates the configuration, `StartAsync()` returns only once every wait strategy is satisfied —
so there is no sleep to tune — and `await using` disposes the container at the end of the scope.

Three rules make such a suite safe to own:

- **It lives in its own `<Target>.IntegrationTests` project.** The tier comes from the name, which keeps it
  out of the pull-request filter without anybody remembering an attribute. See
  [Verification Model](Verification-Model.md).
- **It skips locally when no backend is usable, and fails in CI.** Those are two different situations and
  must not share an outcome. Locally, probe and skip; in CI, pin the backend with
  `PURVIEW_CONTAINERS_BACKEND`, because a named backend never falls back, so an unavailable runtime fails
  loudly rather than letting a green job mean nothing ran:

  ```csharp
  [Before(Class)]
  public static async Task EnsureABackendIsAvailable()
  {
  	var backends = await ContainerBackends.ProbeAllAsync();

  	Skip.Unless(backends.Any(backend => backend.IsUsable), "No container backend is available.");
  }
  ```

  Left unpinned, the automatic selection probes in priority order and prefers WSL Containers over Docker.
- **The version is pinned exactly.** `Purview.Containers` is **experimental and prerelease** — its API,
  defaults and packaging rules can change between releases — so treat it as the direction of travel rather
  than a stability guarantee. The `Purview.Containers` packages are a family, so their version is a single
  property in `Directory.Packages.props` and a bump cannot be applied to half of them.

**`src/` does not demonstrate this either**, and for the stated reason: the reference implementation ships
no persistence and its pipeline runs the `[Category=Unit]` tier only. `samples/Acme.Persistence` shows the
EF Core side — a real provider when given a connection string, in-memory only for the runnable default —
but it is a console self-check rather than a test project, and `just samples` is its check.
[containers](https://github.com/purview-dev/containers) is the repository to read for the second-job
pattern in use, and [EF Core Integration](EF-Core-Integration.md) states the rules a container-backed suite
should prove.

## How it is enforced

| Rule | Enforced by |
| -- | -- |
| TUnit is the only framework | The build — the SDK adds it; a second framework has to be added by hand |
| Test projects are discovered and categorised | The build (name-suffix detection) |
| A shared testing project is referenced, not executed | The build (`IsSharedTestingProject` handling) |
| Style, including the `var` carve-out | The build (analyzers, at `warning` or above) |
| An acceptance suite is categorised `Acceptance` and kept out of the fast tier | The build (name-suffix detection) and the pipeline's `TestFilter` |
| A container-backed suite fails rather than passes when its backend is missing in CI | The pipeline, when the backend is pinned with `PURVIEW_CONTAINERS_BACKEND` |
| AAA structure, naming, `CreateXXX` helpers, `TestBuilder` usage | Review |
| Given/When/Then chains end in `.BDTestAsync()` | Review |
| A real dependency comes from `Purview.Containers` rather than a shared instance | Review |
| An async overload is used inside an `async Task` test | The build — `VSTHRD103` is `error` in the SDK's `.editorconfig` |
| A converted helper carries the `Async` suffix | The build — `VSTHRD200` is `error` |
| A **sync helper** that calls an API with an async overload is converted | Review — a non-`Task`-returning method is outside `VSTHRD103`'s scope, so nothing else can see it |
| A declared `CancellationToken` is the last parameter | The build — `CA1068` is `warning` in the SDK's `.editorconfig`, and a clean build has no warnings |
| A test whose chain accepts a token declares one, and forwards it to every call that accepts one | Review |

## At your scale

- **One test project.** TUnit, AAA, the naming convention, and `TestBuilder`. That is the whole of it.
- **A shared fixture.** Move it into `SharedTestingInfra` and make the class public — a shared testing
  project is a library, so its fixtures are legitimately public API and `CA1515` does not apply.
- **A database or a container.** Add one `<Target>.IntegrationTests` project and reach for
  `Purview.Containers` as described in [A test that needs a real
  dependency](#a-test-that-needs-a-real-dependency). One module and one container is the whole of it at
  this size; do not build a fixture framework around it until a second suite needs the same container.
- **A behaviour somebody outside the team asked for.** That is when BDTest earns its place, and not
  before — a `<Target>.AcceptanceTests` project with one story in it is a reasonable start, and a
  repository with no such audience should keep writing unit tests. Reach for the HTML report only once
  somebody is actually going to read it.
- **A package with a documented contract.** Add the consumer-verification script described in
  [Engineering Principles](Engineering-Principles.md).

## See also

- [Verification Model](Verification-Model.md)
- [Architecture as Tests](Architecture-As-Tests.md)
- [EF Core Integration](EF-Core-Integration.md)
- [Worked Examples](Worked-Examples.md)
- [BDTest](https://bdtest.tomlonghurst.dev/)
- [Purview.Containers](https://purview.dev/docs/containers/)
- [build-sdk: Testing
  Wiring](https://github.com/purview-dev/build-sdk/blob/main/docs/wiki/Testing-Wiring.md)
