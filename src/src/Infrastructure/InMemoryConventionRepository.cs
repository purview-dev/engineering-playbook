using System.Collections.Concurrent;
using Purview.EngineeringPlaybook.Application;
using Purview.EngineeringPlaybook.Domain;

namespace Purview.EngineeringPlaybook.Infrastructure;

/// <summary>
/// An in-memory <see cref="IConventionRepository"/>.
/// </summary>
/// <remarks>
/// It exists so the reference implementation has a real adapter on the far side of the port without
/// dragging in a database. The point being demonstrated is the shape — an adapter that implements a port
/// owned by the layer above it — not the storage.
/// </remarks>
public sealed class InMemoryConventionRepository : IConventionRepository
{
	readonly ConcurrentDictionary<string, Convention> _conventions = new(StringComparer.Ordinal);

	/// <inheritdoc />
	public Task<bool> ExistsAsync(ConventionId id, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(id);
		cancellationToken.ThrowIfCancellationRequested();

		return Task.FromResult(_conventions.ContainsKey(id.Value));
	}

	/// <inheritdoc />
	public Task AddAsync(Convention convention, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(convention);
		cancellationToken.ThrowIfCancellationRequested();

		if (!_conventions.TryAdd(convention.Id.Value, convention))
		{
			throw new InvalidOperationException($"Convention '{convention.Id}' is already registered.");
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<Convention>> ListAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		return Task.FromResult<IReadOnlyList<Convention>>([.. _conventions.Values]);
	}
}
