namespace MG.Passwords;

/// <summary>
/// Represents the named groups of characters that <c>New-RandomPassword -CharacterSet</c> builds passwords from.
/// </summary>
/// <remarks>
/// <para>
/// Each value is a group of characters, and the values combine, as in <c>-CharacterSet Lowercase, Digits</c>, so that
/// every password holds a character from each of their groups when it has room for one. <see cref="NoLookAlikes"/>
/// stands for three groups that leave out characters that are easy to mistake for others, and it takes the place of
/// <see cref="Lowercase"/>, <see cref="Uppercase"/>, and <see cref="Digits"/>, whether or not they're given too.
/// </para>
/// <para>
/// No value is 0, and <c>New-RandomPassword</c> refuses 0, and any bit that no value defines, when it binds the
/// parameter.
/// </para>
/// <para>
/// PowerShell binds a single name by any prefix that begins that name alone, such as <c>Low</c> for
/// <see cref="Lowercase"/>, so a value added later whose name begins the same way as an existing one would break a script
/// that shortened the existing name.
/// </para>
/// </remarks>
[Flags]
public enum CharacterSet
{
	/// <summary>
	/// The lowercase letters, a to z.
	/// </summary>
	Lowercase = 1,

	/// <summary>
	/// The uppercase letters, A to Z.
	/// </summary>
	Uppercase = 2,

	/// <summary>
	/// The digits, 0 to 9.
	/// </summary>
	Digits = 4,

	/// <summary>
	/// The symbols !, @, $, #, %, and &amp;.
	/// </summary>
	Symbols = 8,

	/// <summary>
	/// Three groups that leave out characters that are easy to mistake for others: the lowercase letters except l and o,
	/// the uppercase letters except I and O, and the digits except 0 and 1.
	/// </summary>
	/// <remarks>
	/// The value takes the place of <see cref="Lowercase"/>, <see cref="Uppercase"/>, and <see cref="Digits"/>, and it
	/// combines with <see cref="Symbols"/>.
	/// </remarks>
	NoLookAlikes = 16
}
