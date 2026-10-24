param (
	[Parameter(Mandatory = $false)]
	[string] $LibraryName = 'MG.Passwords'
)

Import-Module "$PSScriptRoot\$LibraryName.dll" -ErrorAction Stop -Force
$myDesktop = [System.Environment]::GetFolderPath("Desktop")

Push-Location $myDesktop
