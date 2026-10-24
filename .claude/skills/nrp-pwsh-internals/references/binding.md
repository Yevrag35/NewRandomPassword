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
- **Other numbers to a `[char]` parameter** (measured 2026-10-24 on the same versions, with 1.2.0's `-FirstCharacter`, a `[char]` parameter with no transformation attribute): the other integral types convert as character codes too, so `[long]65`, `[byte]65`, and `[uint64]65` are `A`. A `[double]`, `[single]`, or `[decimal]` fails with `CannotConvertArgumentNoMessage` even when its value is whole, such as `5.0` and `65.0` ("Invalid cast from 'Double' to 'Char'."). So does a `[bigint]`: "Object must implement IConvertible." in PowerShell 7, and "Cannot convert the "5" value of type "System.Numerics.BigInteger" to type "System.Char"." in Windows PowerShell 5.1.
- **To a `[string]` parameter:** `5` binds as `'5'`, and `$null` binds as `null`.

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

## What it means for NewRandomPassword

- `GetDynamicParameters` alone can't validate a dynamic parameter against a positional argument. Check it again in `BeginProcessing`, against the final values.
- Pipeline input to an `[object]` parameter always arrives in a `PSObject`, and a single wrapped argument to an `[object[]]` parameter keeps its wrapper. Unwrap a value before you test its type in C# (see `psobject.md` for which unwrapping keeps a custom object).
- A transformation attribute sees command output wrapped in `PSObject`, both a single value and each element of an `object[]`, so it unwraps each one before it tests the type.
- A `[string[]]` parameter splits a `[char[]]` argument into one string per character. When a `char[]` has to stay one string, as a group of `-ExtraCharacters` does, a transformation attribute joins it before the binder converts it.
- A `[char]` parameter reads a bare number as a character code, so `-FirstCharacter 5` binds U+0005, not the digit. Reading a bare digit as that digit takes a transformation attribute.
- Only a number of a primitive integral type converts to a `[char]` parameter, as a character code. Every other number, such as `5.0` or a `[bigint]`, fails the conversion unless a transformation attribute turns it into something that converts, so don't promise in help or tests that it converts.
