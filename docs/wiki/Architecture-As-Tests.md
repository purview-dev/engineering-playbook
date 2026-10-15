# Architecture as Tests

## The rule

The dependency directions an architecture depends on are asserted by tests that read the **compiled
assemblies**. A `ProjectReference` that points the wrong way fails the build.

A layer's allowed dependencies are data, not prose:

| Assembly | May reference | Must not reference |
| -- | -- | -- |
| `Acme.Domain` | nothing of ours | `Acme.Application`, `Acme.Infrastructure` |
| `Acme.Application` | `Acme.Domain` | `Acme.Infrastructure` |
| `Acme.Infrastructure` | `Acme.Application` (and `Acme.Domain` transitively) | — |

## Why

A diagram describes an intention. Dependencies erode one convenient reference at a time, and each one
looks reasonable in isolation: the domain needs one enum from the persistence layer, the application
needs one helper from the web layer, and within two releases the layering is decorative. Review catches
this sometimes. A test catches it every time, including at 5pm on a Friday.

There is a second reason that matters more in a repository worked on by agents: an architecture test is
the difference between a rule an agent can *follow* and a rule an agent has to *remember*. A generated
class in the wrong project is not a mistake an agent makes on purpose — it is a mistake nobody told it
was a mistake. The failing test is the telling.

## What is worth asserting

Four rules cover most of the value, and each one corresponds to a real failure:

1. **Direction.** The innermost layer references nothing outward. This is the rule everything else
   supports.
2. **No production assembly references a test assembly.** A test assembly in a package's dependency graph
   is a packaging bug that only shows up for a consumer.
3. **Namespace identity.** A layer's public types live in that layer's namespace. This catches a file
   moved to the wrong project, and it catches the SDK's suffix-stripping behaviour surprising someone (see
   [Repository Shape](Repository-Shape.md)).
4. **The port is owned by the inner layer.** The interface the outer layer implements lives in the inner
   layer, not beside its implementation. This is what makes the inversion real rather than nominal.

## The mechanism

The tests are ordinary unit tests in a project named `Architecture.UnitTests`, and they use nothing
special: `Assembly.GetReferencedAssemblies()` and `Assembly.GetExportedTypes()`.

`Assembly.GetReferencedAssemblies()` reads the **metadata** references of a compiled assembly. That is
exactly the dependency graph the runtime will need, which means the test cannot be fooled by a
`ProjectReference` that is declared but unused, or by a `using` that never became a real reference. If the
reference is in the metadata, the test sees it.

The test project has to reference every layer it inspects, so it declares those references explicitly.
It has no same-named production project, so the SDK adds no automatic reference — which is the correct
behaviour and worth knowing about, because it is why the project file below is not empty.

```xml
<Project Sdk="Microsoft.NET.Sdk">
	<ItemGroup>
		<ProjectReference Include="../../src/Application/Application.csproj" />
		<ProjectReference Include="../../src/Domain/Domain.csproj" />
		<ProjectReference Include="../../src/Infrastructure/Infrastructure.csproj" />
	</ItemGroup>
</Project>
```

## The worked example

`src/tests/Architecture.UnitTests/DependencyDirectionTests.cs` in this repository is a running version of
all four rules. It is small enough to read in full, and it is built by the same pipeline as everything
else — so if the layering breaks, the build fails here rather than in review.

The shape of every rule is the same: gather the offending facts, then assert the collection is empty and
let the failure message name what is wrong.

```csharp
[Test]
public async Task Application_GivenItsReferences_DependsOnTheDomainAndNotOnInfrastructure()
{
	// Arrange
	var application = typeof(ConventionCatalogue).Assembly;

	// Act
	var referenced = ReferencedAssemblyNames(application);

	// Assert
	await Assert.That(referenced).Contains($"{RepositoryPrefix}.Domain");
	await Assert.That(referenced).DoesNotContain($"{RepositoryPrefix}.Infrastructure");
}
```

The `Contains` is as important as the `DoesNotContain`. A rule that only forbids references passes
vacuously the moment somebody deletes the one it expected — and "the domain is no longer used by the
application" is a much larger architectural change than "the application references infrastructure".

The reading helpers are deliberately trivial:

```csharp
static IReadOnlyList<string> ReferencedAssemblyNames(Assembly assembly) =>
	[.. assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty)];
```

Two details in the assertions are worth copying:

- **Assert on names, not on `AssemblyName` objects.** `AssemblyName.Name` is the simple name
  (`Acme.Domain`), which is stable and reads well in a failure message.
- **Name the offender.** Where a rule iterates types, project each offender to a string that includes the
  namespace, so the failure says *which* type is wrong rather than only that a rule broke. A failing
  architecture test that does not name the offending type costs the reader a debugging session.

## Two traps this repository hit

Both are real, both were found by writing the rules, and both are worth knowing before you write yours.

### The telemetry generator emits public types into its own namespace

The SDK enables `Purview.Telemetry.SourceGenerator` on every assembly by default, and it emits **public**
types — `Targets`, `NamingConvention`, `LogPrefixType`, `LoggerGenerationMode`,
`MeterNameGenerationType` — into a `Purview.Telemetry` namespace. A naive "every public type lives in my
namespace" rule therefore fails in every assembly the generator touches.

The rule has to be scoped to types the repository actually authored:

```csharp
static bool IsAuthored(Type type) =>
	!type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
	&& !type.IsDefined(typeof(GeneratedCodeAttribute), inherit: false)
	&& !IsInGeneratorOwnedNamespace(type.Namespace);
```

This is the general lesson, not a telemetry-specific one: **an architecture rule about your code has to
distinguish your code from generated code.** Any source generator can add types to any assembly, and the
rules you write are about the ones you wrote.

### A shared testing project cannot hand its internals to a test project

The instinct is to make a test helper's nested factory classes `internal` so they do not widen the public
surface. That does not work from a shared testing project: the SDK's `InternalsVisibleTo` target runs on
projects where `IsTestProject` is not `true`, and it grants access *to* test and shared-testing projects
rather than *from* them. A shared testing project's internals are not visible to the tests beside it.

The corollary is that an `internal` helper is not an option, and a `public` nested type is a build error —
the SDK ships `dotnet_diagnostic.CA1034.severity = error`, and `PRSGD0008` refuses to let a repository add
`CA1034` to `NoWarn`. Factories therefore sit flat on the builder class rather than in nested groups. See
[Testing and Test Data](Testing-And-Test-Data.md).

## What these tests do not cover

Being explicit about this matters more than the rules themselves:

- **Runtime coupling.** Two layers can be reference-clean and still coupled through a shared static, an
  ambient service locator, or a type from a third-party package both depend on.
- **Package boundaries.** `ProjectReference` is not `PackageReference`. A monorepo can be layered perfectly
  and still ship a package graph that is not.
- **Behaviour.** Nothing here says the domain is *correct*, only that it is *reachable from* the right
  places.
- **The rule you forgot to write.** Every assertion is a rule somebody chose. A dependency direction that
  no test names is a direction nothing enforces.

## How it is enforced

The rules are ordinary tests in a project named `*UnitTests`, so the pipeline runs them on every pull
request as part of the fast tier. No extra workflow, no extra step, no separate tool: an architecture
violation is a failing test with the same status as any other.

These four rules are hand-written here because they read **compiled assemblies**, which is what lets
them catch a dependency that arrived indirectly rather than one that was declared. The broader
**policy** rules — the dependency matrix, naming, sibling independence, the composition boundary,
forbidden packages, cycles, test references and waivers — ship in `Purview.Architecture.Testing`, and
are adopted with a policy file and one derived test class rather than with C#. See
[Project Responsibilities](Project-Responsibilities.md).

## At your scale

- **One project.** Nothing to assert. Skip it.
- **Two or three layers.** Adopt rules 1–3. They are a page of code and they pay for themselves the first
  time someone adds a reference "just to reuse a helper".
- **A repository with packages.** Add rule 2, and consider asserting that a package's dependency graph
  contains no test assembly — the failure mode is invisible until a consumer sees it.
- **Many teams.** Promote the rules into the build itself so they cannot be skipped: an analyzer or an
  MSBuild check that runs before compile, in the same way the SDK's own `ValidatePurviewStylePolicy` does.

## See also

- [Repository Shape](Repository-Shape.md)
- [Verification Model](Verification-Model.md)
- [Testing and Test Data](Testing-And-Test-Data.md)
- [Worked Examples](Worked-Examples.md)
