# NewRandomPassword

PowerShell module that provides the `New-RandomPassword` cmdlet (alias `rpas`). It's published to the PowerShell Gallery as `NewRandomPassword`. GitHub: `Yevrag35/NewRandomPassword`.

The module runs on Windows PowerShell 5.1 (`powershell.exe`) and on PowerShell 7.6 or later (`pwsh`), and every change has to work in both.

## Layout

| Path | Contents |
|---|---|
| `src/MG.Passwords/` | The engine, built for `netstandard2.0` (Windows PowerShell 5.1) and `net10.0` (PowerShell 7): `PasswordGenerator`, `SecureRandom`, the character sets, and the attributes that convert and check the cmdlet's input. |
| `src/MG.Passwords.Shell/` | The cmdlet, `NewRandomPasswordCommand`, and `CharacterSet`, the enum of `-CharacterSet`, built for PowerShell 7.6 or later (`net10.0`). |
| `src/MG.Passwords.Shell-NETFramework/` | The Windows PowerShell 5.1 build of the cmdlet (`net48`). It compiles every `.cs` file of `MG.Passwords.Shell` and adds `ModuleInitializer`, so shell code goes in `MG.Passwords.Shell`, and this project holds only what Windows PowerShell 5.1 alone needs. Both shell projects build `MG.Passwords.Shell.dll`. |
| `src/MG.Passwords.Tests/` | xUnit.net v3 tests of the engine. The `net10.0` target tests the engine's `net10.0` build in the PowerShell 7 that the `Microsoft.PowerShell.SDK` package brings, and the `net48` target tests its `netstandard2.0` build in the Windows PowerShell 5.1 that's installed with Windows. |
| `tests/` | Pester 6 tests, which import the build and test the module the way a user runs it. |
| `NewRandomPassword/` | The module as it ships: the manifest, `NewRandomPassword.psm1`, the help file in `en-US/`, and the Release DLLs in `Core/` (PowerShell 7) and `Desk/` (Windows PowerShell 5.1). |
| `tools/check-bytes.ps1` | Checks files for a BOM, CRLF line endings, and invalid UTF-8. |

`NewRandomPassword.slnx` holds every project. `src/Directory.Build.props` holds the settings that the projects share, the version and the global usings among them. `src/Directory.Packages.props` holds every package version, so a `PackageReference` names none. The root namespace is `MG.Passwords` in every project but the tests, whose root namespace is `MG.Passwords.Tests`.

## How the module loads

- The manifest's `RootModule` is `NewRandomPassword.psm1`. It imports `Desk\MG.Passwords.Shell.dll` in Windows PowerShell 5.1 and `Core\MG.Passwords.Shell.dll` in PowerShell 7.6 or later. PowerShell 7.5 and earlier run on versions of .NET that can't load the `net10.0` build, so there it stops the import with an error that names the supported versions.
- Windows PowerShell 5.1 gives a module no binding redirects, so `ModuleInitializer`, an `IModuleAssemblyInitializer`, registers a resolver that loads the engine's dependencies, such as `System.Memory`, from the module's folder. A package that the engine's `netstandard2.0` build adds ships in `Desk/` with its dependencies, and each of their DLLs goes in the manifest's `FileList`. The `net48` engine tests get binding redirects that a module never gets, so only the Pester tests, or a script run in `powershell.exe`, show that a dependency loads.
- Importing the shell doesn't load `MG.Passwords.dll`, and PowerShell resolves a type name only against the assemblies that are loaded, so a type that scripts name, such as `CharacterSet`, belongs in the shell.

## Building

- `dotnet build NewRandomPassword.slnx -c Debug` builds every project. Building the two shell projects builds the engine too, which is all that the Pester tests need.
- Every project builds into `src/<project>/bin/<configuration>/<target framework>/`, Release included, and nothing in the build writes to `NewRandomPassword/`. A release copies every DLL in `src/MG.Passwords.Shell/bin/Release/net10.0/` into `NewRandomPassword/Core/` and every DLL in `src/MG.Passwords.Shell-NETFramework/bin/Release/net48/` into `NewRandomPassword/Desk/`.
- PowerShell loads a module's dependencies from the module's folder and ignores its `deps.json`, so `CopyLocalLockFileAssemblies` puts every package's runtime assemblies in each build output. The engine and the shell projects reference PowerShell's own packages with their runtime assets excluded, so that no part of PowerShell is copied. Only the tests take `Microsoft.PowerShell.SDK` whole, to host PowerShell 7, and it stays on a stable release, at the same version as `System.Management.Automation`.
- In Debug, every project but the tests copies its `Debug.ps1` into its build output, and the project's launch profiles start `pwsh` or `powershell.exe` with it, to try the build by hand.

## Testing

Both suites run every test in Windows PowerShell 5.1 and in PowerShell 7. Use stable PowerShell releases, not previews or release candidates, unless you're asked to.

- **Pester:** `tests/Invoke-Tests.ps1` builds nothing and tests whatever the last build of the configuration left in the output folders, so build both shell projects first. `pwsh -NoProfile -File tests/Invoke-Tests.ps1` then tests the Debug build in both editions, and `-Configuration Release`, `-Edition Core` or `-Edition Desktop`, and `-Path <file>` change that. The tests import the build output under `src/`, never the DLLs in `NewRandomPassword/`.
- **Engine:** `dotnet test src/MG.Passwords.Tests/MG.Passwords.Tests.csproj -c Debug` builds the projects and tests both targets, and `-f net48` or `-f net10.0` tests one. The tests run on Microsoft.Testing.Platform, which the root `global.json` opts `dotnet test` into, so don't add `Microsoft.NET.Test.Sdk` or `xunit.runner.visualstudio`. `dotnet test` hands an option that it doesn't know to the test app, which stops with exit code 5 without naming the option, so never pass `--no-incremental` to `dotnet test`: for a full rebuild, run `dotnet build --no-incremental`, then `dotnet test --no-build`.

## Code

- The engine's types are `internal`, and its `InternalsVisibleTo` covers the shell and the tests. The shell's public types are the cmdlet, `CharacterSet`, and, in the Windows PowerShell 5.1 build, `ModuleInitializer`, so no script can come to depend on an engine type.
- The cmdlet's surface is public: scripts call the cmdlet by its name or its alias and bind its parameters by name, alias, and position, so a change to a name, an alias, a position, a parameter set, or a `CharacterSet` value can break them. Don't change the surface or add to it unless you're asked to. The Pester tests pin the surface down, so a change that's asked for updates them too.
- Every random choice goes through `SecureRandom`, which draws from the operating system's cryptographically secure random number generator. Never use `System.Random`.
- No copy of a password stays behind. The `net10.0` build writes each password straight into its string with `string.Create`. The `netstandard2.0` build writes it in a buffer on the stack or rented from `ArrayPool<T>.Shared`, copies it into the string, and clears the buffer. Clear a rented array yourself before you return it, because `clearArray: true` doesn't clear an array that the pool drops instead of keeping. A buffer that tells something about a password, such as which groups it drew from, gets cleared too. Characters stay UTF-16 code units from input to output, with no `Encoding` or BSTR in between.
- Where the `net10.0` and `netstandard2.0` builds differ, the difference stays inside the method that needs it, behind `#if NET`, and the builds share the rest of the code.
- Every project compiles C# 14 with nullable reference types on and implicit usings off, and the global usings in `src/Directory.Build.props` are the same for every target framework. `System.Diagnostics.CodeAnalysis` and `System.Management.Automation` both define `AllowNullAttribute`, so a bare `[AllowNull]` fails with CS0104: write `[AllowsNull]` or `[PSAllowNull]`, the aliases defined there. PolySharp generates the types that C# 14 needs on `netstandard2.0` and `net48`, such as the nullable attributes, and each project that targets one of them references PolySharp itself, because one assembly's polyfills aren't visible to another.
- `.editorconfig` sets the C# style, and `dotnet build` reports little of it, so follow it as you write: file-scoped namespaces, explicit types instead of `var`, braces on every block, `this.` before properties and methods but not fields, `_camelCase` private fields, `s_camelCase` private static readonly fields, `ALL_UPPER` private constants, `sealed` classes, and constructors instead of primary constructors.
- Every type and member in the engine and the shell projects has an XML documentation comment, and so do the test project's fixtures and helpers, but not its test classes and test methods. A public type's documentation names no internal type.

## Manifest and help

- `<Version>` in `src/Directory.Build.props` and `ModuleVersion` in `NewRandomPassword/NewRandomPassword.psd1` change together, and a Pester test fails while they differ.
- The manifest's `CmdletsToExport` and `AliasesToExport` list every cmdlet and alias that the build exports, and a Pester test fails while they differ. `FileList` lists every file that ships.
- Leave the manifest's `HelpInfoURI` unset until there's a hosted folder of updatable help. Any other URL makes `Update-Help` fail.
- The help is hand-written MAML, in `NewRandomPassword/en-US/MG.Passwords.Shell.dll-Help.xml`, and both shell projects copy it into `en-US/` in their build output. Get-Help shows the syntax and the parameters as the file writes them, not as the cmdlet declares them, so a change to a parameter changes the file too.
- Three places describe the cmdlet to its users: the help file, `README.md`, and the manifest's `ReleaseNotes`. A change that users can see updates all three.

## Text files

- Every text file, whatever its type, is UTF-8 without a BOM, with LF line endings, and indented with tabs, as `.gitattributes` and `.editorconfig` set. `pwsh -NoProfile -File tools/check-bytes.ps1 <files>`, with the files separated by spaces, checks the bytes, and `-Fix` strips a BOM and turns CRLF into LF. Without files, the script checks every file changed since `HEAD` and every untracked file that isn't ignored.
- Windows PowerShell 5.1 reads a PowerShell file without a BOM as ANSI, so every `.ps1`, `.psm1`, and `.psd1` that it runs stays ASCII, and uses only syntax that 5.1 parses, without `??`, the ternary operator, or `&&`.

## Branches and plans

- Pull requests target `dev`, not `master`, and merge as one squash commit, after which the branch is deleted. Cite merged work by its PR number, as `#<number>`, because the branch's own commit hashes no longer resolve.
- Multi-session plans live in `.ideas/`, one file per topic. Plans are working notes, not documentation: check what one says about the code against the current tree before you build on it.
