#Requires -RunAsAdministrator
# Optimizzzer: Turn off Microsoft Defender
#
# WARNING: this script removes or turns off parts of Windows. Use it at your own risk.
# It can affect security, updates or other apps. Read all of it before running it and create a
# restore point. The authors of Optimizzzer accept no liability for any damage.

$p = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection'
New-Item -Path $p -Force | Out-Null
foreach ($n in 'DisableRealtimeMonitoring','DisableBehaviorMonitoring','DisableOnAccessProtection','DisableScanOnRealtimeEnable','DisableIOAVProtection') { Set-ItemProperty -Path $p -Name $n -Type DWord -Value 1 }
Set-MpPreference -DisableRealtimeMonitoring $true -ErrorAction SilentlyContinue

# To go back:
# Remove-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection' -Recurse -ErrorAction SilentlyContinue
# Set-MpPreference -DisableRealtimeMonitoring $false -ErrorAction SilentlyContinue
# "Go back" removes the policies and turns real-time protection back on. Then turn Tamper Protection on again.
