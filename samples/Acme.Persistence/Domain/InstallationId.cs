namespace Acme.Domain;

/// <summary>The installation's identifier, and the namespace for its well-known identities.</summary>
public readonly record struct InstallationId(Guid Value) : IComparable<Guid>
{
	public int CompareTo(Guid other) => Value.CompareTo(other);

	public static InstallationId Parse(string value) => new(Guid.Parse(value));

	public override string ToString() => Value.ToString("D");
}
