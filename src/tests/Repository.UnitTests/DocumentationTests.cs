using System.Text.RegularExpressions;

namespace Purview.EngineeringPlaybook.Repository;

/// <summary>
/// Asserts that the wiki obeys the conventions in <c>docs/wiki/Documentation-Convention.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// Two documentation rules are machine-checkable and both break silently: a page that is not in the
/// navigation is a page nobody finds, and a bare code fence renders as an untyped block. The
/// convention page states both; these tests make the statement true rather than aspirational.
/// </para>
/// <para>
/// <c>index.md</c> is the landing page rather than a content page, so the navigation checks exclude
/// it; the fence rule applies to every page, including the landing page.
/// </para>
/// </remarks>
public sealed partial class DocumentationTests
{
	[GeneratedRegex(@"\]\((?<target>[^)]+)\)")]
	private static partial Regex LinkPattern();

	static readonly string RepositoryRoot = FindRepositoryRoot();

	static string WikiDirectory => Path.Combine(RepositoryRoot, "docs", "wiki");

	[Test]
	public async Task Pages_GivenTheSidebar_AreAllReachable(CancellationToken cancellationToken)
	{
		// Arrange
		var sidebar = await File.ReadAllTextAsync(Path.Combine(WikiDirectory, "_Sidebar.md"), cancellationToken);

		// Act
		var missing = ContentPages()
			.Where(page => !sidebar.Contains($"({page}.md)", StringComparison.Ordinal))
			.ToArray();

		// Assert
		await Assert
			.That(missing)
			.IsEmpty()
			.Because($"these pages are not in _Sidebar.md: {string.Join(", ", missing)}");
	}

	[Test]
	public async Task Pages_GivenTheHomeTable_AreAllDiscoverable(CancellationToken cancellationToken)
	{
		// Arrange
		var home = await File.ReadAllTextAsync(Path.Combine(WikiDirectory, "Home.md"), cancellationToken);

		// Act
		// Home is the hub itself, so it is the one page the hub's table does not list.
		var missing = ContentPages()
			.Where(page => page != "Home" && !home.Contains($"({page}.md)", StringComparison.Ordinal))
			.ToArray();

		// Assert
		await Assert
			.That(missing)
			.IsEmpty()
			.Because($"these pages are not linked from the Home.md table: {string.Join(", ", missing)}");
	}

	[Test]
	public async Task Navigation_GivenItsLinks_AllResolve(CancellationToken cancellationToken)
	{
		// Arrange
		List<string> offenders = [];

		// Act
		foreach (var file in new[] { "_Sidebar.md", "Home.md" })
		{
			var lines = await File.ReadAllLinesAsync(Path.Combine(WikiDirectory, file), cancellationToken);
			for (var index = 0; index < lines.Length; index++)
			{
				foreach (var target in RelativeLinkTargets(lines[index]))
				{
					if (!File.Exists(Path.Combine(WikiDirectory, target)))
						offenders.Add($"{file}:{index + 1}: '{target}' does not exist");
				}
			}
		}

		// Assert
		await Assert.That(offenders).IsEmpty().Because(string.Join("; ", offenders));
	}

	[Test]
	public async Task Pages_GivenTheirFencedBlocks_AlwaysDeclareALanguage(CancellationToken cancellationToken)
	{
		// Arrange
		// AGENTS.md: Mermaid is not enabled in mkdocs.yml, so Mermaid source lives in a text fence. A
		// bare fence renders as an untyped block and is the easiest convention to break by accident.
		List<string> offenders = [];

		// Act
		foreach (var path in Directory.EnumerateFiles(WikiDirectory, "*.md"))
		{
			var lines = await File.ReadAllLinesAsync(path, cancellationToken);
			var inFence = false;
			for (var index = 0; index < lines.Length; index++)
			{
				if (!lines[index].StartsWith("```", StringComparison.Ordinal))
					continue;

				if (!inFence && lines[index].Trim() == "```")
					offenders.Add($"{Path.GetFileName(path)}:{index + 1}: opening fence declares no language");

				inFence = !inFence;
			}
		}

		// Assert
		await Assert.That(offenders).IsEmpty().Because(string.Join("; ", offenders));
	}

	/// <summary>The content pages: every wiki file except the navigation file and the landing page.</summary>
	/// <returns>The page names, without extension.</returns>
	static IReadOnlyList<string> ContentPages() =>
		[
			.. Directory
				.EnumerateFiles(WikiDirectory, "*.md")
				.Select(path => Path.GetFileNameWithoutExtension(path) ?? string.Empty)
				.Where(name => name is not "_Sidebar" and not "index")
				.Order(StringComparer.Ordinal),
		];

	/// <summary>Reads the relative link targets on a line, ignoring anchors and absolute URLs.</summary>
	/// <param name="line">The markdown line.</param>
	/// <returns>The relative file targets, with any fragment removed.</returns>
	static List<string> RelativeLinkTargets(string line)
	{
		List<string> targets = [];
		foreach (Match match in LinkPattern().Matches(line))
		{
			var target = match.Groups["target"].Value.Trim();
			if (
				target.Length == 0
				|| target.StartsWith("http", StringComparison.OrdinalIgnoreCase)
				|| target.StartsWith('#')
				|| target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
			)
			{
				continue;
			}

			var file = target.Split('#')[0];
			if (file.Length > 0)
				targets.Add(file);
		}

		return targets;
	}

	/// <summary>Walks up from the test binary to the repository root.</summary>
	/// <returns>The repository root path.</returns>
	static string FindRepositoryRoot()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory is not null)
		{
			if (
				File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
				&& Directory.Exists(Path.Combine(directory.FullName, "docs", "wiki"))
			)
			{
				return directory.FullName;
			}

			directory = directory.Parent;
		}

		throw new InvalidOperationException(
			$"Could not locate the repository root above '{AppContext.BaseDirectory}'. "
				+ "Expected an AGENTS.md and a docs/wiki folder."
		);
	}
}
