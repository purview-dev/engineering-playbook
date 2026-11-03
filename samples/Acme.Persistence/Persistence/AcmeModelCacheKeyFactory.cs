using Acme.Persistence.ValueGeneration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Acme.Persistence;

/// <summary>Separates the model cache by context type, design time and strategy.</summary>
public sealed class AcmeModelCacheKeyFactory : IModelCacheKeyFactory
{
	public object Create(DbContext context, bool designTime) =>
		(
			context.GetType(),
			designTime,
			context.GetService<IDbContextOptions>().FindExtension<SequentialGuidOptionsExtension>()?.StrategyName
		);
}
