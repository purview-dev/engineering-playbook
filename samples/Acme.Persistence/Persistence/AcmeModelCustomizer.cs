using Acme.Domain;
using Acme.Persistence.ValueGeneration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Acme.Persistence;

/// <summary>Provider-specific model configuration that is not a convention.</summary>
public sealed class AcmeModelCustomizer<TProvider>(ModelCustomizerDependencies dependencies)
	: ModelCustomizer(dependencies)
	where TProvider : ISequentialGuidProvider
{
	public override void Customize(ModelBuilder modelBuilder, DbContext context)
	{
		base.Customize(modelBuilder, context);
		modelBuilder.Entity<Order>().ToTable("Orders");
	}
}
