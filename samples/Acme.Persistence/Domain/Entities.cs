namespace Acme.Domain;

/// <summary>The installation row. Its id is configuration, not a generated value.</summary>
public sealed record Installation(InstallationId Id, DateTime CreatedAtUtc);

/// <summary>A system tenant, keyed by a UUIDv5 derived from the installation id.</summary>
public sealed class Tenant(Guid id, string name)
{
	public Guid Id { get; } = id;

	public string Name { get; } = name;
}

/// <summary>A system principal, keyed by a UUIDv5 derived from the installation id.</summary>
public sealed class ServicePrincipal(Guid id, string name)
{
	public Guid Id { get; } = id;

	public string Name { get; } = name;
}

/// <summary>A customer keyed by the sequential-GUID convention.</summary>
public sealed class Customer
{
	public Guid Id { get; init; }

	public string Name { get; init; } = string.Empty;
}

/// <summary>A scalar value-object identifier, to prove the generator honours a converter.</summary>
public readonly record struct OrderId(Guid Value) : IComparable<Guid>
{
	public int CompareTo(Guid other) => Value.CompareTo(other);

	public override string ToString() => Value.ToString("D");
}

/// <summary>An order whose key is a value object and whose foreign key must not be generated.</summary>
public sealed class Order
{
	public OrderId Id { get; init; }

	public Guid CustomerId { get; init; }

	public Guid? ExternalId { get; init; }

	public string Reference { get; init; } = string.Empty;
}

/// <summary>A GUID key with explicit configuration, to prove the convention leaves it alone.</summary>
public sealed class Setting
{
	public Guid Id { get; init; }

	public string Name { get; init; } = string.Empty;
}
