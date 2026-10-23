---
name: nrp-testing
description: How to run and write the NewRandomPassword tests, and how to try a build in a repro or smoke test, in Windows PowerShell 5.1 and PowerShell 7. The xUnit.net v3 tests in `src/MG.Passwords.Tests/` call `MG.Passwords` directly, and nothing tests the cmdlets automatically yet. Covers the commands and their traps, which PowerShell versions to use, when a test is worth writing, how to write one, and how to make sure a repro runs the build and not an installed release. Use it before you run, write, change, or debug a test, fix a bug, add or change an `MG.Passwords` type or a cmdlet, try the module in a repro or smoke test, or report test results, even when the request only asks to check that something works.
---

# Testing NewRandomPassword

The xUnit.net v3 tests in `src/MG.Passwords.Tests/` call `MG.Passwords` directly, and each one runs twice: in PowerShell 7 against the engine's `net10.0` build, and in Windows PowerShell 5.1 against its `netstandard2.0` build. A cause in `src/MG.Passwords/` gets its tests there. The engine grants the test assembly `InternalsVisibleTo`, so a test can call an internal type directly.

Nothing tests the shell projects, where the cmdlets live, automatically yet. Check a change there with a repro or smoke test in both editions (see Repros and smoke tests), and say in your report that no automated test covers it.

## PowerShell versions

Use only stable PowerShell releases: Windows PowerShell 5.1, and PowerShell 7.6 or a later stable release. Use a preview or a release candidate only when you're asked to. That goes for repros, smoke tests, and experiments. When you report results, name the versions you used.

- The tests don't run in the installed `pwsh`. The `net10.0` tests host the PowerShell 7 that the `Microsoft.PowerShell.SDK` package brings, and the `net48` tests host the Windows PowerShell 5.1 that's installed with Windows.
- Stable PowerShell 7 installs to `C:\Program Files\PowerShell\7`, and previews and release candidates install to `C:\Program Files\PowerShell\7-preview`. Before a repro, check that the `pwsh` on PATH is the stable one, with `(Get-Command pwsh).Source`, or run `C:\Program Files\PowerShell\7\pwsh.exe` by its full path.
- The module itself refuses a 7.6 release candidate: `NewRandomPassword.psm1` requires `$PSVersionTable.PSVersion -ge [version]'7.6'`, which is `False` for `7.6.0-rc.1`.

## Running the tests

`dotnet test` builds the projects first. Run it from the repo root:

```
dotnet test src/MG.Passwords.Tests/MG.Passwords.Tests.csproj -c Debug            # both targets
dotnet test src/MG.Passwords.Tests/MG.Passwords.Tests.csproj -c Debug -f net48   # Windows PowerShell 5.1 only
dotnet test src/MG.Passwords.Tests/MG.Passwords.Tests.csproj -c Debug --filter-class MG.Passwords.Tests.RunspaceFixtureTests
```

- `xunit.v3` 4.x runs on Microsoft.Testing.Platform v2, so the project builds an executable, and the root `global.json` switches `dotnet test` to that platform. Don't add `Microsoft.NET.Test.Sdk` or `xunit.runner.visualstudio`. Without `global.json`, `dotnet test` takes the VSTest path, and the build fails with "Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later."
	- In that mode `dotnet test` runs only Microsoft.Testing.Platform projects, so a new test project has to use the platform too.
	- If the platform itself ever stops the tests from running, the fallback is the `xunit.v3.mtp-off` package, the same xUnit.net framework on VSTest. VSTest support is going away, so the fallback only buys time. Ask before you switch.
- `dotnet test` passes every option it doesn't recognize to the test app, which stops before it runs any test. The run ends with `error: 1`, "Handshake failures", and exit code 5, and nothing names the option. `--no-incremental` does this, so for a full rebuild, run `dotnet build --no-incremental` and then `dotnet test --no-build`. `--nologo` is fine with .NET SDK 10.0.402, which lists `-nologo, --no-logo` among its own options, and so is `-v q`.
- `dotnet test --help`, run from the repo root, lists the options that `dotnet test` takes and the ones that the test app takes. Check an option there before you use it.
- Check the exit code, because a rejected option and a filter that matches nothing both end with "Zero tests ran". Exit code 5 means the test app rejected an option, so check the command line first. Exit code 8 means no test ran, such as when a filter matched nothing.
- `-f net10.0` or `-f net48` runs one target. xUnit.net's `--filter-class`, `--filter-method`, `--filter-namespace`, and `--filter-trait`, their `--filter-not-` forms, and `--filter` with the VSTest syntax, such as `--filter "FullyQualifiedName~Runspace"`, narrow a run.
- Keep `Microsoft.PowerShell.SDK` in `src/Directory.Packages.props` on a stable release, at the same version as `System.Management.Automation`.
- The `net48` test app gets binding redirects in its generated `MG.Passwords.Tests.exe.config`, for its own dependencies such as `System.Memory`. Windows PowerShell 5.1 gives a module none, so a passing `net48` test doesn't show that the module can load its dependencies there. Check that with a smoke test in `powershell.exe`. The `nrp-pwsh-internals` skill's `references/assembly-loading.md` explains why a module needs its own resolver.

## Reporting results

Name the target framework of each run. Give the passed and failed counts for each target, and quote the message of each failure. A `dotnet test` run that exits with 8 ran no tests, so report it as a filter mistake, not as a pass. For a repro or smoke test, name the PowerShell version that each edition printed.

## When to write a test

Write a test only when it has merit. Don't write a failing test before every fix, and don't give every change a test.

- **Has merit:** the test pins down behavior that users rely on and that a later change could plausibly break, such as a fixed bug whose cause could come back. `CLAUDE.md` names two such bugs that password generation has to be tested for again when it's rewritten. A test also has merit when it records a decision that the code doesn't make obvious, the way `RunspaceFixtureTests` records which PowerShell each target tests in.
- **Has no merit:** the test only restates the code, an existing test already covers the behavior, or the change can't regress in a way that users would notice.
- **Usually needs no new test:** a refactor that doesn't change behavior, removing dead code, making a type internal, a rename, a change to comments or docs, or a release step such as copying the release DLLs into `NewRandomPassword/`. Run the existing tests instead, and if none of them reaches the code you changed, say so.

When a test has merit, write it before or after the change, whichever helps.

## Writing a test

The test folders mirror the engine's. The tests for `src/MG.Passwords/<Folder>/<Type>.cs` go in a `sealed` class in `src/MG.Passwords.Tests/<Folder>/<Type>Tests.cs`, in the namespace `MG.Passwords.Tests.<Folder>`. A test class that runs PowerShell takes the fixture, in this shape, from `RunspaceFixtureTests.cs`:

```csharp
namespace MG.Passwords.Tests;

public sealed class RunspaceFixtureTests : IClassFixture<RunspaceFixture>
{
	private readonly RunspaceFixture _runspace;

	public RunspaceFixtureTests(RunspaceFixture runspace)
	{
		_runspace = runspace;
	}

	[Fact]
	public void Enter_RunsScriptBlocksInThePowerShellOfTheTarget()
	{
		using RunspaceScope scope = _runspace.Enter();
		Collection<PSObject> results = ScriptBlock.Create("'{0} {1}' -f $PSVersionTable.PSEdition, $PSVersionTable.PSVersion.Major").Invoke();
		// ...
	}
}
```

- A script block runs only on a thread whose `Runspace.DefaultRunspace` is set, and that property is thread-static. A test class that runs script blocks, or calls code that does, takes a `RunspaceFixture` through `IClassFixture<RunspaceFixture>`, and each such test starts with `using RunspaceScope scope = _runspace.Enter();`. A test that runs no PowerShell needs no fixture.
- `RunspaceFixture.EngineIntrinsics` is the runspace's `$ExecutionContext`, which PowerShell passes to a parameter's transformation and validation attributes. Pass it when a test calls one of those attributes directly.
- Name each test `Subject_Behavior`, usually the member it calls followed by the behavior it checks. Use `[Theory]` with `[InlineData]` for cases that differ only in their data.
- Pass a `null` argument by name, as in `additionalVariables: null`, so the call shows which parameter it leaves empty.
- Wrap a value in a `PSObject` with `PSObject.AsPSObject(42)` or `new PSObject((object)42)`, never `new PSObject(42)`, which builds an empty custom object on `net10.0` (see the `nrp-pwsh-internals` skill's `references/psobject.md`).
- `Assert.IsType<T>` returns its argument as a `T`, and `Assert.Throws<T>` returns the exception, so one call both checks the value and hands it on to the next assertion.
- Every test also builds and runs for `net48`, against the Windows PowerShell 5 reference assemblies, so an API that only .NET 10 or PowerShell 7 has breaks the build. When a test has to differ between the targets, use `#if NETFRAMEWORK`, as `RunspaceFixtureTests` does.
- When an expected value isn't obvious, say in a comment where it comes from.
- `.editorconfig` sets the code style, and the test project already follows it. Test classes and test methods have no XML documentation comments. Fixtures and helper types, such as `RunspaceFixture`, do, following the `csharp-xml-docs` skill.

## Repros and smoke tests

The version rule under PowerShell versions applies to repros and smoke tests. In PowerShell 7, import `MG.Passwords.Shell.dll` from `src/MG.Passwords.Shell/bin/<configuration>/net10.0/`. In Windows PowerShell 5.1, import `MG.Passwords.Shell.dll` from `src/MG.Passwords.Shell-NETFramework/bin/<configuration>/net48/`. Import it in a new process, such as `pwsh -NoProfile -File <script>` or `powershell.exe -NoProfile -File <script>`. A process that has loaded the DLL keeps the file locked until it exits, so the next build can't replace it.

A machine can have NewRandomPassword 1.2.0 from the PowerShell Gallery installed in a module folder on `PSModulePath`, such as `C:\Program Files\PowerShell\Modules`, where PowerShell 7 finds it. It exports the same command and alias names, `New-RandomPassword` and `rpas`. When a repro fails to import the build, the first call auto-loads 1.2.0 without an error, and every result after that comes from 1.2.0. So in every repro and smoke-test script:

- Set `$PSModuleAutoLoadingPreference = 'None'` first. That also hides the commands of modules that aren't loaded yet, so import `Microsoft.PowerShell.Utility` yourself when the script needs one of its commands, such as `Select-Object`, and `Microsoft.PowerShell.Management` when it needs one of those, such as `Join-Path`.
- Import the build by its absolute path, with `-ErrorAction Stop`. PowerShell treats a relative path without a leading `./`, such as `src/...`, as a module name, and the import fails with `Modules_ModuleNotFound` in both editions.
- Print the path of the module that ran, such as `(Get-Module MG.Passwords.Shell).Path`, so the output shows which build it was.
- If the script names an `MG.Passwords` type, import `MG.Passwords.dll` from the same folder before the shell, as the shell projects' `Debug.ps1` scripts do. Importing the shell doesn't load `MG.Passwords.dll`, so until a cmdlet runs, a type literal for one of its types fails with "Unable to find type". To reach an internal type, get the assembly from `[AppDomain]::CurrentDomain.GetAssemblies()`.
- Call the cmdlet by its name, `New-RandomPassword`, not by its alias. To check the alias, read `$module.ExportedAliases['rpas'].Definition`, which is the cmdlet's name.
- The script runs in Windows PowerShell 5.1 too, so it can't use PowerShell 7 syntax such as `??`, the ternary operator, or `&&`. Keep it ASCII, because 5.1 reads a UTF-8 file without a BOM as ANSI.
- After a session moves to another machine, check whether that machine has an installed copy, with `Get-Module -ListAvailable NewRandomPassword` in each edition.

To try the manifest and `NewRandomPassword.psm1` themselves, copy both to a scratch folder, put each Release output's `MG.Passwords.dll` and `MG.Passwords.Shell.dll` beside them in `Core\` (`net10.0`) and `Desk\` (`net48`), and import the copied manifest by its path. The `NewRandomPassword/` folder in the repo holds only the DLLs copied in for a release. To try the version check with versions that aren't installed, run the `.psm1`'s text with `$PSVersionTable` renamed, as the `nrp-pwsh-internals` skill's `references/assembly-loading.md` describes.

The `nrp-pwsh-internals` skill covers measuring PowerShell's own behavior.
