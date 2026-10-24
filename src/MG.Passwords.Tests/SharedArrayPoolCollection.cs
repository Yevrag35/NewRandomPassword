using System.Buffers;

namespace MG.Passwords.Tests;

/// <summary>
/// Provides the test collection for tests that check which arrays <see cref="ArrayPool{T}.Shared"/> hands out, which runs
/// after the other tests, on its own.
/// </summary>
/// <remarks>
/// In the .NET Framework 4.8 tests, the shared pool comes from System.Buffers, and all threads rent from and return to
/// the same stacks of arrays, so a test that ran at the same time could take or return the array that a test checks. In
/// .NET 10, the pool hands a thread back the array that the thread returned last, whatever other threads do.
/// </remarks>
[CollectionDefinition(nameof(SharedArrayPoolCollection), DisableParallelization = true)]
public sealed class SharedArrayPoolCollection
{
}
