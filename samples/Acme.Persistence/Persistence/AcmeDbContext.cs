using Acme.Domain;
using Acme.Persistence.ValueGeneration;
using Microsoft.EntityFrameworkCore;
using DomainInstallation = Acme.Domain.Installation;

namespace Acme.Persistence;

/// <summary>The sample model. The provider is a type argument, so each store gets its own model.</summary>
public sealed class AcmeDbContext<TProvider>(DbContextOptions<AcmeDbContext<TProvider>> options) : DbContext(options)
	where TProvider : ISequentialGuidProvider
{
	public DbSet<DomainInstallation> Installations => Set<DomainInstallation>();

	public DbSet<Tenant> Tenants => Set<Tenant>();

	public DbSet<ServicePrincipal> ServicePrincipals => Set<ServicePrincipal>();

	public DbSet<Customer> Customers => Set<Customer>();

	public DbSet<Order> Orders => Set<Order>();

	public DbSet<Setting> Settings => Set<Setting>();

	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
	{
		base.ConfigureConventions(configurationBuilder);
		AcmeModelConventions.ApplySequentialGuids<TProvider>(configurationBuilder);
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.Entity<DomainInstallation>(installation =>
		{
			installation.HasKey(i => i.Id);
			installation
				.Property(i => i.Id)
				.HasConversion(id => id.Value, value => new InstallationId(value))
				.ValueGeneratedNever();
		});

		modelBuilder.Entity<Tenant>(tenant =>
		{
			tenant.HasKey(t => t.Id);
			tenant.Property(t => t.Id).ValueGeneratedNever();
			tenant.Property(t => t.Name).HasMaxLength(128);
		});

		modelBuilder.Entity<ServicePrincipal>(principal =>
		{
			principal.HasKey(p => p.Id);
			principal.Property(p => p.Id).ValueGeneratedNever();
			principal.Property(p => p.Name).HasMaxLength(128);
		});

		modelBuilder.Entity<Setting>(setting =>
		{
			setting.HasKey(s => s.Id);
			setting.Property(s => s.Id).ValueGeneratedNever();
			setting.Property(s => s.Name).HasMaxLength(128);
		});

		modelBuilder.Entity<Customer>(customer =>
		{
			customer.HasKey(c => c.Id);
			customer.Property(c => c.Name).HasMaxLength(128);
		});

		modelBuilder.Entity<Order>(order =>
		{
			order.HasKey(o => o.Id);
			order.Property(o => o.Id).HasConversion(id => id.Value, value => new OrderId(value));
			order.Property(o => o.Reference).HasMaxLength(64);
			order.HasIndex(o => o.ExternalId);
			order.HasOne<Customer>().WithMany().HasForeignKey(o => o.CustomerId);
		});
	}
}
