# New-RandomPassword
[![version](https://img.shields.io/powershellgallery/v/NewRandomPassword.svg)](https://www.powershellgallery.com/packages/NewRandomPassword)
[![downloads](https://img.shields.io/powershellgallery/dt/NewRandomPassword.svg?label=downloads)](https://www.powershellgallery.com/stats/packages/NewRandomPassword?groupby=Version)

Just as the name of the cmdlet suggests, it generates as many random password as you want in either:

* __Static Length__
    * All generated passwords will be of the same length.
* __Random Length__
    * Each iteration will be of a random length determined by the minimum and maximum parameters.

There's even an option to specify what the _1st_ character will be in each password.

## Examples

1. <code>New-RandomPassword -PasswordLength 12</code>
1. <code>New-RandomPassword -MinimumLength 6 -MaximumLength 67</code>
1. <code>New-RandomPassword 15 -Count 5</code>
1. <code>rpas 68 3 -FirstCharacter %</code>
