using Purview.EngineeringPlaybook.Domain;

namespace Purview.EngineeringPlaybook.Application;

/// <summary>
/// The persistence port the application layer depends on.
/// </summary>
/// <remarks>
/// The port lives in the layer that needs it, not in the layer that implements it. That inversion is what
/// lets <c>Infrastructure</c> depend on <c>Application</c> while <c>Application</c> stays unaware of it,
/// and it is what the architecture tests assert.
/// </remarks>
public interface IConventionRepository
{
	/// <summary>
	/// Reports whether a convention with the given identifier is already stored.
	/// </summary>
	/// <param name="id">The identifier to look for.</param>
	/// <param name="cancellationToken">Cancels the operation.</param>
	/// <returns><see langword="true"/> when the identifier is already present.</returns>
	Task<bool> ExistsAsync(ConventionId id, CancellationToken cancellationToken);

	/// <summary>
	/// Stores a convention.
	/// </summary>
	/// <param name="convention">The convention to store.</param>
	/// <param name="cancellationToken">Cancels the operation.</param>
	/// <returns>A task that completes when the convention is stored.</returns>
	Task AddAsync(Convention convention, CancellationToken cancellationToken);

	/// <summary>
	/// Lists every stored convention.
	/// </summary>
	/// <param name="cancellationToken">Cancels the operation.</param>
	/// <returns>Every stored convention.</returns>
	Task<IReadOnlyList<Convention>> ListAsync(CancellationToken cancellationToken);
}
