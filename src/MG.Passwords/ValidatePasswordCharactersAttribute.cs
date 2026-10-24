namespace MG.Passwords;

/// <summary>
/// Provides the validation that refuses characters that a password can't hold, for parameters such as
/// <c>-ExtraCharacters</c> and <c>-FirstCharacter</c>.
/// </summary>
/// <remarks>
/// <para>
/// The attribute refuses a control character, meaning one for which <see cref="char.IsControl(char)"/> is
/// <see langword="true"/>: U+0000 to U+001F and U+007F to U+009F. A password with one can't be typed and rarely survives
/// being pasted. It also refuses a surrogate, which can't stand alone in valid text, and an empty string, which as a
/// group of characters couldn't supply a character to a password.
/// </para>
/// <para>
/// PowerShell runs a validation attribute after the parameter's transformation attributes and its conversion to the
/// parameter's type, so the attribute checks each final value: every string of a <c>[string[]]</c> parameter, or the
/// <see cref="char"/> of a <c>[char]</c> parameter. The base class refuses a <see langword="null"/> argument before it
/// calls <see cref="ValidateElement(object)"/>.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
internal sealed class ValidatePasswordCharactersAttribute : ValidateEnumeratedArgumentsAttribute
{
	/// <summary>
	/// Refuses one value of the parameter when a password can't hold its characters.
	/// </summary>
	/// <remarks>
	/// The method only calls <see cref="ValidateCharacters(object)"/>, which tests can call directly.
	/// </remarks>
	/// <param name="element">A string or a <see cref="char"/> that the parameter is bound to.</param>
	/// <exception cref="ValidationMetadataException">
	/// Thrown when <paramref name="element"/> is an empty string, holds a control character or a surrogate, or is neither
	/// a string nor a character.
	/// </exception>
	protected override void ValidateElement(object element)
	{
		ValidateCharacters(element);
	}

	/// <summary>
	/// Refuses the specified value when it's empty, holds a character that a password can't hold, or is neither a string
	/// nor a character.
	/// </summary>
	/// <param name="element">A string or a <see cref="char"/>.</param>
	/// <exception cref="ValidationMetadataException">
	/// Thrown when <paramref name="element"/> is an empty string, holds a control character or a surrogate, or is neither
	/// a string nor a character.
	/// </exception>
	internal static void ValidateCharacters(object? element)
	{
		switch (element)
		{
			case char character:
				ValidateCharacter(character);
				break;

			case string { Length: 0 }:
				throw new ValidationMetadataException("A group of characters can't be empty.");

			case string characters:
				foreach (char character in characters)
				{
					ValidateCharacter(character);
				}

				break;

			default:
				throw new ValidationMetadataException("Expected a string or a char.");
		}
	}

	/// <summary>
	/// Refuses the specified character when a password can't hold it.
	/// </summary>
	/// <param name="character">The character to check.</param>
	/// <exception cref="ValidationMetadataException">
	/// Thrown when <paramref name="character"/> is a control character or a surrogate.
	/// </exception>
	private static void ValidateCharacter(char character)
	{
		if (char.IsControl(character))
		{
			throw new ValidationMetadataException(
				$"U+{(int)character:X4} is a control character. Passwords can't hold control characters.");
		}

		if (char.IsSurrogate(character))
		{
			throw new ValidationMetadataException(
				$"U+{(int)character:X4} is half of a surrogate pair. Passwords can't hold characters that take a "
				+ "surrogate pair, such as emoji.");
		}
	}
}
