using System.Text.RegularExpressions;
using Purview.Architecture.Testing;

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

	/// <summary>Reads the responsibilities page.</summary>
	/// <param name="cancellationToken">Cancels the read.</param>
	/// <returns>The page's text.</returns>
	/// <remarks>
	/// A method rather than a property, because a property cannot be <c>async</c> and a synchronous one
	/// would hide <c>File.ReadAllText</c> from <c>VSTHRD103</c> - the analyzer only sees a synchronous
	/// call when it sits inside a <c>Task</c>-returning method. See <c>docs/wiki/Testing-And-Test-Data.md</c>.
	/// </remarks>
	static async Task<string> ReadDocumentationAsync(CancellationToken cancellationToken) =>
		await File.ReadAllTextAsync(DocumentationPath, cancellationToken);

	[GeneratedRegex(
		@"^\| `(?<name>[A-Za-z]+)` \| (?<category>[a-z]+) \| (?<aliases>[^|]+) \| (?<depends>[^|]+) \|$",
		RegexOptions.Multiline
	)]
	private static partial Regex CatalogueRowPattern();

	[Test]
	public async Task Documentation_GivenTheCatalogueTable_ListsEveryResponsibility(CancellationToken cancellationToken)
	{
		// Arrange
		var documented = CatalogueRowPattern()
			.Matches(await ReadDocumentationAsync(cancellationToken))
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
	public async Task Documentation_GivenTheCatalogueTable_DoesNotInventResponsibilities(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var known = Policy.Responsibilities.Select(responsibility => responsibility.Name).ToArray();
		var text = await ReadDocumentationAsync(cancellationToken);

		// Act
		IReadOnlyList<string> invented =
		[
			.. CatalogueRowPattern()
				.Matches(text)
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
	public async Task Documentation_GivenTheCatalogueTable_MatchesThePolicyDependencies(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var text = await ReadDocumentationAsync(cancellationToken);

		// Act
		List<string> mismatches = [];
		foreach (Match row in CatalogueRowPattern().Matches(text))
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
	public async Task Documentation_GivenTheDiscouragedNames_ListsEveryOne(CancellationToken cancellationToken)
	{
		// Arrange
		var text = await ReadDocumentationAsync(cancellationToken);

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
	public async Task Documentation_GivenEveryWaiver_ExplainsIt(CancellationToken cancellationToken)
	{
		// Arrange
		var text = await ReadDocumentationAsync(cancellationToken);

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
}
