namespace Purview.EngineeringPlaybook.Repository;

/// <summary>
/// Where the files these tests assert on live, resolved from the test binary rather than from the
/// current directory, so a test behaves the same in an IDE run and on a build agent.
/// </summary>
/// <remarks>
/// One walk, shared. Two test classes that each grew their own copy of it would be the duplication
/// this playbook tells a repository to remove — and the copy that drifts is the one that finds the
/// wrong root and passes against it.
/// </remarks>
static class RepositoryLayout
{
	/// <summary>The repository root.</summary>
	/// <value>
	/// Found by walking up from <see cref="AppContext.BaseDirectory"/> to the first directory that has
	/// both an <c>AGENTS.md</c> and a <c>docs/wiki</c> folder — two things a checkout of this
	/// repository cannot be missing.
	/// </value>
	internal static string Root { get; } = FindRoot();

	/// <summary>The playbook's pages.</summary>
	internal static string WikiDirectory => Path.Combine(Root, "docs", "wiki");

	/// <summary>The committed snapshot of the organisation.</summary>
	internal static string OrgInventoryPath => Path.Combine(Root, ".config", "org-inventory.json");

	/// <summary>The skills this repository authors.</summary>
	internal static string SkillsDirectory => Path.Combine(Root, "skills");

	/// <summary>Walks up from the test binary to the repository root.</summary>
	/// <returns>The repository root path.</returns>
	/// <exception cref="InvalidOperationException">No directory above the test binary looks like this repository.</exception>
	static string FindRoot()
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
