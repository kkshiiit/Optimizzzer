# Scripts

PowerShell scripts for the **At your own risk** zone, in Spanish (`es/`) and English (`en/`). They are exactly what the app saves with "Guardar como script .ps1" (a test keeps them in sync).

Each script:

- needs an administrator PowerShell (`#Requires -RunAsAdministrator`);
- does one thing, shown in full, with the way back as comments at the end;
- starts with the responsibility notice.

**Read [docs/RESPONSABILIDAD.md](../docs/RESPONSABILIDAD.md) first.** Use them at your own risk: the authors of Optimizzzer accept no liability for any damage. Create a restore point before running any of them.

| Script | What it does |
|---|---|
| `copilot.ps1` | Removes the Copilot app and turns on the policy that switches it off |
| `xbox.ps1` | Removes the Xbox app, Game Bar and components; disables Xbox services |
| `edge.ps1` | Uninstalls Microsoft Edge with its own uninstaller (WebView2 is kept) |
| `store.ps1` | Removes the Microsoft Store |
| `defender.ps1` | Turns off Defender real-time protection via policies (turn off Tamper Protection first) |

To pause Windows Update, use the app: it saves a script with the date you pick.
