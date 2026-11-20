using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Optimizzzer.Core.Tools
{
    /// <summary>
    /// One action of the "At your own risk" zone: it removes or turns off part of Windows. Every action is a plain
    /// PowerShell script so anyone can read exactly what it does, run it by hand, or not run it at all.
    /// </summary>
    public sealed class RiskItem
    {
        public string Id { get; set; }
        public Text Title { get; set; }
        public Text What { get; set; }
        /// <summary>What stops working.</summary>
        public Text Lose { get; set; }
        /// <summary>How to get it back.</summary>
        public Text Back { get; set; }
        public string ApplyScript { get; set; }
        /// <summary>Partial undo the app can run; reinstalling is done from <see cref="BackLink"/>.</summary>
        public string RevertScript { get; set; }
        /// <summary>Prints "on" while the feature is still present or active, "off" once removed or disabled.</summary>
        public string DetectScript { get; set; }
        /// <summary>Store page or download link to reinstall it.</summary>
        public string BackLink { get; set; }
    }

    public static class RiskZone
    {
        public static string Header(string title, bool spanish)
        {
            var sb = new StringBuilder();
            sb.Append("#Requires -RunAsAdministrator\r\n");
            sb.Append("# Optimizzzer: ").Append(title).Append("\r\n#\r\n");
            if (spanish)
            {
                sb.Append("# AVISO: este script quita o desactiva partes de Windows. Úsalo bajo tu propia responsabilidad.\r\n");
                sb.Append("# Puede afectar a la seguridad, a las actualizaciones o a otras apps. Léelo entero antes de ejecutarlo\r\n");
                sb.Append("# y crea un punto de restauración. Los autores de Optimizzzer no se hacen cargo de ningún daño.\r\n");
            }
            else
            {
                sb.Append("# WARNING: this script removes or turns off parts of Windows. Use it at your own risk.\r\n");
                sb.Append("# It can affect security, updates or other apps. Read all of it before running it and create a\r\n");
                sb.Append("# restore point. The authors of Optimizzzer accept no liability for any damage.\r\n");
            }
            sb.Append("\r\n");
            return sb.ToString();
        }

        /// <summary>The full .ps1 a person can save: header, the action, and the way back as comments.</summary>
        public static string FullScript(RiskItem i, bool spanish)
        {
            var sb = new StringBuilder(Header(spanish ? i.Title.Es : i.Title.En, spanish));
            sb.Append(i.ApplyScript.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n")).Append("\r\n\r\n");
            sb.Append("# ").Append(spanish ? "Para volver atrás:" : "To go back:").Append("\r\n");
            foreach (var line in (i.RevertScript ?? "").Split('\n')) sb.Append("# ").Append(line.TrimEnd('\r')).Append("\r\n");
            sb.Append("# ").Append(spanish ? i.Back.Es : i.Back.En).Append("\r\n");
            return sb.ToString();
        }

        /// <summary>powershell.exe arguments that run a script without quoting problems.</summary>
        public static string PowerShellArgs(string script) =>
            "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        private const string XboxApps = "'Microsoft.XboxApp','Microsoft.GamingApp','Microsoft.XboxGamingOverlay','Microsoft.XboxGameOverlay','Microsoft.XboxIdentityProvider','Microsoft.XboxSpeechToTextOverlay','Microsoft.Xbox.TCUI'";
        private const string XboxServices = "'XblAuthManager','XblGameSave','XboxNetApiSvc','XboxGipSvc'";
        private const string EdgeSetup = "Get-ChildItem \"${env:ProgramFiles(x86)}\\Microsoft\\Edge\\Application\\*\\Installer\\setup.exe\" -ErrorAction SilentlyContinue | Select-Object -Last 1";
        private const string DefenderPolicy = "HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows Defender\\Real-Time Protection";

        public static readonly IReadOnlyList<RiskItem> All = new[]
        {
            new RiskItem
            {
                Id = "copilot",
                Title = new Text("Quitar Copilot", "Remove Copilot"),
                What = new Text("Desinstala la app Copilot para todos los usuarios y activa la directiva que lo apaga.", "Uninstalls the Copilot app for all users and turns on the policy that switches it off."),
                Lose = new Text("El asistente Copilot y su tecla. Las funciones de IA de otras apps (Paint, Fotos) pueden seguir.", "The Copilot assistant and its key. AI features inside other apps (Paint, Photos) may remain."),
                Back = new Text("Reinstálalo desde la Microsoft Store (\"Microsoft Copilot\").", "Reinstall it from the Microsoft Store (\"Microsoft Copilot\")."),
                BackLink = "ms-windows-store://pdp/?productid=9NHT9RB2F4HD",
                ApplyScript =
                    "Get-AppxPackage -AllUsers | Where-Object { $_.Name -like '*Copilot*' } | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue\n" +
                    "Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -like '*Copilot*' } | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Out-Null\n" +
                    "foreach ($h in 'HKLM:','HKCU:') { $p = \"$h\\Software\\Policies\\Microsoft\\Windows\\WindowsCopilot\"; New-Item -Path $p -Force | Out-Null; Set-ItemProperty -Path $p -Name TurnOffWindowsCopilot -Type DWord -Value 1 }",
                RevertScript = "foreach ($h in 'HKLM:','HKCU:') { Remove-ItemProperty -Path \"$h\\Software\\Policies\\Microsoft\\Windows\\WindowsCopilot\" -Name TurnOffWindowsCopilot -ErrorAction SilentlyContinue }",
                DetectScript = "if (Get-AppxPackage -AllUsers | Where-Object { $_.Name -like '*Copilot*' }) { 'on' } else { 'off' }",
            },
            new RiskItem
            {
                Id = "xbox",
                Title = new Text("Quitar Xbox", "Remove Xbox"),
                What = new Text("Desinstala la app Xbox, la Game Bar y sus componentes, y deshabilita los servicios de Xbox.", "Uninstalls the Xbox app, the Game Bar and their components, and disables the Xbox services."),
                Lose = new Text("Game Pass, juegos de la Store y de Xbox, logros, guardado en la nube, mandos Xbox con adaptador y la grabación con Win+G.", "Game Pass, Store and Xbox games, achievements, cloud saves, Xbox controllers with the adapter and Win+G recording."),
                Back = new Text("Los servicios vuelven a Manual con \"Volver atrás\"; la app Xbox y la Game Bar se reinstalan desde la Microsoft Store.", "\"Go back\" sets the services to Manual again; the Xbox app and Game Bar are reinstalled from the Microsoft Store."),
                BackLink = "ms-windows-store://pdp/?productid=9MV0B5HZVK9Z",
                ApplyScript =
                    "foreach ($a in " + XboxApps + ") {\n" +
                    "  Get-AppxPackage -AllUsers -Name $a | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue\n" +
                    "  Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -eq $a } | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Out-Null\n" +
                    "}\n" +
                    "foreach ($s in " + XboxServices + ") { if (Get-Service $s -ErrorAction SilentlyContinue) { Stop-Service $s -Force -ErrorAction SilentlyContinue; Set-Service $s -StartupType Disabled } }",
                RevertScript = "foreach ($s in " + XboxServices + ") { if (Get-Service $s -ErrorAction SilentlyContinue) { Set-Service $s -StartupType Manual } }",
                DetectScript = "if (Get-AppxPackage -AllUsers | Where-Object { @(" + XboxApps + ") -contains $_.Name }) { 'on' } else { 'off' }",
            },
            new RiskItem
            {
                Id = "edge",
                Title = new Text("Quitar Microsoft Edge", "Remove Microsoft Edge"),
                What = new Text("Usa el desinstalador oficial de Edge, habilitado con la clave AllowUninstall de EdgeUpdate. WebView2 no se toca: otras apps lo necesitan.", "Uses Edge's own uninstaller, enabled with EdgeUpdate's AllowUninstall key. WebView2 is left alone: other apps need it."),
                Lose = new Text("El navegador Edge. Los enlaces que Windows abre con Edge (widgets, ayuda) pedirán otro navegador. Una actualización de Windows puede volver a instalarlo.", "The Edge browser. Links Windows opens with Edge (widgets, help) will ask for another browser. A Windows update may install it again."),
                Back = new Text("Descárgalo de microsoft.com/edge o con: winget install Microsoft.Edge", "Download it from microsoft.com/edge or with: winget install Microsoft.Edge"),
                BackLink = "https://www.microsoft.com/edge",
                ApplyScript =
                    "$k = 'HKLM:\\SOFTWARE\\WOW6432Node\\Microsoft\\EdgeUpdateDev'\n" +
                    "New-Item -Path $k -Force | Out-Null; Set-ItemProperty -Path $k -Name AllowUninstall -Type String -Value ''\n" +
                    "$setup = " + EdgeSetup + "\n" +
                    "if ($setup) { Start-Process $setup.FullName -ArgumentList '--uninstall','--system-level','--verbose-logging','--force-uninstall' -Wait }",
                RevertScript = "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\WOW6432Node\\Microsoft\\EdgeUpdateDev' -Name AllowUninstall -ErrorAction SilentlyContinue",
                DetectScript = "if (Test-Path \"${env:ProgramFiles(x86)}\\Microsoft\\Edge\\Application\\msedge.exe\") { 'on' } else { 'off' }",
            },
            new RiskItem
            {
                Id = "store",
                Title = new Text("Quitar Microsoft Store", "Remove Microsoft Store"),
                What = new Text("Desinstala la Microsoft Store y su componente de compras para todos los usuarios.", "Uninstalls the Microsoft Store and its purchase component for all users."),
                Lose = new Text("Instalar o actualizar apps de la Store (incluidas Fotos, Calculadora y juegos). Las apps ya instaladas dejan de actualizarse.", "Installing or updating Store apps (Photos, Calculator and games included). Installed apps stop updating."),
                Back = new Text("\"Volver atrás\" ejecuta wsreset -i, que la reinstala en Windows 11 y Windows 10 recientes.", "\"Go back\" runs wsreset -i, which reinstalls it on Windows 11 and recent Windows 10."),
                ApplyScript =
                    "foreach ($a in 'Microsoft.WindowsStore','Microsoft.StorePurchaseApp') {\n" +
                    "  Get-AppxPackage -AllUsers -Name $a | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue\n" +
                    "  Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -eq $a } | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Out-Null\n" +
                    "}",
                RevertScript = "Start-Process wsreset.exe -ArgumentList '-i' -Wait",
                DetectScript = "if (Get-AppxPackage -AllUsers -Name Microsoft.WindowsStore) { 'on' } else { 'off' }",
            },
            new RiskItem
            {
                Id = "defender",
                Title = new Text("Desactivar Microsoft Defender", "Turn off Microsoft Defender"),
                What = new Text("Windows no permite quitar Defender. Esto apaga la protección en tiempo real con las directivas de Defender. Solo funciona si antes apagas la Protección contra alteraciones en Seguridad de Windows.", "Windows does not allow removing Defender. This turns off real-time protection with Defender's policies. It only works if you first turn off Tamper Protection in Windows Security."),
                Lose = new Text("La protección contra virus en tiempo real. Sin otro antivirus, el equipo queda expuesto a malware.", "Real-time virus protection. Without another antivirus, the PC is exposed to malware."),
                Back = new Text("\"Volver atrás\" borra las directivas y vuelve a activar el tiempo real. Después enciende la Protección contra alteraciones.", "\"Go back\" removes the policies and turns real-time protection back on. Then turn Tamper Protection on again."),
                BackLink = "windowsdefender://threatsettings",
                ApplyScript =
                    "$p = '" + DefenderPolicy + "'\n" +
                    "New-Item -Path $p -Force | Out-Null\n" +
                    "foreach ($n in 'DisableRealtimeMonitoring','DisableBehaviorMonitoring','DisableOnAccessProtection','DisableScanOnRealtimeEnable','DisableIOAVProtection') { Set-ItemProperty -Path $p -Name $n -Type DWord -Value 1 }\n" +
                    "Set-MpPreference -DisableRealtimeMonitoring $true -ErrorAction SilentlyContinue",
                RevertScript =
                    "Remove-Item -Path '" + DefenderPolicy + "' -Recurse -ErrorAction SilentlyContinue\n" +
                    "Set-MpPreference -DisableRealtimeMonitoring $false -ErrorAction SilentlyContinue",
                DetectScript = "$s = Get-MpComputerStatus -ErrorAction SilentlyContinue; if (-not $s -or $s.RealTimeProtectionEnabled) { 'on' } else { 'off' }",
            },
        };

        /// <summary>"True|True" style status of Defender: real-time on, Tamper Protection on.</summary>
        public const string DefenderStatusScript = "$s = Get-MpComputerStatus -ErrorAction SilentlyContinue; if ($s) { \"$($s.RealTimeProtectionEnabled)|$($s.IsTamperProtected)\" } else { 'none' }";

        public static RiskItem Find(string id) => All.FirstOrDefault(i => i.Id == id);
    }
}
