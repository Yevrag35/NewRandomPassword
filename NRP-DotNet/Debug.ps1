$curDir = Split-Path -Parent $MyInvocation.MyCommand.Definition;
Import-Module "$curDir\NRP.dll", "$curDir\NewRandomPassword.dll"