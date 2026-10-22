# Converting values

## What `$null` converts to

`LanguagePrimitives.ConvertTo($null, T)`, measured in both editions on 2026-09-30, with the structs, `[bool]`, `[char]`, and `[hashtable]` added on 2026-10-04:

| Type | Result |
|---|---|
| `[int]` | `0` |
| `[string]` | `''` |
| `[bool]` | `$false` |
| `[char]` | `[char]0` |
| `Nullable[int]`, and most classes, such as `[version]`, `[System.IO.FileInfo]`, and `[hashtable]` | `$null` |
| `[ref]` | a `PSReference` that wraps `$null` |
| `[datetime]`, `[guid]`, `[timespan]`, enums such as `[ConsoleColor]`, and other structs such as `KeyValuePair[string, int]` | the conversion throws |

## What a typed collection stores

- A typed list converts what `$list.Add($x)` receives to its element type. In a `[System.Collections.Generic.List[int]]::new()`, `.Add('123')` stores `123` and `.Add($null)` stores `0`.
- `$list.Add($null)` stores the same value as `ConvertTo` for `[int]`, `[string]`, `Nullable[int]`, `[version]`, `[System.IO.FileInfo]`, and `[ref]` (measured 2026-09-30). So a `$null` in a typed collection of a struct that throws is a conversion error, not `default(T)`.
- A typed dictionary converts its values the same way (measured 2026-10-03): `Add('b', $null)` and the indexer store `''` in a `Dictionary[string, string]`, `0` for `[int]` values, and `$null` for `[object]` and `[version]` values.

## What it means for NewRandomPassword

- These facts cover `LanguagePrimitives.ConvertTo` and typed collections, not the parameter binder. Measure how a parameter binds `$null` or a value of another type before you rely on this table for it.
