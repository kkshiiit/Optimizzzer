#Requires -RunAsAdministrator
# Optimizzzer: Quitar Microsoft Store
#
# AVISO: este script quita o desactiva partes de Windows. Úsalo bajo tu propia responsabilidad.
# Puede afectar a la seguridad, a las actualizaciones o a otras apps. Léelo entero antes de ejecutarlo
# y crea un punto de restauración. Los autores de Optimizzzer no se hacen cargo de ningún daño.

foreach ($a in 'Microsoft.WindowsStore','Microsoft.StorePurchaseApp') {
  Get-AppxPackage -AllUsers -Name $a | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
  Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -eq $a } | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Out-Null
}

# Para volver atrás:
# Start-Process wsreset.exe -ArgumentList '-i' -Wait
# "Volver atrás" ejecuta wsreset -i, que la reinstala en Windows 11 y Windows 10 recientes.
