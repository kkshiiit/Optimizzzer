#Requires -RunAsAdministrator
# Optimizzzer: Remove Copilot
#
# WARNING: this script removes or turns off parts of Windows. Use it at your own risk.
# It can affect security, updates or other apps. Read all of it before running it and create a
# restore point. The authors of Optimizzzer accept no liability for any damage.

Get-AppxPackage -AllUsers | Where-Object { $_.Name -like '*Copilot*' } | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -like '*Copilot*' } | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Out-Null
foreach ($h in 'HKLM:','HKCU:') { $p = "$h\Software\Policies\Microsoft\Windows\WindowsCopilot"; New-Item -Path $p -Force | Out-Null; Set-ItemProperty -Path $p -Name TurnOffWindowsCopilot -Type DWord -Value 1 }

# To go back:
# foreach ($h in 'HKLM:','HKCU:') { Remove-ItemProperty -Path "$h\Software\Policies\Microsoft\Windows\WindowsCopilot" -Name TurnOffWindowsCopilot -ErrorAction SilentlyContinue }
# Reinstall it from the Microsoft Store ("Microsoft Copilot").
