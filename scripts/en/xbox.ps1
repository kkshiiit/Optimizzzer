#Requires -RunAsAdministrator
# Optimizzzer: Remove Xbox
#
# WARNING: this script removes or turns off parts of Windows. Use it at your own risk.
# It can affect security, updates or other apps. Read all of it before running it and create a
# restore point. The authors of Optimizzzer accept no liability for any damage.

foreach ($a in 'Microsoft.XboxApp','Microsoft.GamingApp','Microsoft.XboxGamingOverlay','Microsoft.XboxGameOverlay','Microsoft.XboxIdentityProvider','Microsoft.XboxSpeechToTextOverlay','Microsoft.Xbox.TCUI') {
  Get-AppxPackage -AllUsers -Name $a | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
  Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -eq $a } | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Out-Null
}
foreach ($s in 'XblAuthManager','XblGameSave','XboxNetApiSvc','XboxGipSvc') { if (Get-Service $s -ErrorAction SilentlyContinue) { Stop-Service $s -Force -ErrorAction SilentlyContinue; Set-Service $s -StartupType Disabled } }

# To go back:
# foreach ($s in 'XblAuthManager','XblGameSave','XboxNetApiSvc','XboxGipSvc') { if (Get-Service $s -ErrorAction SilentlyContinue) { Set-Service $s -StartupType Manual } }
# "Go back" sets the services to Manual again; the Xbox app and Game Bar are reinstalled from the Microsoft Store.
