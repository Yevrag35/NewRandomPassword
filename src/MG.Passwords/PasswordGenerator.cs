using System.Buffers;

namespace MG.Passwords;

/// <summary>
/// Provides random passwords built from groups of characters, with a character from each group in every password that
/// has room for one.
/// </summary>
/// <remarks>
/// <para>
/// A password starts with the first character, when there is one, which counts toward the length and never moves.
/// One character from each group comes next, or, when fewer characters are left than there are groups, one from each
/// of a random subset of the groups. Characters drawn from all the groups fill the rest, and last, everything after
/// the first character is shuffled. Every draw comes from <see cref="SecureRandom"/>, so every choice is uniform.
/// </para>
/// <para>
/// Not every password that holds a character from each group is equally likely. Drawing every character from all the
/// groups and starting over until each group shows up would make them so, but the expected number of tries grows
/// fast as the groups grow in number or the passwords get shorter: about 22 for the four default groups at 4
/// characters.
/// </para>
/// <para>
/// The constructor removes repeated characters from each group, comparing them ordinally, and the fill draws from the
/// distinct characters of all the groups, so each character is equally likely within its group and within the fill. A
/// group that's listed twice stays twice, and supplies two characters to every password that has room for them.
/// </para>
/// <para>
/// Characters are UTF-16 code units, and lengths count them. A surrogate can't stand alone in valid text, so the
/// constructor refuses one in a group or as the first character.
/// </para>
/// <para>
/// An instance doesn't change after it's created, so one instance can build any number of passwords, and its members
/// are thread-safe.
/// </para>
/// <para>
/// The .NET 10 build writes each password straight into its string. The .NET Standard 2.0 build has no
/// <c>string.Create</c>, so it builds each password in a buffer, on the stack for up to 256 characters and rented from
/// <see cref="ArrayPool{T}.Shared"/> above that, copies it into the string, and clears the buffer. Neither build
/// leaves a copy of a password behind, but the returned string can't be cleared, and the garbage collector can copy it.
/// </para>
/// </remarks>
internal sealed class PasswordGenerator
{
	// The most characters or group indices that a scratch buffer holds on the stack. Longer buffers are rented.
	private const int STACKALLOC_LIMIT = 256;

	private readonly string[] _groups;
	private readonly string _allCharacters;
	private readonly char? _firstCharacter;

	/// <summary>
	/// Initializes a new <see cref="PasswordGenerator"/> instance with the specified groups of characters and first
	/// character.
	/// </summary>
	/// <remarks>
	/// The instance keeps its own copy of each group, without repeated characters, so a later change to
	/// <paramref name="groups"/> doesn't change the passwords.
	/// </remarks>
	/// <param name="groups">
	/// The groups of characters, each of which supplies a character to every password that has room for it.
	/// </param>
	/// <param name="firstCharacter">
	/// The character that starts every password, or <see langword="null"/> to let any character of the groups start one.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="groups"/> is null.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="groups"/> is empty or holds a group that is null, is empty, or holds a surrogate, or
	/// when <paramref name="firstCharacter"/> is a surrogate.
	/// </exception>
	internal PasswordGenerator(IReadOnlyList<string> groups, char? firstCharacter)
	{
		if (groups is null)
		{
			throw new ArgumentNullException(nameof(groups));
		}

		if (groups.Count == 0)
		{
			throw new ArgumentException("At least one group of characters is required.", nameof(groups));
		}

		if (firstCharacter is char first && char.IsSurrogate(first))
		{
			throw new ArgumentException("The first character can't be a surrogate.", nameof(firstCharacter));
		}

		string[] normalized = new string[groups.Count];
		for (int i = 0; i < normalized.Length; i++)
		{
			string group = groups[i];
			if (string.IsNullOrEmpty(group))
			{
				throw new ArgumentException($"The group at index {i} has no characters.", nameof(groups));
			}

			if (group.Any(char.IsSurrogate))
			{
				throw new ArgumentException($"The group at index {i} holds a surrogate.", nameof(groups));
			}

			normalized[i] = new string(group.Distinct().ToArray());
		}

		_groups = normalized;
		_allCharacters = new string(normalized.SelectMany(static group => group).Distinct().ToArray());
		_firstCharacter = firstCharacter;
	}

	/// <summary>
	/// Generates a password of the specified length.
	/// </summary>
	/// <remarks>
	/// When fewer characters follow the first character than there are groups, the groups that the password represents
	/// are chosen at random for that password alone.
	/// </remarks>
	/// <param name="length">The number of characters in the password, including the first character.</param>
	/// <returns>A new random password of <paramref name="length"/> characters.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="length"/> is less than 1.</exception>
	internal string Generate(int length)
	{
		if (length < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(length), length, "A password needs at least one character.");
		}

#if NET
		return string.Create(length, this, static (password, generator) => generator.Fill(password));
#else
		char[]? rented = length > STACKALLOC_LIMIT ? ArrayPool<char>.Shared.Rent(length) : null;
		Span<char> password = rented is null ? stackalloc char[length] : rented.AsSpan(0, length);
		try
		{
			this.Fill(password);
			return password.ToString();
		}
		finally
		{
			// clearArray: true wouldn't be enough, because the pool doesn't clear an array that it drops instead of
			// keeping, such as one longer than the 1,048,576 characters that System.Buffers keeps at most.
			password.Clear();
			if (rented is not null)
			{
				ArrayPool<char>.Shared.Return(rented);
			}
		}
#endif
	}

	/// <summary>
	/// Generates a password whose length is drawn at random from the specified range.
	/// </summary>
	/// <remarks>
	/// Each call draws a length of its own with <see cref="GetLength(int, int)"/>, so the passwords of a batch don't share
	/// one length.
	/// </remarks>
	/// <param name="minimumLength">The least number of characters in the password, 1 or more.</param>
	/// <param name="maximumLength">The greatest number of characters in the password.</param>
	/// <returns>
	/// A new random password of at least <paramref name="minimumLength"/> and at most <paramref name="maximumLength"/>
	/// characters.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="minimumLength"/> is less than 1 or greater than <paramref name="maximumLength"/>.
	/// </exception>
	internal string Generate(int minimumLength, int maximumLength)
	{
		return this.Generate(GetLength(minimumLength, maximumLength));
	}

	/// <summary>
	/// Returns a random password length from the specified range.
	/// </summary>
	/// <remarks>
	/// Every length in the range has the same probability. The method draws from <c>minimumLength - 1</c> up to, but not
	/// including, <paramref name="maximumLength"/>, and adds 1, so it never computes <c>maximumLength + 1</c>, which
	/// overflows for a range that ends at <see cref="int.MaxValue"/>.
	/// </remarks>
	/// <param name="minimumLength">The least length, 1 or more.</param>
	/// <param name="maximumLength">The greatest length.</param>
	/// <returns>
	/// A length that is greater than or equal to <paramref name="minimumLength"/> and less than or equal to
	/// <paramref name="maximumLength"/>.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="minimumLength"/> is less than 1 or greater than <paramref name="maximumLength"/>.
	/// </exception>
	internal static int GetLength(int minimumLength, int maximumLength)
	{
		if (minimumLength < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(minimumLength), minimumLength, "The minimum length is less than 1.");
		}

		if (minimumLength > maximumLength)
		{
			throw new ArgumentOutOfRangeException(
				nameof(minimumLength), minimumLength, "The minimum length is greater than the maximum length.");
		}

		return SecureRandom.GetInt32(minimumLength - 1, maximumLength) + 1;
	}

	/// <summary>
	/// Fills the specified span with a new password.
	/// </summary>
	/// <remarks>
	/// Both builds fill their passwords with this method, and only how <see cref="Generate(int)"/> makes the string
	/// differs between them.
	/// </remarks>
	/// <param name="password">The characters of the password, one or more.</param>
	private void Fill(Span<char> password)
	{
		Span<char> shuffled = password;
		if (_firstCharacter is char first)
		{
			password[0] = first;
			shuffled = password.Slice(1);
		}

		if (shuffled.Length >= _groups.Length)
		{
			for (int i = 0; i < _groups.Length; i++)
			{
				shuffled[i] = PickFrom(_groups[i]);
			}

			for (int i = _groups.Length; i < shuffled.Length; i++)
			{
				shuffled[i] = PickFrom(_allCharacters);
			}
		}
		else if (!shuffled.IsEmpty)
		{
			this.FillFromRandomGroups(shuffled);
		}

		SecureRandom.Shuffle(shuffled);
	}

	/// <summary>
	/// Fills the specified span with a character from each of as many groups as it has characters, choosing the groups at
	/// random.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method shuffles the indices of all the groups and takes the first ones, so every subset of the groups of that
	/// size has the same probability. The indices live on the stack for up to 256 groups and in an array rented from
	/// <see cref="ArrayPool{T}.Shared"/> above that.
	/// </para>
	/// <para>
	/// The method clears the indices before it returns, because the groups that a password represents tell something about
	/// the password.
	/// </para>
	/// </remarks>
	/// <param name="password">The characters to fill, fewer than there are groups.</param>
	private void FillFromRandomGroups(Span<char> password)
	{
		int[]? rented = _groups.Length > STACKALLOC_LIMIT ? ArrayPool<int>.Shared.Rent(_groups.Length) : null;
		Span<int> indices = rented is null ? stackalloc int[_groups.Length] : rented.AsSpan(0, _groups.Length);
		try
		{
			for (int i = 0; i < indices.Length; i++)
			{
				indices[i] = i;
			}

			SecureRandom.Shuffle(indices);
			for (int i = 0; i < password.Length; i++)
			{
				password[i] = PickFrom(_groups[indices[i]]);
			}
		}
		finally
		{
			indices.Clear();
			if (rented is not null)
			{
				ArrayPool<int>.Shared.Return(rented);
			}
		}
	}

	/// <summary>
	/// Returns a random character of the specified string.
	/// </summary>
	/// <param name="characters">The characters to choose from, one or more.</param>
	/// <returns>
	/// The character at a random index of <paramref name="characters"/>, where every index is equally likely.
	/// </returns>
	private static char PickFrom(string characters)
	{
		return characters[SecureRandom.GetInt32(characters.Length)];
	}
}
