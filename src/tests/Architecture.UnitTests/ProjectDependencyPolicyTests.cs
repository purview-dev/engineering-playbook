using Purview.EngineeringPlaybook.Architecture.Policy;

namespace Purview.EngineeringPlaybook.Architecture;

/// <summary>
/// Evaluates the declared project references in this repository against the architecture policy.
/// </summary>
/// <remarks>
/// These tests read <c>.config/architecture-policy.json</c> rather than restating its rules, so the
/// policy has one definition. A failure names the project, the rule, the offending reference and the
/// direction the dependency should run - enough to act on without opening the policy.
/// See <c>docs/wiki/Project-Responsibilities.md</c>.
/// </remarks>
public sealed class ProjectDependencyPolicyTests
{
	static readonly ArchitecturePolicy Policy = ArchitecturePolicy.Load();
	static readonly IReadOnlyDictionary<string, ProjectNode> Projects = ProjectGraph.Load(
		ArchitecturePolicy.RepositoryRoot()
	);

	static IEnumerable<ProjectNode> ProductionProjects => Projects.Values.Where(project => !project.IsTestProject);

	[Test]
	public async Task Solution_GivenItsProjects_AreAllClassifiableOrDeliberatelyNamed()
	{
		// Act
		IReadOnlyList<string> offenders =
		[
			.. ProductionProjects
				.Where(project =>
					Policy.Classify(project.Name) is null && Policy.WaiverFor(project.Name)?.Excuses("naming") != true
				)
				.Select(project =>
					$"'{project.Name}' does not map to a known responsibility or alias. "
					+ "Rename it to '<Responsibility>[.<Implementation>]', or record a waiver in the policy. "
					+ $"Known responsibilities: {string.Join(", ", Policy.Responsibilities.Select(responsibility => responsibility.Name))}."
				),
		];

		// Assert
		await Assert.That(offenders).IsEmpty().Because(Report(offenders));
	}

	[Test]
	public async Task ProductionProjects_GivenTheirReferences_RespectTheDependencyMatrix()
	{
		// Act
		List<string> offenders = [];
		foreach (var project in ProductionProjects)
		{
			var responsibility = Policy.Classify(project.Name);
			if (responsibility is null)
				continue;

			foreach (var reference in project.ProjectReferences)
			{
				var referenced = Policy.Classify(reference);
				if (referenced is null)
					continue;

				// A qualified sibling of the same responsibility is the same box on the diagram, so it
				// is governed by sibling independence rather than by the matrix.
				if (string.Equals(referenced.Name, responsibility.Name, StringComparison.OrdinalIgnoreCase))
					continue;

				if (responsibility.MustNotDependOn.Contains(referenced.Name, StringComparer.OrdinalIgnoreCase))
				{
					offenders.Add(
						$"'{project.Name}' ({responsibility.Name}) references '{reference}' ({referenced.Name}), "
							+ $"but {responsibility.Name} must not depend on {referenced.Name}. "
							+ $"Invert it: {referenced.Name} may depend on {responsibility.Name}, or introduce an abstraction "
							+ $"that {responsibility.Name} owns and {referenced.Name} implements."
					);
				}
				else if (!responsibility.MayDependOn.Contains(referenced.Name, StringComparer.OrdinalIgnoreCase))
				{
					offenders.Add(
						$"'{project.Name}' ({responsibility.Name}) references '{reference}' ({referenced.Name}), "
							+ "which the policy neither permits nor forbids. "
							+ $"{responsibility.Name} may depend on: {string.Join(", ", responsibility.MayDependOn)}. "
							+ $"Add {referenced.Name} to mayDependOn in .config/architecture-policy.json if this is intended."
					);
				}
			}
		}

		// Assert
		await Assert.That(offenders).IsEmpty().Because(Report(offenders));
	}

	[Test]
	public async Task SiblingImplementations_GivenTheirReferences_DoNotDependOnEachOther()
	{
		// Act
		List<string> offenders = [];
		foreach (var project in ProductionProjects)
		{
			var responsibility = Policy.Classify(project.Name);
			if (responsibility?.SiblingIndependence != true || !project.Name.Contains('.', StringComparison.Ordinal))
				continue;

			foreach (var reference in project.ProjectReferences)
			{
				// Only a *qualified* sibling is a peer; the unqualified project is shared ground.
				var isQualifiedSibling =
					reference.Contains('.', StringComparison.Ordinal)
					&& !string.Equals(reference, project.Name, StringComparison.OrdinalIgnoreCase)
					&& string.Equals(
						Policy.Classify(reference)?.Name,
						responsibility.Name,
						StringComparison.OrdinalIgnoreCase
					);

				if (isQualifiedSibling)
				{
					offenders.Add(
						$"'{project.Name}' references its sibling '{reference}'. Sibling {responsibility.Name} implementations "
							+ $"must stay independent; move the shared part into the unqualified '{responsibility.Name}' project "
							+ "or into an abstraction both can depend on."
					);
				}
			}
		}

		// Assert
		await Assert.That(offenders).IsEmpty().Because(Report(offenders));
	}

	[Test]
	public async Task Implementations_GivenTheirReferences_DoNotDependOnComposition()
	{
		// Act
		IReadOnlyList<string> offenders =
		[
			.. ProductionProjects
				.Where(project =>
					!string.Equals(
						Policy.Classify(project.Name)?.Name,
						"Composition",
						StringComparison.OrdinalIgnoreCase
					)
					&& !string.Equals(
						Policy.Classify(project.Name)?.Category,
						"host",
						StringComparison.OrdinalIgnoreCase
					)
					&& project.ProjectReferences.Any(reference =>
						string.Equals(
							Policy.Classify(reference)?.Name,
							"Composition",
							StringComparison.OrdinalIgnoreCase
						)
					)
				)
				.Select(project =>
					$"'{project.Name}' references Composition. Composition selects implementations, so it depends on them - "
					+ "never the other way round, which would make the choice circular."
				),
		];

		// Assert
		await Assert.That(offenders).IsEmpty().Because(Report(offenders));
	}

	[Test]
	public async Task ProductionProjects_GivenTheirPackages_AvoidForbiddenFrameworkDependencies()
	{
		// Act
		List<string> offenders = [];
		foreach (var project in ProductionProjects)
		{
			var responsibility = Policy.Classify(project.Name);
			if (responsibility is null)
				continue;

			foreach (var package in project.PackageReferences)
			{
				var forbidden = responsibility.ForbiddenPackagePrefixes.FirstOrDefault(prefix =>
					package.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
				);

				if (forbidden is not null)
				{
					offenders.Add(
						$"'{project.Name}' ({responsibility.Name}) references package '{package}', which matches the forbidden "
							+ $"prefix '{forbidden}' for {responsibility.Name}. {responsibility.Purpose} "
							+ "Move the dependency to the responsibility that owns that technology."
					);
				}
			}
		}

		// Assert
		await Assert.That(offenders).IsEmpty().Because(Report(offenders));
	}

	[Test]
	public async Task ProjectGraph_GivenItsReferences_HasNoCycles()
	{
		// Act
		var cycles = ProjectGraph.FindCycles(Projects);

		// Assert
		await Assert.That(cycles).IsEmpty().Because(Report(cycles));
	}

	[Test]
	public async Task ProductionProjects_GivenTheirNames_AvoidCatchAllNames()
	{
		// Arrange
		var discouraged = Policy.DiscouragedProjectNames.Names;

		// Act
		IReadOnlyList<string> offenders =
		[
			.. ProductionProjects
				.Where(project =>
					discouraged.Contains(project.Name.Split('.')[0], StringComparer.OrdinalIgnoreCase)
					&& Policy.WaiverFor(project.Name)?.Excuses("discouragedName") != true
				)
				.Select(project => $"'{project.Name}': {Policy.DiscouragedProjectNames.Reason}"),
		];

		// Assert
		await Assert.That(offenders).IsEmpty().Because(Report(offenders));
	}

	[Test]
	public async Task ProductionProjects_GivenTheirReferences_DoNotReferenceATestProject()
	{
		// Act
		IReadOnlyList<string> offenders =
		[
			.. ProductionProjects.SelectMany(project =>
				project
					.ProjectReferences.Where(reference =>
						Projects.TryGetValue(reference, out var target) && target.IsTestProject
					)
					.Select(reference => $"'{project.Name}' references the test project '{reference}'.")
			),
		];

		// Assert
		await Assert.That(offenders).IsEmpty().Because(Report(offenders));
	}

	[Test]
	public async Task PolicyWaivers_GivenTheirDefinitions_AreJustifiedAndStillApplyToAProject()
	{
		// Act
		List<string> offenders = [];
		foreach (var waiver in Policy.Waivers)
		{
			// A waiver for a project that no longer exists is dead weight that hides the next one.
			if (!Projects.ContainsKey(waiver.Project))
				offenders.Add($"The waiver for '{waiver.Project}' names a project that does not exist. Remove it.");

			if (waiver.Rules.Count == 0)
				offenders.Add($"The waiver for '{waiver.Project}' excuses no named rule, so it does nothing.");

			// A blanket suppression would make the policy unfalsifiable; a reason keeps it reviewable.
			if (waiver.Reason.Length < 40)
				offenders.Add($"The waiver for '{waiver.Project}' has no substantive reason recorded.");

			if (
				waiver.MapsTo.Length > 0
				&& !Policy.Responsibilities.Any(responsibility =>
					string.Equals(responsibility.Name, waiver.MapsTo, StringComparison.OrdinalIgnoreCase)
				)
			)
			{
				offenders.Add($"The waiver for '{waiver.Project}' maps to unknown responsibility '{waiver.MapsTo}'.");
			}
		}

		// Assert
		await Assert.That(offenders).IsEmpty().Because(Report(offenders));
	}

	[Test]
	public async Task WaivedProjects_GivenTheirReferences_AreStillGovernedByTheDependencyMatrix()
	{
		// Arrange
		// A waiver excuses the *name*, never the direction. Infrastructure maps to Adapters, so it is
		// bound by the Adapters rules like any other project.
		var waived = Policy.Waivers.Where(waiver => waiver.MapsTo.Length > 0).Select(waiver => waiver.Project);

		// Act
		IReadOnlyList<string> offenders =
		[
			.. waived
				.Where(name => Policy.Classify(name) is null)
				.Select(name =>
					$"'{name}' has a waiver but still does not classify, so no dependency rule applies to it."
				),
		];

		// Assert
		await Assert.That(offenders).IsEmpty().Because(Report(offenders));
	}

	/// <summary>Renders policy violations as a numbered list for the assertion message.</summary>
	/// <param name="violations">The violations found.</param>
	/// <returns>The rendered report.</returns>
	static string Report(IReadOnlyList<string> violations) =>
		violations.Count == 0
			? "no violations"
			: Environment.NewLine
				+ string.Join(
					Environment.NewLine,
					violations.Select((violation, index) => $"  {index + 1}. {violation}")
				);
}
