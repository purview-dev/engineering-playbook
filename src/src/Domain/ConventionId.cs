namespace Purview.EngineeringPlaybook.Domain;

/// <summary>
/// The stable, human-readable identifier of a convention.
/// </summary>
/// <remarks>
/// A value object: it is constructed only through <see cref="Create"/> (which validates) or
/// <see cref="Hydrate"/> (which trusts a value that already round-tripped through a boundary), and it
/// compares by value because it is a <see langword="record"/>.
/// </remarks>
public sealed record ConventionId
{
	/// <summary>The longest accepted identifier.</summary>
	public const int MaxLength = 64;

	ConventionId(string value) => Value = value;

	/// <summary>The canonical, URL-safe form of the identifier.</summary>
	public string Value { get; }

	/// <summary>
	/// Creates an identifier, rejecting anything that is not a lowercase, hyphen-separated slug.
	/// </summary>
	/// <param name="value">The candidate identifier.</param>
	/// <returns>The validated identifier.</returns>
	/// <exception cref="ArgumentException">The value is not a valid slug.</exception>
	public static ConventionId Create(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new ArgumentException("A convention identifier cannot be empty.", nameof(value));
		}

		if (value.Length > MaxLength)
		{
			throw new ArgumentException(
				$"A convention identifier cannot be longer than {MaxLength} characters.",
				nameof(value)
			);
		}

		if (value[0] == '-' || value[^1] == '-')
		{
			throw new ArgumentException($"'{value}' cannot start or end with a hyphen.", nameof(value));
		}

		foreach (var character in value)
		{
			var isLowercaseLetter = character is >= 'a' and <= 'z';
			var isDigit = character is >= '0' and <= '9';

			if (!isLowercaseLetter && !isDigit && character != '-')
			{
				throw new ArgumentException(
					$"'{value}' must contain only lowercase letters, digits and hyphens.",
					nameof(value)
				);
			}
		}

		return new ConventionId(value);
	}

	/// <summary>
	/// Rebuilds an identifier from a value that is already known to be valid.
	/// </summary>
	/// <remarks>
	/// Hydration is deliberately not validation: it exists for values that have already passed
	/// <see cref="Create"/> in this process, or that were produced by a trusted boundary. A
	/// <see langword="null"/> or empty value is still rejected, because a hydrated identifier that
	/// holds nothing is a bug rather than a data-quality problem.
	/// </remarks>
	/// <param name="value">A value that is already known to be a valid slug.</param>
	/// <returns>The identifier.</returns>
	/// <exception cref="ArgumentException">The value is null, empty or whitespace.</exception>
	public static ConventionId Hydrate(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new ArgumentException("A hydrated convention identifier cannot be empty.", nameof(value));
		}

		// The value is already known to be valid, so we don't need to validate it again.
		return new(value);
	}

	/// <inheritdoc />
	public override string ToString() => Value;
}
