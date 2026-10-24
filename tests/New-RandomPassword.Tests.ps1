BeforeAll {
	& "$PSScriptRoot/Import-NewRandomPassword.ps1"

	# The groups that passwords are built from without -InputStrings: every letter and digit, and six symbols, 68
	# characters in all.
	$symbols = '!@$#%&'
	$defaultGroups = 'abcdefghijklmnopqrstuvwxyz', 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', '1234567890', $symbols

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

		It 'has the parameter sets of 1.2.0 and a -CharacterSet twin of each, with StaticLength as the default' {
			$command.DefaultParameterSet | Should-Be 'StaticLength'
			(@($command.ParameterSets.Name | Sort-Object) -join ', ') | Should-Be 'RandomLength, RandomLengthCharacterSet, StaticLength, StaticLengthCharacterSet'
		}

		It 'has the parameters of 1.2.0, -CharacterSet, and the common parameters, and no other' {
			$names = @($command.Parameters.Keys | Where-Object { $commonParameters -notcontains $_ } | Sort-Object)
			($names -join ', ') | Should-Be 'CharacterSet, Count, ExtraCharacters, FirstCharacter, InputStrings, MaximumLength, MinimumLength, OnlyUseExtraCharacters, PasswordLength'
		}

		# The values are those of 1.2.0's cmdlet at accaf2e, except that -ExtraCharacters takes a String[] instead of an
		# Object, and the lengths stop at 1,048,576 instead of 2,147,483,647, or 2,147,483,646 for -MinimumLength. Each of
		# 1.2.0's sets has a twin in which -CharacterSet, mandatory there, takes the place of -InputStrings. The types are
		# names, because the cmdlet's enum isn't loaded yet when Pester discovers the tests.
		It 'declares the type, aliases, range, parameter sets, and position of -<Name>' -ForEach @(
			@{ Name = 'PasswordLength'; Type = 'System.Int32'; Aliases = 'l', 'length'; ParameterSets = 'StaticLength', 'StaticLengthCharacterSet'; Position = 0; Mandatory = $false; Range = '1, 1048576' }
			@{ Name = 'Count'; Type = 'System.Int32'; Aliases = @(); ParameterSets = 'RandomLength', 'RandomLengthCharacterSet', 'StaticLength', 'StaticLengthCharacterSet'; Position = 1; Mandatory = $false; Range = '1, 2147483647' }
			@{ Name = 'MinimumLength'; Type = 'System.Int32'; Aliases = 'min', 'minlength'; ParameterSets = 'RandomLength', 'RandomLengthCharacterSet'; Position = 'named'; Mandatory = $true; Range = '1, 1048575' }
			@{ Name = 'MaximumLength'; Type = 'System.Int32'; Aliases = 'max', 'maxlength'; ParameterSets = 'RandomLength', 'RandomLengthCharacterSet'; Position = 'named'; Mandatory = $true; Range = '2, 1048576' }
			@{ Name = 'InputStrings'; Type = 'System.String[]'; Aliases = 'strings'; ParameterSets = 'RandomLength', 'StaticLength'; Position = 'named'; Mandatory = $false; Range = '' }
			@{ Name = 'CharacterSet'; Type = 'MG.Passwords.CharacterSet'; Aliases = @(); ParameterSets = 'RandomLengthCharacterSet', 'StaticLengthCharacterSet'; Position = 'named'; Mandatory = $true; Range = '' }
			@{ Name = 'ExtraCharacters'; Type = 'System.String[]'; Aliases = 'chars', 'ec'; ParameterSets = 'RandomLength', 'RandomLengthCharacterSet', 'StaticLength', 'StaticLengthCharacterSet'; Position = 'named'; Mandatory = $false; Range = '' }
			@{ Name = 'OnlyUseExtraCharacters'; Type = 'System.Management.Automation.SwitchParameter'; Aliases = 'onlyec'; ParameterSets = 'RandomLength', 'RandomLengthCharacterSet', 'StaticLength', 'StaticLengthCharacterSet'; Position = 'named'; Mandatory = $false; Range = '' }
			@{ Name = 'FirstCharacter'; Type = 'System.Char'; Aliases = 'fc', 'FirstChar'; ParameterSets = 'RandomLength', 'RandomLengthCharacterSet', 'StaticLength', 'StaticLengthCharacterSet'; Position = 'named'; Mandatory = $false; Range = '' }
		) {
			$parameter = $command.Parameters[$Name]
			$parameter.ParameterType.FullName | Should-Be $Type
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

		# 1.2.0's seven strings, with ABCDEFGHJKLMNPQRSTUVWXYZ in place of 1.2.0's ABCEFGHJKLMNPQRSTUVWXYZ, which left
		# out D by mistake, and the lowercase and the uppercase letters, each ahead of its set that leaves out
		# look-alikes. ValidateSet ignores case, but it passes each value on as typed (see 'InputStrings').
		It 'accepts the nine strings for -InputStrings, ignoring case' {
			$validateSet = $command.Parameters['InputStrings'].Attributes | Where-Object { $_ -is [System.Management.Automation.ValidateSetAttribute] }
			($validateSet.ValidValues -join ' ') | Should-BeString -CaseSensitive 'abcdefghijklmnopqrstuvwxyz abcdefghijkmnpqrstuvwxyz ABCDEFGHIJKLMNOPQRSTUVWXYZ ABCDEFGHJKLMNPQRSTUVWXYZ 1234567890 234567890 23456789 !@$#%& !#%&'
			$validateSet.IgnoreCase | Should-BeTrue
		}

		# Scripts name these values, and PowerShell binds a single name by any prefix that begins that name alone, so a
		# value renamed, or a value added later whose name begins like another's, would break a script. No value is 0.
		It 'takes five flags for -CharacterSet' {
			$type = $command.Parameters['CharacterSet'].ParameterType
			$type.IsDefined([System.FlagsAttribute], $false) | Should-BeTrue
			([enum]::GetNames($type) -join ', ') | Should-BeString -CaseSensitive 'Lowercase, Uppercase, Digits, Symbols, NoLookAlikes'
			(@([enum]::GetValues($type) | ForEach-Object { [int]$_ }) -join ', ') | Should-Be '1, 2, 4, 8, 16'
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

		It 'binds two numbers to -PasswordLength and -Count with -CharacterSet' {
			$passwords = New-RandomPassword 12 3 -CharacterSet Digits
			$passwords | Should-BeCollection -Count 3
			$passwords | Should-All { $_ -match '^\d{12}$' }
		}

		It 'binds a number to -Count with -MinimumLength, -MaximumLength, and -CharacterSet' {
			$passwords = New-RandomPassword -MinimumLength 4 -MaximumLength 6 3 -CharacterSet Digits
			$passwords | Should-BeCollection -Count 3
			$passwords | Should-All { $_ -match '^\d{4,6}$' }
		}
	}

	Context 'Character groups' {
		# With 6,000 characters drawn, the chance that one of the 68 never shows up is below 10^-35.
		It 'draws from the 68 characters of the default groups, and only those' {
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

		# ValidateSet ignores case but passes each value on as typed, as in 1.2.0, so the lowercase letters except l
		# and o, typed in capitals, give the capitals except L and O, which neither uppercase string gives. With 1,500
		# characters drawn, the chance that one of the 24 never shows up is below 10^-26.
		It 'uses each value as typed' {
			$passwords = New-RandomPassword -PasswordLength 30 -Count 50 -InputStrings 'ABCDEFGHIJKMNPQRSTUVWXYZ'
			Get-DistinctCharacter $passwords | Should-BeString -CaseSensitive 'ABCDEFGHIJKMNPQRSTUVWXYZ'
		}

		It 'holds a character for each listing of a group listed twice' {
			$passwords = New-RandomPassword -PasswordLength 3 -Count 200 -InputStrings 'abcdefghijkmnpqrstuvwxyz', '23456789', '23456789'
			$passwords | Should-All { ($_ -replace '\D').Length -eq 2 }
		}
	}

	Context 'CharacterSet' {
		BeforeDiscovery {
			$noLookAlikes = 'abcdefghijkmnpqrstuvwxyz', 'ABCDEFGHJKLMNPQRSTUVWXYZ', '23456789'
			$combinations = @(
				@{ Name = 'Lowercase'; Values = @('Lowercase'); Groups = @('abcdefghijklmnopqrstuvwxyz') }
				@{ Name = 'Uppercase'; Values = @('Uppercase'); Groups = @('ABCDEFGHIJKLMNOPQRSTUVWXYZ') }
				@{ Name = 'Digits'; Values = @('Digits'); Groups = @('1234567890') }
				@{ Name = 'Symbols'; Values = @('Symbols'); Groups = @('!@$#%&') }
				@{ Name = 'Lowercase, Digits'; Values = @('Lowercase', 'Digits'); Groups = @('abcdefghijklmnopqrstuvwxyz', '1234567890') }
				@{ Name = 'NoLookAlikes'; Values = @('NoLookAlikes'); Groups = $noLookAlikes }
				@{ Name = 'NoLookAlikes, Symbols'; Values = @('NoLookAlikes', 'Symbols'); Groups = $noLookAlikes + '!@$#%&' }
				@{ Name = 'NoLookAlikes, Lowercase, Uppercase, Digits'; Values = @('NoLookAlikes', 'Lowercase', 'Uppercase', 'Digits'); Groups = $noLookAlikes }
			)
		}

		# NoLookAlikes takes the place of Lowercase, Uppercase, and Digits, so they add nothing beside it. With 6,000
		# characters drawn, the chance that a character of the groups never shows up is below 10^-38 for each combination.
		It 'draws from the groups of -CharacterSet <Name>, and only those' -ForEach $combinations {
			$passwords = New-RandomPassword -PasswordLength 30 -Count 200 -CharacterSet $Values
			Get-DistinctCharacter $passwords | Should-BeString -CaseSensitive (Get-DistinctCharacter $Groups)
			foreach ($group in $Groups) {
				$passwords | Should-All { $_.IndexOfAny($group.ToCharArray()) -ge 0 } -Because "every password should hold one of $group"
			}
		}

		# With 6,000 characters drawn, the chance that one of the 12 never shows up is below 10^-219.
		It 'adds the -ExtraCharacters groups to its own' {
			$passwords = New-RandomPassword -PasswordLength 30 -Count 200 -CharacterSet Digits -ExtraCharacters '*+'
			Get-DistinctCharacter $passwords | Should-BeString -CaseSensitive (Get-DistinctCharacter '1234567890', '*+')
			$passwords | Should-All { $_.IndexOfAny([char[]]'*+') -ge 0 } -Because 'every password should hold one of *+'
		}

		# Each set of a length has a twin for -CharacterSet, so a check of the set for random lengths has to accept both.
		# With 3 lengths to draw from, the chance that 300 passwords miss one is below 10^-52, and the chance that a digit
		# never shows up is below 10^-67.
		It 'gives each password a random length from -MinimumLength to -MaximumLength' {
			$passwords = New-RandomPassword -MinimumLength 4 -MaximumLength 6 -Count 300 -CharacterSet Digits
			$lengths = @($passwords | ForEach-Object { $_.Length } | Sort-Object -Unique)
			($lengths -join ', ') | Should-Be '4, 5, 6'
			Get-DistinctCharacter $passwords | Should-BeString -CaseSensitive (Get-DistinctCharacter '1234567890')
		}

		It 'fails with MinimumLengthExceedsMaximumLength for a minimum greater than the maximum' {
			$err = { New-RandomPassword -MinimumLength 6 -MaximumLength 4 -CharacterSet Digits } | Should-Throw -FullyQualifiedErrorId 'MinimumLengthExceedsMaximumLength,*'
			$err.CategoryInfo.Category | Should-Be 'InvalidArgument'
		}

		# The parameter sets keep the two apart.
		It 'fails at binding with -InputStrings' {
			{ New-RandomPassword -CharacterSet Digits -InputStrings '23456789' } | Should-Throw -FullyQualifiedErrorId 'AmbiguousParameterSet,*'
		}

		# PowerShell refuses an undefined bit when it converts an argument, but a value that already has the enum's type
		# binds as it is, and 0 binds although no value is 0. The validation after the binding refuses both. Each argument
		# is a script block, because the enum isn't loaded yet when Pester discovers the tests.
		It 'fails at binding for <Description>' -ForEach @(
			@{ Description = '0'; Argument = { 0 } }
			@{ Description = 'a -band of two values that share no bits'; Argument = { [MG.Passwords.CharacterSet]::Lowercase -band [MG.Passwords.CharacterSet]::Digits } }
			@{ Description = 'a value with a bit that no value defines'; Argument = { [enum]::ToObject([MG.Passwords.CharacterSet], 64) } }
		) {
			$value = & $Argument
			{ New-RandomPassword -CharacterSet $value } | Should-Throw -FullyQualifiedErrorId 'ParameterArgumentValidationError,*'
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

		# With 6,000 characters drawn, the chance that one of the default characters never shows up is below 10^-30.
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
		# 10^-30.
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

		# Splatting passes a switch that's off as $false. With 6,000 characters drawn, the chance that one of the 68 default
		# characters never shows up is below 10^-35.
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
			@{ Name = 'StaticLengthCharacterSet'; Index = 1 }
			@{ Name = 'RandomLength'; Index = 2 }
			@{ Name = 'RandomLengthCharacterSet'; Index = 3 }
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

		It 'describes -<_> as the cmdlet declares it' -ForEach 'PasswordLength', 'Count', 'MinimumLength', 'MaximumLength', 'InputStrings', 'CharacterSet', 'ExtraCharacters', 'OnlyUseExtraCharacters', 'FirstCharacter' {
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
