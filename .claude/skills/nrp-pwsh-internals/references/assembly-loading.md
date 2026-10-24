# Assembly loading

## Resolving dependencies in Windows PowerShell 5.1

Measured 2026-10-05 on Windows PowerShell 5.1.26100.9444 with a binary module's Debug `net48` build.

- `ResolveEventArgs.RequestingAssembly` is `null` for a static reference from a module's assembly, including one that `Import-Module` loaded for another module. The only non-null requester seen was `Anonymously Hosted DynamicMethods Assembly`, a dynamic assembly, for `[Reflection.Assembly]::Load()` called from script. Satellite `.resources` lookups have a `null` requester too. So a requester check can't tell one module's binds from another's.
- The runtime rejects a handler's result whose public key token is wrong (`FileLoadException`, 0x80131040), but accepts any version, including a downgrade.
- The runtime reuses each answer: once a handler returns an assembly for an identity, binding that identity again raises no new event.
- `Assembly.LoadFile` and `Assembly.LoadFrom` on the same path return the same `Assembly` object, in either order. No duplicate copies appeared in the session.
- The runtime calls `AssemblyResolve` handlers in registration order and stops at the first non-null result. PowerShell registers two handlers of its own, from `LanguagePrimitives` and `ExecutionContext`, ahead of any module's. To see whether a handler answered an event, register a logging handler before it and another after it.
- PowerShell calls `IModuleAssemblyInitializer.OnImport` on every import, including a re-import after `Remove-Module` and an `Import-Module -Force`, so `OnImport` has to guard against registering a handler twice.

## System.Memory in Windows PowerShell 5.1

Measured 2026-10-22 on Windows PowerShell 5.1.26100.9444 with System.Memory 4.6.3, using a Debug `net48` build whose `netstandard2.0` engine ran span code.

- Each package's `netstandard2.0` build has a lower assembly version than its `net462` build: System.Memory 4.0.2.0 and 4.0.5.0, System.Buffers 4.0.2.0 and 4.0.5.0, System.Numerics.Vectors 4.1.3.0 and 4.1.6.0, and System.Runtime.CompilerServices.Unsafe 6.0.0.0 and 6.0.3.0. Each build of System.Memory references the other three packages' builds for the same framework.
- An engine that uses spans references System.Memory 4.0.2.0, and the `net48` build output gets the `net462` files. Without a resolver, a cmdlet that runs the engine's span code fails with `FileNotFoundException`: "Could not load file or assembly 'System.Memory, Version=4.0.2.0, ...'". With the resolver, the cmdlet runs, and System.Memory 4.0.5.0 loads from the module's folder.
- Importing the engine DLL before the shell fails with the same `FileNotFoundException` when one of the engine's types implements an interface from System.Memory, such as `IBufferWriter<char>`. The import loads the engine's types, and the resolver isn't registered until the shell's import. Importing the shell first and then the engine works.
- The engine's own `netstandard2.0` build output holds the `netstandard2.0` files, whose versions match its references, so importing the engine from there works without a resolver.
- A resolver that answers every request for a file in its folder, without checking the module's references, answers `[Reflection.Assembly]::Load('System.Memory, Version=99.0.0.0, ...')` with System.Memory 4.0.5.0. A resolver that checks the references lets that request fail with `FileNotFoundException`.

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

## What a manifest exports through a `.psm1`

Measured 2026-10-23 on PowerShell 7.6.8 and Windows PowerShell 5.1.26100.9711 with a throwaway binary module laid out like NewRandomPassword: the manifest's `RootModule` is a `.psm1` whose only statement imports the DLL from a subfolder, `CmdletsToExport` names the cmdlet, and `AliasesToExport` names the alias that the cmdlet class's `[Alias]` attribute declares.

- In both editions, importing the manifest exports the cmdlet and the alias, and calling the alias runs the cmdlet. The `.psm1` has no `Export-ModuleMember`. The DLL's nested module lists the alias among its own exported aliases too.

## What it means for NewRandomPassword

- A module can't add binding redirects. When a Windows PowerShell 5.1 build ships a dependency whose assembly version is higher than the version another dependency references, as the `net462` builds of NuGet packages often are, the module needs an `AssemblyResolve` handler that answers only for an identity that an assembly in the module's folder references. Register it from `IModuleAssemblyInitializer.OnImport`, and only on the first call.
- Don't build that handler's list of references in `OnImport`. Right after the import, only the module's own assembly is loaded, and the references that need an answer can come from assemblies that load later.
- In Windows PowerShell 5.1, import the shell before `MG.Passwords.dll`, in scripts and in tests. The shell's import registers the resolver, and without it the engine's import fails as soon as one of the engine's types needs a dependency that the resolver loads.
- PowerShell 7.5 and earlier run on .NET 9 or earlier, which can't load a `net10.0` assembly. A PowerShell 7 build that targets `net10.0` needs a check such as `$PSVersionTable.PSVersion -ge [version]'7.6'` before PowerShell loads it, with an error that names the versions the module supports.
- `NewRandomPassword.psm1` needs no `Export-ModuleMember` for the cmdlet or `rpas`. The cmdlet class's `[Alias("rpas")]` and the manifest's `CmdletsToExport` and `AliasesToExport` export both. The tests import the DLL by its path, so only a test that imports a staged manifest covers this.
