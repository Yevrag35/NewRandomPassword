BeforeAll {
	$module = & "$PSScriptRoot/Import-NewRandomPassword.ps1" -PassThru
	$manifestPath = [System.IO.Path]::GetFullPath("$PSScriptRoot/../NewRandomPassword/NewRandomPassword.psd1")
	$moduleScriptPath = [System.IO.Path]::GetFullPath("$PSScriptRoot/../NewRandomPassword/NewRandomPassword.psm1")
	$manifest = Import-PowerShellDataFile -LiteralPath $manifestPath
}

Describe 'NewRandomPassword module' {
	It 'exports the cmdlets that the manifest lists' {
		$exported = @($module.ExportedCmdlets.Keys | Sort-Object)
		Should-BeCollection -Expected @($manifest.CmdletsToExport | Sort-Object) -Actual $exported
	}

	It 'exports the aliases that the manifest lists' {
		$exported = @($module.ExportedAliases.Keys | Sort-Object)
		Should-BeCollection -Expected @($manifest.AliasesToExport | Sort-Object) -Actual $exported
	}

	# Scripts written for 1.2.0 can call New-RandomPassword by its alias. The manifest tests don't catch a dropped alias,
	# because it would be dropped from the manifest too.
	It 'keeps rpas, the 1.2.0 alias of New-RandomPassword' {
		$module.ExportedAliases['rpas'].Definition | Should-Be 'New-RandomPassword'
	}

	It 'runs every cmdlet from the build under test' {
		# An installed NewRandomPassword 1.2.0 exports the same cmdlet name.
		foreach ($name in $manifest.CmdletsToExport) {
			(Get-Command -Name $name).Module.Path | Should-Be $module.Path -Because "$name should come from the build under test"
		}
	}

	# Directory.Build.props sets the build's version, and the manifest sets the module's, so a release changes both.
	It 'has the version that the manifest gives the module' {
		(Get-Item -LiteralPath $module.Path).VersionInfo.FileVersion | Should-Be $manifest.ModuleVersion
	}

	It 'does not answer a request for a dependency version that it does not reference' {
		# Without NewRandomPassword, the load fails because no System.Memory 99.0.0.0 exists. In Windows PowerShell 5.1, the
		# module's assembly resolver must not answer it with the module's own copy. PowerShell 7 has no such resolver.
		{ [System.Reflection.Assembly]::Load('System.Memory, Version=99.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51') } |
			Should-Throw -ExceptionMessage '*System.Memory, Version=99.0.0.0*'
	}

	Context 'NewRandomPassword.psm1' {
		# A release puts each edition's build in its own folder beside the manifest, and NewRandomPassword.psm1 imports
		# the one for the running edition. The test stages that layout with every DLL of this edition's build and imports
		# it in a new process, because this process has already loaded MG.Passwords.Shell.dll from the build's output folder.
		# The other tests import the DLL by its path, so only this one sees the alias that the manifest exports.
		It 'loads the build for this edition and exports rpas' {
			if ($PSVersionTable.PSEdition -eq 'Core') {
				$folder = 'Core'
			}
			else {
				$folder = 'Desk'
			}
			$stage = Join-Path $TestDrive 'NewRandomPassword'
			$buildFolder = Join-Path $stage $folder
			$null = New-Item -ItemType Directory -Path $buildFolder
			Copy-Item -LiteralPath $manifestPath, $moduleScriptPath -Destination $stage
			Copy-Item -Path (Join-Path (Split-Path -Parent $module.Path) '*.dll') -Destination $buildFolder

			$executable = [System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName
			$command = "`$PSModuleAutoLoadingPreference = 'None'; `$staged = Import-Module -Name '$stage\NewRandomPassword.psd1' -PassThru -ErrorAction Stop; `$staged.NestedModules.Path; `$staged.ExportedAliases['rpas'].Definition"
			$output = & $executable -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command $command 2>&1

			$LASTEXITCODE | Should-Be 0 -Because ($output -join [System.Environment]::NewLine)
			$output | Should-BeCollection -Count 2
			$output[0] | Should-BeLikeString -Expected "*\NewRandomPassword\$folder\MG.Passwords.Shell.dll"
			$output[1] | Should-Be 'New-RandomPassword'
		}

		# PowerShell 7.5 and earlier run on .NET 9 or earlier, which can't load the .NET 10 build, so the module stops the
		# import there with an error of its own. $PSVersionTable can't be shadowed, so the test runs the script's text with
		# the variable renamed.
		It 'stops the import in PowerShell <Version> with an error that names the supported versions' -ForEach @(
			@{ Version = '6.2.7' }
			@{ Version = '7.5.4' }
		) {
			# PowerShell 7's version is a SemanticVersion, and Windows PowerShell 5.1's is a Version.
			$versionType = $PSVersionTable.PSVersion.GetType()
			$fakeVersionTable = @{
				PSEdition = 'Core'
				PSVersion = $versionType::Parse($Version)
			}
			$moduleScript = [scriptblock]::Create((Get-Content -LiteralPath $moduleScriptPath -Raw).Replace('$PSVersionTable', '$fakeVersionTable'))

			{ & $moduleScript } | Should-Throw -ExceptionMessage "NewRandomPassword requires Windows PowerShell 5.1 or PowerShell 7.6 or later. This is PowerShell $Version."
		}
	}
}
