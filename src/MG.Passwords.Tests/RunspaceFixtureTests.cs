namespace MG.Passwords.Tests;

public sealed class RunspaceFixtureTests : IClassFixture<RunspaceFixture>
{
	private readonly RunspaceFixture _runspace;

	public RunspaceFixtureTests(RunspaceFixture runspace)
	{
		_runspace = runspace;
	}

	// Each target tests MG.Passwords in the PowerShell that loads its build: net10.0 hosts PowerShell 7 from the
	// Microsoft.PowerShell.SDK package, and net48 hosts the Windows PowerShell 5.1 that is installed with Windows.
	[Fact]
	public void Enter_RunsScriptBlocksInThePowerShellOfTheTarget()
	{
		using RunspaceScope scope = _runspace.Enter();
		Collection<PSObject> results = ScriptBlock.Create("'{0} {1}' -f $PSVersionTable.PSEdition, $PSVersionTable.PSVersion.Major").Invoke();

		string edition = Assert.IsType<string>(Assert.Single(results).BaseObject);
#if NETFRAMEWORK
		Assert.Equal("Desktop 5", edition);
#else
		Assert.Equal("Core 7", edition);
#endif
	}
}
