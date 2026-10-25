---
name: nrp-testing
description: How to run and write the NewRandomPassword tests in Windows PowerShell 5.1 and PowerShell 7, and how to try a build in a repro or smoke test. The Pester 6 tests in `tests/` import the build and test the module the way a user runs it, and the xUnit.net v3 tests in `src/MG.Passwords.Tests/` call `MG.Passwords` directly. Covers the commands and their traps, which PowerShell versions to use, when a test is worth writing and which suite it goes in, how to write tests, and how to make sure a repro runs the build and not an installed release. Use it before you run, write, change, or debug a test, fix a bug, add or change a cmdlet, parameter, or `MG.Passwords` type, change the manifest or `NewRandomPassword.psm1`, try the module in a repro or smoke test, or report test results, even when the request only asks to check that something works.
---

# Testing NewRandomPassword

NewRandomPassword has two test suites, and both run every test in Windows PowerShell 5.1 and in PowerShell 7:

- The Pester 6 tests in `tests/` import the build and test the module the way a user runs it. They don't build anything, so build first (see Running the Pester tests).
- The xUnit.net v3 tests in `src/MG.Passwords.Tests/` call `MG.Passwords` directly: in PowerShell 7 against its `net10.0` build, and in Windows PowerShell 5.1 against its `netstandard2.0` build. `dotnet test` builds the projects first.

## PowerShell versions

Use only stable PowerShell releases: Windows PowerShell 5.1, and PowerShell 7.6 or a later stable release. Use a preview or a release candidate only when you're asked to. That goes for test runs, repros, smoke tests, and experiments. When you report results, name the versions you used.

- `Invoke-Tests.ps1` starts whichever `pwsh` comes first on PATH, which can be a preview or a release candidate. Each edition's output starts with its version, such as `PowerShell 7.6.8 (Core), Debug build`, so check that line.
- Stable PowerShell 7 installs to `C:\Program Files\PowerShell\7`, and previews and release candidates install to `C:\Program Files\PowerShell\7-preview`. When PATH finds a preview, run the PowerShell 7 tests in the stable `pwsh.exe` with `-InProcess`, as in `& 'C:\Program Files\PowerShell\7\pwsh.exe' -NoProfile -File tests/Invoke-Tests.ps1 -InProcess`, and run the Windows PowerShell tests with `-Edition Desktop`. For a repro, check the `pwsh` on PATH with `(Get-Command pwsh).Source`.
- The module itself refuses a 7.6 release candidate: `NewRandomPassword.psm1` requires `$PSVersionTable.PSVersion -ge [version]'7.6'`, which is `False` for `7.6.0-rc.1`.
- The engine tests don't run in the installed `pwsh`. The `net10.0` tests host the PowerShell 7 that the `Microsoft.PowerShell.SDK` package brings, and the `net48` tests host the Windows PowerShell 5.1 that's installed with Windows.

## Running the Pester tests

Build both shell projects in the configuration you test, then run the tests from the repo root:

```
dotnet build src/MG.Passwords.Shell/MG.Passwords.Shell.csproj -c Debug
dotnet build src/MG.Passwords.Shell-NETFramework/MG.Passwords.Shell-NETFramework.csproj -c Debug
pwsh -NoProfile -File tests/Invoke-Tests.ps1                                              # Debug build, both editions
pwsh -NoProfile -File tests/Invoke-Tests.ps1 -Path tests/Module.Tests.ps1 -Edition Core   # one file, PowerShell 7 only
pwsh -NoProfile -File tests/Invoke-Tests.ps1 -Configuration Release -Output Detailed
pwsh -NoProfile -Command '& ./tests/Invoke-Tests.ps1 -Tag A, B; exit $LASTEXITCODE'        # several tags
```

- The two shell projects build the engine too, which is all that the Pester tests need. Building `NewRandomPassword.slnx` also builds the engine tests.
- `Invoke-Tests.ps1` runs the tests in a new `powershell.exe` process and a new `pwsh` process. The imported DLLs unload when those processes exit, so they don't block the next build. The script exits with 1 when a test fails in either edition.
- The script doesn't build anything. It tests whatever the last build of the configuration left in the output folders, so rebuild after every code change.
- Pass several tags through `-Command`, as in the last example, whose single quotes keep `$LASTEXITCODE` from expanding in PowerShell or Bash. `-File` passes each argument as a single string, so `-Tag A,B` reaches the script as one tag named `A,B`. No test has that tag, so Pester reports every test as NotRun, with 0 passed and 0 failed, and the script exits with 0 even when tests are failing. A summary in which every test is NotRun means the filter matched nothing. It says nothing about the code.
- `-Path` runs only the files or folders you name, such as `-Path tests/Module.Tests.ps1`.
- Every test file calls `tests/Import-NewRandomPassword.ps1` in a top-level `BeforeAll`. It imports `MG.Passwords.Shell.dll` from the `net10.0` output of `MG.Passwords.Shell` (PowerShell 7) or the `net48` output of `MG.Passwords.Shell-NETFramework` (Windows PowerShell 5.1), never the DLLs copied into `NewRandomPassword/` for a release. The `NEWRANDOMPASSWORD_TEST_CONFIGURATION` environment variable picks the configuration, and `Invoke-Tests.ps1` sets it.
- Pester 6 has to be installed where both editions find it, such as `C:\Program Files\WindowsPowerShell\Modules`. Windows PowerShell 5.1 also ships Pester 3.4.0, which `Invoke-Tests.ps1` passes over by importing 6.0 or later.

## Running the engine tests

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
- The `net48` test app gets binding redirects in its generated `MG.Passwords.Tests.exe.config`, for its own dependencies such as `System.Memory`. Windows PowerShell 5.1 gives a module none, so a passing `net48` test doesn't show that the module can load its dependencies there. Check that in `powershell.exe`, with a Pester test or a smoke test.

## Reporting results

Name the versions each run used: the version line that each Pester edition prints, and the target framework of each engine run. Give the passed and failed counts for each edition or target, and quote the message of each failure. A Pester run in which every test is NotRun, or a `dotnet test` run that exits with 8, matched no tests, so report it as a filter mistake, not as a pass. A Pester summary that reports a failed container is a failure even when its Failed count is 0, because the file failed discovery and the tests after the failure never ran. For a repro or smoke test, name the PowerShell version that each edition printed.

## When to write a test

Write a test only when it has merit. Don't write a failing test before every fix, and don't give every change a test.

- **Has merit:** the test pins down behavior that users rely on and that a later change could plausibly break, such as a fixed bug whose cause could come back. A test also has merit when it records a decision that the code doesn't make obvious, the way `RunspaceFixtureTests` records which PowerShell each target tests in.
- **Has no merit:** the test only restates the code, an existing test already covers the behavior, or the change can't regress in a way that users would notice.
- **Usually needs no new test:** a refactor that doesn't change behavior, removing dead code, making a type internal, a rename, a change to comments or docs, or a release step such as copying the release DLLs into `NewRandomPassword/`. Run the existing tests instead, and if none of them reaches the code you changed, say so. The tests import the build output, not the DLLs under `NewRandomPassword/`, so they can't check a release.

When a test has merit, write it before or after the change, whichever helps. Whether or not a change gets tests, `tests/Module.Tests.ps1` checks the module against its manifest in both editions: the build exports exactly the manifest's `CmdletsToExport` and `AliasesToExport`, `rpas` still names `New-RandomPassword`, the build's file version equals `ModuleVersion`, `NewRandomPassword.psm1` loads the build for each edition and the manifest exports `rpas` with it, and PowerShell 7 before 7.6 gets the module's own error. So it fails until `NewRandomPassword.psd1` lists a new cmdlet and its aliases, and until `<Version>` in `src/Directory.Build.props` and `ModuleVersion` change together.

## Which suite a test goes in

- **Pester**, in `tests/<Cmdlet>.Tests.ps1`: anything that can surface during normal use of the module. Test it through the cmdlet, the way a user runs into it. Checks of the module as a whole, such as what it exports and which build each edition loads, go in `tests/Module.Tests.ps1`.
- **Engine**, in `src/MG.Passwords.Tests/`: anything whose cause is in `src/MG.Passwords/`. Test the type directly. The engine grants the test assembly `InternalsVisibleTo`, so a test can call an internal type.
- **Both**, only when each test has merit of its own: an engine problem that also surfaces through a cmdlet, where the engine test pins down the type's behavior and the Pester test shows that the cmdlet passes it on to the user.

Nothing tests the shell projects directly, so a cause there, such as a cmdlet's parameter sets, gets Pester tests only.

## Writing a Pester test

Each cmdlet gets its own `tests/<Cmdlet>.Tests.ps1` file, named after the cmdlet, not an alias, such as `tests/New-RandomPassword.Tests.ps1`. Start it in this shape:

```powershell
BeforeAll {
	& "$PSScriptRoot/Import-NewRandomPassword.ps1"
}

Describe 'New-RandomPassword' {
	Context 'FirstCharacter' {
		It 'starts every password with -FirstCharacter' {
			# ...
		}
	}
}
```

- `Describe` names the cmdlet, and each `Context` names a parameter or a feature, such as `'FirstCharacter'` or `'Pipeline input'`. Each `It` states one behavior, in lowercase and the present tense, such as `'gives each password its own random length'`.
- For cases that differ only in their data, give `-ForEach` one hashtable per case, and put a key such as `<Version>` in the `It` text so that each case reports on its own, as `Module.Tests.ps1` does.
- Pester evaluates `-ForEach` data during discovery, like everything in a file outside its `It` and setup blocks, before any `BeforeAll` imports the build, so the data can't name a type from the build. `-ForEach @(@{ Value = [MG.Passwords.CharacterSet]::Digits })` fails discovery with "Unable to find type [MG.Passwords.CharacterSet].", the tests after it in the file never run, and Pester reports a failed container while its Passed and Failed counts leave them out (measured 2026-10-24 with Pester 6.2.0 in both editions). Give the type's name as a string instead, or put the code in a script block that the test runs.
- Put a helper that test files share in its own script in `tests/`, with comment-based help, and dot-source it in the top-level `BeforeAll`.
- Use Pester 6's `Should-*` commands, not the older `Should -Be` form.
- `Should-Be` compares strings without regard to case, so `'abc' | Should-Be 'ABC'` passes in both editions. A test of a password's characters compares them with `Should-BeString -CaseSensitive`.
- `Should-BeCollection` compares the elements and their count, but not their order: `-Expected 1, 2, 3` passes for `-Actual 3, 1, 2` in both editions. When the order is what a test checks, compare the joined elements with `Should-BeString`, and make sure the test fails when the order is wrong, for example against the code before the change.
- For a terminating error, `{ ... } | Should-Throw` returns the error record, and its `-ExceptionMessage` and `-FullyQualifiedErrorId` take wildcards. For a non-terminating error, run the cmdlet with `-ErrorVariable err -ErrorAction SilentlyContinue`, and then check `$err.Count`, `$err[0].Exception`, and `$err[0].TargetObject`. A non-terminating error that a command writes inside an `It` block fails the test in both editions, so a test that expects one always passes `-ErrorAction SilentlyContinue`.
- For a binding error, the record that `Should-Throw` returns holds a `ParentContainsErrorRecordException` with the same message in place of the `ParameterBindingException`, so the exception's own properties, such as `ParameterName`, aren't there. The record's `FullyQualifiedErrorId` and `CategoryInfo` are the error's own, and an error that a cmdlet raises with `ThrowTerminatingError` keeps its exception (measured 2026-10-24 with Pester 6.2.0 in both editions). To read a binding exception's own properties, catch it with `try` inside the `It` instead.
- Check an exception's type by its name: `$err[0].Exception.GetType().FullName | Should-Be 'Namespace.TypeName'`. Don't pass an exception to `Should-HaveType`. When that assertion fails, Pester 6.2 formats the exception's whole object graph, through `TargetSite`, its assembly, and every type in it, which can take minutes and write hundreds of megabytes. When a run seems to hang, look for a failure like that before you suspect the code.
- For a warning, run the cmdlet with `-WarningVariable warnings -WarningAction SilentlyContinue`. `-WarningVariable` collects the warnings even though they aren't shown.
- To test whether an error ends only its statement or the whole script, run the script in a new process, not inside the `It` block itself: Pester runs each test inside a `try` block, where both kinds of error jump to `catch`. `Module.Tests.ps1` starts a new process of the running edition with `[System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName`.
- When a test covers pipeline input, test piped input and the named parameter separately, because PowerShell binds them differently.
- Before you write a call to a cmdlet, read the transformation and validation attributes on the parameters it uses. A call that fails on input the cmdlet rejects says nothing about the code under test. The same goes for repros and smoke tests.
- Call each cmdlet by its cmdlet name, not an alias, in tests and in scripts that a test runs. After the first `& tests/Import-NewRandomPassword.ps1` in a session, `Get-Command` finds the build's cmdlets but none of its aliases, and after a second import, or after dot-sourcing the script, it finds them too (measured 2026-10-22 in both editions with auto-loading off). Pester runs every file's import in one process, so a test that calls an alias can pass in a full run and fail when its file runs alone or first. To check the alias, read `$module.ExportedAliases['rpas'].Definition`, which is the cmdlet's name, as `Module.Tests.ps1` does.
- Take each expected value from how the module is meant to work and from what native PowerShell does in the same situation in both editions, not from what the code returns today, which may be the bug. When an expected value isn't obvious, say in a comment where it comes from.
- Write each test to pass unchanged in both editions. When the runtimes differ underneath, hide the difference in the test, the way `Module.Tests.ps1` builds its fake version from the running edition's own version type. If a behavior can't match in both editions, ask before you write a test that skips one.
- Test files also run in Windows PowerShell 5.1, so they can't use PowerShell 7 syntax such as `??`, the ternary operator, or `&&`. Keep them ASCII, because 5.1 reads a UTF-8 file without a BOM as ANSI. Indent with tabs.

## Writing an engine test

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
- Wrap a value in a `PSObject` with `PSObject.AsPSObject(42)` or `new PSObject((object)42)`, never `new PSObject(42)`, which builds an empty custom object on `net10.0`.
- `Assert.IsType<T>` returns its argument as a `T`, and `Assert.Throws<T>` returns the exception, so one call both checks the value and hands it on to the next assertion.
- Every test also builds and runs for `net48`, against the Windows PowerShell 5 reference assemblies, so an API that only .NET 10 or PowerShell 7 has breaks the build. When a test has to differ between the targets, use `#if NETFRAMEWORK`, as `RunspaceFixtureTests` does.
- Pass a lone surrogate to a theory as a `char`, and build any string that holds it in the test. An attribute stores a string argument as UTF-8, which has no encoding for a lone surrogate, so the string changes on the way: `"a\uD800b"` came back as a, two U+FFFD, and b (measured 2026-10-24 with PowerShell 7.6.8's `Add-Type`). A `char` argument keeps it.
- A test that checks which arrays `ArrayPool<T>.Shared` hands out belongs in a class marked `[Collection(nameof(SharedArrayPoolCollection))]`, as `PasswordGeneratorTests` is. That collection runs on its own, because in the `net48` tests, all threads share the pool's arrays.
- When an expected value isn't obvious, say in a comment where it comes from.
- `.editorconfig` sets the code style, and the test project already follows it. Test classes and test methods have no XML documentation comments. Fixtures and helper types, such as `RunspaceFixture`, do, following the `csharp-xml-docs` skill.

## Repros and smoke tests

The version rule under PowerShell versions applies to repros and smoke tests. In PowerShell 7, import `MG.Passwords.Shell.dll` from `src/MG.Passwords.Shell/bin/<configuration>/net10.0/`. In Windows PowerShell 5.1, import `MG.Passwords.Shell.dll` from `src/MG.Passwords.Shell-NETFramework/bin/<configuration>/net48/`. `tests/Import-NewRandomPassword.ps1` picks the right one and works with auto-loading off. Import it in a new process, such as `pwsh -NoProfile -File <script>` or `powershell.exe -NoProfile -File <script>`, as `Invoke-Tests.ps1` does. A process that has loaded the DLL keeps the file locked until it exits, so the next build can't replace it.

A machine can have a release of NewRandomPassword from the PowerShell Gallery installed in a module folder on `PSModulePath`, such as `C:\Program Files\PowerShell\Modules`, where PowerShell 7 finds it. It exports the same command and alias names, `New-RandomPassword` and `rpas`. When a repro fails to import the build, the first call auto-loads the installed release without an error, and every result after that comes from it. So in every repro and smoke-test script:

- Set `$PSModuleAutoLoadingPreference = 'None'` first. That also hides the commands of modules that aren't loaded yet, so import `Microsoft.PowerShell.Utility` yourself when the script needs one of its commands, such as `Select-Object`, and `Microsoft.PowerShell.Management` when it needs one of those, such as `Join-Path` or `Test-Path`.
- Import the build by its absolute path, with `-ErrorAction Stop`. PowerShell treats a relative path without a leading `./`, such as `src/...`, as a module name, and the import fails with `Modules_ModuleNotFound` in both editions.
- Print the path of the module that ran, such as `(Get-Module MG.Passwords.Shell).Path`, so the output shows which build it was.
- If the script names a type of the engine, `MG.Passwords.dll`, import that DLL from the same folder after the shell, as the shell projects' `Debug.ps1` scripts do. Importing the shell doesn't load `MG.Passwords.dll`, so until a cmdlet runs, a type literal for one of its types fails with "Unable to find type". A type of the shell itself, such as `MG.Passwords.CharacterSet`, resolves as soon as the shell is imported. In Windows PowerShell 5.1, the shell's import registers the resolver that loads the engine's dependencies, so importing `MG.Passwords.dll` first fails as soon as one of its types needs System.Memory to load. To reach an internal type, get the assembly from `[AppDomain]::CurrentDomain.GetAssemblies()`.
- Call the cmdlet by its name, `New-RandomPassword`. When a repro has to call the alias, import the build at the script's top level, or dot-source `tests/Import-NewRandomPassword.ps1`, so the alias lands in the scope that uses it.
- The script runs in Windows PowerShell 5.1 too, so it can't use PowerShell 7 syntax such as `??`, the ternary operator, or `&&`. Keep it ASCII, because 5.1 reads a UTF-8 file without a BOM as ANSI.
- After a session moves to another machine, check whether that machine has an installed copy, with `Get-Module -ListAvailable NewRandomPassword` in each edition.

To try the manifest and `NewRandomPassword.psm1` themselves, copy both to a scratch folder, put every DLL from each Release output beside them in `Core\` (`net10.0`) and `Desk\` (`net48`), and import the copied manifest by its path. `Module.Tests.ps1` stages the module the same way for the running edition's build. The `NewRandomPassword/` folder in the repo holds only the DLLs copied in for a release. To try the version check with versions that aren't installed, run the `.psm1`'s text with `$PSVersionTable` renamed, as `Module.Tests.ps1` does, because a script can't shadow `$PSVersionTable`.
