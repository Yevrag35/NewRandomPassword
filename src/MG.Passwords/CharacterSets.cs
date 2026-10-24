namespace MG.Passwords;

/// <summary>
/// Provides the groups of characters that <c>-InputStrings</c> accepts, and the groups that passwords are built from
/// without it.
/// </summary>
/// <remarks>
/// <para>
/// The strings are the values that 1.2.0's <c>-InputStrings</c> accepts, in the order of its
/// <see cref="ValidateSetAttribute"/>, and they're constants so that the cmdlet's attribute can name them. The
/// parameter accepts only these strings, ignoring case, so a change to one breaks every script that passes it.
/// </para>
/// <para>
/// The letters leave out l, o, I, and O, which look like 1 and 0, and also D.
/// </para>
/// </remarks>
internal static class CharacterSets
{
	/// <summary>
	/// The lowercase letters except l and o.
	/// </summary>
	internal const string Lowercase = "abcdefghijkmnpqrstuvwxyz";

	/// <summary>
	/// The uppercase letters except D, I, and O.
	/// </summary>
	internal const string Uppercase = "ABCEFGHJKLMNPQRSTUVWXYZ";

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
	/// Gets the groups that passwords are built from when <c>-InputStrings</c> isn't given.
	/// </summary>
	/// <value>
	/// A read-only list of <see cref="Lowercase"/>, <see cref="Uppercase"/>, <see cref="DigitsExceptZeroAndOne"/>, and
	/// <see cref="Symbols"/>, which hold 61 characters in all.
	/// </value>
	internal static IReadOnlyList<string> Default { get; } = [Lowercase, Uppercase, DigitsExceptZeroAndOne, Symbols];
}
