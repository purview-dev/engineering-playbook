using System.CodeDom.Compiler;
using System.Reflection;
using System.Runtime.CompilerServices;
using Purview.EngineeringPlaybook.Application;
using Purview.EngineeringPlaybook.Domain;
using Purview.EngineeringPlaybook.Infrastructure;

namespace Purview.EngineeringPlaybook.Architecture;

/// <summary>
/// The dependency-direction rules for the reference implementation.
/// </summary>
/// <remarks>
/// These are the rules a diagram cannot keep true. Each one reads the compiled assemblies and their
/// metadata references, so a ProjectReference that points the wrong way fails the build rather than
/// being noticed in review. See <c>docs/wiki/Architecture-As-Tests.md</c>.
/// </remarks>
public sealed class DependencyDirectionTests
{
	const string RepositoryPrefix = "Purview.EngineeringPlaybook";

	/// <summary>
	/// Namespaces owned by a source generator the SDK enables, rather than by this repository.
	/// </summary>
	/// <remarks>
	/// <c>Purview.Telemetry.SourceGenerator</c> is on by default and emits public types into its own
	/// <c>Purview.Telemetry</c> namespace inside every assembly it runs in. Those types are the
	/// generator's, not the layer's, so the namespace-identity rules below have to exclude them or they
	/// report a false positive in every assembly the generator touches.
	/// </remarks>
	static readonly string[] GeneratorOwnedNamespaces = ["Purview.Telemetry"];

	[Test]
	public async Task Domain_GivenItsReferences_DoesNotReferenceAnyOtherLayer()
	{
		// Arrange
		var domain = typeof(Convention).Assembly;

		// Act
		var referenced = ReferencedAssemblyNames(domain);

		// Assert
		await Assert.That(referenced).DoesNotContain($"{RepositoryPrefix}.Application");
		await Assert.That(referenced).DoesNotContain($"{RepositoryPrefix}.Infrastructure");
	}

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

	[Test]
	public async Task Infrastructure_GivenItsReferences_DependsOnTheApplicationLayer()
	{
		// Arrange
		var infrastructure = typeof(InMemoryConventionRepository).Assembly;

		// Act
		var referenced = ReferencedAssemblyNames(infrastructure);

		// Assert
		await Assert.That(referenced).Contains($"{RepositoryPrefix}.Application");
	}

	[Test]
	public async Task ProductionAssemblies_GivenTheirReferences_DoNotReferenceATestAssembly()
	{
		// Arrange
		Assembly[] production =
		[
			typeof(Convention).Assembly,
			typeof(ConventionCatalogue).Assembly,
			typeof(InMemoryConventionRepository).Assembly,
		];

		// Act
		IReadOnlyList<string> offenders =
		[
			.. production
				.SelectMany(ReferencedAssemblyNames)
				.Where(name => name.EndsWith("Tests", StringComparison.Ordinal)),
		];

		// Assert
		await Assert.That(offenders).IsEmpty();
	}

	[Test]
	public async Task ProductionAssemblies_GivenTheirPublicTypes_LiveInTheRepositoryNamespace()
	{
		// Arrange
		var production = ProductionAssemblies();

		// Act
		IReadOnlyList<string> offenders =
		[
			.. production
				.SelectMany(AuthoredPublicTypes)
				.Where(type =>
					type.Namespace is null || !type.Namespace.StartsWith(RepositoryPrefix, StringComparison.Ordinal)
				)
				.Select(Describe),
		];

		// Assert
		await Assert.That(offenders).IsEmpty();
	}

	[Test]
	public async Task Domain_GivenItsPublicTypes_AreAllInTheDomainNamespace()
	{
		// Arrange
		var domain = typeof(Convention).Assembly;
		var expected = $"{RepositoryPrefix}.Domain";

		// Act
		IReadOnlyList<string> offenders =
		[
			.. AuthoredPublicTypes(domain)
				.Where(type => type.Namespace is null || !type.Namespace.StartsWith(expected, StringComparison.Ordinal))
				.Select(Describe),
		];

		// Assert
		await Assert.That(offenders).IsEmpty();
	}

	/// <summary>The assemblies that make up the reference implementation's production layers.</summary>
	/// <returns>The production assemblies.</returns>
	static Assembly[] ProductionAssemblies() =>
		[
			typeof(Convention).Assembly,
			typeof(ConventionCatalogue).Assembly,
			typeof(InMemoryConventionRepository).Assembly,
		];

	/// <summary>Reads the simple names of every assembly a given assembly references.</summary>
	/// <param name="assembly">The assembly to inspect.</param>
	/// <returns>The referenced assembly names.</returns>
	static IReadOnlyList<string> ReferencedAssemblyNames(Assembly assembly) =>
		[.. assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty)];

	/// <summary>Reads the public types this repository authored in an assembly.</summary>
	/// <param name="assembly">The assembly to inspect.</param>
	/// <returns>The authored public types.</returns>
	static IReadOnlyList<Type> AuthoredPublicTypes(Assembly assembly) =>
		[.. assembly.GetExportedTypes().Where(IsAuthored)];

	/// <summary>Reports whether a type was authored here rather than emitted by a source generator.</summary>
	/// <param name="type">The type to inspect.</param>
	/// <returns><see langword="true"/> when this repository authored the type.</returns>
	static bool IsAuthored(Type type) =>
		!type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
		&& !type.IsDefined(typeof(GeneratedCodeAttribute), inherit: false)
		&& !IsInGeneratorOwnedNamespace(type.Namespace);

	/// <summary>Reports whether a namespace belongs to a source generator rather than this repository.</summary>
	/// <param name="namespaceName">The namespace to test.</param>
	/// <returns><see langword="true"/> when the namespace is generator-owned.</returns>
	static bool IsInGeneratorOwnedNamespace(string? namespaceName) =>
		namespaceName is not null
		&& GeneratorOwnedNamespaces.Any(owned =>
			string.Equals(namespaceName, owned, StringComparison.Ordinal)
			|| namespaceName.StartsWith($"{owned}.", StringComparison.Ordinal)
		);

	/// <summary>Renders a type as a failure message that names its namespace.</summary>
	/// <param name="type">The offending type.</param>
	/// <returns>The description.</returns>
	static string Describe(Type type) => $"{type.FullName} (namespace: {type.Namespace ?? "<global>"})";
}
