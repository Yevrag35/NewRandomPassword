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

## What PowerShell offers for an enum parameter

Measured 2026-10-24 on PowerShell 7.6.8 and Windows PowerShell 5.1.26100.9711, with `TabExpansion2` on a throwaway `Add-Type` cmdlet whose parameter is a `[Flags]` enum. Both editions gave the same results.

- Completion offers every member's name, sorted, with `None` and members that combine others, such as `All`, among them.
- After a comma, as in `Lowercase,` and `Lowercase, D`, it completes the next name on its own, and it offers names that are already in the list again. Inside a quoted string, as in `'Lowercase, D`, it offers nothing.

## Parameter names after other parameters are bound

Measured 2026-10-24 on the same versions, with `TabExpansion2` on throwaway `Add-Type` cmdlets that have two parameter sets, StaticLength with `-PasswordLength` and RandomLength with mandatory `-MinimumLength` and `-MaximumLength`, and on one that adds a twin of each set, in which a mandatory `-CharacterSet` takes the place of `-InputStrings`.

- **After a bare dash,** both editions leave out the parameters that no set still open to the call has: after `-MinimumLength 4 -`, there's no `-PasswordLength`, and with the twin sets, there's no `-CharacterSet` after `-InputStrings abc -` and no `-InputStrings` after `-CharacterSet Lowercase -`.
- **After a prefix,** PowerShell 7 leaves them out the same way unless none of the parameters that are left matches, and then it offers every parameter that matches: after `-MinimumLength 4`, `-P` gives `-ProgressAction` and `-PipelineVariable`, and `-Pa` gives `-PasswordLength`. Windows PowerShell 5.1 leaves them out only when a parameter that's left and matches isn't a common parameter, and it offers the aliases of common parameters too: after `-MinimumLength 4`, `-P` gives `-PasswordLength`, `-PipelineVariable`, and `-pv`, while with the twin sets, `-InputStrings abc -C` gives `-Count` alone.

## What it means for NewRandomPassword

- A completer returns an empty collection, never `null`, when it has nothing to offer, so that PowerShell doesn't offer file names instead.
- A completer can't see the part of an unquoted argument before a comma, so it completes each comma-separated value on its own.
- The order of a `[ValidateSet]`'s values shows in `Get-Command` and in the list that a binding error gives, not in tab completion, so choose the order for those two.
- An enum parameter needs no argument completer, a `[Flags]` one included: PowerShell completes each name of a comma-separated list on its own.
- Parameter sets keep completion from offering a parameter that the bound ones exclude only after a bare dash. A prefix can still bring it back, so the binding error and the help have to make the exclusion clear on their own.
