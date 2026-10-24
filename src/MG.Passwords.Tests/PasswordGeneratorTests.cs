using System.Buffers;

namespace MG.Passwords.Tests;

[Collection(nameof(SharedArrayPoolCollection))]
public sealed class PasswordGeneratorTests
{
	// The 68 characters of the default groups.
	private const string DEFAULT_CHARACTERS = CharacterSets.Lowercase + CharacterSets.Uppercase
		+ CharacterSets.Digits + CharacterSets.Symbols;

	// 1.2.0 sorted the first character in with the rest, in a random direction, so it came last in about half the
	// passwords (#3). A shuffle that took in index 0 would move the first character in half the passwords of 2 characters,
	// and in more of the longer ones. The .NET Standard 2.0 build builds a password of 257 characters in a rented array.
	[Theory]
	[InlineData(2)]
	[InlineData(8)]
	[InlineData(257)]
	public void Generate_StartsEveryPasswordWithTheFirstCharacter(int length)
	{
		PasswordGenerator generator = new(CharacterSets.Default, '~');
		for (int i = 0; i < 1_000; i++)
		{
			Assert.Equal('~', generator.Generate(length)[0]);
		}
	}

	// The first character counts toward the length, as in 1.2.0.
	[Fact]
	public void Generate_ReturnsTheFirstCharacterAloneForOneCharacter()
	{
		PasswordGenerator generator = new(CharacterSets.Default, '~');

		Assert.Equal("~", generator.Generate(1));
	}

	// Four characters, after the first character when there is one, leave no room for the fill, and drawing them from all
	// the default groups together would leave out a group in about 19 of every 20 passwords. The first character 'a'
	// belongs to a group, but it doesn't stand in for that group's character.
	[Theory]
	[InlineData(4, null)]
	[InlineData(20, null)]
	[InlineData(5, 'a')]
	[InlineData(20, 'a')]
	public void Generate_HoldsACharacterFromEachGroup(int length, char? firstCharacter)
	{
		PasswordGenerator generator = new(CharacterSets.Default, firstCharacter);
		for (int i = 0; i < 1_000; i++)
		{
			string password = generator.Generate(length);
			string drawn = firstCharacter is null ? password : password.Substring(1);
			foreach (string group in CharacterSets.Default)
			{
				Assert.True(drawn.IndexOfAny(group.ToCharArray()) >= 0, $"{password} holds no character of {group}.");
			}
		}
	}

	// With one character in each group, a password's characters name the groups that it represents. 1.2.0 always took the
	// first groups, so the last ones never showed up, and choosing a group for each character on its own would repeat a
	// group in some passwords. With a first character, a password of 5 characters leaves 4 for the 5 groups.
	[Theory]
	[InlineData(1, null)]
	[InlineData(3, null)]
	[InlineData(2, '~')]
	[InlineData(5, '~')]
	public void Generate_ChoosesGroupsAtRandomWhenTheyDoNotAllFit(int length, char? firstCharacter)
	{
		PasswordGenerator generator = new(["a", "b", "c", "d", "e"], firstCharacter);
		HashSet<char> seen = new();
		for (int i = 0; i < 1_000; i++)
		{
			string password = generator.Generate(length);
			string drawn = firstCharacter is null ? password : password.Substring(1);
			Assert.Distinct(drawn);
			seen.UnionWith(drawn);
		}

		Assert.Equal("abcde", new string(seen.OrderBy(static character => character).ToArray()));
	}

	// The group indices live on the stack for up to 256 groups and in a rented array above that, which can be longer than
	// the indices it holds. With one character in each group, a password's characters name the groups that it represents.
	[Theory]
	[InlineData(256)]
	[InlineData(257)]
	public void Generate_ChoosesGroupsAtRandomFromManyGroups(int groupCount)
	{
		string[] groups = CreateGroups(groupCount);
		PasswordGenerator generator = new(groups, firstCharacter: null);
		HashSet<char> seen = new();
		for (int i = 0; i < 2_000; i++)
		{
			string password = generator.Generate(10);
			Assert.Distinct(password);
			seen.UnionWith(password);
		}

		Assert.Equal(groups, seen.OrderBy(static character => character).Select(static character => character.ToString()));
	}

	// A password of 1 character is the character that its one group supplies. The group lists a and c twice, and drawing
	// from it as written would make each of them twice as likely as b.
	[Fact]
	public void Generate_DrawsEachDistinctCharacterOfAGroupEquallyOften()
	{
		PasswordGenerator generator = new(["aabcc"], firstCharacter: null);
		Dictionary<char, int> counts = new();
		for (int i = 0; i < 30_000; i++)
		{
			char character = generator.Generate(1)[0];
			counts.TryGetValue(character, out int count);
			counts[character] = count + 1;
		}

		Assert.Equal("abc", new string(counts.Keys.OrderBy(static character => character).ToArray()));
		Distribution.AssertEquallyLikely(counts.Values);
	}

	// These groups supply each of their characters once per password on average, so only the fill could make one of them
	// more likely than the others: drawing it from the groups' characters concatenated, as 1.2.0 did, would make b, c, and
	// d twice as likely as a. The characters that the groups supply vary less than independent draws, so the counts stray
	// past the tolerance even more rarely than AssertEquallyLikely says.
	[Fact]
	public void Generate_FillsFromTheDistinctCharactersOfAllTheGroups()
	{
		PasswordGenerator generator = new(["a", "bc", "bd", "cd"], firstCharacter: null);
		Dictionary<char, int> counts = new();
		for (int i = 0; i < 2_000; i++)
		{
			foreach (char character in generator.Generate(100))
			{
				counts.TryGetValue(character, out int count);
				counts[character] = count + 1;
			}
		}

		Assert.Equal("abcd", new string(counts.Keys.OrderBy(static character => character).ToArray()));
		Distribution.AssertEquallyLikely(counts.Values);
	}

	// Collapsing the repeated group into one would leave room for the fill, which would draw a letter instead of the
	// second digit in about 3 of every 4 passwords.
	[Fact]
	public void Generate_GuaranteesTwoCharactersForAGroupListedTwice()
	{
		PasswordGenerator generator = new(
			[CharacterSets.Lowercase, CharacterSets.DigitsExceptZeroAndOne, CharacterSets.DigitsExceptZeroAndOne],
			firstCharacter: null);
		for (int i = 0; i < 1_000; i++)
		{
			Assert.Equal(2, generator.Generate(3).Count(char.IsDigit));
		}
	}

	// The archived PasswordHolder garbled characters outside ASCII, which it passed through an Encoding. These come from
	// Latin-1 (n with a tilde and e with an acute accent), Cyrillic (Zhe), and CJK, and the .NET Standard 2.0 build builds a
	// password of 257 characters in a rented array.
	[Theory]
	[InlineData(4)]
	[InlineData(257)]
	public void Generate_KeepsCharactersOutsideAscii(int length)
	{
		PasswordGenerator generator = new(["\u00E9", "\u0416", "\u4E2D"], firstCharacter: '\u00F1');
		string password = generator.Generate(length);

		Assert.Equal('\u00F1', password[0]);
		Assert.Equal("\u00E9\u0416\u4E2D", new string(password.Substring(1).Distinct().OrderBy(static character => character).ToArray()));
	}

	// The .NET Standard 2.0 build builds a password of up to 256 characters on the stack and a longer one in a rented
	// array, which can be longer than the password.
	[Theory]
	[InlineData(1)]
	[InlineData(256)]
	[InlineData(257)]
	public void Generate_ReturnsAPasswordOfTheLength(int length)
	{
		PasswordGenerator generator = new(CharacterSets.Default, firstCharacter: null);
		string password = generator.Generate(length);

		Assert.Equal(length, password.Length);
		Assert.All(password, character => Assert.Contains(character, DEFAULT_CHARACTERS));
	}

	// GetLength never computes maximumLength + 1, which overflows for a range that ends at int.MaxValue. With 5 lengths or
	// fewer, the chance that 1,000 draws miss one of them is below 10^-96.
	[Theory]
	[InlineData(1, 1)]
	[InlineData(1, 5)]
	[InlineData(4, 6)]
	[InlineData(int.MaxValue - 4, int.MaxValue)]
	[InlineData(int.MaxValue, int.MaxValue)]
	public void GetLength_ReturnsEveryLengthInTheRangeAndNoOther(int minimumLength, int maximumLength)
	{
		SortedSet<int> seen = new();
		for (int i = 0; i < 1_000; i++)
		{
			seen.Add(PasswordGenerator.GetLength(minimumLength, maximumLength));
		}

		Assert.Equal(Enumerable.Range(minimumLength, maximumLength - minimumLength + 1), seen);
	}

#if NETFRAMEWORK
	// The .NET 10 build writes each password straight into its string, so only the .NET Standard 2.0 build has a buffer to
	// clear, and it builds a password of 257 characters in an array that it rents from the shared pool.
	[Fact]
	public void Generate_ClearsTheRentedPasswordBuffer()
	{
		PasswordGenerator generator = new(CharacterSets.Default, firstCharacter: null);
		char[] probe = PrepareProbe('*', 257);

		_ = generator.Generate(257);

		AssertCleared(probe, 257);
	}
#endif

	// With fewer characters than its 257 groups, a password's group indices are shuffled in an array that both builds rent
	// from the shared pool.
	[Fact]
	public void Generate_ClearsTheRentedGroupIndices()
	{
		PasswordGenerator generator = new(CreateGroups(257), firstCharacter: null);
		int[] probe = PrepareProbe(-1, 257);

		_ = generator.Generate(10);

		AssertCleared(probe, 257);
	}

	[Fact]
	public void Constructor_ThrowsForNullGroups()
	{
		Assert.Throws<ArgumentNullException>("groups", () => new PasswordGenerator(groups: null!, firstCharacter: null));
	}

	[Fact]
	public void Constructor_ThrowsForNoGroups()
	{
		Assert.Throws<ArgumentException>("groups", () => new PasswordGenerator([], firstCharacter: null));
	}

	// A group has to supply a character to every password that has room for one.
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void Constructor_ThrowsForAGroupWithoutCharacters(string? group)
	{
		Assert.Throws<ArgumentException>("groups", () => new PasswordGenerator(["abc", group!], firstCharacter: null));
	}

	// Each surrogate arrives as a char and goes into the group here, because an attribute stores a string argument as
	// UTF-8, which has no encoding for a lone surrogate.
	[Theory]
	[InlineData('\uD800')]
	[InlineData('\uDC00')]
	public void Constructor_ThrowsForAGroupWithALoneSurrogate(char surrogate)
	{
		Assert.Throws<ArgumentException>("groups", () => new PasswordGenerator(["abc", $"d{surrogate}e"], firstCharacter: null));
	}

	// An emoji is valid text, but the generator would split its surrogate pair, as 1.2.0 did.
	[Fact]
	public void Constructor_ThrowsForAGroupWithASurrogatePair()
	{
		Assert.Throws<ArgumentException>(
			"groups", () => new PasswordGenerator(["abc", char.ConvertFromUtf32(0x1F600)], firstCharacter: null));
	}

	[Theory]
	[InlineData('\uD800')]
	[InlineData('\uDFFF')]
	public void Constructor_ThrowsForASurrogateFirstCharacter(char firstCharacter)
	{
		Assert.Throws<ArgumentException>("firstCharacter", () => new PasswordGenerator(CharacterSets.Default, firstCharacter));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(int.MinValue)]
	public void Generate_ThrowsForALengthBelowOne(int length)
	{
		PasswordGenerator generator = new(CharacterSets.Default, firstCharacter: null);

		Assert.Throws<ArgumentOutOfRangeException>("length", () => generator.Generate(length));
	}

	// The minimum of int.MinValue would overflow if GetLength subtracted 1 from it before it checked it.
	[Theory]
	[InlineData(0, 5)]
	[InlineData(-1, 5)]
	[InlineData(int.MinValue, 5)]
	[InlineData(6, 5)]
	[InlineData(int.MaxValue, 1)]
	public void GetLength_ThrowsForAMinimumBelowOneOrAboveTheMaximum(int minimumLength, int maximumLength)
	{
		Assert.Throws<ArgumentOutOfRangeException>("minimumLength", () => PasswordGenerator.GetLength(minimumLength, maximumLength));
	}

	[Theory]
	[InlineData(0, 5)]
	[InlineData(6, 5)]
	public void Generate_ThrowsForAMinimumBelowOneOrAboveTheMaximum(int minimumLength, int maximumLength)
	{
		PasswordGenerator generator = new(CharacterSets.Default, firstCharacter: null);

		Assert.Throws<ArgumentOutOfRangeException>("minimumLength", () => generator.Generate(minimumLength, maximumLength));
	}

	// One group for each character from U+0100 on, all of them letters.
	private static string[] CreateGroups(int count)
	{
		return Enumerable.Range(0x100, count).Select(static code => ((char)code).ToString()).ToArray();
	}

	// Rents an array from the shared pool, fills it with a value other than the default, so that an array that the
	// generator never wrote to doesn't pass for a cleared one, and returns it. Then it checks that the pool hands out the
	// array that it got back last, which makes the probe the array that the generator rents next.
	private static T[] PrepareProbe<T>(T fill, int minimumLength)
	{
		T[] probe = ArrayPool<T>.Shared.Rent(minimumLength);
		probe.AsSpan().Fill(fill);
		ArrayPool<T>.Shared.Return(probe);

		T[] next = ArrayPool<T>.Shared.Rent(minimumLength);
		ArrayPool<T>.Shared.Return(next);
		Assert.Same(probe, next);
		return probe;
	}

	// Checks that the pool hands out the probe again, so the generator rented it and gave it back, and that the elements
	// that the generator wrote are cleared.
	private static void AssertCleared<T>(T[] probe, int length)
	{
		T[] next = ArrayPool<T>.Shared.Rent(length);
		try
		{
			Assert.Same(probe, next);
			Assert.Equal(new T[length], next.Take(length));
		}
		finally
		{
			ArrayPool<T>.Shared.Return(next);
		}
	}
}
