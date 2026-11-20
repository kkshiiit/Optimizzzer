#Requires -RunAsAdministrator
# Optimizzzer: Quitar Microsoft Edge
#
# AVISO: este script quita o desactiva partes de Windows. Úsalo bajo tu propia responsabilidad.
# Puede afectar a la seguridad, a las actualizaciones o a otras apps. Léelo entero antes de ejecutarlo
# y crea un punto de restauración. Los autores de Optimizzzer no se hacen cargo de ningún daño.

$k = 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdateDev'
New-Item -Path $k -Force | Out-Null; Set-ItemProperty -Path $k -Name AllowUninstall -Type String -Value ''
$setup = Get-ChildItem "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\*\Installer\setup.exe" -ErrorAction SilentlyContinue | Select-Object -Last 1
if ($setup) { Start-Process $setup.FullName -ArgumentList '--uninstall','--system-level','--verbose-logging','--force-uninstall' -Wait }

# Para volver atrás:
# Remove-ItemProperty -Path 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdateDev' -Name AllowUninstall -ErrorAction SilentlyContinue
# Descárgalo de microsoft.com/edge o con: winget install Microsoft.Edge
