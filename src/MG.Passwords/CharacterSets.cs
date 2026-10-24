namespace MG.Passwords;

/// <summary>
/// Provides the groups of characters that <c>-InputStrings</c> accepts and <c>-CharacterSet</c> names, and the groups
/// that passwords are built from without either.
/// </summary>
/// <remarks>
/// <para>
/// The strings are the values that <c>-InputStrings</c> accepts, in the order of its
/// <see cref="ValidateSetAttribute"/>, and they're constants so that the cmdlet's attribute can name them. The
/// parameter accepts only these strings, ignoring case, so a change to one breaks every script that passes it.
/// </para>
/// <para>
/// <see cref="Default"/> holds every letter and digit. <see cref="LowercaseExceptLAndO"/>,
/// <see cref="UppercaseExceptIAndO"/>, <see cref="DigitsExceptOne"/>, and <see cref="DigitsExceptZeroAndOne"/> leave
/// out characters that are easy to mistake for others, such as l and 1, or O and 0, for a caller to choose instead.
/// </para>
/// </remarks>
internal static class CharacterSets
{
	/// <summary>
	/// The lowercase letters.
	/// </summary>
	internal const string Lowercase = "abcdefghijklmnopqrstuvwxyz";

	/// <summary>
	/// <see cref="Lowercase"/> without l and o.
	/// </summary>
	internal const string LowercaseExceptLAndO = "abcdefghijkmnpqrstuvwxyz";

	/// <summary>
	/// The uppercase letters.
	/// </summary>
	internal const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

	/// <summary>
	/// <see cref="Uppercase"/> without I and O.
	/// </summary>
	/// <remarks>
	/// The string takes the place of 1.2.0's <c>ABCEFGHJKLMNPQRSTUVWXYZ</c>, which left out D too, by mistake.
	/// </remarks>
	internal const string UppercaseExceptIAndO = "ABCDEFGHJKLMNPQRSTUVWXYZ";

	/// <summary>
	/// The digits 1 to 9, and then 0.
	/// </summary>
	internal const string Digits = "1234567890";

	/// <summary>
	/// <see cref="Digits"/> without 1.
	/// </summary>
	internal const string DigitsExceptOne = "234567890";

	/// <summary>
	/// <see cref="Digits"/> without 0 and 1.
	/// </summary>
	internal const string DigitsExceptZeroAndOne = "23456789";

	/// <summary>
	/// The symbols !, @, $, #, %, and &amp;.
	/// </summary>
	internal const string Symbols = "!@$#%&";

	/// <summary>
	/// <see cref="Symbols"/> without @ and $.
	/// </summary>
	internal const string SymbolsExceptAtAndDollar = "!#%&";

	/// <summary>
	/// Gets the groups that passwords are built from when neither <c>-CharacterSet</c> nor <c>-InputStrings</c> is given.
	/// </summary>
	/// <value>
	/// A read-only list of <see cref="Lowercase"/>, <see cref="Uppercase"/>, <see cref="Digits"/>, and
	/// <see cref="Symbols"/>, which hold 68 characters in all.
	/// </value>
	internal static IReadOnlyList<string> Default { get; } = [Lowercase, Uppercase, Digits, Symbols];
}
