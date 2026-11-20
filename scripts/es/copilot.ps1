#Requires -RunAsAdministrator
# Optimizzzer: Quitar Copilot
#
# AVISO: este script quita o desactiva partes de Windows. Úsalo bajo tu propia responsabilidad.
# Puede afectar a la seguridad, a las actualizaciones o a otras apps. Léelo entero antes de ejecutarlo
# y crea un punto de restauración. Los autores de Optimizzzer no se hacen cargo de ningún daño.

Get-AppxPackage -AllUsers | Where-Object { $_.Name -like '*Copilot*' } | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -like '*Copilot*' } | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Out-Null
foreach ($h in 'HKLM:','HKCU:') { $p = "$h\Software\Policies\Microsoft\Windows\WindowsCopilot"; New-Item -Path $p -Force | Out-Null; Set-ItemProperty -Path $p -Name TurnOffWindowsCopilot -Type DWord -Value 1 }

# Para volver atrás:
# foreach ($h in 'HKLM:','HKCU:') { Remove-ItemProperty -Path "$h\Software\Policies\Microsoft\Windows\WindowsCopilot" -Name TurnOffWindowsCopilot -ErrorAction SilentlyContinue }
# Reinstálalo desde la Microsoft Store ("Microsoft Copilot").
