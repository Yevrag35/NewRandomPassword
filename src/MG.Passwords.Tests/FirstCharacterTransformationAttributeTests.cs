using System.Numerics;

namespace MG.Passwords.Tests;

public sealed class FirstCharacterTransformationAttributeTests : IClassFixture<RunspaceFixture>
{
	private readonly FirstCharacterTransformationAttribute _attribute = new();
	private readonly RunspaceFixture _runspace;

	public FirstCharacterTransformationAttributeTests(RunspaceFixture runspace)
	{
		_runspace = runspace;
	}

	// PowerShell converts an integer of the integral types to the character with that code, so that 5 is U+0005, and it
	// can't convert a BigInteger to a char at all. The digits of 0 to 9 convert to that digit, and those of any other
	// integer fail PowerShell's conversion, because they aren't one character.
	public static TheoryData<object, string> Integers { get; } = new()
	{
		{ (sbyte)5, "5" },
		{ (byte)5, "5" },
		{ (short)5, "5" },
		{ (ushort)5, "5" },
		{ 5, "5" },
		{ 5U, "5" },
		{ 5L, "5" },
		{ 5UL, "5" },
		{ new BigInteger(5), "5" },
		{ 0, "0" },
		{ 9, "9" },
		{ 10, "10" },
		{ 65, "65" },
		{ -1, "-1" },
	};

	// PowerShell converts a char, and a string of one character, and it refuses every other number, such as 5.0, even when
	// its value is whole.
	public static TheoryData<object> OtherArguments { get; } = new()
	{
		'x',
		"x",
		"65",
		5.0,
		5.0F,
		5.0M,
		PSObject.AsPSObject("x"),
		PSObject.AsPSObject(5.0),
	};

	[Theory]
	[MemberData(nameof(Integers))]
	public void Transform_ReturnsTheDigitsOfAnInteger(object argument, string expected)
	{
		Assert.Equal(expected, Assert.IsType<string>(_attribute.Transform(_runspace.EngineIntrinsics, argument)));
		Assert.Equal(expected, Assert.IsType<string>(_attribute.Transform(_runspace.EngineIntrinsics, PSObject.AsPSObject(argument))));
	}

	[Theory]
	[MemberData(nameof(OtherArguments))]
	public void Transform_ReturnsAnyOtherArgumentUnchanged(object argument)
	{
		Assert.Same(argument, _attribute.Transform(_runspace.EngineIntrinsics, argument));
	}

	// PowerShell calls the attribute for $null too, because the parameter isn't mandatory, and then refuses to convert it
	// to a char.
	[Fact]
	public void Transform_ReturnsNullUnchanged()
	{
		Assert.Null(_attribute.Transform(_runspace.EngineIntrinsics, inputData: null!));
	}
}
