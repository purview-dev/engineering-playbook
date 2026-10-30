using Acme.Domain;
using Acme.Persistence;
using Acme.Persistence.ValueGeneration;
using Microsoft.EntityFrameworkCore;
using DomainInstallation = Acme.Domain.Installation;

namespace Acme.Installation;

/// <summary>What a bootstrap run did, so the caller can prove the second run is a no-op.</summary>
public sealed record BootstrapResult(
	InstallationId InstallationId,
	Guid SystemTenantId,
	Guid SystemPrincipalId,
	bool Created
);

/// <summary>Read-or-insert bootstrap for one installation and its well-known identities.</summary>
public sealed class InstallationBootstrap<TProvider>(AcmeDbContext<TProvider> dbContext)
	where TProvider : ISequentialGuidProvider
{
	public async Task<BootstrapResult> RunAsync(Guid installationId, CancellationToken cancellationToken)
	{
		var configured = new InstallationId(installationId);
		var persisted = await dbContext
			.Installations.OrderBy(installation => installation.CreatedAtUtc)
			.FirstOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		// The persisted identity is authoritative; a configured one that disagrees is a
		// deployment error, not a reason to rewrite the row.
		if (persisted is not null && persisted.Id != configured)
		{
			throw new InvalidOperationException(
				$"Configured installation id '{configured.Value}' does not match "
					+ $"the persisted id '{persisted.Id.Value}'."
			);
		}

		var created = persisted is null;
		var installation = persisted ?? new DomainInstallation(configured, DateTime.UtcNow);
		if (created)
			dbContext.Installations.Add(installation);

		var wellKnown = new WellKnownIdentifiers(installation.Id);
		if (
			!await dbContext
				.Tenants.AnyAsync(tenant => tenant.Id == wellKnown.SystemTenantId, cancellationToken)
				.ConfigureAwait(false)
		)
		{
			dbContext.Tenants.Add(new Tenant(wellKnown.SystemTenantId, "System"));
		}

		if (
			!await dbContext
				.ServicePrincipals.AnyAsync(principal => principal.Id == wellKnown.SystemPrincipalId, cancellationToken)
				.ConfigureAwait(false)
		)
		{
			dbContext.ServicePrincipals.Add(new ServicePrincipal(wellKnown.SystemPrincipalId, "System"));
		}

		// One commit: either the installation and its system identities exist, or none do.
		await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		return new BootstrapResult(installation.Id, wellKnown.SystemTenantId, wellKnown.SystemPrincipalId, created);
	}
}
