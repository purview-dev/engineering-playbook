using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Purview.EngineeringPlaybook.Repository;

/// <summary>
/// Runs the harvest skill's snapshot script as a real subprocess, replaying recorded input, so the
/// public-only rule is exercised against the code that ships rather than against a copy of it.
/// </summary>
/// <remarks>
/// <para>
/// The script is Node, not a shell, so there is no PowerShell variant and no Bash variant to keep in
/// step — that is the cross-platform answer, and these tests are what stop it drifting back into one.
/// They spawn it the same way on Windows and on Linux: <c>node</c>, the script, and arguments. No
/// shell, no <c>PATH</c> shim, no quoting rules to differ per platform.
/// </para>
/// <para>
/// The <c>gh</c> boundary is replaced with <c>--from</c> and <c>--details</c> rather than with a fake
/// binary. A shim on <c>PATH</c> was tried first and rejected: a non-shell spawn will not resolve a
/// <c>.cmd</c>, so the tests silently reached the real <c>gh</c> and asserted against sixteen live
/// repositories. A seam that can be bypassed without failing is not a seam.
/// </para>
/// <para>
/// <b>Node is a soft dependency.</b> Where it is absent the tests skip with a reason instead of
/// failing, because a .NET pipeline should not require a JavaScript runtime in order to prove a .NET
/// rule. It is not vacuous here: the harvest skill's own <c>just org-inventory</c> target needs it.
/// </para>
/// </remarks>
public sealed class InventoryScriptTests
{
	/// <summary>Whether <c>node</c> can be started at all.</summary>
	static readonly bool NodeIsAvailable = DetectNode();

	/// <summary>Why the tests skip when Node is missing.</summary>
	const string NodeMissing =
		"node is not on PATH, so the harvest script cannot be started; the assertions were not run.";

	/// <summary>The script under test.</summary>
	static string ScriptPath =>
		Path.Combine(RepositoryLayout.SkillsDirectory, "purview-org-harvest", "scripts", "inventory.mjs");

	[Test]
	public async Task Help_GivenNoRepository_PrintsTheUsage(CancellationToken cancellationToken)
	{
		Skip.When(!NodeIsAvailable, NodeMissing);

		// Arrange
		using var workspace = Workspace.Create();

		// Act
		var result = await RunAsync(["--help"], cancellationToken);

		// Assert
		await Assert.That(result.ExitCode).IsEqualTo(0);
		await Assert.That(result.StandardOutput).Contains("print the drift");
	}

	[Test]
	public async Task Arguments_GivenAnUnknownOne_FailsRatherThanGuessing(CancellationToken cancellationToken)
	{
		Skip.When(!NodeIsAvailable, NodeMissing);

		// Arrange
		using var workspace = Workspace.Create();

		// Act
		// A typo that silently fell back to printing would be read as "no drift".
		var result = await RunAsync(["--wrote"], cancellationToken);

		// Assert
		await Assert.That(result.ExitCode).IsEqualTo(1);
		await Assert.That(result.StandardError).Contains("Unknown argument");
	}

	[Test]
	public async Task Snapshot_GivenAMixedOrganisation_RecordsOnlyThePublicRepositories(
		CancellationToken cancellationToken
	)
	{
		Skip.When(!NodeIsAvailable, NodeMissing);

		// Arrange
		using var workspace = Workspace.Create();
		await workspace.PrepareAsync(
			Membership(
				("public-one", "PUBLIC", false),
				("public-two", "PUBLIC", false),
				("private-one", "PRIVATE", false),
				("archived-one", "PUBLIC", true)
			),
			["public-one", "public-two"],
			cancellationToken
		);

		// Act
		// The organisation the token can see contains private and archived repositories. Neither may
		// reach a file that is committed to a public repository.
		var result = await RunAsync(Replay(workspace, "--write", "--out", workspace.SnapshotPath), cancellationToken);

		// Assert
		await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.StandardError);
		var snapshot = await workspace.ReadSnapshotAsync(cancellationToken);
		var recorded = snapshot["repositories"]!.AsArray();

		await Assert
			.That(recorded.Select(entry => entry!["name"]!.GetValue<string>()).ToArray())
			.IsEquivalentTo(["public-one", "public-two"]);

		foreach (var entry in recorded)
		{
			await Assert.That(entry!["visibility"]!.GetValue<string>()).IsEqualTo("public");
			await Assert.That(entry["archived"]!.GetValue<bool>()).IsFalse();
			await Assert.That(entry["commitSha"]!.GetValue<string>().Length).IsEqualTo(40);
		}
	}

	[Test]
	public async Task Snapshot_GivenAPrivateEntryInTheTarget_IsRefusedAndLeftAlone(CancellationToken cancellationToken)
	{
		Skip.When(!NodeIsAvailable, NodeMissing);

		// Arrange
		// A merge carries forward what the file already held, so an entry a hand edit left behind
		// would survive every regeneration. The writer must refuse instead of persisting it.
		using var workspace = Workspace.Create();
		await workspace.PrepareAsync(Membership(("public-one", "PUBLIC", false)), ["public-one"], cancellationToken);
		await workspace.WriteSnapshotAsync(
			Snapshot("private-one", "private"),
			withByteOrderMark: false,
			cancellationToken
		);
		var before = await File.ReadAllBytesAsync(workspace.SnapshotPath, cancellationToken);

		// Act
		var result = await RunAsync(Replay(workspace, "--write", "--out", workspace.SnapshotPath), cancellationToken);

		// Assert
		await Assert.That(result.ExitCode).IsEqualTo(1);
		await Assert.That(result.StandardError).Contains("Refusing");
		await Assert
			.That(await File.ReadAllBytesAsync(workspace.SnapshotPath, cancellationToken))
			.IsEquivalentTo(before);
	}

	[Test]
	public async Task Print_GivenASnapshotWithAByteOrderMark_StillReadsIt(CancellationToken cancellationToken)
	{
		Skip.When(!NodeIsAvailable, NodeMissing);

		// Arrange
		using var workspace = Workspace.Create();
		await workspace.PrepareAsync(Membership(("public-one", "PUBLIC", false)), ["public-one"], cancellationToken);

		// The snapshot is the one file in this workflow a person is likely to open and re-save.
		await workspace.WriteSnapshotAsync(
			Snapshot("public-one", "public"),
			withByteOrderMark: true,
			cancellationToken
		);

		// Act
		var result = await RunAsync(Replay(workspace, "--print", "--out", workspace.SnapshotPath), cancellationToken);

		// Assert
		await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.StandardError);
		await Assert.That(result.StandardError).IsEmpty();
		await Assert.That(result.StandardOutput).Contains("public, non-archived repositories");
	}

	[Test]
	public async Task Print_GivenAChangedCommit_ReportsItAsDrift(CancellationToken cancellationToken)
	{
		Skip.When(!NodeIsAvailable, NodeMissing);

		// Arrange
		using var workspace = Workspace.Create();
		await workspace.PrepareAsync(Membership(("public-one", "PUBLIC", false)), ["public-one"], cancellationToken);
		await workspace.WriteSnapshotAsync(
			Snapshot("public-one", "public"),
			withByteOrderMark: false,
			cancellationToken
		);

		// Act
		// Drift is reported and the exit code stays 0: a stale snapshot is a review obligation, not a
		// red build for whoever happened to push next.
		var result = await RunAsync(Replay(workspace, "--print", "--out", workspace.SnapshotPath), cancellationToken);

		// Assert
		await Assert.That(result.ExitCode).IsEqualTo(0);
		await Assert.That(result.StandardOutput).Contains("commitSha");
	}

	/// <summary>Appends the replay flags, so a run reads recorded input instead of the network.</summary>
	/// <param name="workspace">The sandbox holding the recorded files.</param>
	/// <param name="arguments">The run's own arguments.</param>
	/// <returns>The full argument list.</returns>
	static string[] Replay(Workspace workspace, params string[] arguments) =>
		[.. arguments, "--from", workspace.MembershipPath, "--details", workspace.DetailsPath];

	/// <summary>Runs the script as a subprocess, with no shell and no per-platform quoting.</summary>
	/// <param name="arguments">The script's arguments.</param>
	/// <param name="cancellationToken">Cancels the run.</param>
	/// <returns>The exit code and both streams.</returns>
	static async Task<Result> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
	{
		ProcessStartInfo start = new("node")
		{
			WorkingDirectory = RepositoryLayout.Root,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};

		start.ArgumentList.Add(ScriptPath);
		foreach (var argument in arguments)
		{
			start.ArgumentList.Add(argument);
		}

		using var process = Process.Start(start) ?? throw new InvalidOperationException("node could not be started.");

		var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
		var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
		await process.WaitForExitAsync(cancellationToken);

		return new Result(process.ExitCode, await standardOutput, await standardError);
	}

	/// <summary>Builds a <c>gh repo list --json</c> response.</summary>
	/// <param name="repositories">Name, visibility and archived flag for each repository.</param>
	/// <returns>The JSON array the CLI would have printed.</returns>
	static string Membership(params (string Name, string Visibility, bool Archived)[] repositories)
	{
		JsonArray listed =
		[
			.. repositories.Select(repository =>
				(JsonNode?)
					new JsonObject
					{
						["name"] = repository.Name,
						["visibility"] = repository.Visibility,
						["isArchived"] = repository.Archived,
						["pushedAt"] = "2026-11-03T00:00:00Z",
						["defaultBranchRef"] = new JsonObject { ["name"] = "main" },
					}
			),
		];

		return listed.ToJsonString();
	}

	/// <summary>Builds a snapshot document holding one entry, with a deliberately stale SHA.</summary>
	/// <param name="name">The repository name.</param>
	/// <param name="visibility">The visibility to record, public or not.</param>
	/// <returns>The JSON document.</returns>
	static string Snapshot(string name, string visibility)
	{
		JsonObject document = new()
		{
			["schemaVersion"] = 1,
			["org"] = "purview-dev",
			["branch"] = "main",
			["fetchedAt"] = "2026-11-03T00:00:00Z",
			["repositories"] = new JsonArray
			{
				new JsonObject
				{
					["name"] = name,
					["visibility"] = visibility,
					["archived"] = false,
					["commitSha"] = new string('0', 40),
					["pushedAt"] = "2026-11-03T00:00:00Z",
					["docsDir"] = null,
					["docsPages"] = 0,
					["skills"] = new JsonArray(),
					["agentGuidance"] = new JsonArray(),
					["concerns"] = new JsonArray(),
				},
			},
		};

		return document.ToJsonString();
	}

	/// <summary>Builds the recorded commits and file lists that <c>--details</c> reads.</summary>
	/// <param name="repositories">The repositories to describe.</param>
	/// <returns>The JSON object the script expects.</returns>
	static string Details(IReadOnlyList<string> repositories)
	{
		JsonObject recorded = [];
		foreach (var name in repositories)
		{
			// A run records what the organisation said at the time, so the tree and the SHA are
			// supplied together; one without the other would describe a repository nobody read.
			recorded[name] = new JsonObject
			{
				["commitSha"] = new string('a', 40),
				["paths"] = new JsonArray { "docs/wiki/Home.md", $"skills/{name}-skill/SKILL.md" },
			};
		}

		return recorded.ToJsonString();
	}

	/// <summary>Reports whether <c>node</c> can be started.</summary>
	/// <returns>True when it runs and exits cleanly.</returns>
	static bool DetectNode()
	{
		try
		{
			using var process = Process.Start(
				new ProcessStartInfo("node")
				{
					ArgumentList = { "--version" },
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
				}
			);

			if (process is null)
			{
				return false;
			}

			process.WaitForExit();
			return process.ExitCode == 0;
		}
		catch (Win32Exception)
		{
			return false;
		}
	}

	/// <summary>One script run.</summary>
	/// <param name="ExitCode">The process exit code.</param>
	/// <param name="StandardOutput">What it wrote to stdout.</param>
	/// <param name="StandardError">What it wrote to stderr.</param>
	sealed record Result(int ExitCode, string StandardOutput, string StandardError);

	/// <summary>A temporary directory holding the recorded input and the snapshot under test.</summary>
	/// <remarks>
	/// Nothing here is platform-specific: the files are JSON, and the script reads them the same way
	/// everywhere. That is the whole point of replacing a fake binary with recorded input.
	/// </remarks>
	sealed class Workspace : IDisposable
	{
		Workspace(string root)
		{
			Root = root;
			MembershipPath = Path.Combine(root, "membership.json");
			DetailsPath = Path.Combine(root, "details.json");
			SnapshotPath = Path.Combine(root, "snapshot.json");
		}

		/// <summary>The sandbox root.</summary>
		internal string Root { get; }

		/// <summary>Where the recorded <c>gh repo list</c> response is written.</summary>
		internal string MembershipPath { get; }

		/// <summary>Where the recorded commits and file lists are written.</summary>
		internal string DetailsPath { get; }

		/// <summary>The snapshot the script reads and writes.</summary>
		internal string SnapshotPath { get; }

		/// <summary>Creates an empty sandbox.</summary>
		/// <returns>The sandbox; dispose it to remove the directory.</returns>
		internal static Workspace Create()
		{
			var root = Path.Combine(Path.GetTempPath(), $"purview-inventory-{Guid.NewGuid():N}");
			Directory.CreateDirectory(root);

			return new Workspace(root);
		}

		/// <summary>Writes the recorded input a replay reads.</summary>
		/// <param name="membership">The <c>gh repo list --json</c> response.</param>
		/// <param name="detailed">The repositories the details file describes.</param>
		/// <param name="cancellationToken">Cancels the writes.</param>
		/// <returns>A task that completes when both files are written.</returns>
		internal async Task PrepareAsync(
			string membership,
			IReadOnlyList<string> detailed,
			CancellationToken cancellationToken
		)
		{
			await File.WriteAllTextAsync(MembershipPath, membership, cancellationToken);
			await File.WriteAllTextAsync(DetailsPath, Details(detailed), cancellationToken);
		}

		/// <summary>Writes the snapshot the script will read.</summary>
		/// <param name="json">The document.</param>
		/// <param name="withByteOrderMark">Whether to prefix a UTF-8 byte-order mark.</param>
		/// <param name="cancellationToken">Cancels the write.</param>
		/// <returns>A task that completes when the file is written.</returns>
		internal async Task WriteSnapshotAsync(string json, bool withByteOrderMark, CancellationToken cancellationToken)
		{
			// UTF8Encoding(true) writes the BOM that an editor would; the encode is deliberate here.
			System.Text.UTF8Encoding encoding = new(withByteOrderMark);
			await File.WriteAllTextAsync(SnapshotPath, json, encoding, cancellationToken);
		}

		/// <summary>Reads the snapshot the script wrote.</summary>
		/// <param name="cancellationToken">Cancels the read.</param>
		/// <returns>The parsed document.</returns>
		internal async Task<JsonNode> ReadSnapshotAsync(CancellationToken cancellationToken) =>
			JsonNode.Parse(await File.ReadAllTextAsync(SnapshotPath, cancellationToken))
			?? throw new InvalidOperationException($"'{SnapshotPath}' is empty.");

		/// <inheritdoc />
		public void Dispose()
		{
			try
			{
				Directory.Delete(Root, recursive: true);
			}
			catch (IOException)
			{
				// A leaked temp directory is not worth failing a passing test over.
			}
			catch (UnauthorizedAccessException)
			{
				// Same.
			}
		}
	}
}
