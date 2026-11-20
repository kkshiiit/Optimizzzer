#Requires -RunAsAdministrator
# Optimizzzer: Quitar Xbox
#
# AVISO: este script quita o desactiva partes de Windows. Úsalo bajo tu propia responsabilidad.
# Puede afectar a la seguridad, a las actualizaciones o a otras apps. Léelo entero antes de ejecutarlo
# y crea un punto de restauración. Los autores de Optimizzzer no se hacen cargo de ningún daño.

foreach ($a in 'Microsoft.XboxApp','Microsoft.GamingApp','Microsoft.XboxGamingOverlay','Microsoft.XboxGameOverlay','Microsoft.XboxIdentityProvider','Microsoft.XboxSpeechToTextOverlay','Microsoft.Xbox.TCUI') {
  Get-AppxPackage -AllUsers -Name $a | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
  Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -eq $a } | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Out-Null
}
foreach ($s in 'XblAuthManager','XblGameSave','XboxNetApiSvc','XboxGipSvc') { if (Get-Service $s -ErrorAction SilentlyContinue) { Stop-Service $s -Force -ErrorAction SilentlyContinue; Set-Service $s -StartupType Disabled } }

# Para volver atrás:
# foreach ($s in 'XblAuthManager','XblGameSave','XboxNetApiSvc','XboxGipSvc') { if (Get-Service $s -ErrorAction SilentlyContinue) { Set-Service $s -StartupType Manual } }
# Los servicios vuelven a Manual con "Volver atrás"; la app Xbox y la Game Bar se reinstalan desde la Microsoft Store.
