namespace MG.Passwords.Tests;

public sealed class SecureRandomTests
{
	// The ranges reach each branch of the .NET Standard 2.0 build: one value, sizes that are powers of two and never draw
	// again, sizes that do, negative bounds, and both ends of int. With 5 values or fewer, the chance that 1,000 draws miss
	// one of them is below 10^-96.
	[Theory]
	[InlineData(5, 6)]
	[InlineData(0, 2)]
	[InlineData(0, 3)]
	[InlineData(0, 4)]
	[InlineData(0, 5)]
	[InlineData(10, 13)]
	[InlineData(-2, 2)]
	[InlineData(int.MinValue, int.MinValue + 3)]
	[InlineData(int.MaxValue - 3, int.MaxValue)]
	public void GetInt32_ReturnsEveryValueInTheRangeAndNoOther(int fromInclusive, int toExclusive)
	{
		SortedSet<int> seen = new();
		for (int i = 0; i < 1_000; i++)
		{
			seen.Add(SecureRandom.GetInt32(fromInclusive, toExclusive));
		}

		Assert.Equal(Enumerable.Range(fromInclusive, toExclusive - fromInclusive), seen);
	}

	// Before the lower bound is added back, about half of these draws are above int.MaxValue, and the .NET Standard 2.0
	// build turns them into the range's non-negative values by wrapping around.
	[Fact]
	public void GetInt32_ReturnsBothSignsFromANearlyFullRange()
	{
		bool sawNegative = false;
		bool sawNonNegative = false;
		for (int i = 0; i < 1_000; i++)
		{
			int value = SecureRandom.GetInt32(int.MinValue, int.MaxValue);
			sawNegative |= value < 0;
			sawNonNegative |= value >= 0;
		}

		Assert.True(sawNegative, "No draw was negative.");
		Assert.True(sawNonNegative, "No draw was non-negative.");
	}

	// Without the check that comes first, the unsigned arithmetic of the .NET Standard 2.0 build would turn
	// (int.MaxValue, int.MinValue) into a range of one value and (0, 0) into all of int.
	[Theory]
	[InlineData(0, 0)]
	[InlineData(6, 5)]
	[InlineData(int.MaxValue, int.MinValue)]
	public void GetInt32_ThrowsForAnEmptyRange(int fromInclusive, int toExclusive)
	{
		Assert.Throws<ArgumentException>(() => SecureRandom.GetInt32(fromInclusive, toExclusive));
	}

	// 61 is the size of the legacy default character set. Taking byte % 61 instead would make 12 of the values 25% more
	// likely than the other 49 (256 = 4 * 61 + 12), which puts their counts about 11 standard deviations high.
	[Fact]
	public void GetInt32_ReturnsEachValueEquallyOften()
	{
		int[] counts = new int[61];
		for (int i = 0; i < 200_000; i++)
		{
			counts[SecureRandom.GetInt32(counts.Length)]++;
		}

		AssertEquallyLikely(counts);
	}

	// Drawing j below i instead of up to i (Sattolo's algorithm) produces only the 2 rotations of 3 elements, and drawing j
	// from the whole span makes some orders more likely than others.
	[Fact]
	public void Shuffle_ProducesEveryOrderEquallyOften()
	{
		Dictionary<string, int> counts = new(StringComparer.Ordinal);
		for (int i = 0; i < 60_000; i++)
		{
			char[] letters = ['a', 'b', 'c'];
			SecureRandom.Shuffle(letters.AsSpan());

			string order = new(letters);
			counts.TryGetValue(order, out int count);
			counts[order] = count + 1;
		}

		string[] expected = ["abc", "acb", "bac", "bca", "cab", "cba"];
		Assert.Equal(expected, counts.Keys.OrderBy(static order => order, StringComparer.Ordinal));
		AssertEquallyLikely(counts.Values);
	}

	// With -FirstCharacter, a password of 1 character leaves an empty slice to shuffle, and one of 2 characters leaves a
	// slice of one.
	[Theory]
	[InlineData("")]
	[InlineData("a")]
	public void Shuffle_LeavesFewerThanTwoElementsAsTheyAre(string text)
	{
		char[] letters = text.ToCharArray();
		SecureRandom.Shuffle(letters.AsSpan());

		Assert.Equal(text, new string(letters));
	}

	// Fails when a count is more than 7 standard deviations from the average count of equally likely outcomes. When the
	// outcomes are equally likely, that happens to a count about once in 400 billion tries, so the tests don't fail by
	// chance.
	private static void AssertEquallyLikely(ICollection<int> counts)
	{
		int trials = counts.Sum();
		double p = 1.0 / counts.Count;
		double expected = trials * p;
		double tolerance = 7 * Math.Sqrt(trials * p * (1 - p));
		Assert.All(counts, count => Assert.InRange(count, expected - tolerance, expected + tolerance));
	}
}
