namespace MG.Passwords.Tests;

public sealed class ValidateFlagsAttributeTests
{
	// A, B, and C each hold a bit of their own, and AB holds none of its own.
	[Flags]
	public enum Letters
	{
		A = 1,
		B = 2,
		AB = A | B,
		C = 4
	}

	// The top bit of a ulong, which Convert.ToInt64 refuses.
	[Flags]
	public enum Wide : ulong
	{
		Low = 1,
		Top = 1UL << 63
	}

	// A negative sbyte, which Convert.ToUInt64 refuses.
	[Flags]
	public enum Narrow : sbyte
	{
		Low = 1,
		Top = sbyte.MinValue
	}

	[Theory]
	[InlineData(Letters.A)]
	[InlineData(Letters.AB)]
	[InlineData(Letters.A | Letters.C)]
	[InlineData(Letters.AB | Letters.C)]
	public void ValidateFlags_AcceptsAValueThatTheMembersCover(Letters value)
	{
		ValidateFlagsAttribute.ValidateFlags(value);
	}

	// PowerShell binds 0 whether or not a member is 0. Enum.GetNames lists the members in the order of their values.
	[Fact]
	public void ValidateFlags_RefusesZero()
	{
		ValidationMetadataException exception = Assert.Throws<ValidationMetadataException>(
			() => ValidateFlagsAttribute.ValidateFlags((Letters)0));
		Assert.Contains("A, B, AB, C", exception.Message);
	}

	// No member holds 8, and 9 is A with 8. Enum.ToString gives either one as its number.
	[Theory]
	[InlineData(8)]
	[InlineData(9)]
	public void ValidateFlags_RefusesABitThatNoMemberDefines(int bits)
	{
		ValidationMetadataException exception = Assert.Throws<ValidationMetadataException>(
			() => ValidateFlagsAttribute.ValidateFlags((Letters)bits));
		Assert.Contains($"The value {bits} ", exception.Message);
	}

	[Fact]
	public void ValidateFlags_ComparesTheBitsOfEachUnderlyingType()
	{
		ValidateFlagsAttribute.ValidateFlags(Wide.Top | Wide.Low);
		Assert.Throws<ValidationMetadataException>(() => ValidateFlagsAttribute.ValidateFlags((Wide)2));

		ValidateFlagsAttribute.ValidateFlags(Narrow.Top | Narrow.Low);
		Assert.Throws<ValidationMetadataException>(() => ValidateFlagsAttribute.ValidateFlags((Narrow)2));
	}

	[Theory]
	[InlineData(1)]
	[InlineData("A")]
	[InlineData(null)]
	public void ValidateFlags_RefusesAValueThatIsntAnEnum(object? value)
	{
		Assert.Throws<ValidationMetadataException>(() => ValidateFlagsAttribute.ValidateFlags(value));
	}
}
