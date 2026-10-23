using System.Security.Cryptography;

namespace MG.Passwords;

/// <summary>
/// Provides cryptographically secure random integers and shuffling for password generation.
/// </summary>
/// <remarks>
/// <para>
/// Every member draws from the operating system's cryptographically secure random number generator through
/// <see cref="RandomNumberGenerator"/>. The .NET 10 build calls its static methods. The .NET Standard 2.0 build, which
/// Windows PowerShell 5.1 loads, has none of those methods, so it reads random bytes from one shared instance that
/// <see cref="RandomNumberGenerator.Create()"/> returns and turns them into integers the way the .NET 10 methods do. In
/// both builds, every integer in a range has the same probability.
/// </para>
/// <para>
/// The class doesn't use <see cref="Random"/>, which isn't designed to be unpredictable. In .NET Framework, its default
/// seed is <see cref="Environment.TickCount"/>, so instances created within the same tick return the same values, and a
/// password that it generates can't be harder to guess than the seed, which has at most 2^31 values.
/// </para>
/// <para>
/// All members are thread-safe, so runspaces that run at the same time, such as those of <c>ForEach-Object -Parallel</c>,
/// can call them concurrently. In .NET Framework, the shared instance is a thread-safe
/// <see cref="RNGCryptoServiceProvider"/>. Every instance of that class that is created without arguments uses the same
/// cryptographic service provider handle, so separate instances wouldn't be any more isolated than the shared one. None
/// of them owns the handle, so disposing the shared instance would release nothing, and the class never disposes it.
/// </para>
/// </remarks>
internal static class SecureRandom
{
#if !NET
	private static readonly RandomNumberGenerator s_rng = RandomNumberGenerator.Create();
#endif

	/// <summary>
	/// Returns a random integer that is greater than or equal to 0 and less than the specified upper bound.
	/// </summary>
	/// <param name="toExclusive">The exclusive upper bound of the range.</param>
	/// <returns>An integer that is greater than or equal to 0 and less than <paramref name="toExclusive"/>.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="toExclusive"/> is less than or equal to 0.</exception>
	internal static int GetInt32(int toExclusive)
	{
		return GetInt32(0, toExclusive);
	}

	/// <summary>
	/// Returns a random integer that is greater than or equal to the specified lower bound and less than the specified
	/// upper bound.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Every integer in the range has the same probability. The method masks a random 32-bit value down to the bits that
	/// the range needs, and when the result falls outside the range, it discards the result and draws again, so it draws
	/// fewer than two values on average. Taking the remainder of a random value instead would make the lowest integers of
	/// most ranges more likely than the rest.
	/// </para>
	/// <para>
	/// In the .NET Standard 2.0 build, each call allocates a 4-byte array to draw into. A buffer shared between calls
	/// would let concurrent calls read the same random bytes.
	/// </para>
	/// </remarks>
	/// <param name="fromInclusive">The inclusive lower bound of the range.</param>
	/// <param name="toExclusive">The exclusive upper bound of the range.</param>
	/// <returns>
	/// An integer that is greater than or equal to <paramref name="fromInclusive"/> and less than
	/// <paramref name="toExclusive"/>.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="fromInclusive"/> is greater than or equal to <paramref name="toExclusive"/>.
	/// </exception>
	internal static int GetInt32(int fromInclusive, int toExclusive)
	{
#if NET
		return RandomNumberGenerator.GetInt32(fromInclusive, toExclusive);
#else
		if (fromInclusive >= toExclusive)
		{
			throw new ArgumentException("Range of random number does not contain at least one possibility.");
		}

		uint range = unchecked((uint)toExclusive - (uint)fromInclusive - 1);
		if (range == 0)
		{
			return fromInclusive;
		}

		// Sets every bit below the highest set bit of range.
		uint mask = range;
		mask |= mask >> 1;
		mask |= mask >> 2;
		mask |= mask >> 4;
		mask |= mask >> 8;
		mask |= mask >> 16;

		byte[] bytes = new byte[sizeof(uint)];
		uint result;
		do
		{
			s_rng.GetBytes(bytes);
			result = BitConverter.ToUInt32(bytes, 0) & mask;
		}
		while (result > range);

		return unchecked((int)result + fromInclusive);
#endif
	}

	/// <summary>
	/// Shuffles the elements of the specified span.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method reorders the elements in place with the Fisher-Yates algorithm, so every order has the same probability.
	/// It doesn't call <c>RandomNumberGenerator.Shuffle</c> in the .NET 10 build, so both builds shuffle with the same code.
	/// </para>
	/// <para>
	/// A span with fewer than two elements stays as it is. To keep an element where it is, such as a password's first
	/// character, shuffle a slice that leaves it out.
	/// </para>
	/// </remarks>
	/// <typeparam name="T">The type of the elements.</typeparam>
	/// <param name="values">The elements to shuffle.</param>
	internal static void Shuffle<T>(Span<T> values)
	{
		for (int i = values.Length - 1; i > 0; i--)
		{
			int j = GetInt32(i + 1);
			(values[i], values[j]) = (values[j], values[i]);
		}
	}
}
