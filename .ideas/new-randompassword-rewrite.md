# Plan: Rewrite New-RandomPassword

> **Status:** Phases 1 and 2 done 2026-10-24. `MG.Passwords` builds the passwords, and both shell projects ship `New-RandomPassword` and `rpas` with the 1.2.0 surface and the behavior that **What changes from 1.2.0** lists. Phase 3 (help and README) and Phase 4 (tests) remain.

## Context

The overhaul on `dev` replaced the 2019 projects with three new ones, and none of them has a cmdlet yet. `MG.Passwords`, the engine (`netstandard2.0` and `net10.0`), holds only `SecureRandom`. (Phase 1 has since added `CharacterSets` and `PasswordGenerator`.) `MG.Passwords.Shell` (`net10.0`, for PowerShell 7.6 or later) and `MG.Passwords.Shell-NETFramework` (`net48`, for Windows PowerShell 5.1, compiling every `.cs` file of the other) each build an `MG.Passwords.Shell.dll` with no commands in it. (Phase 2 has since added `New-RandomPassword` to both, and the engine attributes that check its input.) Everything around them is in place: the manifest and `NewRandomPassword.psm1` load the right build for each edition, the 5.1 build resolves its `System.Memory` dependencies, and both test suites run in both editions. This plan tracks writing the cmdlet that those pieces exist to ship: a `New-RandomPassword` that keeps what 1.2.0 users call, fixes the bugs that 1.2.0 shipped with, and runs on the new engine.

This file is working notes. Check what it says about the code against the current tree before you build on it.

References:

- `CLAUDE.md`'s reference points. 1.2.0 on the Gallery, built from `accaf2e`, is the compatibility baseline. #3 fixed two generation bugs in the legacy source after 1.2.0 shipped, and a rewrite has to be tested for both again. The `archive/DifferentApproach` tag is an abandoned 2020 rewrite whose `PasswordHolder` must not be copied.
- ~~`legacy/NRP-DotNet/NewRandomPassword.cs`~~ `NRP-DotNet/NewRandomPassword.cs` at #3 (`8e90f21`), the legacy cmdlet with the #3 fixes. The Gallery build predates them. (#4 moved it under `legacy/`, which was removed after Phase 2, so only git history keeps it.)
- The repo's `nrp-pwsh-internals` skill, for measured engine behavior. The binding facts this plan relies on were measured for it and added to its `references/binding.md`, and the alias-export fact to its `references/assembly-loading.md`.
- The repo's `nrp-testing` skill, for how both suites run and where a test goes.

## Audit

Done on 2026-10-23 on #4's branch, before `SecureRandom.cs` and `SecureRandomTests.cs` were committed there, with .NET SDK 10.0.402, PowerShell 7.6.8, and Windows PowerShell 5.1.26100.9711.

### What exists and what the rewrite needs

| Piece | State | Needed |
|---|---|---|
| Uniform secure random integers and shuffling | `src/MG.Passwords/SecureRandom.cs`, documented, with 17 tests per target in `src/MG.Passwords.Tests/SecureRandomTests.cs`. ~~Neither file is committed.~~ (#4 committed both.) | ~~Commit them (Phase 1)~~ Nothing |
| Character sets | ~~None~~ (Phase 1 added `src/MG.Passwords/CharacterSets.cs`.) | ~~`CharacterSets` (Phase 1)~~ Nothing |
| Password generation | ~~None~~ (Phase 1 added `src/MG.Passwords/PasswordGenerator.cs`.) | ~~`PasswordGenerator` (Phase 1)~~ Nothing |
| The cmdlet | ~~None. Both shell projects build an empty `MG.Passwords.Shell.dll`.~~ (Phase 2 added `src/MG.Passwords.Shell/NewRandomPasswordCommand.cs`.) | ~~`NewRandomPasswordCommand` (Phase 2)~~ Nothing |
| Converting and checking `-ExtraCharacters` and `-FirstCharacter` input | ~~None~~ (Phase 2 added three attributes to `src/MG.Passwords/`.) | ~~Two transformation attributes and a validation attribute (Phase 2)~~ Nothing |
| Loading dependencies in Windows PowerShell 5.1 | `ModuleInitializer` in the `net48` shell | Nothing, as long as the engine adds no package |
| Manifest and `.psm1` | Version 2.0.0. Exports `New-RandomPassword` and `rpas`, and loads the build for each edition. (Phase 2 added the release notes.) | ~~Release notes (Phase 2), and the~~ The help file in `FileList` (Phase 3) |
| Help | None. 1.2.0 had none either. | A MAML help file (Phase 3) |
| Tests | `Module.Tests.ps1`, the runspace fixture, and `SecureRandomTests` | Cmdlet and engine tests (Phase 4) |

Checked:

- Both shell projects build in Debug with no warnings. The `net48` output holds `MG.Passwords.dll`, `MG.Passwords.Shell.dll`, and the four `System.*` DLLs that the manifest's `FileList` names.
- `dotnet test` passes 36 of 36: 18 per target, `net10.0` and `net48`.
- `tests/Invoke-Tests.ps1` passes 5 of 9 in each edition. All four failures come from the missing cmdlet: the build exports no cmdlet and no alias, `rpas` doesn't resolve, and `New-RandomPassword` auto-loads from the installed 1.2.0 instead, the trap that the `nrp-testing` skill warns about.
- A binary cmdlet's `[Alias]` alias reaches the caller through a manifest whose `RootModule` is a `.psm1` that imports the DLL, in both editions, measured with a throwaway module. 1.2.0 named its DLL as the `RootModule`, so this path is new in 2.0.0, and it needs no change to `NewRandomPassword.psm1`.

### The 1.2.0 surface

Measured in both editions from the installed Gallery build, whose two DLLs are byte-identical to the ones committed in `accaf2e`. The editions agree on every item.

| Parameter | Type | Parameter sets | Position | Mandatory | Aliases | Validation | Default |
|---|---|---|---|---|---|---|---|
| `PasswordLength` | `int` | StaticLength | 0 | No | `l`, `length` | `ValidateRange(1, 2147483647)` | 8 |
| `Count` | `int` | Both | 1 | No | | `ValidateRange(1, 2147483647)` | 1 |
| `MinimumLength` | `int` | RandomLength | | Yes | `min`, `minlength` | `ValidateRange(1, 2147483646)` | |
| `MaximumLength` | `int` | RandomLength | | Yes | `max`, `maxlength` | `ValidateRange(2, 2147483647)` | |
| `InputStrings` | `string[]` | Both | | No | `strings` | `ValidateSet`, 7 values, ignoring case | The four default groups |
| `ExtraCharacters` | `object` | Both | | No | `chars`, `ec` | | |
| `OnlyUseExtraCharacters` | `switch` | Both | | No | `onlyec` | | |
| `FirstCharacter` | `char` | Both | | No | `fc`, `FirstChar` | | |

- The command is `New-RandomPassword`, with the alias `rpas`, the default parameter set StaticLength, and `[OutputType(typeof(string))]`. No parameter takes pipeline input.
- `-InputStrings` takes these values, in this order: `abcdefghijkmnpqrstuvwxyz`, `ABCEFGHJKLMNPQRSTUVWXYZ`, `1234567890`, `234567890`, `23456789`, `!@$#%&`, and `!#%&`. `ValidateSet` ignores case but passes the value on as typed, so `-InputStrings 'ABCDEFGHIJKMNPQRSTUVWXYZ'`, the lowercase set in capitals, is accepted and yields capitals that include D and I.
- Without `-InputStrings`, the groups are `abcdefghijkmnpqrstuvwxyz`, `ABCEFGHJKLMNPQRSTUVWXYZ`, `23456789`, and `!@$#%&`: 61 characters, leaving out the look-alikes l, o, I, O, 0, and 1, and also D.
- `New-RandomPassword 12 3` gives three 12-character passwords, and in `-MinimumLength 4 -MaximumLength 6 3`, the 3 binds to `-Count`.
- `Get-Command New-RandomPassword -Syntax` prints:

```
New-RandomPassword [[-PasswordLength] <int>] [[-Count] <int>] [-InputStrings <string[]>] [-ExtraCharacters <Object>] [-OnlyUseExtraCharacters] [-FirstCharacter <char>] [<CommonParameters>]

New-RandomPassword [[-Count] <int>] -MinimumLength <int> -MaximumLength <int> [-InputStrings <string[]>] [-ExtraCharacters <Object>] [-OnlyUseExtraCharacters] [-FirstCharacter <char>] [<CommonParameters>]
```

### What changes from 1.2.0

Measured in both editions unless the row says otherwise. Phase 2 ~~sums~~ summed this table up in the manifest's `ReleaseNotes`.

| Call | 1.2.0 | 2.0.0 |
|---|---|---|
| `-PasswordLength 8 -FirstCharacter '~' -Count 400` | `~` comes first in 195 (PowerShell 7) and 203 (5.1) of the 400 passwords, and last in the rest. | Always first (#3). |
| `-MinimumLength 4 -MaximumLength 20 -Count 50` | All 50 passwords share one random length. | Each password gets a length of its own (#3). |
| `-PasswordLength 2`, or `3`, with the default groups | Every one of 2,000 passwords draws from the first two (or three) groups only, so none has a symbol. | The groups that a short password represents are picked at random. |
| `-ExtraCharacters ''` | `DivideByZeroException` | A binding error: an empty group can't supply a character. |
| `-ExtraCharacters (Get-Content $file)`, or any command's output | One line fails with `ArgumentException` ("must only be a string, string[], or char[]"). Several lines are all silently dropped, which with `-OnlyUseExtraCharacters` ends in `DivideByZeroException`. | Accepted, one group per line. |
| `-ExtraCharacters` with a collection that isn't an array, such as a `List[string]` | `ArgumentException` ("must only be a string, string[], or char[]"). (Found in Phase 2.) | Accepted, one group per element. |
| `-ExtraCharacters ([char]'x')` | `ArgumentException` | Accepted as a one-character group. |
| `-ExtraCharacters 1, 2`, or any list element that isn't a string | Silently dropped. With `-OnlyUseExtraCharacters`, `DivideByZeroException`. | A binding error that names the accepted types. |
| `-ExtraCharacters` with an emoji | The surrogate pair is split, so the passwords hold lone surrogates. | A binding error. |
| `-InputStrings @()`, or `-OnlyUseExtraCharacters -ExtraCharacters @()` | `DivideByZeroException` | A terminating error: no characters to draw from. |
| `-MinimumLength 6 -MaximumLength 4` | A raw `ArgumentException`, with the error ID `System.ArgumentException,MG.NewRandomPassword.Cmdlets.NewRandomPassword`. | A terminating error with its own ID and the `InvalidArgument` category. |
| A character listed twice, as in `-InputStrings '1234567890', '234567890'` | Drawn twice as often in the fill. ~~(From the source, not measured: the fill draws from the groups' characters concatenated.)~~ (Phase 2 measured it: across 200 passwords of 100 characters, `1` showed up 0.51 times as often as the other digits on average, in both editions.) | Every distinct character is equally likely. (Phase 2 measured 0.97 and 0.99 for the same call.) |
| `-FirstCharacter 5` | U+0005, a control character. `-FirstCharacter 65` is `A`. | The digit 5 (**Bare numbers for -FirstCharacter**). `-FirstCharacter 65` is a binding error: more than one character. |
| `-FirstCharacter ([bigint]5)` | A binding error: PowerShell converts no `[bigint]` to a `char`. (Found in Phase 2.) | The digit 5, as for the other integer types. |
| A control character in `-ExtraCharacters` or `-FirstCharacter`, such as a tab | Passed into the passwords. ~~(From the source, not measured, apart from U+0005 above.)~~ (Phase 2 measured that a tab binds to either parameter without an error, and that `-FirstCharacter 9 -PasswordLength 1` gives a tab.) | A binding error (**Control characters in user input**). |
| `-PasswordLength 2000000000`, or a `-MaximumLength` that large | Passes validation, though no .NET string can hold that many characters. (Not measured.) | A binding error: the ranges top out at 1,048,576 (**Upper bound on lengths**). |
| `-OnlyUseExtraCharacters` without `-ExtraCharacters` | Ignored. The default groups are used. | A terminating error, `OnlyUseExtraCharactersWithoutExtraCharacters` (**-OnlyUseExtraCharacters without -ExtraCharacters**). |
| The type that `Get-Command` shows for `-ExtraCharacters` | `Object` | `String[]` |

## Resolved design questions

### 2.0.0 keeps the 1.2.0 surface

Every name, alias, position, parameter set, mandatory flag, default, `ValidateRange` minimum, and `ValidateSet` value in the 1.2.0 table stays, so a script written for 1.2.0 runs unchanged. Nothing forces a break. The major version is already earned by dropping every PowerShell 6 and 7 release before 7.6: 1.2.0's manifest lists the Core edition with `PowerShellVersion = '5.1'`, so it lets all of them import the module, and 2.0.0's `.psm1` refuses them. And the module already treats 1.2.0 scripts as a contract, as the `rpas` test in `Module.Tests.ps1` shows. What changes is behavior that no script can sensibly depend on: crashes, silently dropped input, and the two #3 bugs, as listed above. `Get-Command` shows two changes: the type of `-ExtraCharacters` (see below), and the length maxima, which **Upper bound on lengths** lowers to 1,048,576. (Phase 2 found a third, which no script plausibly reads: `Parameters` lists `-PasswordLength` and `-Count` first, in declaration order, where 1.2.0, which declared them as fields, listed them after the properties. `-Syntax` shows the same order in both.)

1.2.0's `ConfirmImpact = ConfirmImpact.None` and `[CmdletBinding(PositionalBinding = false)]` aren't carried over. The cmdlet doesn't support `ShouldProcess`, so its confirm impact means nothing, and the rewrite gives a `Position` only to `-PasswordLength` and `-Count`, which Phase 2 ~~checks~~ checked against the table.

### The engine builds passwords, and the shell is only the cmdlet

`MG.Passwords` holds the generator and the parameter attributes that convert and check user input. `MG.Passwords.Shell` holds only `NewRandomPasswordCommand`, which both shell projects compile. The attributes belong in the engine because nothing tests the shell projects directly, while an engine test can call an attribute's `Transform` with `RunspaceFixture.EngineIntrinsics`, which the fixture exposes for exactly that. The engine already references PowerShell in both of its targets, and `Directory.Build.props` gives every project the `System.Management.Automation` usings.

Engine types are `internal`, like `SecureRandom`, and the engine's `InternalsVisibleTo` already covers the shell and the tests. That leaves the cmdlet class, and in the 5.1 build the `ModuleInitializer` that PowerShell has to find, as the module's only public types, so no script can come to depend on an engine type.

### How a password is built

For each password, the generator:

1. Takes the length: `-PasswordLength`, or a uniformly random length between `-MinimumLength` and `-MaximumLength`, drawn for that password alone (#3).
2. Puts `-FirstCharacter`, when there is one, at index 0, outside every group and outside the shuffle (#3). It takes one of the requested characters, so `-PasswordLength 1 -FirstCharacter '~'` gives `~`, as in 1.2.0.
3. Fills the next slots with one character from each group. When fewer slots remain than there are groups, it represents a uniformly random subset of the groups, where 1.2.0 always took the first ones in order.
4. Fills the rest from the union of all the groups' characters.
5. Shuffles everything after index 0 with `SecureRandom.Shuffle`, which is the slice that its remarks describe.

Every draw comes from `SecureRandom`, whose integers are exactly uniform. This is 1.2.0's construction (guarantee, fill, shuffle), which isn't uniform over all the passwords that satisfy the guarantee. Rejection sampling would be: draw from the union, and start over until every group appears. But the expected number of tries grows fast as the groups grow in number or the length shrinks: about 22 for the default groups at length 4, and about 2,800 for ten one-character groups at length 10.

### Groups are normalized once

A group is a string of characters, and each group guarantees one of its characters in every password that has room for it. The generator removes repeated characters within each group, comparing ordinally, and builds the union from distinct characters, so each character is equally likely within its group and within the fill. Repeated groups stay as they are: `-InputStrings '23456789', '23456789'` guarantees two digits, which is the only way to ask for that, so a caller may mean it.

Characters are UTF-16 code units, as in 1.2.0, and lengths count them. A surrogate can't stand alone in valid text, and 1.2.0 split pairs into lone surrogates, so a surrogate in a group or as the first character is invalid. Treating a pair as one character is out of scope. An empty group is invalid too, because it can't supply its guaranteed character. The generator throws `ArgumentException` for either, and Phase 2's attributes report both at binding, before the generator sees them.

`-InputStrings` values are used as typed, as in 1.2.0.

### `-ExtraCharacters` becomes `[string[]]` with a transformation attribute

As measured for `references/binding.md`, a `[string[]]` parameter converts a `char[]` argument element by element, so `[char[]]'xy'` would become two groups, `x` and `y`, where 1.2.0 made it one. An `ArgumentTransformationAttribute` sees the argument before that conversion, as the caller passed it, with command output wrapped in `PSObject`.

`CharacterGroupsTransformationAttribute` turns the argument into one string per group:

- A string is one group, a `char` is a one-character group, and a `char[]` is one group of its characters.
- A collection, meaning any value other than a `char[]` that `LanguagePrimitives.GetEnumerator` enumerates, gives one group per element, by the same three rules.
- It unwraps each value and each element from its `PSObject` through `BaseObject` first, which is what makes `(Get-Content $file)` work.
- Anything else fails with an `ArgumentTransformationMetadataException` that names the accepted types, as 1.2.0's error did: a number, a hashtable, `$null`, or a collection inside the collection. Which characters a group may hold is left to `ValidatePasswordCharactersAttribute`, which runs after the transformation (**Control characters in user input**).

The strict rule keeps 1.2.0's contract ("must only be a string, string[], or char[]"). Without it, the `[string[]]` binder would take `-ExtraCharacters (Get-Date)` as the characters of the date's text. With `[string[]]` instead of `[object]` as the type, `Get-Command` and the help say what the parameter takes, and every argument that 1.2.0 accepted keeps its meaning.

### Errors

A bad argument fails at binding, in a transformation or validation attribute, so PowerShell's message names the parameter ("Cannot process argument transformation on parameter 'ExtraCharacters'." or "Cannot validate argument on parameter 'FirstCharacter'."). A problem that only shows across parameters ends the statement from `BeginProcessing` through `ThrowTerminatingError`, with the `InvalidArgument` category: `MinimumLengthExceedsMaximumLength` when `-MinimumLength` is greater than `-MaximumLength`, `OnlyUseExtraCharactersWithoutExtraCharacters` when `-OnlyUseExtraCharacters` is present without `-ExtraCharacters`, and `NoCharacters` when no group is left to draw from. A statement-ending error ignores `-ErrorAction` (`references/errors.md`), which is right here: with nothing generated, the rest of the call has nothing to do. No raw exception escapes the cmdlet, as 1.2.0's `ArgumentException` and `DivideByZeroException` did. Error IDs and messages aren't part of the compatibility promise.

### No copies of a password are left behind

The `net10.0` build writes each password straight into its string with `string.Create`. `netstandard2.0` has no `string.Create`, so that build fills a scratch buffer, `stackalloc` up to 256 characters and a rented `ArrayPool<char>` array above that, copies it into the string, and clears it. ~~A rented array goes back with `clearArray: true`.~~ (Phase 1 clears the characters it wrote and then returns the array without `clearArray`, which doesn't clear an array that the pool drops instead of keeping. See Phase 1.) Choosing the characters and shuffling them is the same code in both builds, and only the way the string is made differs, the way `SecureRandom` keeps its own difference between the builds inside one method.

Nothing goes through `SecureString`, a BSTR, or an `Encoding`. The archived `PasswordHolder` left an unzeroed BSTR copy of every password in memory and garbled non-ASCII characters. The returned string can't be cleared, and the GC may copy it, so the goal is only that the generator adds no copies of its own.

### No new packages

`Span<T>` and `ArrayPool<T>` come from `System.Memory` and `System.Buffers`, which the engine's `netstandard2.0` build already depends on, so the 5.1 resolver and the manifest's `FileList` stay correct.

### Tests come last

Phases 1 to 3 are verified by hand (see Verification) and keep the existing suites passing. Phase 4 writes the new tests once the surface has stopped moving. The two #3 bugs are tested there again, as `CLAUDE.md` requires, and each of those tests is run once against 1.2.0 to show that it fails there. `SecureRandomTests.cs` ~~lands in Phase 1~~ landed in #4, before Phase 1, with `SecureRandom` because it isn't new work for this plan: it was written with `SecureRandom`, whose surface this plan doesn't change.

## Architecture sketch

Files:

- `src/MG.Passwords/SecureRandom.cs`: exists.
- `src/MG.Passwords/CharacterSets.cs`: exists (Phase 1).
- `src/MG.Passwords/PasswordGenerator.cs`: exists (Phase 1).
- `src/MG.Passwords/CharacterGroupsTransformationAttribute.cs`: exists (Phase 2).
- `src/MG.Passwords/FirstCharacterTransformationAttribute.cs`: exists (Phase 2).
- `src/MG.Passwords/ValidatePasswordCharactersAttribute.cs`: exists (Phase 2).
- `src/MG.Passwords.Shell/NewRandomPasswordCommand.cs`: exists (Phase 2), compiled by both shell projects.
- `NewRandomPassword/en-US/MG.Passwords.Shell.dll-Help.xml`: Phase 3.

```csharp
namespace MG.Passwords;

internal static class CharacterSets
{
	internal const string Lowercase = "abcdefghijkmnpqrstuvwxyz";
	internal const string Uppercase = "ABCEFGHJKLMNPQRSTUVWXYZ";
	internal const string Digits = "1234567890";
	internal const string DigitsExceptOne = "234567890";
	internal const string DigitsExceptZeroAndOne = "23456789";
	internal const string Symbols = "!@$#%&";
	internal const string SymbolsExceptAtAndDollar = "!#%&";

	// The four groups that 1.2.0 uses without -InputStrings.
	internal static IReadOnlyList<string> Default { get; }
}

internal sealed class PasswordGenerator
{
	internal PasswordGenerator(IReadOnlyList<string> groups, char? firstCharacter);

	internal string Generate(int length);
	internal string Generate(int minimumLength, int maximumLength);
	internal static int GetLength(int minimumLength, int maximumLength);
}
```

```csharp
[Cmdlet(VerbsCommon.New, "RandomPassword", DefaultParameterSetName = "StaticLength")]
[Alias("rpas")]
[OutputType(typeof(string))]
public sealed class NewRandomPasswordCommand : PSCmdlet
```

## Phasing

### Phase 1 — Generation engine. ✅ Done — 2026-10-24.

Shipped: `src/MG.Passwords/CharacterSets.cs` and `src/MG.Passwords/PasswordGenerator.cs`, both `internal`, without a cmdlet yet. Every type and member has XML documentation that follows the `csharp-xml-docs` skill, as `SecureRandom`'s members do. (#4 committed `SecureRandom.cs` and `SecureRandomTests.cs` before Phase 1 started.)

- `CharacterSets` holds the seven 1.2.0 strings as `const` fields, in 1.2.0's `ValidateSet` order, so that the cmdlet's `[ValidateSet]` can name them, and `Default`, the four groups that 1.2.0 uses without `-InputStrings`. `Default` is a collection expression typed `IReadOnlyList<string>`, so it's the compiler's read-only list, not an array that a caller could cast back and change.
- `PasswordGenerator`'s constructor normalizes the groups as **Groups are normalized once** says: it removes repeated characters within each group, ordinally, builds the fill from the distinct characters of all the groups, and keeps repeated groups. It throws `ArgumentException` for no groups, an empty group, or a surrogate in a group or as the first character, as planned, and in two cases the plan didn't name: `ArgumentNullException` for a null list, and `ArgumentException` for a null group. The instance doesn't change after that, so one serves a whole `-Count` batch.
- `Generate(int length)` builds one password as **How a password is built** says, and throws `ArgumentOutOfRangeException` for a length below 1.
- `GetLength(int minimumLength, int maximumLength)` draws `SecureRandom.GetInt32(minimumLength - 1, maximumLength) + 1`, which never computes `maximumLength + 1`, and throws `ArgumentOutOfRangeException` for a minimum below 1 or above the maximum. `Generate(int minimumLength, int maximumLength)` calls it and then `Generate(int length)`.
- With fewer slots than groups, a private `FillFromRandomGroups` shuffles the group indices with `SecureRandom.Shuffle` and takes as many as there are slots. The indices live in a `stackalloc` buffer for up to 256 groups and in a rented `ArrayPool<int>` array above that.
- A private `Fill` chooses and shuffles the characters, the same code in both builds. The `net10.0` build hands it to `string.Create`, and the `netstandard2.0` build gives it a `stackalloc` buffer for up to 256 characters and a rented `ArrayPool<char>` array above that.

Two details differ from the plan:

- A rented array doesn't go back with `clearArray: true`. The generator clears the characters it wrote, in whichever buffer holds them, and then returns the array, because `clearArray` doesn't clear an array that the pool drops instead of keeping. Measured on 2026-10-24 in Windows PowerShell 5.1.26100.9711: System.Buffers 4.0.5.0's shared pool cleared a returned array of 1,048,576 characters, the largest it keeps, and left one of 1,048,577 as it was. The cmdlet's cap stays within that limit, but the engine doesn't assume the cap. In PowerShell 7.6.8, the pool cleared both, because it keeps larger arrays: it handed out 2,097,152 elements for 1,048,577.
- The group indices are cleared before their buffer is released too, because which groups a password represents tells something about it.

No test changed, as predicted. Verified on 2026-10-24 with .NET SDK 10.0.402, PowerShell 7.6.8, and Windows PowerShell 5.1.26100.9711:

- Both shell projects built in Debug with no warnings, and `dotnet test` passed 36 of 36, 18 per target. `tests/Invoke-Tests.ps1` passed 5 of 9 in each edition, with the audit's four failures, all from the missing cmdlet.
- A scratch smoke script, outside the repo, imported each edition's Debug build by its path, the shell first, with auto-loading off, and reached both types through reflection. In both editions, it found the seven constants and `Default` as 1.2.0 has them; `~` at index 0, and only there, in 1,000 passwords of 8; every length from 4 to 20, and no other, across 1,000 calls of `Generate(4, 20)`; all four default groups in each of 1,000 passwords of 4; the two characters of each of 6,000 passwords of 2 always from two different groups, with each of the six pairs of groups about equally often; two `x` and one `y` from the groups `x`, `x`, and `y`; equal counts from the group `aab`, and from the groups `ab` and `bc`; U+00E9, U+00DF, U+03A9, and U+4E2D unchanged; lengths 1, 255, 256, 257, 1,048,576, and 1,048,577; 300 one-character groups, which take the rented-indices path; `GetLength` up to `int.MaxValue`; and every exception above.
- In Windows PowerShell 5.1, System.Memory 4.0.5.0 and System.Buffers 4.0.5.0 loaded from the build folder. After `Generate(300)`, the `char` array that the pool handed out next, the one the generator had returned, held no characters, and with the clear taken out, the same check found all 300 of the password's. In both editions, the `int` array handed out after a password of 5 from the 300 groups held no indices.

**Outcome:** `MG.Passwords` builds every password that the cmdlet will ask for, in both builds, with both #3 fixes and random group subsets, and leaves no copy of a password in a buffer.

### Phase 2 — The cmdlet. ✅ Done — 2026-10-24.

Shipped: `src/MG.Passwords.Shell/NewRandomPasswordCommand.cs`, which both shell projects compile, and three `internal` attributes in `src/MG.Passwords/`: `CharacterGroupsTransformationAttribute.cs`, `FirstCharacterTransformationAttribute.cs`, and `ValidatePasswordCharactersAttribute.cs`. Every type and member has XML documentation that follows the `csharp-xml-docs` skill, and the docs of the cmdlet, the one public type, name no internal type. The manifest's `ReleaseNotes` sum up **What changes from 1.2.0**, in ASCII, because Windows PowerShell 5.1 reads a manifest without a BOM as ANSI.

- `MG.Passwords.NewRandomPasswordCommand` carries `[Cmdlet(VerbsCommon.New, "RandomPassword", DefaultParameterSetName = "StaticLength")]`, `[Alias("rpas")]`, and `[OutputType(typeof(string))]`, and declares the eight parameters of the 1.2.0 table as properties, in 1.2.0's order, with their parameter sets, positions, mandatory flags, aliases, defaults, and validation. `-OnlyUseExtraCharacters` is a static switch in both parameter sets, and the `[ValidateSet]` on `-InputStrings` names the `CharacterSets` constants. `-ExtraCharacters` is a `string[]` with `[CharacterGroupsTransformation]`, `-FirstCharacter` is a `char` with `[FirstCharacterTransformation]`, and both carry `[ValidatePasswordCharacters]`. A private constant, `MAX_LENGTH`, sets the `ValidateRange` maxima: 1,048,576 for `-PasswordLength` and `-MaximumLength`, and 1,048,575 for `-MinimumLength` (**Upper bound on lengths**).
- `CharacterGroupsTransformationAttribute` follows **`-ExtraCharacters` becomes `[string[]]` with a transformation attribute**. It also disposes of a collection's enumerator, which can hold a resource such as an open file, and its error names the type that it was given or, in a collection, the index and type of the element.
- `FirstCharacterTransformationAttribute` turns an integer, unwrapped from its `PSObject`, into its decimal text in the invariant culture, and passes every other argument through unchanged (**Bare numbers for -FirstCharacter**). An integer is a value of an integral type that a PowerShell number can have: `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, or `BigInteger`.
- `ValidatePasswordCharactersAttribute.ValidateElement` calls the internal static `ValidateCharacters(object?)`, the rule that the engine tests can call directly. It throws `ValidationMetadataException` for an empty string, a control character, a surrogate, or a value that is neither a string nor a `char`, and the message names a refused character by its code point, such as U+0009 (**Control characters in user input**).
- `BeginProcessing` raises the three errors under **Errors** through `ThrowTerminatingError`, in the planned order, each with an `ArgumentException` and the `InvalidArgument` category: `MinimumLengthExceedsMaximumLength`, whose target object is the minimum length, `OnlyUseExtraCharactersWithoutExtraCharacters` (read through `IsPresent`), and `NoCharacters`. It treats a null `-InputStrings` or `-ExtraCharacters` as unbound, which is exact: `ValidateEnumeratedArgumentsAttribute`, the base class of `ValidateSet` and of the validation attribute, refuses a null argument in both editions (measured with 1.2.0's `-InputStrings $null`), and for `-ExtraCharacters` the transformation refuses one first. `-FirstCharacter` comes from `MyInvocation.BoundParameters`, because no `char` value means unbound. Last, it creates the call's one `PasswordGenerator`.
- `ProcessRecord` generates and writes the `-Count` passwords one at a time, so `Select-Object -First` stops the loop early.

Three details differ from the plan:

- **Bare numbers for -FirstCharacter** said that a number that isn't an integer, such as `5.0`, still converts as a character code. It doesn't: PowerShell converts no `double`, `single`, or `decimal` to a `char`, so `-FirstCharacter 5.0` fails at binding with "Invalid cast from 'Double' to 'Char'.", in 1.2.0 as in 2.0.0. Measured on 2026-10-24 in both editions, and added to the `nrp-pwsh-internals` skill's `references/binding.md`. The attribute passes such a number through unchanged, as planned.
- PowerShell converts no `[bigint]` to a `char` either, so `-FirstCharacter ([bigint]5)` fails in 1.2.0. The attribute counts `BigInteger` as an integer, like the integral types that do convert, so it now gives the digit 5. **What changes from 1.2.0** has a row for it, and one for a `List[string]` given to `-ExtraCharacters`, which 1.2.0 refuses.
- **2.0.0 keeps the 1.2.0 surface** named two changes that `Get-Command` shows. There's a third, in the order of `Parameters` (see that section).

No test changed, as predicted: the four failing tests in `Module.Tests.ps1` pass in both editions. Verified on 2026-10-24 with .NET SDK 10.0.402, PowerShell 7.6.8, and Windows PowerShell 5.1.26100.9711:

- The engine and both shell projects built in Debug with no warnings, with and without `--no-incremental`. `tests/Invoke-Tests.ps1` passed 9 of 9 in each edition, and `dotnet test` passed 36 of 36, 18 per target.
- A scratch smoke script, outside the repo, imported each edition's Debug build by its path with auto-loading off, and passed all 89 of its checks in both editions: the parameter metadata and `Get-Command -Syntax` against the 1.2.0 table, with only this plan's differences; the README's four examples; every row of **What changes from 1.2.0**; `New-RandomPassword 12 3` and `-MinimumLength 4 -MaximumLength 6 3`; only the 61 default characters, and all four default groups, in each of 500 default passwords; every length from 4 to 20, and no other, across 2,000 passwords; the six pairs of default groups about equally often across 2,000 passwords of 2; a `[char[]]` as one group and a `string[]` or `List[string]` as one group per element; `-FirstCharacter` 0, 9, `5L`, `[byte]5`, `[bigint]5`, and `(Write-Output 7)` read as digits; U+0009, U+007F, and U+009F refused, and U+007E and U+00A0 accepted; U+00E9 unchanged in both parameters; a length of 1,048,576 accepted and 1,048,577 refused; `-OnlyUseExtraCharacters:$false` alone drawing from the default groups; a statement-ending error under `-ErrorAction SilentlyContinue`; and `Select-Object -First 3` stopping `-Count 100000000` at once. Against the installed 1.2.0, the same script failed 55 checks in each edition, one for every changed behavior, and passed the 32 that cover what 2.0.0 keeps, such as the default parameter set, the output type, `rpas`, and the metadata of the unchanged parameters. It skipped the two lengths of 2,000,000,000, which 1.2.0 would try to build.
- A staged copy of the manifest layout, with the edited manifest, `NewRandomPassword.psm1`, and each edition's Debug DLLs in `Core\` and `Desk\`, imported in each edition and exported `New-RandomPassword` and `rpas`, and `rpas 12 2` ran the staged build's cmdlet. `Test-ModuleManifest` accepted the manifest in both editions.

**Outcome:** `New-RandomPassword` and `rpas` run on the new engine in both editions, with 1.2.0's surface and both #3 fixes, and where 1.2.0 crashed or silently dropped input, they fail at binding or with an error of their own.

### Phase 3 — Help and README

- `NewRandomPassword/en-US/MG.Passwords.Shell.dll-Help.xml` is hand-written MAML. It has a synopsis; a description of both parameter sets, the group guarantee, `-FirstCharacter`, and per-password lengths; every parameter with its type, required flag, position, pipeline input (none), aliases, and default; and examples: the README's four, plus one each for `-ExtraCharacters` and `-OnlyUseExtraCharacters`. Its notes name the `rpas` alias, because Get-Help shows no ALIASES section for a cmdlet that has a help file, and each example's remarks end with an empty paragraph, which keeps the examples apart (both from `references/help.md`).
- Both shell projects copy the file into `en-US\` in their build output, where Get-Help finds it when the DLL is imported by its path, as the tests import it. In the release layout, Get-Help finds the copy beside the manifest. One file serves both editions, because both builds are named `MG.Passwords.Shell.dll`.
- `FileList` gains `en-US\MG.Passwords.Shell.dll-Help.xml`. `HelpInfoURI` stays unset, because any URL but a folder of updatable help makes `Update-Help` fail.
- `README.md` covers 2.0.0: the PowerShell versions it runs on; `-InputStrings`, `-ExtraCharacters`, and `-OnlyUseExtraCharacters`, which it doesn't mention now; and the changes from 1.2.0.

Predicted test changes: none.

### Phase 4 — Tests

Each test follows the `nrp-testing` skill, which says how to write it for both editions and where its expected values come from.

Engine tests, in `src/MG.Passwords.Tests/`, one class per type:

- `PasswordGeneratorTests`: the first character is at index 0 of every password in a large batch; `GetLength` returns every value of a small range and no other, including a range that ends at `int.MaxValue`; every password that's long enough holds a character from each group; with fewer slots than groups, every group shows up across a batch and no password repeats one; the fill draws each distinct character equally often, repeats included, in the `AssertEquallyLikely` style of `SecureRandomTests`; a group listed twice guarantees two characters; non-ASCII characters come through unchanged; lengths 1, 256, and 257, on both sides of the `stackalloc` limit; 257 groups with fewer slots, which rents the group indices; the scratch buffers coming back cleared, where the array that `ArrayPool<T>.Shared` hands out next, after a password of 257 characters (`net48` only, since `net10.0` writes into the string) or one with fewer characters than its 257 groups (both targets), holds no characters or indices, checked only after the pool has been shown to hand out the array returned to it last, as Phase 1's smoke script did; and each `ArgumentNullException`, `ArgumentException`, and `ArgumentOutOfRangeException`.
- A class for each transformation attribute, calling `Transform` with `RunspaceFixture.EngineIntrinsics`: each accepted form, wrapped in a `PSObject` and not, and each refused one, with its exception type. For `FirstCharacterTransformationAttribute`, that includes 0, 9, 10, and a wrapped integer.
- `ValidatePasswordCharactersAttributeTests`, through the attribute's internal rule: a control character at each end of both `char.IsControl` ranges, a lone high and a lone low surrogate, an empty string, and characters just outside those ranges, which pass.

Pester tests, in `tests/New-RandomPassword.Tests.ps1`, with a `Context` per parameter or feature:

- 'Parameters': the cmdlet's parameter sets and parameters match the 1.2.0 table with this plan's changes, so a later edit can't drop an alias or move a position unnoticed. The expected values are written into the test, with a comment that they come from `accaf2e`.
- 'FirstCharacter': every password in a large batch starts with it (#3); `-PasswordLength 1` gives it alone; `-FirstCharacter 5` starts each password with the digit 5; and `-FirstCharacter 65` and a control character fail at binding.
- 'MinimumLength and MaximumLength': one batch holds more than one length (#3); over enough passwords, every length in a small range shows up and none outside it; equal bounds give that length; and a minimum above the maximum fails with `MinimumLengthExceedsMaximumLength`.
- 'Positional binding': `New-RandomPassword 12 3`, and `-MinimumLength 4 -MaximumLength 6 3`.
- 'Character groups': the default output uses only the 61 default characters; every password of 4 or more characters has a character from each default group; and some 2-character passwords hold a digit or a symbol.
- 'ExtraCharacters': a string, a `string[]`, a `char[]`, a `char`, and `Get-Content` output of one line and of several, with and without `-OnlyUseExtraCharacters`; the error for each refused form, a control character among them; and a non-ASCII character, written as `[char]0x00E9` so that the file stays ASCII.
- 'OnlyUseExtraCharacters': with `-ExtraCharacters`, the passwords hold only its characters, whether or not `-InputStrings` is given; without it, the switch fails with `OnlyUseExtraCharactersWithoutExtraCharacters`, with `-InputStrings` too; and `-OnlyUseExtraCharacters:$false` without `-ExtraCharacters` generates from the default groups.
- 'Help': `Get-Help MG.Passwords.Shell\New-RandomPassword -Full` returns a `MamlCommandHelpInfo`, which fails when the build doesn't copy the file, and its syntax blocks and parameters match the cmdlet's (`references/help.md`).
- In `Module.Tests.ps1`, 'loads the build for this edition' also checks that the staged manifest exports `rpas` for `New-RandomPassword`. The other tests import the DLL by its path, so nothing else covers the alias through the manifest.

Each #3 test also runs once against 1.2.0, from a scratch copy of the test file that imports the installed 1.2.0 manifest, or `accaf2e`'s DLLs, by its path, to see it fail there.

### Out of scope

| Item | Blocked on |
|---|---|
| Release staging: copying the Release DLLs into `NewRandomPassword/Core` and `NewRandomPassword/Desk`, and publishing | A plan of its own. ~~The legacy projects' Release builds still write into `NewRandomPassword/Core` and `NewRandomPassword/DotNet`, so staging has to come after they're gone or keep them out of the build.~~ (The legacy projects are gone, so no build writes into `NewRandomPassword/` now.) |
| ~~Removing `legacy/` and its solution entries~~ | ~~Phase 2.~~ ✅ Done — 2026-10-24, after Phase 2: `legacy/` and the solution's `/Legacy/` folder are gone. Until the rewrite matched 1.2.0, the legacy cmdlet was the readable reference, and git history keeps it (see References). Phase 4 runs the #3 tests against the installed 1.2.0 or `accaf2e`'s DLLs, not against `legacy/`. |
| New features, such as `SecureString` output, named character sets, excluding characters, or pipeline input | A decision to grow the surface past 1.2.0. |
| Changing the 1.2.0 character sets, such as putting D back | A decision to change the 1.2.0 surface: `-InputStrings` validates against these exact strings. |
| Characters outside the Basic Multilingual Plane, with a surrogate pair counted as one character | A decision to count lengths in code points instead of UTF-16 code units. |
| Updatable help | A hosted folder of updatable help for `HelpInfoURI`. |

## New files

- `src/MG.Passwords/CharacterSets.cs` (Phase 1): the 1.2.0 strings and the default groups.
- `src/MG.Passwords/PasswordGenerator.cs` (Phase 1): normalized groups, length draws, and building passwords.
- `src/MG.Passwords/CharacterGroupsTransformationAttribute.cs` (Phase 2): turns `-ExtraCharacters` input into groups.
- `src/MG.Passwords/FirstCharacterTransformationAttribute.cs` (Phase 2): reads a bare number given to `-FirstCharacter` as its digits.
- `src/MG.Passwords/ValidatePasswordCharactersAttribute.cs` (Phase 2): refuses control characters, surrogates, and empty groups in both parameters.
- `src/MG.Passwords.Shell/NewRandomPasswordCommand.cs` (Phase 2): the cmdlet, for both shells.
- `NewRandomPassword/en-US/MG.Passwords.Shell.dll-Help.xml` (Phase 3): help for both editions.
- `src/MG.Passwords.Tests/PasswordGeneratorTests.cs`, a test class per transformation attribute, and `ValidatePasswordCharactersAttributeTests.cs` (Phase 4).
- `tests/New-RandomPassword.Tests.ps1` (Phase 4).

~~`SecureRandom.cs` and `SecureRandomTests.cs` exist but aren't committed. They land in Phase 1.~~ (#4 committed both before Phase 1 started.)

## Modified files

- `NewRandomPassword/NewRandomPassword.psd1`: `ReleaseNotes` (Phase 2), and the help file in `FileList` (Phase 3).
- `src/MG.Passwords.Shell/MG.Passwords.Shell.csproj` and `src/MG.Passwords.Shell-NETFramework/MG.Passwords.Shell-NETFramework.csproj`: copy the help file into `en-US\` (Phase 3).
- `README.md`: 2.0.0 (Phase 3).
- `tests/Module.Tests.ps1`: the alias through the manifest (Phase 4).

Not modified: `NewRandomPassword.psm1`, `ModuleInitializer.cs`, and `src/Directory.Packages.props`.

## Reused existing pieces

- `SecureRandom`: `GetInt32` for every draw, and `Shuffle` for the slots after the first character, which its remarks anticipate.
- `ModuleInitializer`: loads `System.Memory` and `System.Buffers` for the engine in Windows PowerShell 5.1, including on the `ArrayPool<char>` path.
- `RunspaceFixture.EngineIntrinsics`: the argument for testing an attribute's `Transform` directly.
- `tests/Import-NewRandomPassword.ps1`, `tests/Invoke-Tests.ps1`, and `Module.Tests.ps1`'s staging of the manifest layout, for every phase's checks.
- Each shell project's `Debug.ps1` and launch profiles, for trying a build by hand in each edition.
- 1.2.0 itself, as the build from before the change: installed (`Get-Module -ListAvailable NewRandomPassword`), or `accaf2e`'s committed DLLs, which match the Gallery package byte for byte.

## Verification

Every run uses stable releases, builds the two shell projects rather than the solution, and reports versions and counts as the `nrp-testing` skill says.

- **Phase 1:** both shell projects build in Debug with no warnings, and `dotnet test` still passes 36 of 36. A scratch smoke script, run in `pwsh` and in `powershell.exe`, imports each Debug build by its absolute path (the shell first, then `MG.Passwords.dll`) with auto-loading off, gets `MG.Passwords.PasswordGenerator` from the loaded assembly through reflection, and checks: a first character at index 0 in 1,000 passwords; every length from 4 to 20 across 1,000 calls of `Generate(4, 20)`; all four default groups in every 4-character password; a digit or a symbol in some 2-character passwords; U+00E9 unchanged; and lengths 256 and 257. The second takes the `ArrayPool` path, which in Windows PowerShell 5.1 also shows that `System.Buffers` loads.
- **Phase 2:** `tests/Invoke-Tests.ps1` passes 9 of 9 in each edition, and `dotnet test` still passes. A smoke script in each edition, following the `nrp-testing` skill's rules for repros, runs the README's four examples and every row of **What changes from 1.2.0**, and compares `Get-Command New-RandomPassword -Syntax` and the parameter metadata with the 1.2.0 table, expecting only this plan's differences. A staged copy of the manifest layout, built the way `Module.Tests.ps1` builds it, imports in each edition, and `rpas` runs the build's cmdlet.
- **Phase 3:** in each edition, `Get-Help MG.Passwords.Shell\New-RandomPassword -Full` on the Debug build, imported by its path, is a `MamlCommandHelpInfo` with every parameter and example. The same holds through a staged manifest layout with the help file beside the manifest, and `Update-Help -Module NewRandomPassword` still says that the module has no updatable help.
- **Phase 4:** the Pester tests pass in both editions against the Debug and the Release builds, and `dotnet test` passes on both targets. Each #3 test fails against 1.2.0.

## Open questions

- **Bare numbers for -FirstCharacter.** ✅ Resolved 2026-10-24 — read as the digit, option (b). `FirstCharacterTransformationAttribute` turns an integer argument, unwrapped from its `PSObject` first, into its decimal text before the binder converts the argument to a `char`, so 0 to 9 become that digit, and 10 or more fails with the binder's own "String must be exactly one character long." Every other argument passes through unchanged. 1.2.0 bound `-FirstCharacter 5` as U+0005 (measured), so `rpas 12 -fc 5` silently started every password with a control character, while now it does what it reads as. `-FirstCharacter 65`, which 1.2.0 binds as `A`, becomes an error, which no script plausibly relies on. ~~A number that isn't an integer, such as `5.0`, still converts as a character code, and **Control characters in user input** refuses the control characters that gives.~~ (Phase 2 measured that no such number converts: PowerShell refuses to convert a `double`, `single`, or `decimal` to a `char`, so `-FirstCharacter 5.0` was a binding error in 1.2.0 and stays one. A `[bigint]` doesn't convert either, and Phase 2 counted it as an integer, so `-FirstCharacter ([bigint]5)` now gives the digit. A negative integer fails like 10 does.)
- **Control characters in user input.** ✅ Resolved 2026-10-24 — refused at binding, option (b). `ValidatePasswordCharactersAttribute`, a `ValidateEnumeratedArgumentsAttribute` on both `-ExtraCharacters` and `-FirstCharacter`, refuses a control character (`char.IsControl`: U+0000 to U+001F and U+007F to U+009F) or a surrogate in each string or character, and an empty string, with PowerShell's "Cannot validate argument on parameter" error. 1.2.0 passed control characters into passwords, but a password with a control character can't be typed and rarely survives being pasted. The rule became a validation attribute because the binder runs it after the transformation attributes and its own conversion to `char`, so one attribute checks both parameters' final values, `CharacterGroupsTransformationAttribute` only converts forms, and nothing converts a string to a `char` twice.
- **-OnlyUseExtraCharacters without -ExtraCharacters.** ✅ Resolved 2026-10-24 — an error from `BeginProcessing`, option (b). When `-OnlyUseExtraCharacters` is present and `-ExtraCharacters` isn't bound, `BeginProcessing` ends the statement through `ThrowTerminatingError`, with the error ID `OnlyUseExtraCharactersWithoutExtraCharacters`, the `InvalidArgument` category, and a message that says the switch needs `-ExtraCharacters`, before any password is generated. 1.2.0 ignored the switch (measured) and drew from the default groups, or from the `-InputStrings` groups when they were given, so keeping that, option (a), would have left the switch's promise of passwords from the caller's characters only silently broken. The check reads `IsPresent`, so `-OnlyUseExtraCharacters:$false`, as splatting passes it, still works without `-ExtraCharacters`, and the switch stays a static parameter in 1.2.0's two parameter sets.
	Two ways to make the binder enforce the rule were considered on 2026-10-24 and lost. (c), parameter sets, needed four sets, because the length choice already splits the cmdlet in two, so each set needed a twin in which `-OnlyUseExtraCharacters` and `-ExtraCharacters` are both mandatory. It would have failed `-OnlyUseExtraCharacters:$false` without `-ExtraCharacters`, since an explicit `$false` still counts as bound, would have given the switch alone the binder's generic error instead of one that names the rule, and depended on set resolution that was never measured. (d), a dynamic switch that `GetDynamicParameters` offers only when `-ExtraCharacters` is bound, would have failed the switch alone with `NamedParameterNotFound`, which reads as if the parameter doesn't exist, dropped the switch from `Get-Command -Syntax` and `Parameters`, and failed the `:$false` call too. Both would only have moved the failure from `BeginProcessing` to binding, and every option ends the statement before a password exists, so failing earlier gained the caller nothing.
- **Upper bound on lengths.** ✅ Resolved 2026-10-24 — capped at 1,048,576 characters, option (b). The `ValidateRange` maxima become 1,048,576 for `-PasswordLength` and `-MaximumLength` and 1,048,575 for `-MinimumLength`, and the minima stay 1, 2, and 1. That's a 2 MB string, far beyond any password or key, and a longer request now fails at binding with the range message. 1.2.0's maxima of 2,147,483,647 accepted lengths that no .NET string can hold, about 2^30 characters at most, so the largest requests could only end in an `OutOfMemoryException`. `Get-Command` shows the new maxima, which no script reads. `GetLength` keeps its overflow-safe draw, because the engine doesn't assume the cmdlet's cap.
