using Purview.EngineeringPlaybook.Builders;
using Purview.EngineeringPlaybook.Domain;

namespace Purview.EngineeringPlaybook.Application;

/// <summary>
/// Tests for <see cref="ConventionCatalogue"/>.
/// </summary>
/// <remarks>
/// The dependency the service declares is an interface it owns, so the test supplies a
/// <c>TUnit.Mocks</c> substitute and asserts on the interaction rather than on a database.
/// </remarks>
public sealed class ConventionCatalogueTests
{
	[Test]
	public async Task RegisterAsync_GivenAnUnregisteredConvention_StoresIt(CancellationToken cancellationToken)
	{
		// Arrange
		var repository = CreateRepository();
		repository.ExistsAsync(Any(), Any()).Returns(false);
		var catalogue = CreateCatalogue(repository.Object);
		var convention = TestBuilder.EnforcedConvention();

		// Act
		await catalogue.RegisterAsync(convention, cancellationToken);

		// Assert
		repository.AddAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task RegisterAsync_GivenAnAlreadyRegisteredConvention_Throws(CancellationToken cancellationToken)
	{
		// Arrange
		var repository = CreateRepository();
		repository.ExistsAsync(Any(), Any()).Returns(true);
		var catalogue = CreateCatalogue(repository.Object);

		// Act
		Task Act() => catalogue.RegisterAsync(TestBuilder.EnforcedConvention(), cancellationToken);

		// Assert
		await Assert.That(Act).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task RegisterAsync_GivenAnAlreadyRegisteredConvention_DoesNotStoreIt(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var repository = CreateRepository();
		repository.ExistsAsync(Any(), Any()).Returns(true);
		var catalogue = CreateCatalogue(repository.Object);

		// Act
		await Assert
			.That(async () => await catalogue.RegisterAsync(TestBuilder.EnforcedConvention(), cancellationToken))
			.Throws<InvalidOperationException>();

		// Assert
		repository.AddAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task RegisterAsync_GivenNoConvention_Throws(CancellationToken cancellationToken)
	{
		// Arrange
		var catalogue = CreateCatalogue();

		// Act
		Task Act() => catalogue.RegisterAsync(null!, cancellationToken);

		// Assert
		await Assert.That(Act).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task ListEnforcedByBuildAsync_GivenAMixedCatalogue_ReturnsTheBuildEnforcedConventionsInOrder(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var repository = CreateRepository();
		Convention[] conventions =
		[
			TestBuilder.Convention(id: TestBuilder.ConventionId("review-only"), enforcedByBuild: false),
			TestBuilder.Convention(id: TestBuilder.ConventionId("build-enforced"), enforcedByBuild: true),
			TestBuilder.Convention(id: TestBuilder.ConventionId("another-enforced"), enforcedByBuild: true),
		];
		repository.ListAsync(Any()).Returns(conventions);
		var catalogue = CreateCatalogue(repository.Object);

		// Act
		var result = await catalogue.ListEnforcedByBuildAsync(cancellationToken);

		// Assert
		await Assert.That(result).Count().IsEqualTo(2);
		await Assert.That(result[0].Id.Value).IsEqualTo("another-enforced");
		await Assert.That(result[1].Id.Value).IsEqualTo("build-enforced");
	}

	[Test]
	public async Task ListEnforcedByBuildAsync_GivenNoEnforcedConventions_ReturnsAnEmptyList(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var repository = CreateRepository();
		Convention[] conventions = [TestBuilder.Convention(enforcedByBuild: false)];
		repository.ListAsync(Any()).Returns(conventions);
		var catalogue = CreateCatalogue(repository.Object);

		// Act
		var result = await catalogue.ListEnforcedByBuildAsync(cancellationToken);

		// Assert
		await Assert.That(result).IsEmpty();
	}

	[Test]
	public async Task ConventionCatalogue_GivenNoRepository_Throws()
	{
		// Arrange
		// Act
		static void Act() => _ = new ConventionCatalogue(null!);

		// Assert
		await Assert.That(Act).Throws<ArgumentNullException>();
	}

	/// <summary>Creates the repository substitute.</summary>
	/// <returns>The substitute.</returns>
	static Mock<IConventionRepository> CreateRepository() => Mock.Of<IConventionRepository>();

	/// <summary>Creates the system under test.</summary>
	/// <param name="repository">The repository port; omitted creates a substitute.</param>
	/// <returns>The catalogue.</returns>
	static ConventionCatalogue CreateCatalogue(IConventionRepository? repository = null) =>
		new(repository ?? CreateRepository().Object);
}
