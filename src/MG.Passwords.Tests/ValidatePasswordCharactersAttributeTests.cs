namespace MG.Passwords.Tests;

public sealed class ValidatePasswordCharactersAttributeTests
{
	// Both ends of the two ranges of control characters, U+0000 to U+001F and U+007F to U+009F, of the high surrogates,
	// U+D800 to U+DBFF, and of the low ones, U+DC00 to U+DFFF. Each character arrives as a char and goes into a group here,
	// because an attribute stores a string argument as UTF-8, which has no encoding for a lone surrogate.
	[Theory]
	[InlineData('\u0000')]
	[InlineData('\u001F')]
	[InlineData('\u007F')]
	[InlineData('\u009F')]
	[InlineData('\uD800')]
	[InlineData('\uDBFF')]
	[InlineData('\uDC00')]
	[InlineData('\uDFFF')]
	public void ValidateCharacters_RefusesAControlCharacterOrASurrogate(char character)
	{
		string codePoint = $"U+{(int)character:X4}";

		ValidationMetadataException exception = Assert.Throws<ValidationMetadataException>(
			() => ValidatePasswordCharactersAttribute.ValidateCharacters(character));
		Assert.Contains(codePoint, exception.Message);

		exception = Assert.Throws<ValidationMetadataException>(
			() => ValidatePasswordCharactersAttribute.ValidateCharacters($"ab{character}cd"));
		Assert.Contains(codePoint, exception.Message);
	}

	// The space after U+001F, the tilde before U+007F, the no-break space after U+009F, and the characters on either side
	// of the surrogates.
	[Theory]
	[InlineData('\u0020')]
	[InlineData('\u007E')]
	[InlineData('\u00A0')]
	[InlineData('\uD7FF')]
	[InlineData('\uE000')]
	public void ValidateCharacters_AcceptsTheCharactersAroundThoseRanges(char character)
	{
		ValidatePasswordCharactersAttribute.ValidateCharacters(character);
		ValidatePasswordCharactersAttribute.ValidateCharacters($"ab{character}cd");
	}

	// An emoji is valid text, but the generator would split its surrogate pair.
	[Fact]
	public void ValidateCharacters_RefusesASurrogatePair()
	{
		ValidationMetadataException exception = Assert.Throws<ValidationMetadataException>(
			() => ValidatePasswordCharactersAttribute.ValidateCharacters(char.ConvertFromUtf32(0x1F600)));
		Assert.Contains("U+D83D", exception.Message);
	}

	// An empty group couldn't supply the character that each group supplies to a password.
	[Fact]
	public void ValidateCharacters_RefusesAnEmptyGroup()
	{
		Assert.Throws<ValidationMetadataException>(() => ValidatePasswordCharactersAttribute.ValidateCharacters(string.Empty));
	}
}
