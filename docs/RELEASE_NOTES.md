**Novedades en 1.3**
- Menú nuevo: botón ≡ a la izquierda del título, que se desliza al contraer; iconos que nunca se cortan; Buscar, Ajustes y Ayuda arriba a la derecha, solo con icono. Icono propio del programa y texto un poco más grande, con tamaño 100, 115 o 130 % en Ajustes.
- Inicio muestra tu equipo: procesador, gráficas, RAM (velocidad, módulos y aviso de canal simple), discos, pantalla (aviso si va a menos Hz de los que admite), placa y Windows.
- Página Niveles: qué es cada nivel con un ejemplo, la escalera Rápido, Equilibrado, Juegos y Máximo (Juegos está dentro de Máximo), y una vista completa de cada cambio antes de aplicar, con consejo de reinicio.
- 25 ajustes nuevos con evidencia: teclado y ratón (repetición de teclas, velocidad 1:1, avisos de accesibilidad), efectos visuales (contenido al arrastrar, rectángulo de selección, fundidos, sombras, desplazamiento suave), menú contextual clásico, Explorador en Este equipo, suspensión USB y PCIe, gráfica dedicada por juego, Integridad de memoria y Plataforma de máquina virtual (avanzados).
- Modo juego temporal desde la cabecera: pausa Windows Update un día, usa Alto rendimiento y cierra las apps en segundo plano que marques. Al desactivarlo todo vuelve como estaba.
- Servicios y Tareas dicen qué tocar: guía de unos 50 servicios, servicios cambiados por un nivel en verde, impacto de desactivar cada tarea y búsqueda en internet con tu navegador y tu buscador.
- Programas y apps con el icono de cada programa, ficha de detalle y más aire. Programas de inicio con lanzadores de juegos agrupados.
- 9 reparaciones nuevas: índice de búsqueda, hora, cola de impresión, proxy, audio, planes de energía, contadores de rendimiento, WMI y Defender.
- Mediciones de 10, 30 o 60 s con media y pico, gráfico de tendencia con marcas de "después" y borrado automático a los 7 días (salvo lo exportado a CSV).
- Zona aparte **Bajo tu responsabilidad**: quitar Copilot, Xbox, Edge o la Store, desactivar Defender y pausar Windows Update hasta unos 20 años, con aviso que hay que aceptar, copia completa del registro y cada acción guardable como script `.ps1`. Ver docs/RESPONSABILIDAD.md.
- Ajustes > Datos y rastros: qué guarda la app y por qué, borrar todo al cerrar si no queda nada por deshacer, o borrarlo todo.
- Seguridad: las webs y carpetas se abren sin permisos de administrador, la tarea de medición usa una copia protegida del programa, las DLL solo se cargan de System32 y la carpeta de datos es solo para administradores.

**Novedades en 1.2**
- La ventana se adapta a cualquier pantalla: ya no se esconde la barra de título en portátiles de 768 px, F11 pone pantalla completa y el menú se reduce a iconos en ventanas estrechas.
- Las hojas de cada nivel nunca se cortan: el botón Aplicar queda siempre visible y la lista se desplaza.
- Arreglado: "Volver a analizar" en Personalizar se quedaba en "Leyendo la información del equipo".
- Aviso claro de que el análisis puede tardar varios minutos según el equipo.
- Programas y apps: programas, juegos y apps de la Store; desinstalación por lotes y limpieza de restos (a la Papelera, con copia del registro).
- Red y DNS: dice qué DNS está en uso y cuál aplicaste la última vez; IP y router; restablecer adaptador; cómo activar DNS cifrado en Windows 11.
- Mediciones completas: CPU, memoria comprometida, hilos, handles, apagado, discos y carpeta temporal, con gráfico, CSV y medición tras reiniciar.
- Salud del equipo en Inicio.
- Limpieza: caché de navegadores, "qué ocupa más" y explicación de cada elemento. Reparación: comprobación de disco, restablecer Firewall, cuándo usar cada una y guardar la salida.
- Servicios: filtros "de otros programas" y "cambiados por mí", y volver al original. Tareas: desactivar varias a la vez. Programas de inicio: de dónde arranca cada uno.
- Menú reordenado: Inicio, Personalizar y Mediciones arriba; Limpiar y reparar; Sistema; Seguimiento.

**Novedades en 1.1**
- Inicio con 4 niveles en 1 clic: Rápido, Equilibrado, Juegos y Máximo rendimiento. Cada uno incluye los anteriores. Antes de aplicar ves qué ganas, qué pierdes, qué apps se quitan y si hay que reiniciar. Máximo pide doble confirmación.
- Buscador Ctrl+K, Ayuda con glosario, recorrido de bienvenida y avisos claros de éxito y error.
- Exportar e importar la selección en archivos `.optz`.
- Tema claro u oscuro con 5 colores de acento, y modo ligero automático en equipos modestos.
- Los ajustes que Windows protege se detectan, no se cambian y no se vuelven a ofrecer.
- 30 correcciones de dos auditorías completas (deshacer, DNS, reparación, historial, detección de gráficas).

**Novedades en 1.1.1**
- Descripciones largas de Servicios y Tareas recortadas a 2 líneas, con "Ver más".
- Personalizar: buscador y filtro en una sola fila, y el resto en "Más acciones".
- Ajustes muestra el nombre del color de acento elegido.
- Aviso al terminar de reiniciar el Explorador.
- El tema oscuro es el predeterminado. El claro y el de Windows se eligen en Ajustes.

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
