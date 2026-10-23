param (
	[Parameter(Mandatory = $false)]
	[string] $LibraryName = 'MG.Passwords.Shell'
)

# PowerShell resolves a type name only against loaded assemblies, and importing the shell doesn't load the engine.
# Importing the engine first makes its types usable at the prompt before a cmdlet runs.
Import-Module "$PSScriptRoot\MG.Passwords.dll" -ErrorAction Stop -Force
Import-Module "$PSScriptRoot\$LibraryName.dll" -ErrorAction Stop -Force
$myDesktop = [System.Environment]::GetFolderPath("Desktop")

Push-Location $myDesktop
