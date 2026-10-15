using Purview.EngineeringPlaybook.Domain;

namespace Purview.EngineeringPlaybook.Application;

/// <summary>
/// The application service that owns the rules for maintaining the convention catalogue.
/// </summary>
/// <remarks>
/// The class holds no infrastructure of its own: it takes the <see cref="IConventionRepository"/> port,
/// which is what makes it testable without a database and what keeps the dependency direction one-way.
/// </remarks>
/// <remarks>
/// Initialises the service.
/// </remarks>
/// <param name="repository">The persistence port.</param>
/// <exception cref="ArgumentNullException"><paramref name="repository"/> is <see langword="null"/>.</exception>
public sealed class ConventionCatalogue(IConventionRepository repository)
{
	readonly IConventionRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));

	/// <summary>
	/// Registers a convention, rejecting an identifier that is already in use.
	/// </summary>
	/// <param name="convention">The convention to register.</param>
	/// <param name="cancellationToken">Cancels the operation.</param>
	/// <returns>A task that completes when the convention is registered.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="convention"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">The identifier is already registered.</exception>
	public async Task RegisterAsync(Convention convention, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(convention);

		var alreadyRegistered = await _repository.ExistsAsync(convention.Id, cancellationToken).ConfigureAwait(false);

		if (alreadyRegistered)
		{
			throw new InvalidOperationException($"Convention '{convention.Id}' is already registered.");
		}

		await _repository.AddAsync(convention, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Lists the conventions the build refuses to let you break, ordered by identifier.
	/// </summary>
	/// <param name="cancellationToken">Cancels the operation.</param>
	/// <returns>The build-enforced conventions.</returns>
	public async Task<IReadOnlyList<Convention>> ListEnforcedByBuildAsync(CancellationToken cancellationToken)
	{
		var conventions = await _repository.ListAsync(cancellationToken).ConfigureAwait(false);

		return
		[
			.. conventions
				.Where(convention => convention.EnforcedByBuild)
				.OrderBy(convention => convention.Id.Value, StringComparer.Ordinal),
		];
	}
}
