using Purview.EngineeringPlaybook.Builders;

namespace Purview.EngineeringPlaybook.Domain;

/// <summary>
/// Tests for <see cref="Convention"/>.
/// </summary>
public sealed class ConventionTests
{
	[Test]
	public async Task Create_GivenValidArguments_ReturnsTheConvention()
	{
		// Arrange
		var id = TestBuilder.ConventionId("build-enforced");

		// Act
		var convention = Convention.Create(id, "Architecture is a test", VerificationTier.Unit, true);

		// Assert
		await Assert.That(convention.Id).IsEqualTo(id);
		await Assert.That(convention.Title).IsEqualTo("Architecture is a test");
		await Assert.That(convention.Tier).IsEqualTo(VerificationTier.Unit);
		await Assert.That(convention.EnforcedByBuild).IsTrue();
	}

	[Test]
	public async Task Create_GivenAPaddedTitle_TrimsIt()
	{
		// Arrange
		var id = TestBuilder.ConventionId();

		// Act
		var convention = Convention.Create(
			id,
			"   Architecture is a test   ",
			VerificationTier.Unit,
			enforcedByBuild: true
		);

		// Assert
		await Assert.That(convention.Title).IsEqualTo("Architecture is a test");
	}

	[Test]
	[Arguments("")]
	[Arguments("   ")]
	public async Task Create_GivenNoTitle_Throws(string title)
	{
		// Arrange
		var id = TestBuilder.ConventionId();

		// Act
		Action act = () => Convention.Create(id, title, VerificationTier.Unit, enforcedByBuild: true);

		// Assert
		await Assert.That(act).Throws<ArgumentException>();
	}

	[Test]
	public async Task Create_GivenATitleLongerThanTheMaximum_Throws()
	{
		// Arrange
		var id = TestBuilder.ConventionId();
		string title = new('a', Convention.MaxTitleLength + 1);

		// Act
		Action act = () => Convention.Create(id, title, VerificationTier.Unit, enforcedByBuild: true);

		// Assert
		await Assert.That(act).Throws<ArgumentException>();
	}

	[Test]
	public async Task Create_GivenNoId_Throws()
	{
		// Arrange
		// Act
		Action act = () => Convention.Create(null!, "Architecture is a test", VerificationTier.Unit, true);

		// Assert
		await Assert.That(act).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task Hydrate_GivenValuesCreateWouldReject_ReturnsThemUnvalidated()
	{
		// Arrange
		var id = ConventionId.Hydrate("Not-A-Slug");

		// Act
		var convention = Convention.Hydrate(id, "  untrimmed  ", VerificationTier.Integration, false);

		// Assert
		await Assert.That(convention.Title).IsEqualTo("  untrimmed  ");
		await Assert.That(convention.Tier).IsEqualTo(VerificationTier.Integration);
		await Assert.That(convention.EnforcedByBuild).IsFalse();
	}
}
