// COPY TO: src/tests/SharedTestingInfra/Builders/TestBuilder.cs
//
// The single means by which a test creates domain content. Replace `Acme` with your prefix and the
// `Convention` factories with your own types.
//
// Two things to keep when you copy this:
//
//   * The omitted-argument contract. An omitted argument is generated; a supplied argument is used exactly
//     as given. Where the domain itself allows null, the factory takes the domain's own nullable type, so
//     `null` means "generate one" and no wrapper type is needed to tell "not supplied" apart from
//     "supplied as null".
//
//   * The flat shape. The obvious alternative is a nested class per feature area
//     (TestBuilder.Catalogue.Convention(...)), and it cannot compile: the SDK ships
//     `dotnet_diagnostic.CA1034.severity = error`, so a publicly visible nested type is a build failure,
//     and PRSGD0008 refuses to let a repository add CA1034 to NoWarn. Making the group `internal` does not
//     help either, because the SDK's InternalsVisibleTo target runs on non-test projects and grants access
//     *to* test and shared-testing projects rather than *from* them. An area that outgrows a flat builder
//     gets its own top-level builder class.

using Acme.Domain;

namespace Acme.Builders;

/// <summary>
/// The single means by which a test creates domain content.
/// </summary>
public static class TestBuilder
{
	/// <summary>The number of items a collection factory generates when no count is supplied.</summary>
	public const int DefaultCount = 3;

	/// <summary>The characters a generated identifier suffix is drawn from.</summary>
	const string LowercaseAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

	/// <summary>The generator every factory draws from.</summary>
	/// <remarks>Bogus arrives as a global <c>using</c> from the SDK, so this file needs no import for it.</remarks>
	internal static Faker Faker { get; } = new();

	/// <summary>
	/// Creates a convention identifier.
	/// </summary>
	/// <param name="value">The slug to use; omitted generates a unique one.</param>
	/// <returns>The identifier.</returns>
	public static Domain.ConventionId ConventionId(string? value = null) =>
		Domain.ConventionId.Create(value ?? $"{Slug()}-{Faker.Random.String2(6, LowercaseAlphabet)}");

	/// <summary>
	/// Creates a convention.
	/// </summary>
	/// <param name="id">The identifier; omitted generates one.</param>
	/// <param name="title">The title; omitted generates one.</param>
	/// <param name="tier">The verification tier; omitted generates one.</param>
	/// <param name="enforcedByBuild">Whether the build enforces it; omitted generates one.</param>
	/// <returns>The convention.</returns>
	public static Domain.Convention Convention(
		Domain.ConventionId? id = null,
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
	public static Domain.Convention EnforcedConvention(Domain.ConventionId? id = null) =>
		Convention(id: id, tier: VerificationTier.Unit, enforcedByBuild: true);

	/// <summary>
	/// Creates <paramref name="count"/> conventions, each with a distinct identifier.
	/// </summary>
	/// <param name="count">How many to create; defaults to <see cref="DefaultCount"/>.</param>
	/// <returns>The conventions.</returns>
	public static IReadOnlyList<Domain.Convention> Conventions(int count = DefaultCount) =>
		[.. Enumerable.Range(0, count).Select(_ => Convention())];

	/// <summary>
	/// Creates a lower-case, hyphen-separated word pair.
	/// </summary>
	/// <returns>A slug-shaped string.</returns>
	/// <remarks>
	/// Lorem words are already lower case, so no casing call is needed — which also keeps the factory clear
	/// of CA1308, whose advice to prefer upper-case normalisation is wrong for a slug.
	/// </remarks>
	static string Slug() => $"{Faker.Lorem.Word()}-{Faker.Lorem.Word()}";
}
