# Help files

Measured 2026-10-10 on PowerShell 7.6.6 and Windows PowerShell 5.1.26100.9444, with copies of a binary module's Debug build that hold a test MAML file, and with that module's real help file.

## Where Get-Help looks

- **The file name:** a binary cmdlet's `HelpFile` is its assembly's file name followed by `-Help.xml`, such as `<AssemblyName>.dll-Help.xml`. A module whose editions load differently named assemblies needs the file under each name.
- **Imported by the DLL's path,** the cmdlets' module is the DLL, and Get-Help finds the file in a culture folder of the DLL's folder, such as `en-US`, or in the DLL's folder itself.
- **Imported through a manifest whose `.psm1` imports the DLL from a subfolder,** the cmdlets' module is the manifest's module, whose module base is the manifest's folder. Get-Help finds the file in the `en-US` folder beside the manifest, and in the `en-US` folder of the DLL's subfolder. When both hold it, the one beside the manifest wins.
- **Culture:** a folder for the UI culture wins, such as `de-DE` under de-DE. Without one, both editions fall back to `en-US`, under de-DE and under fr-FR.
- **An installed copy:** with auto-loading on and an older release of the module installed, Get-Help for one of its cmdlets still returns only the loaded build's help. A module-qualified name, such as `Get-Help <module>\<cmdlet>`, works in both editions.

## What Get-Help shows

- **The syntax and the parameter attributes come from the file as it's written.** A syntax block that names a parameter the cmdlet doesn't have shows it, and PARAMETERS lists only the parameters that the file describes, with the file's `required`, `position`, `pipelineInput`, and `aliases`. Get-Help adds `[<CommonParameters>]` to each syntax line, and a `<CommonParameters>` entry after the parameters, by itself.
- **A parameter's aliases:** PowerShell 7 shows an `Aliases` row for each parameter. Windows PowerShell 5.1 doesn't show them.
- **A command's aliases:** with a help file, Get-Help has no ALIASES section in either edition.
- **Without a help file,** Get-Help makes up help from the cmdlet. Its synopsis is the syntax, and its description is empty. Its type names say so (see The help object).
- **Paragraphs:** Get-Help wraps each `maml:para` at the console's width, with a blank line between paragraphs. A paragraph that starts with `- ` prints as it is, so it reads as a list item. An XML comment before `helpItems` doesn't get in the way. A line break inside a `maml:para` stays, and so does the indentation of the line after it, so a paragraph that the file wraps over two lines prints as two lines, the second with the file's tabs in it (measured 2026-10-24 on PowerShell 7.6.8 and Windows PowerShell 5.1.26100.9711).
- **Examples:** each example's title, code, and remarks print as they're written, so a long title wraps too. When an example has no remarks, Get-Help indents the next example's title by a space. When the remarks don't end with an empty `maml:para`, the next title follows them without a blank line. The line breaks in an example's code stay, such as those between a command and its output, and Get-Help indents every line of the code by 4 spaces. A code line longer than the width wraps at a space, like a paragraph, so the rest of the command prints as a line of its own (measured 2026-10-24 on PowerShell 7.6.8 and Windows PowerShell 5.1.26100.9711).

## The help object

- In `Get-Help -Full` output, the entries of `parameters.parameter` and of `syntax.syntaxItem.parameter` hold `name`, `required`, `position`, `pipelineInput`, and `aliases` as the strings that the file writes, in both editions. A parameter's type is `type.name`. In a syntax block, `parameterValue` is the text of the `command:parameterValue` element, or `$null` when the parameter has none.
- `[System.Management.Automation.Cmdlet]::CommonParameters` and `OptionalCommonParameters` list the common parameters in both editions. PowerShell 7's list includes `ProgressAction`.
- **The type names** tell help from a file apart from help made up from the cmdlet. Help from a file has `MamlCommandHelpInfo#<module>#<cmdlet>`, `MamlCommandHelpInfo#<module>`, `MamlCommandHelpInfo`, and `HelpInfo`, where `<module>` is the DLL's module when the DLL is imported by its path and the manifest's module through a manifest. Help made up from the cmdlet has `ExtendedCmdletHelpInfo`, `CmdletHelpInfo`, and `HelpInfo`. `-Full` puts each name with `#FullView` added in front of the rest, so the first type name differs with and without `-Full`. Measured 2026-10-24 on PowerShell 7.6.8 and Windows PowerShell 5.1.26100.9711, with and without NewRandomPassword's help file, both through a manifest whose `.psm1` imports the DLL and with the DLL imported by its path.

## Update-Help

- With a `HelpInfoURI` that isn't a folder of updatable help, such as a GitHub issues page, `Update-Help -Module <module>` fails in both editions: "The HelpInfoUri '...' does not resolve to a container."
- Without a `HelpInfoURI`, `Update-Help -Module <module>` fails, and so does `-Module <module>*`: "The Update-Help command failed because the specified module does not support updatable help."

## What it means for NewRandomPassword

- These measurements cover a manifest whose `.psm1` imports the DLL, and a DLL imported by its path. A manifest that names a DLL as its `RootModule` wasn't measured, so measure where Get-Help finds the file before you rely on a location for that layout.
- Each edition's build needs the help file under its own assembly's name, so renaming an assembly renames the file it needs.
- Because Get-Help trusts the file, a test that compares the help's syntax blocks and parameters with the cmdlet's parameter sets and parameters catches a stale file. Have it read the help with a module-qualified name and check that its type names include `MamlCommandHelpInfo`, so that it also fails when the build doesn't copy the file. Don't compare the first type name, which changes with `-Full`.
- List the cmdlet's alias in the help's notes, because Get-Help shows no ALIASES section.
- Give every example remarks that end with an empty paragraph, because of the spacing above.
- Write each `maml:para` on one line of the file, however long. Keep each line of an example's code within 76 characters, so that with Get-Help's indent it fits a console 80 characters wide, and a wrapped command doesn't read as two.
- Give the manifest a `HelpInfoURI` only when it points at a folder of updatable help. A web page, such as an issues page, makes `Update-Help` fail, and without one `Update-Help` says the module has no updatable help.
