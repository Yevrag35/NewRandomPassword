# NewRandomPassword

PowerShell module that provides the `New-RandomPassword` cmdlet (alias `rpas`). It's published to the PowerShell Gallery as `NewRandomPassword`. GitHub: `Yevrag35/NewRandomPassword`.

## Overhaul in progress

The whole repo is being overhauled on `dev`. Every part of the C# solution can change: projects, folder layout, target frameworks, dependencies, build and packaging. Don't trust a description of the solution from memory, git history, or older docs. Look at the current tree.

## Reference points

- **`archive/DifferentApproach` tag:** an abandoned 2020 rewrite (it includes the old `Maybe` branch). Use it for reference only. Don't copy the SecureString/unsafe byte handling in its `PasswordHolder`: it left an unzeroed BSTR copy of every password in memory and garbled non-ASCII characters.

## Plans

Multi-session plans live in `.ideas/`, one file per topic. Plans are working notes, not documentation: check what one says about the code against the current tree before you build on it.

## Building and testing

- Windows PowerShell 5.1 (`powershell.exe`) and PowerShell 7 (`pwsh`) are both supported editions.
- Before running a Release build, check where its output goes. At 1.2.0, Release builds wrote straight into the module folder that's checked into git.
