using Purview.Architecture.Testing;

namespace Purview.EngineeringPlaybook.Architecture;

/// <summary>
/// Enforces the repository's architecture policy as tests.
/// </summary>
/// <remarks>
/// <para>
/// The rule set lives in <c>Purview.Architecture.Testing</c>, so the dependency matrix, naming,
/// sibling-independence, composition, forbidden-package, cycle, test-reference and waiver rules are
/// defined once rather than copied here. This class is the whole of the adoption: the base class
/// reads <c>.config/architecture-policy.json</c> and derives every rule from it.
/// </para>
/// <para>
/// <b><c>[InheritsTests]</c> is load-bearing.</b> TUnit does not walk base types when it discovers
/// tests, so without it this class compiles, the run is green and nothing is enforced. TUnit's
/// analyzer reports <c>TUnit0030</c>; treat that as an error rather than a suggestion.
/// </para>
/// <para>
/// <see cref="DependencyDirectionTests"/> stays hand-written and separate: it reads compiled assembly
/// metadata, which catches a dependency that arrived indirectly and which the policy graph - built
/// from declared <c>ProjectReference</c>s - cannot see. See <c>docs/wiki/Architecture-As-Tests.md</c>.
/// </para>
/// </remarks>
[InheritsTests]
public sealed class ArchitectureTests : PurviewArchitectureTests;
