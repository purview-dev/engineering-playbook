using System.Text.RegularExpressions;

namespace Purview.EngineeringPlaybook.Repository;

/// <summary>
/// Asserts that the wiki obeys the conventions in <c>docs/wiki/foundations/Documentation-Convention.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// Three documentation rules are machine-checkable and all break silently: a page that is not in the
/// navigation is a page nobody finds, a link that points at the wrong folder is a 404 nobody sees in
/// review, and a bare code fence renders as an untyped block. The convention page states all three;
/// these tests make the statement true rather than aspirational.
/// </para>
/// <para>
/// The page walk is recursive, so a page in a topic folder is held to the same rules as one at the wiki
/// root. <c>index.md</c> is the landing page rather than a content page, so the navigation checks
/// exclude it; the link and fence rules apply to every page, including the landing page.
/// </para>
/// </remarks>
public sealed partial class DocumentationTests
{
	[GeneratedRegex(@"\]\((?<target>[^)]+)\)")]
	private static partial Regex LinkPattern();

	[GeneratedRegex(@"^\s*-\s+\*\*(?<label>.+)\*\*\s*$")]
	private static partial Regex SectionLabelPattern();

	[Test]
	public async Task Pages_GivenTheSidebar_AreAllReachable(CancellationToken cancellationToken)
	{
		// Arrange
		var sidebar = await File.ReadAllTextAsync(
			Path.Combine(RepositoryLayout.WikiDirectory, "_Sidebar.md"),
			cancellationToken
		);

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
	public async Task Sections_GivenTheSidebar_MatchTheHomeHeadings(CancellationToken cancellationToken)
	{
		// Arrange
		var sidebar = await File.ReadAllLinesAsync(
			Path.Combine(RepositoryLayout.WikiDirectory, "_Sidebar.md"),
			cancellationToken
		);
		var home = await File.ReadAllTextAsync(
			Path.Combine(RepositoryLayout.WikiDirectory, "Home.md"),
			cancellationToken
		);

		// Act
		// The sidebar groups pages under `- **Label**`; Home groups tables under `## Label`. A reader
		// who sees one order in the navigation and another on the hub is being told two different maps.
		var labels = sidebar
			.Select(line => SectionLabelPattern().Match(line))
			.Where(match => match.Success)
			.Select(match => match.Groups["label"].Value)
			.ToArray();
		var headings = home.Split('\n')
			.Where(line => line.StartsWith("## ", StringComparison.Ordinal))
			.Select(line => line[3..].Trim())
			.ToArray();
		var missing = labels.Where(label => !headings.Contains(label, StringComparer.Ordinal)).ToArray();
		var positions = labels.Select(label => Array.IndexOf(headings, label)).ToArray();
		var outOfOrder = positions
			.Where((position, index) => position < 0 || (index > 0 && position < positions[index - 1]))
			.ToArray();

		// Assert
		await Assert
			.That(missing)
			.IsEmpty()
			.Because($"these sidebar sections have no matching Home.md heading: {string.Join(", ", missing)}");
		await Assert
			.That(outOfOrder)
			.IsEmpty()
			.Because("the sidebar sections and the Home.md headings are not in the same order");
	}

	[Test]
	public async Task Pages_GivenTheHomeTable_AreAllDiscoverable(CancellationToken cancellationToken)
	{
		// Arrange
		var home = await File.ReadAllTextAsync(
			Path.Combine(RepositoryLayout.WikiDirectory, "Home.md"),
			cancellationToken
		);

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
	public async Task Pages_GivenTheirLinks_AllResolve(CancellationToken cancellationToken)
	{
		// Arrange
		List<string> offenders = [];

		// Act
		foreach (var path in Directory.EnumerateFiles(WikiDirectory(), "*.md", SearchOption.AllDirectories))
		{
			var file = Path.GetRelativePath(WikiDirectory(), path).Replace('\\', '/');
			var lines = await File.ReadAllLinesAsync(path, cancellationToken);
			var inFence = false;
			for (var index = 0; index < lines.Length; index++)
			{
				if (lines[index].StartsWith("```", StringComparison.Ordinal))
				{
					inFence = !inFence;
					continue;
				}

				if (inFence)
					continue;

				foreach (var target in RelativeLinkTargets(lines[index]))
				{
					var resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, target));
					if (!File.Exists(resolved))
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
		foreach (var path in Directory.EnumerateFiles(WikiDirectory(), "*.md", SearchOption.AllDirectories))
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

	static string WikiDirectory() => RepositoryLayout.WikiDirectory;

	/// <summary>
	/// The content pages: every wiki file except the navigation file and the landing page. A page's
	/// path is relative to the wiki root, with forward slashes, so a page in a topic folder is named
	/// <c>foundations/Repository-Shape</c> here and linked as <c>(foundations/Repository-Shape.md)</c>.
	/// </summary>
	/// <returns>The page paths, without extension.</returns>
	static IReadOnlyList<string> ContentPages() =>
		[
			.. Directory
				.EnumerateFiles(WikiDirectory(), "*.md", SearchOption.AllDirectories)
				.Select(path => Path.ChangeExtension(Path.GetRelativePath(WikiDirectory(), path), null))
				.Select(name => name.Replace('\\', '/'))
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
}
