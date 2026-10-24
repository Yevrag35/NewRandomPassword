# New-RandomPassword
[![version](https://img.shields.io/powershellgallery/v/NewRandomPassword.svg)](https://www.powershellgallery.com/packages/NewRandomPassword)
[![downloads](https://img.shields.io/powershellgallery/dt/NewRandomPassword.svg?label=downloads)](https://www.powershellgallery.com/stats/packages/NewRandomPassword?groupby=Version)

NewRandomPassword is a PowerShell module with one cmdlet, `New-RandomPassword`, or `rpas` for short. It generates as many random passwords as you ask for, in one of two ways:

- **Fixed length:** every password has `-PasswordLength` characters, 8 by default.
- **Random length:** each password gets a random length of its own, from `-MinimumLength` to `-MaximumLength`.

Every password holds a character from each group of characters, such as the lowercase letters or the digits, when it has room for one, and `-FirstCharacter` sets the character that every password starts with. Every random choice comes from the operating system's cryptographically secure random number generator.

## Installing

NewRandomPassword 2.0.0 runs on Windows PowerShell 5.1 and on PowerShell 7.6 or later. In PowerShell 7.5 or earlier, the import fails with an error that says so.

```powershell
Install-Module -Name NewRandomPassword
```

`Get-Help New-RandomPassword -Full` describes every parameter, with examples.

## Examples

| Command | Generates |
|---|---|
| `New-RandomPassword -PasswordLength 12` | One password of 12 characters |
| `New-RandomPassword -MinimumLength 6 -MaximumLength 67` | One password of 6 to 67 characters |
| `New-RandomPassword 15 -Count 5` | Five passwords of 15 characters |
| `rpas 68 3 -FirstCharacter %` | Three passwords of 68 characters, each of which starts with `%` |
| `New-RandomPassword -PasswordLength 16 -ExtraCharacters '*+-=?'` | One password of 16 characters that holds one of `*+-=?`, besides a character from each default group |
| `New-RandomPassword 6 -ExtraCharacters '0123456789' -OnlyUseExtraCharacters` | A PIN of six digits |

## Character groups

Without `-InputStrings`, passwords are built from four groups, 61 characters in all, which leave out characters that are easy to mistake for others, such as `l` and `1`, or `O` and `0`:

| Group | Characters |
|---|---|
| The lowercase letters except l and o | `abcdefghijkmnpqrstuvwxyz` |
| The uppercase letters except D, I, and O | `ABCEFGHJKLMNPQRSTUVWXYZ` |
| The digits except 0 and 1 | `23456789` |
| Six symbols | `!@$#%&` |

A password holds a character from each group, and characters drawn from all the groups fill the rest of it. A password with fewer characters than there are groups holds characters from a random choice of the groups instead. Then the characters are shuffled, all but `-FirstCharacter`, which always comes first. It counts toward the length, but it doesn't take the place of any group's character.

Three parameters change the groups:

- **`-InputStrings`** replaces the default groups. Each value has to be one of seven strings: the four above, `1234567890` (all the digits), `234567890` (the digits except 1), or `!#%&` (the symbols except @ and $). The parameter accepts each one regardless of case and uses it as you typed it, and a group that's listed twice supplies two characters to every password that has room for them.
- **`-ExtraCharacters`** adds groups of your own. A string is one group, and so is a `[char]` or a `[char[]]`. Each element of a collection is a group of its own, such as each line that `Get-Content` outputs. A group can't be empty, and it can't hold a control character, such as a tab, or a character that takes a surrogate pair, such as an emoji.
- **`-OnlyUseExtraCharacters`** builds passwords from the `-ExtraCharacters` groups alone, without the default groups or the `-InputStrings` groups. It requires `-ExtraCharacters`.

This command builds a password of 16 characters from the lowercase letters, the digits 2 to 9, and the characters `-` and `_`, with at least one of each:

```powershell
New-RandomPassword 16 -InputStrings 'abcdefghijkmnpqrstuvwxyz', '23456789' -ExtraCharacters '-_'
```

## Changes from 1.2.0

2.0.0 keeps every parameter, alias, position, parameter set, and default of 1.2.0. It no longer imports in PowerShell 6 or in PowerShell 7 before 7.6, which 1.2.0's manifest allowed. What changes:

- `-FirstCharacter` always comes first. 1.2.0 put it last in about half the passwords.
- With `-MinimumLength` and `-MaximumLength`, each password gets a random length of its own. 1.2.0 gave every password of a call the same length.
- A password with fewer characters than there are groups holds characters from a random choice of the groups. 1.2.0 always took the first groups, so a password of 2 or 3 characters never held a symbol.
- A repeated character doesn't count twice. A character that a group lists twice is as likely as the group's other characters, and the characters that fill a password after one from each group are drawn from the distinct characters of all the groups. 1.2.0 drew a repeated character twice as often.
- `-ExtraCharacters` takes a string, a `[char]`, a `[char[]]`, or a collection of them, such as the lines that `Get-Content` outputs, with one group per element. 1.2.0 refused a single line, a `[char]`, or a collection other than an array, and dropped every element that wasn't a string. `Get-Command` shows the type of `-ExtraCharacters` as `String[]` instead of `Object`.
- `-FirstCharacter 5` starts each password with the digit 5. 1.2.0 read a bare number as a character code, so 5 was a control character, and 65, which was `A`, is now an error.
- An argument that can't make a password fails when PowerShell binds it, with an error that names the parameter: an empty group, a control character, a character that takes a surrogate pair, such as an emoji, or a value of another type for `-ExtraCharacters`.
- `-OnlyUseExtraCharacters` without `-ExtraCharacters` is an error. 1.2.0 ignored the switch.
- A minimum length greater than the maximum, and arguments that leave no characters to draw from, end the command with an error of their own. 1.2.0 threw raw exceptions, such as `DivideByZeroException`.
- `-PasswordLength` and `-MaximumLength` take at most 1,048,576 characters, and `-MinimumLength` at most 1,048,575. 1.2.0 accepted lengths that no string can hold.
