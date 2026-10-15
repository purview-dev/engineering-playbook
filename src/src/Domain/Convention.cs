namespace Purview.EngineeringPlaybook.Domain;

/// <summary>
/// A single engineering convention recorded by the playbook.
/// </summary>
/// <remarks>
/// <see cref="EnforcedByBuild"/> is not decoration. A convention the build refuses to let you break and
/// a convention a reviewer has to notice are different promises, and the playbook states which one it
/// is making. See <c>docs/wiki/Home.md</c>.
/// </remarks>
public sealed record Convention
{
	/// <summary>The longest accepted title.</summary>
	public const int MaxTitleLength = 120;

	Convention(ConventionId id, string title, VerificationTier tier, bool enforcedByBuild)
	{
		Id = id;
		Title = title;
		Tier = tier;
		EnforcedByBuild = enforcedByBuild;
	}

	/// <summary>The convention's stable identifier.</summary>
	public ConventionId Id { get; }

	/// <summary>The one-line statement of the convention.</summary>
	public string Title { get; }

	/// <summary>The tier the convention is proved in.</summary>
	public VerificationTier Tier { get; }

	/// <summary>Whether the build refuses a change that breaks the convention.</summary>
	public bool EnforcedByBuild { get; }

	/// <summary>
	/// Creates a convention, validating its title and normalising its surrounding whitespace.
	/// </summary>
	/// <param name="id">The convention's identifier.</param>
	/// <param name="title">The one-line statement of the convention.</param>
	/// <param name="tier">The tier the convention is proved in.</param>
	/// <param name="enforcedByBuild">Whether the build refuses a change that breaks the convention.</param>
	/// <returns>The validated convention.</returns>
	/// <exception cref="ArgumentException">The title is empty or longer than <see cref="MaxTitleLength"/>.</exception>
	public static Convention Create(ConventionId id, string title, VerificationTier tier, bool enforcedByBuild)
	{
		ArgumentNullException.ThrowIfNull(id);

		if (string.IsNullOrWhiteSpace(title))
		{
			throw new ArgumentException("A convention needs a title.", nameof(title));
		}

		var normalised = title.Trim();

		if (normalised.Length > MaxTitleLength)
		{
			throw new ArgumentException(
				$"A convention title cannot be longer than {MaxTitleLength} characters.",
				nameof(title)
			);
		}

		// The title is valid and normalised, so construct the convention.
		return new(id, normalised, tier, enforcedByBuild);
	}

	/// <summary>
	/// Rebuilds a convention from values that are already known to be valid.
	/// </summary>
	/// <param name="id">The convention's identifier.</param>
	/// <param name="title">The convention's title.</param>
	/// <param name="tier">The tier the convention is proved in.</param>
	/// <param name="enforcedByBuild">Whether the build refuses a change that breaks the convention.</param>
	/// <returns>The convention.</returns>
	public static Convention Hydrate(ConventionId id, string title, VerificationTier tier, bool enforcedByBuild) =>
		new(id, title, tier, enforcedByBuild);
}
