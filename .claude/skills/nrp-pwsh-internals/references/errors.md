# Errors

How a cmdlet's errors end a statement or the whole script, which exception types carry an error, and what the error preferences record. Measured 2026-10-03 on PowerShell 7.6.6 and Windows PowerShell 5.1.26100, with `Add-Type` test cmdlets and script blocks that a cmdlet runs, unless a fact gives another date.

## Ending a statement or the script

- `ThrowTerminatingError` ends only the statement. A cmdlet that catches an error from a script block it runs and calls `ThrowTerminatingError` makes even a `throw`, or a `Write-Error` under `Stop`, end only the statement, although either one ends the whole script when `ForEach-Object` runs it.
- A statement-ending error from a cmdlet ignores the cmdlet's own `-ErrorAction`. `-ErrorAction SilentlyContinue` doesn't hide it.
- `throw 'boom'` gives the fully qualified error ID `boom`, with no cmdlet name.

## Exception types

Seen from C# that runs a script block (measured 2026-10-05):

- `throw 'x'` is exactly a `RuntimeException`.
- `Write-Error` under `Stop` is an `ActionPreferenceStopException`. Its `ErrorRecord` is the record of the error that caused the stop, so its `ToString()` is that error's message, not "The running command stopped because..." (measured 2026-10-06).
- `break` is a `BreakException`, which is a `FlowControlException`, not a `RuntimeException`.
- `PSInvalidOperationException` derives from `InvalidOperationException`, not `RuntimeException`. Likewise, `PSInvalidCastException` derives from `InvalidCastException` (checked 2026-10-07). So C# overload resolution sends either one to an `Exception` overload, not a `RuntimeException` one.

## Preferences

Measured 2026-10-06.

- `$Error` also records errors that are caught in a `try` block, hidden with `-ErrorAction SilentlyContinue`, or redirected with `2>$null`, but not ones hidden with `-ErrorAction Ignore`. So `$Error` can't tell which errors a preference hid.
- `-WarningVariable` collects a cmdlet's warnings even under `-WarningAction SilentlyContinue`.

## Measuring traps

- Inside any `try` block, an error that ends a statement and one that ends the script both jump to `catch`, and Pester runs every test inside one. To tell them apart, run the command in a new process, outside any `try` block, with another statement after it. When that statement runs, the error ended only its own statement.

## What it means for NewRandomPassword

- Choose between `ThrowTerminatingError` and `WriteError` by whether the rest of the call should still run. `-ErrorAction` can't hide a statement-ending error, so don't raise one for a problem that a caller should be able to skip.
- Don't use `$Error` to tell which errors a preference hid.
