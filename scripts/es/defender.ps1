#Requires -RunAsAdministrator
# Optimizzzer: Desactivar Microsoft Defender
#
# AVISO: este script quita o desactiva partes de Windows. Úsalo bajo tu propia responsabilidad.
# Puede afectar a la seguridad, a las actualizaciones o a otras apps. Léelo entero antes de ejecutarlo
# y crea un punto de restauración. Los autores de Optimizzzer no se hacen cargo de ningún daño.

$p = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection'
New-Item -Path $p -Force | Out-Null
foreach ($n in 'DisableRealtimeMonitoring','DisableBehaviorMonitoring','DisableOnAccessProtection','DisableScanOnRealtimeEnable','DisableIOAVProtection') { Set-ItemProperty -Path $p -Name $n -Type DWord -Value 1 }
Set-MpPreference -DisableRealtimeMonitoring $true -ErrorAction SilentlyContinue

# Para volver atrás:
# Remove-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection' -Recurse -ErrorAction SilentlyContinue
# Set-MpPreference -DisableRealtimeMonitoring $false -ErrorAction SilentlyContinue
# "Volver atrás" borra las directivas y vuelve a activar el tiempo real. Después enciende la Protección contra alteraciones.
