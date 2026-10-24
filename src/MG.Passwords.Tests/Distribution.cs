namespace MG.Passwords.Tests;

/// <summary>
/// Provides an assertion about how often random outcomes occur.
/// </summary>
internal static class Distribution
{
	/// <summary>
	/// Verifies that the specified counts are what equally likely outcomes give.
	/// </summary>
	/// <remarks>
	/// The assertion fails when a count is more than 7 standard deviations from the average count of equally likely
	/// outcomes, where the standard deviation is that of independent trials. When the outcomes are equally likely, that
	/// happens to a count about once in 400 billion tries, so the tests don't fail by chance.
	/// </remarks>
	/// <param name="counts">The number of times that each outcome occurred.</param>
	internal static void AssertEquallyLikely(ICollection<int> counts)
	{
		int trials = counts.Sum();
		double p = 1.0 / counts.Count;
		double expected = trials * p;
		double tolerance = 7 * Math.Sqrt(trials * p * (1 - p));
		Assert.All(counts, count => Assert.InRange(count, expected - tolerance, expected + tolerance));
	}
}
