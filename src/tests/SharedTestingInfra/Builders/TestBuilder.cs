using Purview.EngineeringPlaybook.Domain;

namespace Purview.EngineeringPlaybook.Builders;

/// <summary>
/// The single means by which a test creates domain content.
/// </summary>
/// <remarks>
/// <para>
/// Every factory returns a realistic value without the test repeating literals: an omitted argument is
/// generated with Bogus, and a test overrides only the value it is proving. Bogus arrives as a global
/// <c>using</c> from the SDK, so <c>Faker</c> needs no import here.
/// </para>
/// <para>
/// <b>The omitted-argument contract.</b> An omitted argument is generated; an argument that is passed is
/// used exactly as given. Where the domain itself allows a null value, the factory takes the domain's own
/// nullable type, so <see langword="null"/> means "generate one" and a factory never needs a wrapper type
/// to tell "not supplied" apart from "supplied as null".
/// </para>
/// <para>
/// <b>Why the factories are flat.</b> The obvious shape is a nested class per feature area
/// (<c>TestBuilder.Catalogue.Convention(...)</c>), which is how a repository with many areas keeps them
/// apart. The SDK ships <c>dotnet_diagnostic.CA1034.severity = error</c>, so an externally visible nested
/// type is a build failure, and <c>PRSGD0008</c> rejects adding <c>CA1034</c> to <c>NoWarn</c>. A
/// shared-testing project also cannot hand its internals to the test projects beside it, because the
/// SDK's <c>InternalsVisibleTo</c> target runs on non-test projects only. Factories therefore sit
/// directly on <see cref="TestBuilder"/>, and an area that needs its own namespace gets its own builder
/// class instead.
/// </para>
/// <para>
/// A test that proves a type's own <c>Create</c>, <c>Hydrate</c> or schema behaviour is the deliberate
/// exception: it constructs the invalid value directly so the rejected input is explicit.
/// </para>
/// </remarks>
public static class TestBuilder
{
	/// <summary>The number of items a collection factory generates when no count is supplied.</summary>
	public const int DefaultCount = 3;

	/// <summary>The characters a generated identifier suffix is drawn from.</summary>
	const string LowercaseAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

	/// <summary>The generator every factory draws from.</summary>
	internal static Faker Faker { get; } = new();

	/// <summary>
	/// Creates a convention identifier.
	/// </summary>
	/// <param name="value">The slug to use; omitted generates a unique one.</param>
	/// <returns>The identifier.</returns>
	public static ConventionId ConventionId(string? value = null) =>
		Domain.ConventionId.Create(value ?? $"{Slug()}-{Faker.Random.String2(6, LowercaseAlphabet)}");

	/// <summary>
	/// Creates a convention.
	/// </summary>
	/// <param name="id">The identifier; omitted generates one.</param>
	/// <param name="title">The title; omitted generates one.</param>
	/// <param name="tier">The verification tier; omitted generates one.</param>
	/// <param name="enforcedByBuild">Whether the build enforces it; omitted generates one.</param>
	/// <returns>The convention.</returns>
	public static Convention Convention(
		ConventionId? id = null,
		string? title = null,
		VerificationTier? tier = null,
		bool? enforcedByBuild = null
	) =>
		Domain.Convention.Create(
			id ?? ConventionId(),
			title ?? Faker.Lorem.Sentence(4),
			tier ?? Faker.Random.Enum<VerificationTier>(),
			enforcedByBuild ?? Faker.Random.Bool()
		);

	/// <summary>
	/// Creates a convention the build refuses to let you break.
	/// </summary>
	/// <param name="id">The identifier; omitted generates one.</param>
	/// <returns>The convention.</returns>
	public static Convention EnforcedConvention(ConventionId? id = null) =>
		Convention(id: id, tier: VerificationTier.Unit, enforcedByBuild: true);

	/// <summary>
	/// Creates <paramref name="count"/> conventions, each with a distinct identifier.
	/// </summary>
	/// <param name="count">How many to create; defaults to <see cref="DefaultCount"/>.</param>
	/// <returns>The conventions.</returns>
	public static IReadOnlyList<Convention> Conventions(int count = DefaultCount) =>
		[.. Enumerable.Range(0, count).Select(_ => Convention())];

	/// <summary>
	/// Creates a lower-case, hyphen-separated word pair.
	/// </summary>
	/// <returns>A slug-shaped string.</returns>
	/// <remarks>
	/// Lorem words are already lower case, so no casing call is needed — which also keeps the factory
	/// clear of <c>CA1308</c>, whose advice to prefer upper-case normalisation is wrong for a slug.
	/// </remarks>
	static string Slug() => $"{Faker.Lorem.Word()}-{Faker.Lorem.Word()}";
}
