#Requires -RunAsAdministrator
# Optimizzzer: Remove Microsoft Edge
#
# WARNING: this script removes or turns off parts of Windows. Use it at your own risk.
# It can affect security, updates or other apps. Read all of it before running it and create a
# restore point. The authors of Optimizzzer accept no liability for any damage.

$k = 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdateDev'
New-Item -Path $k -Force | Out-Null; Set-ItemProperty -Path $k -Name AllowUninstall -Type String -Value ''
$setup = Get-ChildItem "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\*\Installer\setup.exe" -ErrorAction SilentlyContinue | Select-Object -Last 1
if ($setup) { Start-Process $setup.FullName -ArgumentList '--uninstall','--system-level','--verbose-logging','--force-uninstall' -Wait }

# To go back:
# Remove-ItemProperty -Path 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdateDev' -Name AllowUninstall -ErrorAction SilentlyContinue
# Download it from microsoft.com/edge or with: winget install Microsoft.Edge
