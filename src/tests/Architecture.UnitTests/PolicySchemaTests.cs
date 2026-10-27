using System.Text.Json.Nodes;
using Purview.Architecture.Testing;

namespace Purview.EngineeringPlaybook.Architecture;

/// <summary>
/// Validates <c>.config/architecture-policy.json</c> against the schema the package ships.
/// </summary>
/// <remarks>
/// The schema is embedded in <c>Purview.Architecture.Testing</c> and published at
/// <see cref="PolicySchemaValidator.SchemaId"/> for editors, so the policy is described in one place
/// and validation needs no network. A schema nothing checks is decoration: it drifts from the
/// document it claims to describe and becomes a second, wrong account of the format. The rejection
/// tests below are the point - a schema that cannot fail proves nothing.
/// </remarks>
public sealed class PolicySchemaTests
{
	static string PolicyPath =>
		Path.Combine(ArchitecturePolicy.RepositoryRoot(), ".config", "architecture-policy.json");

	[Test]
	public async Task Policy_GivenItsSchemaPointer_ReferencesThePublishedSchemaIdentifier(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var policyText = await File.ReadAllTextAsync(PolicyPath, cancellationToken);

		// Act
		// An absolute identifier is what an editor can actually fetch; a relative path only works for
		// someone who already has the repository open, which is the case that needs help least.
		var declared = JsonNode.Parse(policyText)?["$schema"]?.GetValue<string>();

		// Assert
		await Assert.That(declared).IsEqualTo(PolicySchemaValidator.SchemaId);
	}

	[Test]
	public async Task Policy_GivenTheSchema_Validates()
	{
		// Act
		var failures = PolicySchemaValidator.Validate(PolicyPath);

		// Assert
		await Assert.That(failures).IsEmpty().Because(string.Join(Environment.NewLine, failures));
	}

	[Test]
	public async Task Schema_GivenADocumentMissingARequiredField_RejectsIt(CancellationToken cancellationToken)
	{
		// The schema must be able to fail, or "the policy validates" says nothing.
		await AssertRejectedAsync("""{ "version": 1 }""", cancellationToken);
	}

	[Test]
	public async Task Schema_GivenAWaiverWithATokenReason_RejectsIt(CancellationToken cancellationToken)
	{
		// The minLength on 'reason' is the rule that stops a waiver becoming a silent suppression.
		await AssertRejectedAsync(
			"""
			{
				"version": 1,
				"responsibilities": [
					{
						"name": "Core",
						"category": "domain",
						"purpose": "The fundamental domain model and its rules.",
						"mayDependOn": [],
						"mustNotDependOn": []
					}
				],
				"waivers": [ { "project": "Infrastructure", "rules": ["naming"], "reason": "because" } ]
			}
			""",
			cancellationToken
		);
	}

	[Test]
	public async Task Schema_GivenAnUnknownResponsibilityField_RejectsIt(CancellationToken cancellationToken)
	{
		// additionalProperties:false is what catches a typo'd key, which would otherwise be read as an
		// absent rule - the most dangerous kind of mistake in a policy file.
		await AssertRejectedAsync(
			"""
			{
				"version": 1,
				"responsibilities": [
					{
						"name": "Core",
						"category": "domain",
						"purpose": "The fundamental domain model and its rules.",
						"mayDependOn": [],
						"mustNotDependOn": [],
						"mustNotDependOnn": ["Application"]
					}
				]
			}
			""",
			cancellationToken
		);
	}

	[Test]
	public async Task Schema_GivenTheRemovedRulesObject_RejectsIt(CancellationToken cancellationToken)
	{
		// Version 2 removed 'rules': its flags were deserialised but never consulted, so accepting the
		// block again would restore a switch that looks enforced and is not.
		await AssertRejectedAsync(
			"""
			{
				"version": 2,
				"responsibilities": [
					{
						"name": "Core",
						"category": "domain",
						"purpose": "The fundamental domain model and its rules.",
						"mayDependOn": [],
						"mustNotDependOn": []
					}
				],
				"rules": { "noCycles": true }
			}
			""",
			cancellationToken
		);
	}

	/// <summary>Writes a candidate policy, validates it, and asserts the schema rejected it.</summary>
	/// <param name="json">The malformed policy.</param>
	/// <param name="cancellationToken">Cancels the write.</param>
	/// <returns>A task that completes when the assertion has run.</returns>
	static async Task AssertRejectedAsync(string json, CancellationToken cancellationToken)
	{
		var path = Path.Combine(Path.GetTempPath(), $"architecture-policy-{Guid.NewGuid():N}.json");
		try
		{
			await File.WriteAllTextAsync(path, json, cancellationToken);

			var failures = PolicySchemaValidator.Validate(path);

			await Assert.That(failures).IsNotEmpty().Because("the schema accepted a document it must reject");
		}
		finally
		{
			File.Delete(path);
		}
	}
}
