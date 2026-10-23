# Windows PowerShell 5.1 loads the .NET Framework 4.8 build from Desk, and PowerShell 7.6 or later loads the .NET 10 build
# from Core. Earlier releases of PowerShell 7 run on earlier versions of .NET, which can't load the .NET 10 build.
if ($PSVersionTable.PSEdition -eq 'Desktop') {
	$script:dllPath = "$PSScriptRoot\Desk\MG.Passwords.Shell.dll"
}
elseif ($PSVersionTable.PSVersion -ge [version]'7.6') {
	$script:dllPath = "$PSScriptRoot\Core\MG.Passwords.Shell.dll"
}
else {
	throw "NewRandomPassword requires Windows PowerShell 5.1 or PowerShell 7.6 or later. This is PowerShell $($PSVersionTable.PSVersion)."
}

Import-Module $script:dllPath -ErrorAction Stop
