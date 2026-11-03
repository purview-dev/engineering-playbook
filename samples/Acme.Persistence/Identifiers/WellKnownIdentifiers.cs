using Acme.Domain;

namespace Acme.Installation;

/// <summary>Well-known identities scoped to one installation.</summary>
public sealed record WellKnownIdentifiers(InstallationId Installation)
{
	public Guid SystemTenantId => Uuid5.Create(Installation.Value, "system-tenant");

	public Guid SystemPrincipalId => Uuid5.Create(Installation.Value, "system-service-principal");

	public Guid ReferenceRow(string entity, string code) =>
		Uuid5.Create(Installation.Value, $"{entity}:{code}".ToLowerInvariant());
}
