namespace MG.Passwords;

/// <summary>
/// Provides the argument transformation that turns the argument of a parameter such as <c>-ExtraCharacters</c> into
/// groups of characters, one string per group.
/// </summary>
/// <remarks>
/// <para>
/// A string is one group, a <see cref="char"/> is a group of one character, and a <see cref="char"/> array is one group
/// of its characters. Any other value that <see cref="LanguagePrimitives.GetEnumerator(object)"/> enumerates is a
/// collection, and each of its elements is one group, by the same rules. The attribute unwraps the argument and each
/// element from its <see cref="PSObject"/> first, because command output, such as the lines that <c>Get-Content</c>
/// outputs, arrives wrapped.
/// </para>
/// <para>
/// The attribute refuses every other value, such as a number, a hashtable, <see langword="null"/>, or a collection
/// inside the collection. Without it, a <c>[string[]]</c> parameter would take any value as the text that it converts
/// to, such as the characters of a date, and it would split a <see cref="char"/> array into one group per character.
/// </para>
/// <para>
/// The attribute checks only the form of the argument. Which characters a group may hold is left to
/// <see cref="ValidatePasswordCharactersAttribute"/>, which PowerShell runs after the transformation.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
internal sealed class CharacterGroupsTransformationAttribute : ArgumentTransformationAttribute
{
	/// <summary>
	/// Turns the specified argument into groups of characters.
	/// </summary>
	/// <remarks>
	/// The method enumerates a collection once and then disposes of its enumerator, which can hold a resource, such as an
	/// open file.
	/// </remarks>
	/// <param name="engineIntrinsics">
	/// The engine intrinsics of the runspace that binds the parameter, which the method doesn't use.
	/// </param>
	/// <param name="inputData">The argument as the caller passed it.</param>
	/// <returns>A new array that holds one string for each group of characters in <paramref name="inputData"/>.</returns>
	/// <exception cref="ArgumentTransformationMetadataException">
	/// Thrown when <paramref name="inputData"/> is null, is neither a string, a character, a character array, nor a
	/// collection, or is a collection that holds an element that is neither a string, a character, nor a character array.
	/// </exception>
	public override object Transform(EngineIntrinsics engineIntrinsics, object inputData)
	{
		object? argument = Unwrap(inputData);
		if (TryGetGroup(argument, out string? group))
		{
			return new[] { group };
		}

		IEnumerator? elements = LanguagePrimitives.GetEnumerator(argument);
		if (elements is null)
		{
			throw new ArgumentTransformationMetadataException(
				"Expected a string, a char, a char[], or a collection of them, but the argument is "
				+ $"{Describe(argument)}.");
		}

		try
		{
			List<string> groups = [];
			while (elements.MoveNext())
			{
				object? element = Unwrap(elements.Current);
				if (!TryGetGroup(element, out group))
				{
					throw new ArgumentTransformationMetadataException(
						"Expected each element to be a string, a char, or a char[], but the element at index "
						+ $"{groups.Count} is {Describe(element)}.");
				}

				groups.Add(group);
			}

			return groups.ToArray();
		}
		finally
		{
			(elements as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Returns the value that the specified object wraps, or the object itself when it isn't a <see cref="PSObject"/>.
	/// </summary>
	/// <remarks>
	/// A custom object unwraps to its <see cref="PSCustomObject"/> placeholder, which isn't a group of characters, so the
	/// transformation refuses it.
	/// </remarks>
	/// <param name="value">The value to unwrap, or <see langword="null"/>.</param>
	/// <returns>
	/// The <see cref="PSObject.BaseObject"/> of <paramref name="value"/> when it's a <see cref="PSObject"/>; otherwise,
	/// <paramref name="value"/>.
	/// </returns>
	private static object? Unwrap(object? value)
	{
		return value is PSObject wrapped ? wrapped.BaseObject : value;
	}

	/// <summary>
	/// Gets the characters of the specified value when the value is a group of characters on its own.
	/// </summary>
	/// <param name="value">An argument or an element of one, unwrapped from its <see cref="PSObject"/>.</param>
	/// <param name="group">
	/// When the method returns <see langword="true"/>, the characters of <paramref name="value"/>; otherwise,
	/// <see langword="null"/>.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if <paramref name="value"/> is a string, a <see cref="char"/>, or a <see cref="char"/> array;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	private static bool TryGetGroup(object? value, [NotNullWhen(true)] out string? group)
	{
		group = value switch
		{
			string characters => characters,
			char character => character.ToString(),
			char[] characters => new string(characters),
			_ => null,
		};

		return group is not null;
	}

	/// <summary>
	/// Describes the specified value for an error message.
	/// </summary>
	/// <param name="value">The value that isn't a group of characters, or <see langword="null"/>.</param>
	/// <returns>"null", or "a value of type" followed by the full name of the type of <paramref name="value"/>.</returns>
	private static string Describe(object? value)
	{
		return value is null ? "null" : $"a value of type {value.GetType().FullName}";
	}
}
