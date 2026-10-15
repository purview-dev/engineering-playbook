using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Purview.EngineeringPlaybook.Architecture.Policy;

namespace Purview.EngineeringPlaybook.Architecture;

/// <summary>
/// Validates <c>.config/architecture-policy.json</c> against its JSON Schema.
/// </summary>
/// <remarks>
/// A schema nothing checks is decoration: it drifts from the document it claims to describe and
/// becomes a second, wrong account of the format. These tests make the schema load-bearing, so an
/// editor's red squiggle and the build agree. Same library the Purview.Build tool uses for
/// <c>purview-build.json</c>, so the ecosystem has one JSON Schema implementation.
/// </remarks>
public sealed class PolicySchemaTests
{
	const string SchemaId =
		"https://raw.githubusercontent.com/purview-dev/engineering-playbook/main/.config/architecture-policy.schema.json";

	/// <summary>
	/// The schema, parsed once.
	/// </summary>
	/// <remarks>
	/// <c>JsonSchema.FromText</c> registers a schema globally by its <c>$id</c>, and a second
	/// registration of the same identifier throws "Overwriting registered schemas is not permitted".
	/// Parsing per test therefore fails as soon as two tests run.
	/// </remarks>
	static readonly Lazy<JsonSchema> PolicySchema = new(() => JsonSchema.FromText(File.ReadAllText(SchemaPath)));

	static string ConfigDirectory => Path.Combine(ArchitecturePolicy.RepositoryRoot(), ".config");

	static string SchemaPath => Path.Combine(ConfigDirectory, "architecture-policy.schema.json");

	static string PolicyPath => Path.Combine(ConfigDirectory, "architecture-policy.json");

	[Test]
	public async Task Schema_GivenTheRepository_Exists()
	{
		// A '$schema' pointing at a file nobody wrote is worse than none: an editor reports no error,
		// so the format looks validated when it is not.
		await Assert
			.That(File.Exists(SchemaPath))
			.IsTrue()
			.Because($"the policy declares a $schema, so '{SchemaPath}' must exist");
	}

	[Test]
	public async Task Policy_GivenItsSchemaPointer_ReferencesTheSchemaByItsPublishedIdentifier(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var policyText = await File.ReadAllTextAsync(PolicyPath, cancellationToken);

		// Act
		var document = JsonNode.Parse(policyText);
		var declared = document?["$schema"]?.GetValue<string>();

		// Assert
		// An absolute identifier is what an editor can actually fetch; a relative path only works for
		// someone who already has the repository open, which is the case that needs help least.
		await Assert
			.That(declared)
			.IsEqualTo(SchemaId)
			.Because(
				"the policy must point at the published schema identifier, matching the purview-build.json convention"
			);
	}

	[Test]
	public async Task Schema_GivenItsContent_DeclaresTheMatchingIdentifier(CancellationToken cancellationToken)
	{
		// Arrange
		var schemaText = await File.ReadAllTextAsync(SchemaPath, cancellationToken);

		// Act
		var document = JsonNode.Parse(schemaText);

		// Assert
		await Assert.That(document?["$id"]?.GetValue<string>()).IsEqualTo(SchemaId);
		await Assert
			.That(document?["$schema"]?.GetValue<string>())
			.IsEqualTo("https://json-schema.org/draft/2020-12/schema");
	}

	[Test]
	public async Task Policy_GivenTheSchema_Validates(CancellationToken cancellationToken)
	{
		// Arrange
		var schema = PolicySchema.Value;
		using var policy = JsonDocument.Parse(await File.ReadAllTextAsync(PolicyPath, cancellationToken));

		// Act
		var result = schema.Evaluate(
			policy.RootElement,
			new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical, RequireFormatValidation = true }
		);

		// Assert
		await Assert.That(result.IsValid).IsTrue().Because(Describe(result));
	}

	[Test]
	public async Task Schema_GivenADocumentMissingARequiredField_RejectsIt()
	{
		// Arrange
		// The schema must be able to fail, or "the policy validates" says nothing.
		var schema = PolicySchema.Value;
		using var invalid = JsonDocument.Parse("""{ "version": 1 }""");

		// Act
		var result = schema.Evaluate(
			invalid.RootElement,
			new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical }
		);

		// Assert
		await Assert.That(result.IsValid).IsFalse().Because("a policy with no responsibilities must not validate");
	}

	[Test]
	public async Task Schema_GivenAWaiverWithNoReason_RejectsIt()
	{
		// Arrange
		// The minLength on 'reason' is the rule that stops a waiver becoming a silent suppression, so
		// it is worth pinning rather than trusting.
		var schema = PolicySchema.Value;
		using var invalid = JsonDocument.Parse(
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
			"""
		);

		// Act
		var result = schema.Evaluate(
			invalid.RootElement,
			new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical }
		);

		// Assert
		await Assert.That(result.IsValid).IsFalse().Because("a waiver with a token reason must not validate");
	}

	[Test]
	public async Task Schema_GivenAnUnknownResponsibilityField_RejectsIt()
	{
		// Arrange
		// additionalProperties:false is what catches a typo'd key, which would otherwise be read as
		// an absent rule - the most dangerous kind of mistake in a policy file.
		var schema = PolicySchema.Value;
		using var invalid = JsonDocument.Parse(
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
			"""
		);

		// Act
		var result = schema.Evaluate(
			invalid.RootElement,
			new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical }
		);

		// Assert
		await Assert.That(result.IsValid).IsFalse().Because("a misspelled key must not be silently ignored");
	}

	/// <summary>Renders schema evaluation errors with the instance location that failed.</summary>
	/// <param name="result">The evaluation result.</param>
	/// <returns>The rendered failures.</returns>
	static string Describe(EvaluationResults result)
	{
		if (result.IsValid)
			return "valid";

		var failures = Leaves(result)
			.SelectMany(leaf => leaf.Errors?.Select(error => $"  {Location(leaf)}: {error.Value}") ?? []);

		return Environment.NewLine + string.Join(Environment.NewLine, failures);
	}

	/// <summary>
	/// The failing results that have no failing children, so a container failure reports the key that
	/// caused it rather than a generic message about the object.
	/// </summary>
	/// <param name="results">The evaluation results to walk.</param>
	/// <returns>The failing leaves.</returns>
	static IEnumerable<EvaluationResults> Leaves(EvaluationResults results)
	{
		var children = (results.Details ?? []).Where(detail => !detail.IsValid).ToList();

		if (children.Count == 0)
		{
			if (!results.IsValid)
				yield return results;

			yield break;
		}

		foreach (var child in children)
		{
			foreach (var leaf in Leaves(child))
				yield return leaf;
		}
	}

	/// <summary>Renders the instance location of a failure as a readable path.</summary>
	/// <param name="result">The failing result.</param>
	/// <returns>The location.</returns>
	static string Location(EvaluationResults result)
	{
		var pointer = result.InstanceLocation.ToString();

		return string.IsNullOrEmpty(pointer) ? "(root)" : pointer.TrimStart('/').Replace('/', '.');
	}
}
