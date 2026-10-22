# Assembly loading

## Resolving dependencies in Windows PowerShell 5.1

Measured 2026-10-05 on Windows PowerShell 5.1.26100.9444 with a binary module's Debug `net48` build.

- `ResolveEventArgs.RequestingAssembly` is `null` for a static reference from a module's assembly, including one that `Import-Module` loaded for another module. The only non-null requester seen was `Anonymously Hosted DynamicMethods Assembly`, a dynamic assembly, for `[Reflection.Assembly]::Load()` called from script. Satellite `.resources` lookups have a `null` requester too. So a requester check can't tell one module's binds from another's.
- The runtime rejects a handler's result whose public key token is wrong (`FileLoadException`, 0x80131040), but accepts any version, including a downgrade.
- The runtime reuses each answer: once a handler returns an assembly for an identity, binding that identity again raises no new event.
- `Assembly.LoadFile` and `Assembly.LoadFrom` on the same path return the same `Assembly` object, in either order. No duplicate copies appeared in the session.
- The runtime calls `AssemblyResolve` handlers in registration order and stops at the first non-null result. PowerShell registers two handlers of its own, from `LanguagePrimitives` and `ExecutionContext`, ahead of any module's. To see whether a handler answered an event, register a logging handler before it and another after it.
- PowerShell calls `IModuleAssemblyInitializer.OnImport` on every import, including a re-import after `Remove-Module` and an `Import-Module -Force`, so `OnImport` has to guard against registering a handler twice.

## Type names and lazy loading

PowerShell resolves a type name only against assemblies that are already loaded, and the runtime loads an assembly the first time code needs it. Importing a module's DLL doesn't load the assemblies it references, so until a cmdlet runs, a type from one of them isn't found: `'<type name>' -as [type]` is `$null` and a type literal fails with "Unable to find type", in both editions (seen 2026-10-07).

## Loading a build's DLLs in a script

To inspect a build, such as counting the public types each build exports with `Assembly.GetExportedTypes()`, load each DLL with `[Reflection.Assembly]::LoadFrom` in a new process, so the build output stays unlocked.

- Load two builds of the same assembly, with the same name and version, in separate PowerShell 7 processes. A second `LoadFrom` in the same process throws "Assembly with same name is already loaded". In a `foreach` loop that error doesn't stop the script, the variable keeps the first assembly, and the second path prints the first build's results under its own name (found 2026-10-07). DLLs whose assembly names differ can share a process.
- In Windows PowerShell 5.1, `GetExportedTypes()` on a `netstandard2.0` assembly whose dependencies need the module's resolver throws `FileNotFoundException` for one of them, because loading the DLL from a script runs no module resolver (found 2026-10-07). PowerShell 7 loads the same `netstandard2.0` assembly without help.

## The PowerShell 7 version

Measured 2026-10-10 on PowerShell 7.6.6 with a binary module's Release build.

- PowerShell 7.6.6 runs `System.Management.Automation` 7.6.0.500. An assembly compiled against the 7.6.6 package references 7.6.0.500 too, so the reference doesn't name the package's patch release.
- `$PSVersionTable.PSVersion` is a `SemanticVersion`, and `-ge [version]'7.6'` compares it with 7.6.0. It's `True` for 7.6.0, 7.6.6, 7.7.0-preview.1, and 8.0.0, and `False` for 6.2.7, 7.4.13, 7.5.4, 7.6.0-preview.5, and 7.6.0-rc.1.
- A child scope can't shadow `$PSVersionTable`: assigning to it fails with "Cannot overwrite variable PSVersionTable because it is read-only or constant." To try a version check with other versions, run its text with the variable renamed.

## What it means for NewRandomPassword

- A module can't add binding redirects. When a Windows PowerShell 5.1 build ships a dependency whose assembly version is higher than the version another dependency references, as the `net462` builds of NuGet packages often are, the module needs an `AssemblyResolve` handler that answers only for an identity that an assembly in the module's folder references. Register it from `IModuleAssemblyInitializer.OnImport`, and only on the first call.
- Don't build that handler's list of references in `OnImport`. Right after the import, only the module's own assembly is loaded, and the references that need an answer can come from assemblies that load later.
- PowerShell 7.5 and earlier run on .NET 9 or earlier, which can't load a `net10.0` assembly. A PowerShell 7 build that targets `net10.0` needs a check such as `$PSVersionTable.PSVersion -ge [version]'7.6'` before PowerShell loads it, with an error that names the versions the module supports.
