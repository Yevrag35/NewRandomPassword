# Binding input and parameters

## Pipeline input and named arguments

Measured 2026-10-03 on PowerShell 7.6.6 and Windows PowerShell 5.1.26100 with a throwaway `Add-Type` cmdlet.

- **Pipeline to an `[object]` parameter:** every non-null object arrives wrapped in a `PSObject`, even an `[int]`. A piped array arrives as one `PSObject` around the `object[]`. `$null` arrives as `$null`.
- **Pipeline to an `[object[]]` parameter:** a scalar arrives in a one-element array, unwrapped from its `PSObject` unless it's a `[pscustomobject]`, and its instance notes are dropped. An array binds as itself, so it's flattened.
- **A named argument to an `[object[]]` parameter, such as `-InputObject`:** only an `IList`, after unwrapping, supplies its elements. A hashtable, a `HashSet[T]`, a `Queue`, a string, or a LINQ iterator is one element, and a single `PSObject`-wrapped argument keeps its wrapper.
- PowerShell never enumerates a string, whether in the pipeline, in `foreach`, or in its operators.

## Named arguments to `[object]`, `[string[]]`, `[string]`, and `[char]` parameters

Measured 2026-10-23 on PowerShell 7.6.8 and Windows PowerShell 5.1.26100.9711 with a throwaway `Add-Type` cmdlet. Both editions gave the same results.

- **To an `[object]` parameter:** a quoted string, or a variable that holds one, arrives as the raw `string`, and `5` as an `int`. A parenthesized command's output arrives wrapped: one object, as from `(Write-Output 'xy')` or a one-line `(Get-Content ...)`, is a `PSObject`, and several are an `object[]` of `PSObject`s. A comma list such as `'x', 'y'` is an `object[]` of the raw values, a typed array such as `[char[]]'xy'` or a `[string[]]` arrives as itself, `@()` is an empty `object[]`, and `$null` is `null`.
- **To a `[string[]]` parameter:** the binder converts each element, so `[char[]]'xy'` becomes `'x'` and `'y'`, one string per character. It unwraps `PSObject`-wrapped strings, turns `1, 2` into `'1'` and `'2'`, binds `''` as a one-element array that holds `''`, and binds `$null` as `null`.
- **An `ArgumentTransformationAttribute`** receives the argument before that conversion: a `char[]` as itself, one command output as a `PSObject`, and several as an `object[]` of `PSObject`s. On a parameter that isn't mandatory, PowerShell calls it for `$null` too.
- **To a `[char]` parameter:** an integer converts to the character with that code, so `5` is U+0005 and `65` is `A`, while `'5'` is the digit. A string has to be exactly one UTF-16 code unit: `''`, `'ab'`, and a surrogate pair such as an emoji fail with `CannotConvertArgumentNoMessage` ("String must be exactly one character long."). `$null` fails with `ParameterBindingFailed` ("Cannot convert null to type "System.Char"."), although `LanguagePrimitives.ConvertTo($null, [char])` gives `[char]0` (see `conversions.md`). A transformation attribute on a `[char]` parameter receives the integer itself.
- **Other numbers to a `[char]` parameter** (measured 2026-10-24 on the same versions, with a `[char]` parameter that has no transformation attribute): the other integral types convert as character codes too, so `[long]65`, `[byte]65`, and `[uint64]65` are `A`. A `[double]`, `[single]`, or `[decimal]` fails with `CannotConvertArgumentNoMessage` even when its value is whole, such as `5.0` and `65.0` ("Invalid cast from 'Double' to 'Char'."). So does a `[bigint]`: "Object must implement IConvertible." in PowerShell 7, and "Cannot convert the "5" value of type "System.Numerics.BigInteger" to type "System.Char"." in Windows PowerShell 5.1.
- **To a `[string]` parameter:** `5` binds as `'5'`, and `$null` binds as `null`.

## Named arguments to a `[Flags]` enum parameter

Measured 2026-10-24 on PowerShell 7.6.8 and Windows PowerShell 5.1.26100.9711 with throwaway `Add-Type` cmdlets whose parameter is a `[Flags]` enum, such as one whose members are `None` (0), `Lowercase` (1), `Uppercase` (2), `Digits` (4), `Symbols` (8), `All` (15), and `NoLookAlikes` (16). Both editions gave the same results.

- **Lists:** the binder ORs the members of a list in every form tried: `Lowercase, Digits`, `Lowercase,Digits`, a variable that holds the two strings, command output such as `(Write-Output Lowercase Digits)`, and a `List[string]`. It joins a list's elements with commas and parses the result, as the error for `Lowercase, Bogus` shows by quoting `"Lowercase,Bogus"`, so one string with commas binds the same, as in `'Lowercase, Digits'`, with spaces around the names ignored. Integers combine too, as in `1, 4`, and so do a name and an integer, as in `Lowercase, 4`. A string of digits, such as `'5'`, binds as its number.
- **Names:** a name matches without regard to case. When the whole argument is one name, a prefix of one member's name binds that member, and an exact name binds its member even when it's also the start of another member's name: with the members `Uppercase` and `UppercaseExceptIAndO`, `Uppercase` binds `Uppercase`, `UppercaseE` binds `UppercaseExceptIAndO`, and `Upper` fails ("... too similar or identical to the following enumerator names: Uppercase, UppercaseExceptIAndO. Use a more specific identifier name."). In a list, every name has to be exact: `NoL` alone binds `NoLookAlikes`, but `Uppercase, NoL` fails, as an array and as one string. Wildcards, such as `Up*`, `Non?`, and `*`, match nothing.
- **Repeats:** `Lowercase, Lowercase` binds `Lowercase`. A flags value is a set of bits, so it can't hold a member twice.
- **Values that no member defines:** a name that matches no member fails with `CannotConvertArgumentNoMessage`, and so does an integer with a bit that no member defines, such as 32, 33, or -1 when the members cover 31. Each message lists the members, and a list fails as a whole when one of its names does. The binder checks only the arguments that it converts: a value that already has the enum's type binds as it is, so `[enum]::ToObject($type, 64)` binds 64, from command output too.
- **Zero and nothing:** `0` and `None` bind `None`. With no member for 0, a lone `0` or `'0'` still binds 0, and so does a `-band` of two members that share no bits, which already has the enum's type, while a 0 in a list, as in `Lowercase, 0`, fails. `$null` fails with `ParameterBindingFailed` ("Cannot convert null to type ..."), and `''` and `@()` fail with `CannotConvertArgumentNoMessage`, as an empty name that's too similar to every member.
- **A validation attribute** on the parameter receives every value that binds, converted or not, as the enum itself, unwrapped from the `PSObject` of command output. So it sees the 0 and the undefined bits that the binder lets through, and its error is `ParameterArgumentValidationError`: "Cannot validate argument on parameter '<name>'.", followed by its own message. An argument that the binder can't convert, such as 32, fails with the binder's error before the attribute runs.
- `Get-Command -Syntax` shows the enum's type name, as in `-CharacterSet <NrpCharSet>`, not its members.

## Enumerating a value the way the pipeline does

Measured 2026-10-06 in both editions.

- `LanguagePrimitives.GetEnumerator` gives exactly the pipeline's elements for every value tried: arrays, lists, sets, queues, stacks, LINQ iterators, a 2-D array (all its cells), a `BitArray`, a `NameValueCollection` (its keys), a `DataTable` (its rows), a `PSObject` around an array, and enumerators such as `$hashtable.GetEnumerator()`.
- `LanguagePrimitives.GetEnumerable` matches too, except that it treats an `IEnumerator` as one element, while the pipeline enumerates it.
- Both return `null` for strings, dictionaries, an `XmlNode`, custom objects, and scalars, which the pipeline sends as one object, and for `$null`, which the pipeline sends as one `$null`.
- Neither follows the named-argument rule above. Both enumerate sets, queues, and LINQ iterators.
- From script, Windows PowerShell 5.1 can't call `GetEnumerator()` on LINQ's private iterator types ("Cannot find an overload"). That looks like a difference between the editions, but isn't one: C# can call it in both.

## Dynamic parameters

Measured 2026-10-06 in both editions, with cmdlets that take a type as their first positional parameter, `-GenericType`, and whose `GetDynamicParameters` offers a `-CaseSensitive` switch only while that type is unbound or is one the switch applies to.

- With the positional argument first, as in `<cmdlet> [int] -CaseSensitive`, the call fails with `NamedParameterNotFound`. `GetDynamicParameters` sees `-GenericType` bound to `[int]` and offers no switch.
- With the switch first, as in `<cmdlet> -CaseSensitive [int]`, the call succeeds and the switch binds, although `GetDynamicParameters` offers no switch for `[int]`. While `-CaseSensitive` is still unknown, positional binding would take the argument after it as its value, so PowerShell calls `GetDynamicParameters` with `-GenericType` unbound, gets the switch, and binds `[int]` by position only afterward.
- A named `-GenericType ([int])` binds before PowerShell calls `GetDynamicParameters`, in either order.
- A dynamic parameter whose only parameter set excludes a bound static parameter fails with `ParameterNotInParameterSet` ("Parameter 'CaseSensitive' cannot be specified in parameter set 'WithComparingScript'."). That's a clearer error than the `NamedParameterNotFound` that withholding the parameter gives.

## Resolving parameter sets

Measured 2026-10-24 on PowerShell 7.6.8 and Windows PowerShell 5.1.26100.9711 with throwaway `Add-Type` cmdlets that have NewRandomPassword's two parameter sets: StaticLength, the default, with `-PasswordLength` at position 0, and RandomLength, with `-MinimumLength` and `-MaximumLength` mandatory. `-Count` is at position 1 in every set. Two of the cmdlets add a twin of each set, in which a `-CharacterSet` parameter takes the place of `-InputStrings`: one makes `-CharacterSet` mandatory in the twins, and the other leaves it optional. Both editions resolved every call the same way.

- **Mandatory in the twins:** every call without `-CharacterSet` resolves to the set that the two sets alone give it, with the same values bound. That covers no arguments, `12`, `12 3`, `-Count 3`, `-PasswordLength 12 -Count 3`, `-MinimumLength 4 -MaximumLength 6` alone or with `3` or `-Count 3`, the same splatted, `12 -MinimumLength 4 -MaximumLength 6`, which binds 12 to `-Count`, and calls with `-InputStrings`, `-ExtraCharacters`, `-OnlyUseExtraCharacters`, or `-FirstCharacter`. A call with `-CharacterSet` resolves to a twin, and a call with both `-CharacterSet` and `-InputStrings` fails with `AmbiguousParameterSet`.
- **Optional in the twins:** a call with `-MinimumLength` and `-MaximumLength` and with neither `-InputStrings` nor `-CharacterSet` fails with `AmbiguousParameterSet`, even `-MinimumLength 4 -MaximumLength 6`. Calls without the two lengths resolve as they do with the mandatory twins.
- **A missing mandatory parameter:** with the two sets alone, `-MinimumLength 4` prompts for `-MaximumLength` in an interactive session, and fails with `MissingMandatoryParameter` under `-NonInteractive`. With the twins, two sets are left, each missing a mandatory parameter, so the call fails with `AmbiguousParameterSet` without prompting.
- **The error:** `AmbiguousParameterSet`'s message is "Parameter set cannot be resolved using the specified named parameters. One or more parameters issued cannot be used together or an insufficient number of parameters were provided." in PowerShell 7, and its first sentence alone in Windows PowerShell 5.1. It names no parameter.
- **`Get-Command -Syntax`** lists the default set first, but the order of the others isn't the same for every build of a cmdlet. NewRandomPassword's own builds, which declare the twins the same way, list StaticLengthCharacterSet, RandomLength, and RandomLengthCharacterSet in both editions, and so did PowerShell 7's `Add-Type` build of the throwaway cmdlet, while Windows PowerShell 5.1's `Add-Type` build of it listed StaticLengthCharacterSet, RandomLengthCharacterSet, and RandomLength. A twin's syntax shows `-CharacterSet` without brackets, as a mandatory parameter.

## What it means for NewRandomPassword

- `GetDynamicParameters` alone can't validate a dynamic parameter against a positional argument. Check it again in `BeginProcessing`, against the final values.
- Pipeline input to an `[object]` parameter always arrives in a `PSObject`, and a single wrapped argument to an `[object[]]` parameter keeps its wrapper. Unwrap a value before you test its type in C# (see `psobject.md` for which unwrapping keeps a custom object).
- A transformation attribute sees command output wrapped in `PSObject`, both a single value and each element of an `object[]`, so it unwraps each one before it tests the type.
- A `[string[]]` parameter splits a `[char[]]` argument into one string per character. When a `char[]` has to stay one string, as a group of `-ExtraCharacters` does, a transformation attribute joins it before the binder converts it.
- A `[char]` parameter reads a bare number as a character code, so `-FirstCharacter 5` binds U+0005, not the digit. Reading a bare digit as that digit takes a transformation attribute.
- Only a number of a primitive integral type converts to a `[char]` parameter, as a character code. Every other number, such as `5.0` or a `[bigint]`, fails the conversion unless a transformation attribute turns it into something that converts, so don't promise in help or tests that it converts.
- A `[Flags]` enum parameter takes a list or a string with commas, so the cmdlet needs no conversion of its own. It does need a check of its own: 0 binds whether or not a member has that value, and a value that already has the enum's type keeps bits that no member defines. When 0 or an undefined bit means nothing to the cmdlet, refuse it in a validation attribute, which sees every bound value and gives an error that names the parameter.
- Settle a `[Flags]` enum's member names before it ships. A single name can be cut to a prefix of one member's name, so a member added later can make a script's prefix fit two, and the script fails. Exact names keep working, so help, tests, and examples use them.
- To make two parameters exclude each other through parameter sets, give each set a twin in which one parameter takes the other's place, and make it mandatory in the twins. Left optional there, it leaves the binder two sets for every call that names neither parameter, and every such call that the default set doesn't settle fails.
- Twin sets turn a call that names one of two mandatory parameters, such as `-MinimumLength` without `-MaximumLength`, from a prompt for the other into an `AmbiguousParameterSet` error, which names no parameter. The help says which parameters exclude each other.
- With twin sets, a check of `ParameterSetName` for one length mode accepts both of that mode's sets.
- Match parameter sets by name, in tests and anywhere else, and not by the order in which `Get-Command` lists them after the default set, which can differ between builds of the same cmdlet.
