# Argument completion

Measured 2026-10-07 on PowerShell 7.6.6 and Windows PowerShell 5.1.26100.9444, with `TabExpansion2`, with completers on throwaway functions, and with a binary module's `IArgumentCompleter`.

## What a completer receives

- **The whole argument:** `wordToComplete` is the argument's whole text, wherever the cursor is in it, so `[guid]` with the cursor after `gu` arrives as `[guid]`. The results replace the whole argument too: their replacement range starts where the argument starts.
- **Strings:** a quoted argument arrives as its value between straight quotes. PowerShell unescapes the value, adds the closing quote when it's missing, and replaces typographic quotation marks, U+2018 through U+201E, with straight ones. So `'gu` arrives as `'gu'`, and so does `gu` after U+2018, with or without U+2019 after it. `gu` after U+201C arrives as `"gu"`, and `'it''s` as `'it's'`.
- **Commas:** PowerShell splits an argument that isn't in quotes at each comma, and passes only the part after the last comma: in `[System.Collections.Generic.Dictionary[string,in`, the completer gets `in`, and the results replace only `in`. In quotes, the whole string arrives.
- **Which parameter:** the completer runs for a named parameter, for its alias, for the `-Name:value` form, and for a positional argument. It runs for a cmdlet that has dynamic parameters too, with a dynamic switch before the argument.

## What a completer returns

- An `IArgumentCompleter` that returns `null` gets PowerShell's default completion, which offers file names. One that returns an empty collection gets no completions at all.

## What PowerShell offers for a ValidateSet

Measured 2026-10-24 on PowerShell 7.6.8 and Windows PowerShell 5.1.26100.9711, with `TabExpansion2` on a binary cmdlet's `[ValidateSet]` that ignores case.

- **Order:** completion offers the values sorted, not in the attribute's order, and puts a value that needs quotes, such as `!@$#%&`, in single quotes. The editions sort symbols differently: PowerShell 7 offered `'!@$#%&'` before `'!#%&'`, and Windows PowerShell 5.1 offered them the other way around.
- **Case:** an argument matches the values that start with it in either case, so `abc` and `ABCD` both get `abcdefghijklmnopqrstuvwxyz` and `ABCDEFGHIJKLMNOPQRSTUVWXYZ`. Neither edition minds two values that differ only in case: both bind, and completion offers both.

## What it means for NewRandomPassword

- A completer returns an empty collection, never `null`, when it has nothing to offer, so that PowerShell doesn't offer file names instead.
- A completer can't see the part of an unquoted argument before a comma, so it completes each comma-separated value on its own.
- The order of a `[ValidateSet]`'s values shows in `Get-Command` and in the list that a binding error gives, not in tab completion, so choose the order for those two.
