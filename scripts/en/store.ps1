#Requires -RunAsAdministrator
# Optimizzzer: Remove Microsoft Store
#
# WARNING: this script removes or turns off parts of Windows. Use it at your own risk.
# It can affect security, updates or other apps. Read all of it before running it and create a
# restore point. The authors of Optimizzzer accept no liability for any damage.

foreach ($a in 'Microsoft.WindowsStore','Microsoft.StorePurchaseApp') {
  Get-AppxPackage -AllUsers -Name $a | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
  Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -eq $a } | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Out-Null
}

# To go back:
# Start-Process wsreset.exe -ArgumentList '-i' -Wait
# "Go back" runs wsreset -i, which reinstalls it on Windows 11 and recent Windows 10.
