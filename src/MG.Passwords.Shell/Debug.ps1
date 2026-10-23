param (
	[Parameter(Mandatory = $false)]
	[string] $LibraryName = 'MG.Passwords.Shell'
)

# PowerShell resolves a type name only against loaded assemblies, and importing the shell doesn't load the engine.
# Importing the engine too makes its types usable at the prompt before a cmdlet runs. The shell goes first, as in the
# Windows PowerShell 5.1 project, where its import registers the resolver that loads the engine's dependencies.
Import-Module "$PSScriptRoot\$LibraryName.dll" -ErrorAction Stop -Force
Import-Module "$PSScriptRoot\MG.Passwords.dll" -ErrorAction Stop -Force
$myDesktop = [System.Environment]::GetFolderPath("Desktop")

Push-Location $myDesktop
