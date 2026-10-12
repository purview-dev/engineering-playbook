using Purview.EngineeringPlaybook.Builders;

namespace Purview.EngineeringPlaybook.Domain;

/// <summary>
/// Tests for <see cref="ConventionId"/>.
/// </summary>
/// <remarks>
/// This is the value-object exception to the "build everything through <see cref="TestBuilder"/>"
/// rule: a test that proves what <c>Create</c> and <c>Hydrate</c> accept and reject has to name the
/// rejected input itself, so the invalid values below are constructed directly and deliberately.
/// </remarks>
public sealed class ConventionIdTests
{
	[Test]
	public async Task Create_GivenLowercaseSlug_ReturnsTheValue()
	{
		// Arrange
		var id = TestBuilder.ConventionId("build-enforced");

		// Act
		var value = id.Value;

		// Assert
		await Assert.That(value).IsEqualTo("build-enforced");
	}

	[Test]
	public async Task Create_GivenSlugAtMaximumLength_ReturnsTheValue()
	{
		// Arrange
		string slug = new('a', ConventionId.MaxLength);

		// Act
		var id = ConventionId.Create(slug);

		// Assert
		await Assert.That(id.Value.Length).IsEqualTo(ConventionId.MaxLength);
	}

	[Test]
	[Arguments("")]
	[Arguments("   ")]
	[Arguments("Build-Enforced")]
	[Arguments("build_enforced")]
	[Arguments("-leading-hyphen")]
	[Arguments("trailing-hyphen-")]
	[Arguments("has a space")]
	[Arguments("has.a.dot")]
	public async Task Create_GivenSlugThatIsNotALowercaseSlug_Throws(string value)
	{
		// Arrange
		// Act
		Action act = () => ConventionId.Create(value);

		// Assert
		await Assert.That(act).Throws<ArgumentException>();
	}

	[Test]
	public async Task Create_GivenSlugLongerThanTheMaximum_Throws()
	{
		// Arrange
		string slug = new('a', ConventionId.MaxLength + 1);

		// Act
		Action act = () => ConventionId.Create(slug);

		// Assert
		await Assert.That(act).Throws<ArgumentException>();
	}

	[Test]
	public async Task Hydrate_GivenAValueThatIsAlreadyValid_ReturnsTheValue()
	{
		// Arrange
		// Act
		var id = ConventionId.Hydrate("build-enforced");

		// Assert
		await Assert.That(id.Value).IsEqualTo("build-enforced");
	}

	[Test]
	public async Task Hydrate_GivenAnEmptyValue_Throws()
	{
		// Arrange
		// Act
		Action act = () => ConventionId.Hydrate(" ");

		// Assert
		await Assert.That(act).Throws<ArgumentException>();
	}

	[Test]
	public async Task Hydrate_GivenAValueCreateWouldReject_ReturnsItUnvalidated()
	{
		// Arrange
		// Act
		var id = ConventionId.Hydrate("Not-A-Slug");

		// Assert
		await Assert.That(id.Value).IsEqualTo("Not-A-Slug");
	}

	[Test]
	public async Task Equality_GivenTwoIdsWithTheSameValue_AreEqual()
	{
		// Arrange
		var first = ConventionId.Create("build-enforced");
		var second = ConventionId.Hydrate("build-enforced");

		// Act
		// Assert
		await Assert.That(first).IsEqualTo(second);
	}

	[Test]
	public async Task Equality_GivenTwoIdsWithDifferentValues_AreNotEqual()
	{
		// Arrange
		var first = ConventionId.Create("build-enforced");
		var second = ConventionId.Create("review-only");

		// Act
		// Assert
		await Assert.That(first).IsNotEqualTo(second);
	}

	[Test]
	public async Task ToString_ReturnsTheValue()
	{
		// Arrange
		var id = ConventionId.Create("build-enforced");

		// Act
		var text = id.ToString();

		// Assert
		await Assert.That(text).IsEqualTo("build-enforced");
	}
}
