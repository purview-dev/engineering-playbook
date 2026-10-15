using System.Text.RegularExpressions;
using Purview.EngineeringPlaybook.Architecture.Policy;

namespace Purview.EngineeringPlaybook.Architecture;

/// <summary>
/// Asserts that the wiki agrees with the architecture policy.
/// </summary>
/// <remarks>
/// The policy is stated twice by necessity - once as JSON the tests evaluate, once as a table a human
/// reads. Two statements of one rule drift, and the prose is the copy that drifts silently, because
/// nothing fails when it is wrong. These tests make the documentation falsifiable.
/// See <c>docs/wiki/Project-Responsibilities.md</c>.
/// </remarks>
public sealed partial class PolicyDocumentationTests
{
	static readonly ArchitecturePolicy Policy = ArchitecturePolicy.Load();

	static string DocumentationPath =>
		Path.Combine(ArchitecturePolicy.RepositoryRoot(), "docs", "wiki", "Project-Responsibilities.md");

	static string Documentation => File.ReadAllText(DocumentationPath);

	[GeneratedRegex(
		@"^\| `(?<name>[A-Za-z]+)` \| (?<category>[a-z]+) \| (?<aliases>[^|]+) \| (?<depends>[^|]+) \|$",
		RegexOptions.Multiline
	)]
	private static partial Regex CatalogueRowPattern();

	[Test]
	public async Task Documentation_GivenTheCatalogueTable_ListsEveryResponsibility()
	{
		// Arrange
		var documented = CatalogueRowPattern()
			.Matches(Documentation)
			.Select(match => match.Groups["name"].Value)
			.ToArray();

		// Act
		IReadOnlyList<string> missing =
		[
			.. Policy
				.Responsibilities.Select(responsibility => responsibility.Name)
				.Where(name => !documented.Contains(name, StringComparer.Ordinal)),
		];

		// Assert
		await Assert.That(missing).IsEmpty().Because($"the catalogue table omits: {string.Join(", ", missing)}");
	}

	[Test]
	public async Task Documentation_GivenTheCatalogueTable_DoesNotInventResponsibilities()
	{
		// Arrange
		var known = Policy.Responsibilities.Select(responsibility => responsibility.Name).ToArray();

		// Act
		IReadOnlyList<string> invented =
		[
			.. CatalogueRowPattern()
				.Matches(Documentation)
				.Select(match => match.Groups["name"].Value)
				.Where(name => !known.Contains(name, StringComparer.Ordinal))
				.Distinct(StringComparer.Ordinal),
		];

		// Assert
		await Assert
			.That(invented)
			.IsEmpty()
			.Because(
				$"the catalogue table names responsibilities the policy does not define: {string.Join(", ", invented)}"
			);
	}

	[Test]
	public async Task Documentation_GivenTheCatalogueTable_MatchesThePolicyDependencies()
	{
		// Act
		List<string> mismatches = [];
		foreach (Match row in CatalogueRowPattern().Matches(Documentation))
		{
			var name = row.Groups["name"].Value;
			var responsibility = Policy.Responsibilities.FirstOrDefault(candidate =>
				string.Equals(candidate.Name, name, StringComparison.Ordinal)
			);

			if (responsibility is null)
				continue;

			var documented = row.Groups["depends"].Value.Trim();
			var expected =
				responsibility.MayDependOn.Count == 0 ? "*(nothing)*" : string.Join(", ", responsibility.MayDependOn);

			if (!string.Equals(documented, expected, StringComparison.Ordinal))
			{
				mismatches.Add(
					$"{name}: the page says '{documented}' but the policy says '{expected}'. "
						+ "Update docs/wiki/Project-Responsibilities.md, or change the policy if the rule moved."
				);
			}

			var documentedCategory = row.Groups["category"].Value.Trim();
			if (!string.Equals(documentedCategory, responsibility.Category, StringComparison.Ordinal))
				mismatches.Add(
					$"{name}: category '{documentedCategory}' but the policy says '{responsibility.Category}'."
				);

			var documentedAliases = row.Groups["aliases"]
				.Value.Trim()
				.Replace("`", string.Empty, StringComparison.Ordinal);
			var expectedAliases = responsibility.Aliases.Count == 0 ? "—" : string.Join(", ", responsibility.Aliases);

			if (!string.Equals(documentedAliases, expectedAliases, StringComparison.Ordinal))
				mismatches.Add($"{name}: aliases '{documentedAliases}' but the policy says '{expectedAliases}'.");
		}

		// Assert
		await Assert
			.That(mismatches)
			.IsEmpty()
			.Because(Environment.NewLine + string.Join(Environment.NewLine, mismatches));
	}

	[Test]
	public async Task Documentation_GivenTheDiscouragedNames_ListsEveryOne()
	{
		// Arrange
		var text = Documentation;

		// Act
		IReadOnlyList<string> missing =
		[
			.. Policy.DiscouragedProjectNames.Names.Where(name =>
				!text.Contains($"`{name}`", StringComparison.Ordinal)
			),
		];

		// Assert
		await Assert
			.That(missing)
			.IsEmpty()
			.Because($"the page does not mention these discouraged names: {string.Join(", ", missing)}");
	}

	[Test]
	public async Task Documentation_GivenEveryWaiver_ExplainsIt()
	{
		// Arrange
		var text = Documentation;

		// Act
		// A waiver that nobody wrote down is indistinguishable from a rule that was never enforced.
		IReadOnlyList<string> undocumented =
		[
			.. Policy
				.Waivers.Where(waiver => !text.Contains(waiver.Project, StringComparison.Ordinal))
				.Select(waiver => waiver.Project),
		];

		// Assert
		await Assert
			.That(undocumented)
			.IsEmpty()
			.Because($"these waivers are not explained on the page: {string.Join(", ", undocumented)}");
	}

	[Test]
	public async Task Documentation_GivenItsFencedBlocks_AlwaysDeclaresALanguage(CancellationToken cancellationToken)
	{
		// Arrange
		// AGENTS.md: Mermaid is not enabled in mkdocs.yml, so Mermaid source lives in a text fence.
		// A bare fence renders as an untyped block and is the easiest convention to break by accident.
		var lines = await File.ReadAllLinesAsync(DocumentationPath, cancellationToken);

		// Act
		List<string> offenders = [];
		var inFence = false;
		for (var index = 0; index < lines.Length; index++)
		{
			if (!lines[index].StartsWith("```", StringComparison.Ordinal))
				continue;

			if (!inFence && lines[index].Trim() == "```")
				offenders.Add($"line {index + 1}: opening fence declares no language");

			inFence = !inFence;
		}

		// Assert
		await Assert.That(offenders).IsEmpty().Because(string.Join("; ", offenders));
	}
}
