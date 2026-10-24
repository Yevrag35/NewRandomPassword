using System.Numerics;

namespace MG.Passwords;

/// <summary>
/// Provides the argument transformation that reads an integer given to a <see cref="char"/> parameter, such as
/// <c>-FirstCharacter</c>, as its digits instead of a character code.
/// </summary>
/// <remarks>
/// <para>
/// PowerShell converts an integer argument of a <see cref="char"/> parameter to the character with that code, so
/// without the attribute, <c>-FirstCharacter 5</c> binds U+0005, a control character, and <c>-FirstCharacter 65</c>
/// binds A. The attribute turns an integer, unwrapped from its <see cref="PSObject"/> first, into its decimal text
/// before that conversion. So 0 to 9 bind as that digit, and any other integer fails with PowerShell's own error for a
/// string that isn't exactly one character long.
/// </para>
/// <para>
/// An integer is a value of an integral type that a PowerShell number can have: <see cref="sbyte"/>,
/// <see cref="byte"/>, <see cref="short"/>, <see cref="ushort"/>, <see cref="int"/>, <see cref="uint"/>,
/// <see cref="long"/>, <see cref="ulong"/>, or <see cref="BigInteger"/>. Every other argument passes through unchanged.
/// A number of any other type, such as the <see cref="double"/> 5.0, fails PowerShell's conversion to a
/// <see cref="char"/>, even when its value is whole, as a <see cref="BigInteger"/> does without the attribute.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
internal sealed class FirstCharacterTransformationAttribute : ArgumentTransformationAttribute
{
	/// <summary>
	/// Turns the specified argument into its decimal text when it's an integer.
	/// </summary>
	/// <param name="engineIntrinsics">
	/// The engine intrinsics of the runspace that binds the parameter, which the method doesn't use.
	/// </param>
	/// <param name="inputData">The argument as the caller passed it.</param>
	/// <returns>
	/// The decimal text of <paramref name="inputData"/>, in the invariant culture, when it's an integer; otherwise,
	/// <paramref name="inputData"/> itself.
	/// </returns>
	public override object Transform(EngineIntrinsics engineIntrinsics, object inputData)
	{
		object? argument = inputData is PSObject wrapped ? wrapped.BaseObject : inputData;
		return argument is sbyte or byte or short or ushort or int or uint or long or ulong or BigInteger
			? ((IFormattable)argument).ToString(null, CultureInfo.InvariantCulture)
			: inputData;
	}
}
