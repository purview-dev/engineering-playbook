using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Purview.EngineeringPlaybook.Repository;

/// <summary>
/// Asserts that the committed snapshot of the organisation, <c>.config/org-inventory.json</c>, is
/// well formed and that every repository the wiki names is in it.
/// </summary>
/// <remarks>
/// <para>
/// The snapshot is what makes the harvest rule checkable without a network call. A repository that
/// was renamed, archived or made private drops out of the organisation while every page citing it
/// keeps rendering, so the only symptom is a link a reader has to click to discover it is dead.
/// </para>
/// <para>
/// These tests are offline deliberately: the pipeline has the file, not the organisation, and a test
/// that called GitHub would fail whenever the network did. Whether the snapshot is <i>current</i> is
/// therefore a review obligation — <c>just org-inventory</c> reports the drift, and this file does
/// not pretend to. See <c>docs/wiki/governance/Cross-Repository-Harvest.md</c>.
/// </para>
/// </remarks>
public sealed partial class OrgInventoryTests
{
	[GeneratedRegex(@"(?:github\.com|raw\.githubusercontent\.com)/purview-dev/(?<repo>[A-Za-z0-9._-]+)")]
	private static partial Regex RepositoryUrlPattern();

	[GeneratedRegex(@"(?<![A-Za-z0-9._/-])purview-dev/(?<repo>[A-Za-z0-9._-]+)")]
	private static partial Regex RepositoryNamePattern();

	[GeneratedRegex(@"^[0-9a-f]{40}$")]
	private static partial Regex CommitShaPattern();

	/// <summary>Directory names that hold build output or tool state, never published content.</summary>
	static readonly string[] ExcludedDirectories =
	[
		".git",
		".purview",
		".tools",
		".vs",
		"artifacts",
		"bin",
		"node_modules",
		"obj",
		"TestResults",
	];

	/// <summary>The extensions this repository publishes as text.</summary>
	static readonly string[] TextExtensions =
	[
		".cs",
		".csproj",
		".json",
		".js",
		".md",
		".mjs",
		".mts",
		".props",
		".ps1",
		".sh",
		".slnx",
		".targets",
		".ts",
		".txt",
		".xml",
		".yaml",
		".yml",
	];

	[Test]
	public async Task Snapshot_GivenEveryEntry_IsAPublicRepositoryThatIsNotArchived(CancellationToken cancellationToken)
	{
		// Arrange
		using var snapshot = await ReadSnapshotAsync(cancellationToken);
		List<string> offenders = [];

		// Act
		// A private repository may shape a rule, but it is never published, and it is never recorded
		// here: the snapshot is committed to a public repository. An archived one is no longer a
		// source of anything.
		foreach (var repository in Repositories(snapshot.RootElement))
		{
			var name = Name(repository);
			var visibility = repository.GetProperty("visibility").GetString();
			var archived = repository.GetProperty("archived").GetBoolean();

			if (!string.Equals(visibility, "public", StringComparison.Ordinal) || archived)
			{
				offenders.Add($"{name}: visibility '{visibility}', archived {archived}");
			}
		}

		// Assert
		await Assert
			.That(offenders)
			.IsEmpty()
			.Because(
				"the snapshot is committed to a public repository, so it records public, unarchived "
					+ $"ones only: {string.Join("; ", offenders)}"
			);
	}

	[Test]
	public async Task Snapshot_GivenEveryEntry_PinsTheBranchAtACommit(CancellationToken cancellationToken)
	{
		// Arrange
		using var snapshot = await ReadSnapshotAsync(cancellationToken);
		var root = snapshot.RootElement;
		List<string> offenders = [];

		// Act
		// A snapshot without a SHA is a claim about "recently", and nothing can be re-read at
		// "recently". The branch is recorded too, because a convention read at a different branch is
		// a different convention.
		var branch = root.GetProperty("branch").GetString();
		if (!string.Equals(branch, "main", StringComparison.Ordinal))
		{
			offenders.Add($"the snapshot reads branch '{branch}' rather than 'main'");
		}

		var fetchedAt = root.GetProperty("fetchedAt").GetString();
		if (!DateTimeOffset.TryParse(fetchedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
		{
			offenders.Add($"fetchedAt '{fetchedAt}' is not a parseable RFC 3339 timestamp");
		}

		foreach (var repository in Repositories(root))
		{
			var sha = repository.GetProperty("commitSha").GetString() ?? string.Empty;
			if (!CommitShaPattern().IsMatch(sha))
			{
				offenders.Add($"{Name(repository)}: commitSha '{sha}' is not a 40-character SHA");
			}

			var pushedAt = repository.GetProperty("pushedAt").GetString();
			if (string.IsNullOrEmpty(pushedAt))
			{
				offenders.Add($"{Name(repository)}: pushedAt is missing");
			}
		}

		// Assert
		await Assert.That(offenders).IsEmpty().Because(string.Join("; ", offenders));
	}

	[Test]
	public async Task Files_GivenARepositoryTheyName_ResolveToASnapshotEntry(CancellationToken cancellationToken)
	{
		// Arrange
		using var snapshot = await ReadSnapshotAsync(cancellationToken);
		var known = Repositories(snapshot.RootElement).Select(Name).ToHashSet(StringComparer.Ordinal);
		List<string> offenders = [];

		// Act
		// Two failures, one assertion. GitHub redirects a renamed repository, so a stale name still
		// resolves in a browser and nothing else notices. And a repository the snapshot does not
		// contain is either private or gone - either way, naming it publishes something this
		// repository has no business publishing. Skills count: a skill is installed into somebody
		// else's agent.
		foreach (var path in PublishedTextFiles())
		{
			var lines = await File.ReadAllLinesAsync(path, cancellationToken);
			for (var index = 0; index < lines.Length; index++)
			{
				foreach (var name in NamedRepositories(lines[index]))
				{
					if (!known.Contains(name))
					{
						offenders.Add($"{Path.GetRelativePath(RepositoryLayout.Root, path)}:{index + 1}: '{name}'");
					}
				}
			}
		}

		// Assert
		await Assert
			.That(offenders)
			.IsEmpty()
			.Because(
				"every repository this repository names - in a page, a skill, a workflow or a config - "
					+ "must be one the snapshot recorded as public and unarchived, so a rename, an "
					+ "archive or a private repository is caught here rather than by a reader: "
					+ string.Join("; ", offenders)
			);
	}

	[Test]
	public async Task Skills_GivenTheirFrontmatter_NameTheRepositoryThatPublishesThem(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		List<string> offenders = [];

		// Act
		// The skills CLI clones the repository named in the source it was given, and the frontmatter
		// is what a reader of an installed skill can check. A skill that names no repository is
		// guidance with no owner.
		foreach (var directory in Directory.EnumerateDirectories(RepositoryLayout.SkillsDirectory))
		{
			var manifest = Path.Combine(directory, "SKILL.md");
			if (!File.Exists(manifest))
			{
				continue;
			}

			var frontmatter = Frontmatter(await File.ReadAllTextAsync(manifest, cancellationToken));
			if (!frontmatter.Contains("repository: purview-dev/engineering-playbook", StringComparison.Ordinal))
			{
				offenders.Add(Path.GetFileName(directory));
			}
		}

		// Assert
		await Assert
			.That(offenders)
			.IsEmpty()
			.Because(
				"these skills do not declare metadata.repository in their frontmatter: " + string.Join(", ", offenders)
			);
	}

	/// <summary>Every published text file in the repository.</summary>
	/// <returns>The file paths, as full paths.</returns>
	/// <remarks>
	/// The snapshot itself is excluded: it is generated from the organisation, so checking it against
	/// itself proves nothing. Everything else is read, because a repository named in a skill reaches
	/// somebody else's agent and a repository named in a workflow reaches a public build log — the
	/// wiki is the loudest surface, not the only one.
	/// </remarks>
	static IEnumerable<string> PublishedTextFiles()
	{
		Stack<string> pending = new([RepositoryLayout.Root]);
		while (pending.Count > 0)
		{
			var directory = pending.Pop();
			foreach (var child in Directory.EnumerateDirectories(directory))
			{
				if (!ExcludedDirectories.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase))
				{
					pending.Push(child);
				}
			}

			foreach (var file in Directory.EnumerateFiles(directory))
			{
				if (
					TextExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)
					&& !string.Equals(file, RepositoryLayout.OrgInventoryPath, StringComparison.OrdinalIgnoreCase)
				)
				{
					yield return file;
				}
			}
		}
	}

	/// <summary>Reads the committed snapshot.</summary>
	/// <param name="cancellationToken">Cancels the read.</param>
	/// <returns>The parsed snapshot; the caller disposes it.</returns>
	static async Task<JsonDocument> ReadSnapshotAsync(CancellationToken cancellationToken) =>
		JsonDocument.Parse(await File.ReadAllTextAsync(RepositoryLayout.OrgInventoryPath, cancellationToken));

	/// <summary>The snapshot's repositories.</summary>
	/// <param name="root">The snapshot document's root element.</param>
	/// <returns>The repository entries.</returns>
	static JsonElement.ArrayEnumerator Repositories(JsonElement root) =>
		root.GetProperty("repositories").EnumerateArray();

	/// <summary>One repository entry's name.</summary>
	/// <param name="repository">The repository entry.</param>
	/// <returns>The repository name.</returns>
	static string Name(JsonElement repository) => repository.GetProperty("name").GetString() ?? string.Empty;

	/// <summary>Reads the repository names a line of markdown mentions.</summary>
	/// <param name="line">The markdown line.</param>
	/// <returns>The distinct names, deduplicated because a URL also matches the bare-name pattern.</returns>
	static HashSet<string> NamedRepositories(string line)
	{
		HashSet<string> names = new(StringComparer.Ordinal);
		foreach (var pattern in new[] { RepositoryUrlPattern(), RepositoryNamePattern() })
		{
			foreach (Match match in pattern.Matches(line))
			{
				// A name that ends a sentence picks up the full stop, and a clone URL ends in `.git`.
				// A repository name ends with neither.
				var name = match.Groups["repo"].Value.TrimEnd('.');
				if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
				{
					name = name[..^4];
				}

				if (name.Length > 0)
				{
					names.Add(name);
				}
			}
		}

		return names;
	}

	/// <summary>Reads a manifest's YAML frontmatter.</summary>
	/// <param name="text">The manifest's text.</param>
	/// <returns>The frontmatter block, or an empty string when there is none.</returns>
	static string Frontmatter(string text)
	{
		if (!text.StartsWith("---", StringComparison.Ordinal))
		{
			return string.Empty;
		}

		var end = text.IndexOf("\n---", 3, StringComparison.Ordinal);
		return end < 0 ? string.Empty : text[3..end];
	}
}
