**Novedades en 1.0.1** (correcciones de la revisión de código):
- Si un ajuste falla a mitad, también se deshace la parte que llegó a cambiar. Por ejemplo, ya no queda un plan de energía duplicado.
- Si deshacer un cambio falla, el cambio sigue en Historial para reintentarlo, en vez de darse por deshecho.
- La desinstalación de OneDrive solo se da por buena si OneDrive desaparece de verdad.

---

Optimizzzer es un optimizador para Windows 10 y 11. Es un único `Optimizzzer.exe` que no instala nada y pide permisos de administrador al abrirse.

**Qué incluye**
- Asistente paso a paso: análisis, perfil, selección, revisión exacta de cada cambio y aplicar.
- 107 ajustes en 10 categorías, cada uno con explicación, riesgo y fuente. Los ajustes placebo quedan fuera (ver `docs/EXCLUDED.md`).
- Punto de restauración antes de aplicar, y posibilidad de deshacer cada cambio o una sesión entera desde Historial.
- Herramientas:
  - limpieza;
  - reparación (DISM, SFC, red, Windows Update, Store);
  - DNS con medición de velocidad;
  - programas de inicio, servicios, tareas programadas y apps.
- Detección de ajustes revertidos por actualizaciones de Windows, mediciones del equipo e informe HTML.
- Español e inglés, tema claro y oscuro.
- Línea de comandos: `-Help` muestra las opciones.

**Verificado** en Windows 11 Enterprise y Windows Server 2025: todas las pantallas, aplicar y deshacer, punto de restauración y todas las herramientas. No se ha probado en Windows 10.

Windows SmartScreen avisará al abrirlo porque el ejecutable no está firmado: pulsa "Más información" y luego "Ejecutar de todas formas".
