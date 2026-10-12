namespace Purview.EngineeringPlaybook.Domain;

/// <summary>
/// The tier a convention is proved in.
/// </summary>
/// <remarks>
/// The tier is part of the convention's identity because it decides where the convention is allowed to
/// fail: a <see cref="Unit"/> rule has to be fast and deterministic enough to run on every commit,
/// while an <see cref="Integration"/> rule may need a real external resource and is therefore selected
/// rather than assumed. See <c>docs/wiki/Verification-Model.md</c>.
/// </remarks>
public enum VerificationTier
{
	/// <summary>Fast, deterministic, and free of external resources.</summary>
	Unit,

	/// <summary>Exercises a real external resource, and is selected rather than run by default.</summary>
	Integration,

	/// <summary>Exercises the assembled system from the outside.</summary>
	EndToEnd,
}
