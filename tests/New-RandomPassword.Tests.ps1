BeforeAll {
	& "$PSScriptRoot/Import-NewRandomPassword.ps1"

	# The groups that passwords are built from without -InputStrings, as in 1.2.0: 61 characters in all.
	$symbols = '!@$#%&'
	$defaultGroups = 'abcdefghijkmnpqrstuvwxyz', 'ABCEFGHJKLMNPQRSTUVWXYZ', '23456789', $symbols

	# Returns the distinct characters of the strings as one string, in ordinal order.
	function Get-DistinctCharacter {
		param(
			[string[]] $Text
		)

		$characters = [System.Collections.Generic.SortedSet[char]]::new()
		foreach ($item in $Text) {
			$characters.UnionWith($item.ToCharArray())
		}
		-join $characters
	}
}

Describe 'New-RandomPassword' {
	Context 'Parameters' {
		BeforeAll {
			$command = Get-Command -Name 'MG.Passwords.Shell\New-RandomPassword'
			$commonParameters = @([System.Management.Automation.Cmdlet]::CommonParameters) + @([System.Management.Automation.Cmdlet]::OptionalCommonParameters)
		}

		It 'has the StaticLength and RandomLength parameter sets, with StaticLength as the default' {
			$command.DefaultParameterSet | Should-Be 'StaticLength'
			(@($command.ParameterSets.Name | Sort-Object) -join ', ') | Should-Be 'RandomLength, StaticLength'
		}

		It 'has the parameters of 1.2.0 and the common parameters, and no other' {
			$names = @($command.Parameters.Keys | Where-Object { $commonParameters -notcontains $_ } | Sort-Object)
			($names -join ', ') | Should-Be 'Count, ExtraCharacters, FirstCharacter, InputStrings, MaximumLength, MinimumLength, OnlyUseExtraCharacters, PasswordLength'
		}

		# The values are those of 1.2.0's cmdlet at accaf2e, except that -ExtraCharacters takes a String[] instead of an
		# Object, and the lengths stop at 1,048,576 instead of 2,147,483,647, or 2,147,483,646 for -MinimumLength.
		It 'declares the type, aliases, range, parameter sets, and position of -<Name>' -ForEach @(
			@{ Name = 'PasswordLength'; Type = [int]; Aliases = 'l', 'length'; ParameterSets = 'StaticLength'; Position = 0; Mandatory = $false; Range = '1, 1048576' }
			@{ Name = 'Count'; Type = [int]; Aliases = @(); ParameterSets = 'RandomLength', 'StaticLength'; Position = 1; Mandatory = $false; Range = '1, 2147483647' }
			@{ Name = 'MinimumLength'; Type = [int]; Aliases = 'min', 'minlength'; ParameterSets = 'RandomLength'; Position = 'named'; Mandatory = $true; Range = '1, 1048575' }
			@{ Name = 'MaximumLength'; Type = [int]; Aliases = 'max', 'maxlength'; ParameterSets = 'RandomLength'; Position = 'named'; Mandatory = $true; Range = '2, 1048576' }
			@{ Name = 'InputStrings'; Type = [string[]]; Aliases = 'strings'; ParameterSets = 'RandomLength', 'StaticLength'; Position = 'named'; Mandatory = $false; Range = '' }
			@{ Name = 'ExtraCharacters'; Type = [string[]]; Aliases = 'chars', 'ec'; ParameterSets = 'RandomLength', 'StaticLength'; Position = 'named'; Mandatory = $false; Range = '' }
			@{ Name = 'OnlyUseExtraCharacters'; Type = [switch]; Aliases = 'onlyec'; ParameterSets = 'RandomLength', 'StaticLength'; Position = 'named'; Mandatory = $false; Range = '' }
			@{ Name = 'FirstCharacter'; Type = [char]; Aliases = 'fc', 'FirstChar'; ParameterSets = 'RandomLength', 'StaticLength'; Position = 'named'; Mandatory = $false; Range = '' }
		) {
			$parameter = $command.Parameters[$Name]
			$parameter.ParameterType | Should-Be $Type
			(@($parameter.Aliases | Sort-Object) -join ', ') | Should-Be (@($Aliases | Sort-Object) -join ', ')
			$ranges = @($parameter.Attributes | Where-Object { $_ -is [System.Management.Automation.ValidateRangeAttribute] } | ForEach-Object { '{0}, {1}' -f $_.MinRange, $_.MaxRange })
			($ranges -join '; ') | Should-Be $Range -Because 'ValidateRange should allow the same lengths and counts'

			$memberOf = @($command.ParameterSets | Where-Object { $_.Parameters.Name -contains $Name })
			(@($memberOf.Name | Sort-Object) -join ', ') | Should-Be ($ParameterSets -join ', ')
			foreach ($set in $memberOf) {
				$setParameter = $set.Parameters | Where-Object Name -EQ $Name
				$actualPosition = $setParameter.Position
				if ($actualPosition -eq [int]::MinValue) {
					$actualPosition = 'named'
				}
				$actualPosition | Should-Be $Position -Because "of the position in $($set.Name)"
				$setParameter.IsMandatory | Should-Be $Mandatory -Because "of whether it's mandatory in $($set.Name)"
				($setParameter.ValueFromPipeline -or $setParameter.ValueFromPipelineByPropertyName) | Should-BeFalse -Because "it takes no pipeline input in $($set.Name)"
			}
		}

		# ValidateSet ignores case, but it passes each value on as typed (see 'InputStrings').
		It 'accepts the seven strings of 1.2.0 for -InputStrings, ignoring case' {
			$validateSet = $command.Parameters['InputStrings'].Attributes | Where-Object { $_ -is [System.Management.Automation.ValidateSetAttribute] }
			($validateSet.ValidValues -join ' ') | Should-BeString -CaseSensitive 'abcdefghijkmnpqrstuvwxyz ABCEFGHJKLMNPQRSTUVWXYZ 1234567890 234567890 23456789 !@$#%& !#%&'
			$validateSet.IgnoreCase | Should-BeTrue
		}

		It 'declares that it outputs strings' {
			@($command.OutputType).Count | Should-Be 1
			$command.OutputType[0].Type | Should-Be ([string])
		}

		It 'generates one password of 8 characters by default' {
			$passwords = @(New-RandomPassword)
			$passwords.Count | Should-Be 1
			$passwords[0].Length | Should-Be 8
		}
	}

	Context 'FirstCharacter' {
		# 1.2.0 sorted -FirstCharacter in with the other characters, in a random direction, so it came last in about half
		# the passwords (#3).
		It 'starts every password with -FirstCharacter' {
			$passwords = New-RandomPassword -PasswordLength 8 -FirstCharacter '~' -Count 400
			$passwords | Should-BeCollection -Count 400
			$passwords | Should-All { $_[0] -ceq '~' }
		}

		It 'gives -FirstCharacter alone for a password of 1 character' {
			New-RandomPassword -PasswordLength 1 -FirstCharacter '~' | Should-BeString -CaseSensitive '~'
		}

		# 1.2.0 read a bare number as a character code, so 5 started every password with U+0005, a control character.
		It 'reads a bare digit as that digit' {
			$passwords = New-RandomPassword -FirstCharacter 5 -Count 20
			$passwords | Should-All { $_[0] -ceq '5' }
		}

		# 1.2.0 read 65 as A. The digits fail PowerShell's conversion to a char, because they aren't one character.
		It 'fails at binding for a number of two digits' {
			{ New-RandomPassword -FirstCharacter 65 } | Should-Throw -FullyQualifiedErrorId 'CannotConvertArgumentNoMessage,*'
		}

		# Each is one character, so PowerShell converts it to a char, and the validation after the conversion refuses it.
		It 'fails at binding for <Description>' -ForEach @(
			@{ Description = 'a control character'; Character = [char]9 }
			@{ Description = 'half of a surrogate pair'; Character = [char]0xD83D }
		) {
			{ New-RandomPassword -FirstCharacter $Character } | Should-Throw -FullyQualifiedErrorId 'ParameterArgumentValidationError,*'
		}
	}

	Context 'MinimumLength and MaximumLength' {
		# 1.2.0 drew one length for the whole call (#3). With 17 lengths to draw from, the chance that 50 passwords share
		# one is below 10^-60.
		It 'gives each password a random length of its own' {
			$passwords = New-RandomPassword -MinimumLength 4 -MaximumLength 20 -Count 50
			$lengths = @($passwords | ForEach-Object { $_.Length } | Sort-Object -Unique)
			$lengths.Count | Should-BeGreaterThan 1
		}

		# With 3 lengths to draw from, the chance that 300 passwords miss one is below 10^-52.
		It 'draws every length from -MinimumLength to -MaximumLength, and no other' {
			$passwords = New-RandomPassword -MinimumLength 4 -MaximumLength 6 -Count 300
			$lengths = @($passwords | ForEach-Object { $_.Length } | Sort-Object -Unique)
			($lengths -join ', ') | Should-Be '4, 5, 6'
		}

		It 'gives every password the length of equal bounds' {
			New-RandomPassword -MinimumLength 5 -MaximumLength 5 -Count 20 | Should-All { $_.Length -eq 5 }
		}

		# 1.2.0 threw a raw ArgumentException.
		It 'fails with MinimumLengthExceedsMaximumLength for a minimum greater than the maximum' {
			$err = { New-RandomPassword -MinimumLength 6 -MaximumLength 4 } | Should-Throw -FullyQualifiedErrorId 'MinimumLengthExceedsMaximumLength,*'
			$err.CategoryInfo.Category | Should-Be 'InvalidArgument'
		}
	}

	# As in 1.2.0, the first number binds to -PasswordLength and the second to -Count.
	Context 'Positional binding' {
		It 'binds two numbers to -PasswordLength and -Count' {
			$passwords = New-RandomPassword 12 3
			$passwords | Should-BeCollection -Count 3
			$passwords | Should-All { $_.Length -eq 12 }
		}

		It 'binds a number to -Count with -MinimumLength and -MaximumLength' {
			$passwords = New-RandomPassword -MinimumLength 4 -MaximumLength 6 3
			$passwords | Should-BeCollection -Count 3
			$passwords | Should-All { $_.Length -ge 4 -and $_.Length -le 6 }
		}
	}

	Context 'Character groups' {
		# With 6,000 characters drawn, the chance that one of the 61 never shows up is below 10^-38.
		It 'draws from the 61 characters of the default groups, and only those' {
			$passwords = New-RandomPassword -PasswordLength 30 -Count 200
			Get-DistinctCharacter $passwords | Should-BeString -CaseSensitive (Get-DistinctCharacter $defaultGroups)
		}

		It 'holds a character from each default group in every password of <_> characters' -ForEach 4, 5, 8 {
			$passwords = New-RandomPassword -PasswordLength $_ -Count 200
			foreach ($group in $defaultGroups) {
				$passwords | Should-All { $_.IndexOfAny($group.ToCharArray()) -ge 0 } -Because "every password should hold one of $group"
			}
		}

		# 1.2.0 took the first groups in order, so a password of 2 or 3 characters never held a symbol. Each password picks
		# its groups at random, so the chance that none of 200 holds a symbol is below 10^-60.
		It 'holds a symbol in some passwords of <_> characters' -ForEach 2, 3 {
			$passwords = New-RandomPassword -PasswordLength $_ -Count 200
			$passwords | Should-Any { $_.IndexOfAny($symbols.ToCharArray()) -ge 0 }
		}

		# 1.2.0 threw DivideByZeroException.
		It 'fails with NoCharacters for <Description>' -ForEach @(
			@{ Description = '-InputStrings @()'; Parameters = @{ InputStrings = @() } }
			@{ Description = '-ExtraCharacters @() with -OnlyUseExtraCharacters'; Parameters = @{ ExtraCharacters = @(); OnlyUseExtraCharacters = $true } }
		) {
			$err = { New-RandomPassword @Parameters } | Should-Throw -FullyQualifiedErrorId 'NoCharacters,*'
			$err.CategoryInfo.Category | Should-Be 'InvalidArgument'
		}
	}

	Context 'InputStrings' {
		# With 1,500 characters drawn, the chance that one of the 12 never shows up is below 10^-54.
		It 'draws from the -InputStrings groups in place of the default groups' {
			$passwords = New-RandomPassword -PasswordLength 30 -Count 50 -InputStrings '23456789', '!#%&'
			Get-DistinctCharacter $passwords | Should-BeString -CaseSensitive (Get-DistinctCharacter '23456789', '!#%&')
		}

		# ValidateSet ignores case but passes each value on as typed, as in 1.2.0, so the lowercase group typed in
		# capitals gives capitals, D and I among them. With 1,500 characters drawn, the chance that one of the 24 never
		# shows up is below 10^-26.
		It 'uses each value as typed' {
			$passwords = New-RandomPassword -PasswordLength 30 -Count 50 -InputStrings 'ABCDEFGHIJKMNPQRSTUVWXYZ'
			Get-DistinctCharacter $passwords | Should-BeString -CaseSensitive 'ABCDEFGHIJKMNPQRSTUVWXYZ'
		}

		It 'holds a character for each listing of a group listed twice' {
			$passwords = New-RandomPassword -PasswordLength 3 -Count 200 -InputStrings 'abcdefghijkmnpqrstuvwxyz', '23456789', '23456789'
			$passwords | Should-All { ($_ -replace '\D').Length -eq 2 }
		}
	}

	Context 'ExtraCharacters' {
		BeforeDiscovery {
			# No default group holds these characters.
			$forms = @(
				@{ Form = 'a string'; Argument = '*+'; Groups = @('*+') }
				@{ Form = 'a string[]'; Argument = [string[]]('*+', '-='); Groups = @('*+', '-=') }
				@{ Form = 'a char[]'; Argument = [char[]]'*+'; Groups = @('*+') }
				@{ Form = 'a char'; Argument = [char]'*'; Groups = @('*') }
			)
			$fileContents = @(
				@{ Description = 'one line'; Lines = @('*+') }
				@{ Description = 'two lines'; Lines = @('*+', '-=') }
			)
		}

		# With 6,000 characters drawn, the chance that one of the default characters never shows up is below 10^-33.
		It 'adds <Form> to the default groups' -ForEach $forms {
			$passwords = New-RandomPassword -PasswordLength 30 -Count 200 -ExtraCharacters $Argument
			Get-DistinctCharacter $passwords | Should-BeString -CaseSensitive (Get-DistinctCharacter ($defaultGroups + $Groups))
			foreach ($group in $Groups) {
				$passwords | Should-All { $_.IndexOfAny($group.ToCharArray()) -ge 0 } -Because "every password should hold one of $group"
			}
		}

		# A password of 2 characters holds a character from each of 2 groups, which it wouldn't always do if the 2 were
		# taken as one group.
		It 'draws from <Form> alone with -OnlyUseExtraCharacters' -ForEach $forms {
			$passwords = New-RandomPassword -PasswordLength 2 -Count 200 -ExtraCharacters $Argument -OnlyUseExtraCharacters
			Get-DistinctCharacter $passwords | Should-BeString -CaseSensitive (Get-DistinctCharacter $Groups)
			foreach ($group in $Groups) {
				$passwords | Should-All { $_.IndexOfAny($group.ToCharArray()) -ge 0 } -Because "every password should hold one of $group"
			}
		}

		# PowerShell would split a char[] into a group for each character, so every password of 2 characters would hold
		# both. As one group, half the passwords hold one of its characters twice, so the chance that none of 200 does is
		# below 10^-60.
		It 'keeps a char[] together as one group' {
			$passwords = New-RandomPassword -PasswordLength 2 -Count 200 -ExtraCharacters ([char[]]'*+') -OnlyUseExtraCharacters
			$passwords | Should-Any { $_[0] -eq $_[1] }
		}

		# Get-Content outputs one line in a PSObject, and several in an object[] of PSObjects. 1.2.0 refused one line and
		# dropped several. As with the other forms, the chance that one of the default characters never shows up is below
		# 10^-33.
		It 'adds each line of Get-Content output to the default groups, from <Description>' -ForEach $fileContents {
			$path = Join-Path $TestDrive 'groups.txt'
			Set-Content -LiteralPath $path -Value $Lines
			$passwords = New-RandomPassword -PasswordLength 30 -Count 200 -ExtraCharacters (Get-Content -LiteralPath $path)
			Get-DistinctCharacter $passwords | Should-BeString -CaseSensitive (Get-DistinctCharacter ($defaultGroups + $Lines))
			foreach ($group in $Lines) {
				$passwords | Should-All { $_.IndexOfAny($group.ToCharArray()) -ge 0 } -Because "every password should hold one of $group"
			}
		}

		It 'draws from the lines of Get-Content output alone with -OnlyUseExtraCharacters, from <Description>' -ForEach $fileContents {
			$path = Join-Path $TestDrive 'groups.txt'
			Set-Content -LiteralPath $path -Value $Lines
			$passwords = New-RandomPassword -PasswordLength 2 -Count 200 -ExtraCharacters (Get-Content -LiteralPath $path) -OnlyUseExtraCharacters
			Get-DistinctCharacter $passwords | Should-BeString -CaseSensitive (Get-DistinctCharacter $Lines)
			foreach ($group in $Lines) {
				$passwords | Should-All { $_.IndexOfAny($group.ToCharArray()) -ge 0 } -Because "every password should hold one of $group"
			}
		}

		# A value of another type fails in the transformation, and a group that a password can't hold fails in the
		# validation after it. 1.2.0 refused some of these, dropped others, and passed the rest into the passwords.
		It 'fails at binding for <Description>' -ForEach @(
			@{ Description = 'a number'; Argument = 5; ErrorId = 'ParameterArgumentTransformationError' }
			@{ Description = 'a list that holds a number'; Argument = 'ab', 5; ErrorId = 'ParameterArgumentTransformationError' }
			@{ Description = 'a list inside the list'; Argument = 'ab', @('c', 'd'); ErrorId = 'ParameterArgumentTransformationError' }
			@{ Description = 'a hashtable'; Argument = @{ a = 'b' }; ErrorId = 'ParameterArgumentTransformationError' }
			@{ Description = 'a date'; Argument = [datetime]::new(2026, 10, 24); ErrorId = 'ParameterArgumentTransformationError' }
			@{ Description = '$null'; Argument = $null; ErrorId = 'ParameterArgumentTransformationError' }
			@{ Description = 'an empty string'; Argument = ''; ErrorId = 'ParameterArgumentValidationError' }
			@{ Description = 'a control character'; Argument = 'a' + [char]9 + 'b'; ErrorId = 'ParameterArgumentValidationError' }
			@{ Description = 'an emoji'; Argument = [char]::ConvertFromUtf32(0x1F600); ErrorId = 'ParameterArgumentValidationError' }
		) {
			{ New-RandomPassword -ExtraCharacters $Argument } | Should-Throw -FullyQualifiedErrorId "$ErrorId,*"
		}

		# The character is written as a code, because Windows PowerShell 5.1 reads this file as ANSI.
		It 'passes a character outside ASCII into the passwords unchanged' {
			$eAcute = [string][char]0x00E9
			$passwords = New-RandomPassword -PasswordLength 5 -Count 10 -ExtraCharacters $eAcute -OnlyUseExtraCharacters
			$passwords | Should-All { $_ -ceq ($eAcute * 5) }
		}
	}

	Context 'OnlyUseExtraCharacters' {
		It 'draws from -ExtraCharacters alone, <Description>' -ForEach @(
			@{ Description = 'without the default groups'; Parameters = @{} }
			@{ Description = 'without the -InputStrings groups'; Parameters = @{ InputStrings = '23456789' } }
		) {
			$passwords = New-RandomPassword @Parameters -PasswordLength 30 -Count 50 -ExtraCharacters '*+' -OnlyUseExtraCharacters
			Get-DistinctCharacter $passwords | Should-BeString -CaseSensitive '*+'
		}

		# 1.2.0 ignored the switch without -ExtraCharacters, and drew from the default groups or the -InputStrings groups.
		It 'fails with OnlyUseExtraCharactersWithoutExtraCharacters without -ExtraCharacters, <Description>' -ForEach @(
			@{ Description = 'with the default groups'; Parameters = @{} }
			@{ Description = 'with -InputStrings'; Parameters = @{ InputStrings = '23456789' } }
		) {
			$err = { New-RandomPassword @Parameters -OnlyUseExtraCharacters } | Should-Throw -FullyQualifiedErrorId 'OnlyUseExtraCharactersWithoutExtraCharacters,*'
			$err.CategoryInfo.Category | Should-Be 'InvalidArgument'
		}

		# Splatting passes a switch that's off as $false. With 6,000 characters drawn, the chance that one of the 61 default
		# characters never shows up is below 10^-38.
		It 'draws from the default groups with -OnlyUseExtraCharacters:$false' {
			$passwords = New-RandomPassword -PasswordLength 30 -Count 200 -OnlyUseExtraCharacters:$false
			Get-DistinctCharacter $passwords | Should-BeString -CaseSensitive (Get-DistinctCharacter $defaultGroups)
		}
	}

	Context 'Help' {
		BeforeAll {
			$command = Get-Command -Name 'MG.Passwords.Shell\New-RandomPassword'
			$help = Get-Help -Name 'MG.Passwords.Shell\New-RandomPassword' -Full
			$commonParameters = @([System.Management.Automation.Cmdlet]::CommonParameters) + @([System.Management.Automation.Cmdlet]::OptionalCommonParameters)
		}

		# Without a help file in en-US beside the DLL, Get-Help makes up help from the cmdlet, and its type names don't
		# include MamlCommandHelpInfo.
		It 'comes from the help file' {
			$help.PSObject.TypeNames -contains 'MamlCommandHelpInfo' | Should-BeTrue -Because ($help.PSObject.TypeNames -join ', ')
		}

		# Get-Help shows the syntax as the file writes it, not as the cmdlet declares it. Each syntax block lists the
		# parameters of its set in the order that Get-Command shows them, where the set's syntax names each one as -Name.
		It 'shows the <Name> parameter set in syntax block <Index> as the cmdlet declares it' -ForEach @(
			@{ Name = 'StaticLength'; Index = 0 }
			@{ Name = 'RandomLength'; Index = 1 }
		) {
			@($help.syntax.syntaxItem).Count | Should-Be @($command.ParameterSets).Count
			$set = $command.ParameterSets | Where-Object Name -EQ $Name
			$syntax = @($help.syntax.syntaxItem)[$Index]
			$expectedNames = [regex]::Matches($set.ToString(), '-(\w+)') | ForEach-Object { $_.Groups[1].Value }
			(@($syntax.parameter.name) -join ', ') | Should-Be ($expectedNames -join ', ')

			foreach ($parameter in $syntax.parameter) {
				$setParameter = $set.Parameters | Where-Object Name -EQ $parameter.name
				$expectedPosition = 'named'
				if ($setParameter.Position -ne [int]::MinValue) {
					$expectedPosition = [string]$setParameter.Position
				}
				$parameter.required | Should-Be ([string]$setParameter.IsMandatory) -Because "of whether -$($parameter.name) is mandatory"
				$parameter.position | Should-Be $expectedPosition -Because "of the position of -$($parameter.name)"
				$parameter.type.name | Should-Be $setParameter.ParameterType.Name -Because "of the type of -$($parameter.name)"
				if ($setParameter.ParameterType -eq [switch]) {
					Should-BeNull -Actual $parameter.parameterValue -Because "-$($parameter.name) is a switch, which takes no value"
				}
				else {
					$parameter.parameterValue | Should-Be $setParameter.ParameterType.Name -Because "of the value of -$($parameter.name)"
				}
			}
		}

		It 'describes each parameter of the cmdlet, and no other' {
			$names = @($command.Parameters.Keys | Where-Object { $commonParameters -notcontains $_ } | Sort-Object)
			(@($help.parameters.parameter.name | Sort-Object) -join ', ') | Should-Be ($names -join ', ')
		}

		It 'describes -<_> as the cmdlet declares it' -ForEach 'PasswordLength', 'Count', 'MinimumLength', 'MaximumLength', 'InputStrings', 'ExtraCharacters', 'OnlyUseExtraCharacters', 'FirstCharacter' {
			$name = $_
			$parameter = @($help.parameters.parameter) | Where-Object name -EQ $name
			$metadata = $command.Parameters[$name]
			$inSets = @($command.ParameterSets | ForEach-Object { $_.Parameters | Where-Object Name -EQ $name })

			$mandatory = @($inSets | Where-Object IsMandatory).Count -gt 0
			$positions = @($inSets | ForEach-Object { if ($_.Position -eq [int]::MinValue) { 'named' } else { [string]$_.Position } } | Sort-Object -Unique)
			$takesPipelineInput = @($inSets | Where-Object { $_.ValueFromPipeline -or $_.ValueFromPipelineByPropertyName }).Count -gt 0
			$aliases = 'none'
			if ($metadata.Aliases.Count -gt 0) {
				$aliases = (@($metadata.Aliases) | Sort-Object) -join ', '
			}

			$parameter.required | Should-Be ([string]$mandatory) -Because 'of whether the parameter is mandatory in a parameter set'
			$parameter.position | Should-Be ($positions -join ', ') -Because 'of its position'
			$parameter.type.name | Should-Be $metadata.ParameterType.Name -Because 'of its type'
			(@($parameter.aliases -split ', ' | Sort-Object) -join ', ') | Should-Be $aliases -Because 'of its aliases'
			# A help file writes pipeline input as False, or as True followed by how it binds, such as True (ByValue).
			$parameter.pipelineInput | Should-BeLikeString "$takesPipelineInput*" -Because 'of whether it takes pipeline input'
		}
	}
}
