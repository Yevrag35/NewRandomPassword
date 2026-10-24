namespace MG.Passwords;

/// <summary>
/// Provides the validation that refuses a value of a <see cref="FlagsAttribute"/> enum that names none of the enum's
/// values or holds a bit that none of them defines, for parameters such as <c>-CharacterSet</c>.
/// </summary>
/// <remarks>
/// <para>
/// PowerShell refuses an unknown name or an undefined bit when it converts an argument to the enum, but an argument that
/// already has the enum's type binds as it is, such as <c>[enum]::ToObject($type, 64)</c>. So does 0, whether or not one
/// of the enum's values is 0, as from a <c>-band</c> of two values that share no bits. PowerShell runs a validation
/// attribute on every value that binds, so the attribute refuses each of those.
/// </para>
/// <para>
/// A bit counts as defined when any of the enum's values holds it, so a value that combines others needs no bit of its
/// own. The attribute compares bits, not values, so it suits only a <see cref="FlagsAttribute"/> enum.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
internal sealed class ValidateFlagsAttribute : ValidateArgumentsAttribute
{
	/// <summary>
	/// Refuses the value of the parameter when it names none of its enum's values or holds a bit that none of them defines.
	/// </summary>
	/// <remarks>
	/// The method only calls <see cref="ValidateFlags(object)"/>, which tests can call directly.
	/// </remarks>
	/// <param name="arguments">The enum value that the parameter is bound to.</param>
	/// <param name="engineIntrinsics">
	/// The engine intrinsics of the runspace that binds the parameter, which the method doesn't use.
	/// </param>
	/// <exception cref="ValidationMetadataException">
	/// Thrown when <paramref name="arguments"/> is 0, holds a bit that none of its enum's values defines, or isn't an enum
	/// value.
	/// </exception>
	protected override void Validate(object arguments, EngineIntrinsics engineIntrinsics)
	{
		ValidateFlags(arguments);
	}

	/// <summary>
	/// Refuses the specified value when it names none of its enum's values, holds a bit that none of them defines, or isn't
	/// an enum value.
	/// </summary>
	/// <param name="value">A value of a <see cref="FlagsAttribute"/> enum.</param>
	/// <exception cref="ValidationMetadataException">
	/// Thrown when <paramref name="value"/> is 0, holds a bit that none of its enum's values defines, or isn't an enum value.
	/// </exception>
	internal static void ValidateFlags(object? value)
	{
		if (value is not Enum flags)
		{
			throw new ValidationMetadataException("Expected an enum value.");
		}

		Type type = flags.GetType();
		ulong defined = 0;
		foreach (Enum member in Enum.GetValues(type))
		{
			defined |= GetBits(member);
		}

		string names = string.Join(", ", Enum.GetNames(type));
		ulong bits = GetBits(flags);
		if (bits == 0)
		{
			throw new ValidationMetadataException(
				$"The value 0 names none of the values of {type.Name}. Specify one or more of these values: {names}.");
		}

		if ((bits & ~defined) != 0)
		{
			throw new ValidationMetadataException(
				$"The value {flags} holds a bit that none of the values of {type.Name} defines. Specify one or more of these "
				+ $"values: {names}.");
		}
	}

	/// <summary>
	/// Gets the bits of the specified enum value, whatever the enum's underlying type.
	/// </summary>
	/// <remarks>
	/// A value of a signed type is sign-extended, as every value of the same enum is, so their bits line up.
	/// </remarks>
	/// <param name="value">The enum value.</param>
	/// <returns>The bits of <paramref name="value"/>, widened to 64.</returns>
	private static ulong GetBits(Enum value)
	{
		return value.GetTypeCode() switch
		{
			TypeCode.Byte or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64
				=> Convert.ToUInt64(value, CultureInfo.InvariantCulture),
			_ => unchecked((ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture)),
		};
	}
}
