---
name: nrp-pwsh-internals
description: Measured PowerShell engine behavior that NewRandomPassword's cmdlet code can depend on, in Windows PowerShell 5.1 and PowerShell 7. Covers how pipeline input, named arguments, and dynamic parameters bind, PSObject wrapping and AutomationNull, how a cmdlet's errors end a statement or the whole script and which exception types carry them, what converting `$null` to a type gives, what an argument completer receives, where Get-Help finds a binary module's help file and how it shows it, what `Update-Help` does with the manifest's `HelpInfoURI`, how Windows PowerShell 5.1 resolves a module's assemblies, and the traps in measuring any of it. Use it before you write or change code that reads cmdlet input or a parameter that takes more than one type, unwraps a PSObject, catches or reports an exception, converts a value, adds a dynamic parameter or an argument completer, changes the help file, where it ships, or the manifest's `HelpInfoURI`, or touches assembly loading or which build each edition loads, before you answer a question about how PowerShell behaves in these areas, and before you run a PowerShell experiment or proof of concept to find out.
---

# PowerShell internals for NewRandomPassword

NewRandomPassword's cmdlet is written in C#, so how it receives input, reports errors, ships help, and loads in each edition depends on details of the PowerShell engine that the documentation leaves out. This skill records those details as measured in Windows PowerShell 5.1.26100 and PowerShell 7.6.6, with the date of each measurement. A fact holds in both editions unless it says otherwise.

- Trust a measurement here over recollection. When a change depends on a fact that a newer PowerShell release could change, measure it again.
- When you measure something that later work could depend on, add it to the reference it belongs to, with the versions and the date, instead of saving it as a memory. Say what it means for NewRandomPassword beside it, as a rule the code has to follow rather than a description of the code as it is today.
- Don't point to working notes, such as review or bug lists, which may not stay in the repo. State the fact, the rule, or the reason itself.

## References

Read the reference for the area you're working in. Each one also says what its facts mean for NewRandomPassword, where that's more than the facts themselves.

- `references/binding.md`: how pipeline input and a named argument bind to `[object]` and `[object[]]` parameters, which values `LanguagePrimitives.GetEnumerator` enumerates, and when PowerShell calls `GetDynamicParameters`. Read it before you change how the cmdlet reads its input or a parameter that takes more than one type, or add or change a dynamic parameter.
- `references/psobject.md`: PowerShell 7's `PSObject(int)` constructor, each edition's private `PSObject` members, how to tell a custom object from a wrapped value, `PSObject.BaseObject` versus unwrapping one layer at a time, and where `AutomationNull.Value` reaches C# code. Read it before you unwrap a `PSObject`, build one in C#, or handle a command that outputs nothing.
- `references/errors.md`: how `ThrowTerminatingError` and a statement-ending error interact with `-ErrorAction`, which exception types carry a `throw`, a `Write-Error` under `Stop`, and `break`, what `$Error` and `-WarningVariable` record, and the traps in measuring it. Read it before you catch, wrap, or rethrow an exception, or change how the cmdlet reports one.
- `references/conversions.md`: what converting `$null` gives for each kind of type, and what a typed list or dictionary stores for it. Read it before you change how a value converts to a parameter's or an element's type.
- `references/completion.md`: what PowerShell passes an argument completer for a quoted or comma-split argument, which arguments it runs for, and what it does when a completer returns `null` or nothing. Read it before you add or change an argument completer.
- `references/help.md`: where Get-Help looks for a binary module's help file, when the DLL is imported by its path and through a manifest, and for other UI cultures, what it shows from the file as written, what it does without one, the help objects that tests read, and what `Update-Help` does with the manifest's `HelpInfoURI`. Read it before you add or change the help file, where it's copied or shipped, the tests that check it, or the manifest's `HelpInfoURI`.
- `references/assembly-loading.md`: how Windows PowerShell 5.1 resolves a module's dependencies, what PowerShell does on each import, why a referenced assembly's type names don't resolve right after the import, how to load a build's DLLs in a script, and how to check the PowerShell 7 version. Read it before you add a dependency, change the frameworks the builds target or which build each edition loads, add an assembly resolver or an `IModuleAssemblyInitializer`, or chase a `FileNotFoundException` or `FileLoadException` in Windows PowerShell 5.1.

## Measuring PowerShell behavior

- Measure in both editions while the module supports both, on stable releases rather than previews, and record the versions and the date with each result.
- Run each experiment in a new process, with `pwsh -NoProfile -Command ...` or `powershell.exe -NoProfile -Command ...`. A process that has loaded the build keeps its DLLs locked until it exits, and PowerShell 7 can't load two builds of the same assembly in one process (see `references/assembly-loading.md`).
- When an experiment imports the module, import the build by its path with `-ErrorAction Stop`, and check that the command comes from the build, with `(Get-Command New-RandomPassword).Module.ModuleBase`, before you trust a result. Otherwise an installed NewRandomPassword, such as a release from the PowerShell Gallery, can answer instead: when the import fails, the next call auto-loads the installed module without an error.
- To pass an array to a script, use `-Command "& '<script>' -Path a, b"`. Under `-File`, `-Path a,b` arrives as the single string `'a,b'`.
- `Add-Type` in Windows PowerShell 5.1 compiles C# 5, so scratch C# that runs in both editions can't use pattern matching such as `is PSObject p`, string interpolation, `?.`, or `nameof`. Read a non-public member through reflection, by the name it has in each edition.
- In script, `$psObject.BaseObject` reads a member of the wrapped object, which is usually `$null`. Use `$psObject.psobject.BaseObject`.
- Pester runs each test inside a `try` block, where an error that ends a statement and one that ends the script both jump to `catch`. To see how an error ends a script, run it in a new process, outside any `try` block (see `references/errors.md`).
- When an experiment or a test should show that a change makes a difference, also run it against the build from before the change, and check that the difference shows.
