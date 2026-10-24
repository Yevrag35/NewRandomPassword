namespace MG.Passwords.Tests;

public sealed class CharacterGroupsTransformationAttributeTests : IClassFixture<RunspaceFixture>
{
	private readonly CharacterGroupsTransformationAttribute _attribute = new();
	private readonly RunspaceFixture _runspace;

	public CharacterGroupsTransformationAttributeTests(RunspaceFixture runspace)
	{
		_runspace = runspace;
	}

	// 1.2.0 took a string, a char[], and an array of strings, and those keep their meaning. Command output, such as the
	// lines of Get-Content, arrives in PSObjects: one object on its own, and several in an object[] of PSObjects.
	public static TheoryData<object, string[]> Groups { get; } = new()
	{
		{ "xyz", ["xyz"] },
		{ 'x', ["x"] },
		{ new[] { 'x', 'y', 'z' }, ["xyz"] },
		{ new[] { "ab", "cd" }, ["ab", "cd"] },
		{ new object[] { "ab", 'c', new[] { 'd', 'e' } }, ["ab", "c", "de"] },
		{ new List<string> { "ab", "cd" }, ["ab", "cd"] },
		{ new object[] { PSObject.AsPSObject("ab"), PSObject.AsPSObject("cd") }, ["ab", "cd"] },
		{ Array.Empty<object>(), [] },
	};

	// Without the attribute, the [string[]] parameter would take any value as the text that it converts to, such as a
	// date's characters. A custom object unwraps to its PSCustomObject placeholder.
	public static TheoryData<object, string> Values { get; } = new()
	{
		{ 5, "System.Int32" },
		{ 5.0, "System.Double" },
		{ new DateTime(2026, 10, 24), "System.DateTime" },
		{ new Hashtable { ["a"] = "b" }, "System.Collections.Hashtable" },
		{ CreateCustomObject(), "System.Management.Automation.PSCustomObject" },
	};

	// 1.2.0 dropped every element that wasn't a string.
	public static TheoryData<object?[], int, string> Elements { get; } = new()
	{
		{ ["ab", 5], 1, "System.Int32" },
		{ ["ab", null], 1, "null" },
		{ ["ab", new object[] { "c", "d" }], 1, "System.Object[]" },
		{ ["ab", new[] { "c", "d" }], 1, "System.String[]" },
		{ [PSObject.AsPSObject(5)], 0, "System.Int32" },
	};

	[Theory]
	[MemberData(nameof(Groups))]
	public void Transform_ReturnsAStringForEachGroup(object argument, string[] expected)
	{
		Assert.Equal(expected, Assert.IsType<string[]>(_attribute.Transform(_runspace.EngineIntrinsics, argument)));
		Assert.Equal(expected, Assert.IsType<string[]>(_attribute.Transform(_runspace.EngineIntrinsics, PSObject.AsPSObject(argument))));
	}

	[Theory]
	[MemberData(nameof(Values))]
	public void Transform_RefusesAValueThatIsNeitherAGroupNorACollection(object argument, string typeName)
	{
		ArgumentTransformationMetadataException exception = Assert.Throws<ArgumentTransformationMetadataException>(
			() => _attribute.Transform(_runspace.EngineIntrinsics, argument));
		Assert.Contains(typeName, exception.Message);

		exception = Assert.Throws<ArgumentTransformationMetadataException>(
			() => _attribute.Transform(_runspace.EngineIntrinsics, PSObject.AsPSObject(argument)));
		Assert.Contains(typeName, exception.Message);
	}

	// PowerShell calls the attribute for $null too, because the parameter isn't mandatory.
	[Fact]
	public void Transform_RefusesNull()
	{
		ArgumentTransformationMetadataException exception = Assert.Throws<ArgumentTransformationMetadataException>(
			() => _attribute.Transform(_runspace.EngineIntrinsics, inputData: null!));
		Assert.Contains("null", exception.Message);
	}

	[Theory]
	[MemberData(nameof(Elements))]
	public void Transform_RefusesACollectionWithAnElementThatIsNotAGroup(object?[] argument, int index, string description)
	{
		ArgumentTransformationMetadataException exception = Assert.Throws<ArgumentTransformationMetadataException>(
			() => _attribute.Transform(_runspace.EngineIntrinsics, argument));
		Assert.Contains($"index {index}", exception.Message);
		Assert.Contains(description, exception.Message);
	}

	// An enumerator can hold a resource until it's disposed, such as the open file of File.ReadLines. A C# iterator runs
	// its finally block when it's disposed before it ends.
	[Fact]
	public void Transform_DisposesOfTheEnumeratorWhenItRefusesAnElement()
	{
		bool disposed = false;
		IEnumerable Lines()
		{
			try
			{
				yield return "ab";
				yield return 5;
				yield return "cd";
			}
			finally
			{
				disposed = true;
			}
		}

		Assert.Throws<ArgumentTransformationMetadataException>(() => _attribute.Transform(_runspace.EngineIntrinsics, Lines()));
		Assert.True(disposed);
	}

	// A custom object, as [pscustomobject]@{ Name = 'x' } makes one.
	private static PSObject CreateCustomObject()
	{
		PSObject custom = new();
		custom.Properties.Add(new PSNoteProperty("Name", "x"));
		return custom;
	}
}
