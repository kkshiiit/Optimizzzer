# Optimizzzer

Optimizador para Windows 10 y 11. Es un único `Optimizzzer.exe` de unos 500 KB que no instala nada: funciona con .NET Framework 4.8, que ya viene con Windows 10 (1903 o posterior) y con Windows 11.

## Qué hace

**Asistente paso a paso**
1. Análisis: lee tu equipo y el estado real de cada ajuste. Solo lee.
2. Perfil: Mínimo seguro, Trabajo y estudio, Juegos, Portátil y batería o Privacidad máxima. Te recomienda uno según tu hardware.
3. Selección: 107 ajustes en 10 categorías, con buscador. Cada uno se puede desplegar para ver por qué, qué pierdes, qué cambia exactamente (clave de registro, servicio o tarea) y la fuente.
4. Revisión: la lista exacta de cambios antes de tocar nada (modo simulación).
5. Aplicar: crea un punto de restauración y guarda el valor anterior de cada cambio. Si un ajuste falla, se revierte solo.
6. Resultado: mediciones antes y después tomadas en tu equipo, e informe HTML.

**Herramientas**
- Limpieza: calcula el espacio a liberar antes de borrar. Nunca fuerza archivos en uso.
- Reparación: DISM, SFC, red, Windows Update, Microsoft Store y caché de iconos, con la salida visible.
- Red y DNS: mide la velocidad real de cada DNS desde tu conexión y permite volver al anterior.
- Programas de inicio, Servicios, Tareas programadas y Apps instaladas.

**Seguimiento**
- Historial: deshacer cada cambio por separado o una sesión entera.
- Cambios revertidos: detecta qué ajustes ha deshecho una actualización de Windows y los vuelve a aplicar.
- Mediciones: memoria en uso, procesos, servicios, programas de inicio, espacio libre y duración del arranque (del registro de eventos de Windows).

## Qué no hace (a propósito)

No aplica ajustes placebo ni peligrosos. La lista y los motivos están en [docs/EXCLUDED.md](docs/EXCLUDED.md). Tampoco desactiva Windows Defender ni Windows Update, ni desinstala Edge.

## Descargar

Cada push genera el ejecutable en GitHub Actions: pestaña **Actions**, ejecución **build**, artefacto **Optimizzzer**.

Windows SmartScreen avisará al abrirlo porque el exe no está firmado. Pulsa "Más información" y luego "Ejecutar de todas formas".

## Línea de comandos

```
Optimizzzer.exe -Scan
Optimizzzer.exe -List
Optimizzzer.exe -Profile Gaming -DryRun
Optimizzzer.exe -Profile Privacy -Silent -Report informe.html
Optimizzzer.exe -Apply privacy.telemetry,ui.fileExtensions -Silent
Optimizzzer.exe -Undo all
```

`-Help` muestra todas las opciones.

## Compilar

- Núcleo y tests en cualquier sistema: `dotnet test tests/Optimizzzer.Tests`
- Ejecutable: `dotnet build src/Optimizzzer.App -c Release`. Funciona en Windows y también en Linux, porque la interfaz se construye en C# sin XAML compilado.

## Antes de usarlo en tu PC

Cada cambio de código se verifica automáticamente en un Windows real de GitHub (Windows Server 2025, misma base que Windows 11 24H2):

- 27 tests del núcleo, entre ellos "aplicar todo y deshacer deja el estado idéntico".
- Auto-prueba de la interfaz: primer análisis como en un arranque normal, todas las pantallas en tema claro y oscuro, en español e inglés, y aplicar y deshacer tres ajustes desde el asistente.
- Ida y vuelta real por línea de comandos: aplica 18 ajustes de registro, servicios, tareas programadas, DirectX y plan de energía, comprueba que quedan aplicados, lo deshace todo y verifica que el análisis final es idéntico al inicial.

Windows 10 no se prueba automáticamente. Antes de usarlo en tu PC, pruébalo en una máquina virtual con Windows 10 y otra con Windows 11:

1. Abre la app y completa el análisis.
2. Aplica el perfil Mínimo seguro.
3. Comprueba en Historial que aparecen los cambios.
4. Pulsa "Deshacer toda la sesión" y verifica que el estado vuelve a ser el de antes con un nuevo análisis.
5. Prueba Limpieza, Red y DNS (y "Volver a mi DNS anterior").
6. Cambia el tema y el idioma, y navega con el teclado (Tab, Enter, Esc).

Si ejecutas la app con una cuenta de administrador distinta a la tuya, los ajustes de usuario (HKCU) se aplican a esa cuenta, no a la tuya.

## Datos que guarda

Todo queda en `C:\ProgramData\Optimizzzer`: el diario para deshacer, las mediciones, la copia de seguridad del DNS y los ajustes de idioma y tema. Borrar la carpeta no deshace nada, solo olvida el historial.

## Créditos

Los ajustes se han reimplementado a partir de la documentación de Microsoft y del trabajo de estos proyectos, sin copiar su código:
[hellzerg/optimizer](https://github.com/hellzerg/optimizer),
[ChrisTitusTech/winutil](https://github.com/ChrisTitusTech/winutil),
[itsfatduck/optimizerDuck](https://github.com/itsfatduck/optimizerDuck),
[churupitaromario-ops/system-tweaks-wintoys](https://github.com/churupitaromario-ops/system-tweaks-wintoys) y
[nokaraempire/nokara-system-manager](https://github.com/nokaraempire/nokara-system-manager).

Las reglas de diseño vienen de [antislop](https://github.com/miqdadbadjuber/anti-slop) (MIT), instalado en `.claude/skills/`. La dirección visual está en [DESIGN.md](DESIGN.md).
