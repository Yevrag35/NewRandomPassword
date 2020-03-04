param
(
    [parameter(Mandatory=$true, Position=0)]
    [string] $DebugDirectory,

    [parameter(Mandatory=$true, Position=1)]
    [string] $ModuleFileDirectory,

    [parameter(Mandatory=$true, Position=2)]
    [string] $AssemblyInfo,

    [parameter(Mandatory=$true, Position=3)]
    [string] $TargetFileName
)

## Clear out files
Get-ChildItem -Path $DebugDirectory -Include *.ps1xml -Recurse | Remove-Item -Force;

## Get Module Version
$assInfo = Get-Content $AssemblyInfo;
foreach ($line in $assInfo)
{
    if ($line -like "*AssemblyFileVersion(*")
    {
        $vers = $line -replace '^\s*\[assembly\:\sAssemblyFileVersion\(\"(.*?)\"\)\]$', '$1';
    }
}
$allFiles = Get-ChildItem $ModuleFileDirectory -Include * -Exclude *.old -Recurse;
$References = Join-Path "$ModuleFileDirectory\.." "Assemblies";

[string[]]$allDlls = Get-ChildItem $References -Include *.dll -Exclude 'System.Management.Automation.dll' -Recurse | Select -ExpandProperty Name;
[string[]]$allFormats = $allFiles | ? { $_.Extension -eq ".ps1xml" } | Select -ExpandProperty Name;

$manifestFile = "PoshTypes.psd1"

$allFiles | Copy-Item -Destination $DebugDirectory -Force;

$manifest = @{
    Path               = $(Join-Path $DebugDirectory $manifestFile)
    Guid               = '9b97132b-925e-46c2-a0d0-266d8b09f1cd';
    Description        = 'A shorthand module for performing ''GetType()'' and ''Get-Member'' methods quickly.'
    Author             = 'Mike Garvey'
    CompanyName        = 'DGR Systems, LLC.'
    Copyright          = 'Copyright (c) 2019 Yevrag35, LLC.  All rights reserved.'
    ModuleVersion      = $vers.Trim()
	RequiredModules	   = @{ ModuleName = "Microsoft.PowerShell.Utility"; ModuleVersion = '3.1.0.0'; Guid = '1da87e53-152b-403e-98dc-74d7b4d63d59' }
    PowerShellVersion  = '5.1'
    RootModule         = $TargetFileName
#    RequiredAssemblies = @('System.Collections', 'System.Linq', 'System.Reflection')
    AliasesToExport    = @("gdt", "gmt", "gpm", "gpt", "gt", "pm")
    CmdletsToExport    = '*'
    FunctionsToExport  = @()
    VariablesToExport  = ''
    FormatsToProcess   = if ($allFormats.Length -gt 0) { $allFormats } else { @() };
    ProjectUri	       = 'https://github.com/Yevrag35/PoshTypes'
    Tags               = @( 'Reflection', 'Get', 'Type', 'Method', 'Property', 'Properties', 'Name', 'Full',
						    'Force', 'Member', 'switch', 'Parameter', 'arguments', 'pipeline', 'inputobject' )
};

New-ModuleManifest @manifest;