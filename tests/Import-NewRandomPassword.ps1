<#
.SYNOPSIS
Imports the NewRandomPassword build for the current PowerShell edition.

.DESCRIPTION
PowerShell 7 imports MG.Passwords.Shell.dll from the net10.0 output of MG.Passwords.Shell, and Windows PowerShell 5.1
imports it from the net48 output of MG.Passwords.Shell-NETFramework. That's the same split that NewRandomPassword.psm1
makes, but it skips the DLLs copied into NewRandomPassword/ for a release.

The NEWRANDOMPASSWORD_TEST_CONFIGURATION environment variable selects the build configuration and defaults to Debug.
Invoke-Tests.ps1 sets it.

Every test file calls this script in a top-level BeforeAll block. The script also works with module auto-loading off,
as in a repro: it uses only Import-Module and .NET calls, not commands from other modules such as Test-Path.

.PARAMETER PassThru
Returns the imported module.
#>
[CmdletBinding()]
param(
	[switch] $PassThru
)

$configuration = $env:NEWRANDOMPASSWORD_TEST_CONFIGURATION
if (-not $configuration) {
	$configuration = 'Debug'
}

$srcRoot = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\src")
if ($PSVersionTable.PSEdition -eq 'Core') {
	$project = 'MG.Passwords.Shell'
	$targetFramework = 'net10.0'
}
else {
	$project = 'MG.Passwords.Shell-NETFramework'
	$targetFramework = 'net48'
}
$modulePath = "$srcRoot\$project\bin\$configuration\$targetFramework\MG.Passwords.Shell.dll"

if (-not [System.IO.File]::Exists($modulePath)) {
	throw "There's no $configuration build for PowerShell $($PSVersionTable.PSEdition) at '$modulePath'. Build src/$project/$project.csproj first."
}

Import-Module -Name $modulePath -PassThru:$PassThru -ErrorAction Stop
