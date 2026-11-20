# Bajo tu responsabilidad / At your own risk

## Español

Optimizzzer tiene una zona aparte, **Bajo tu responsabilidad**, al final del menú. Desde ahí se puede:

- quitar Copilot;
- quitar Xbox (app, Game Bar y servicios);
- quitar Microsoft Edge (con su desinstalador oficial; WebView2 no se toca);
- quitar la Microsoft Store;
- desactivar la protección en tiempo real de Microsoft Defender;
- pausar Windows Update hasta unos 20 años.

Nada de esto es una optimización con pruebas de que mejore el rendimiento. Son decisiones personales, y por eso no forman parte de ningún nivel ni de Personalizar.

**Riesgos.** Estas acciones pueden:

- dejar el equipo sin antivirus o sin parches de seguridad, y exponerlo a malware;
- romper juegos, Game Pass, apps de la Store y funciones que Windows abre con Edge;
- volverse atrás solas: una actualización de Windows puede reinstalar Edge o recuperar otras piezas;
- no poder deshacerse desde la app: hay que reinstalar desde la Store o la web.

**Antes de usarla:**

1. Crea un punto de restauración y una copia del registro (la propia página tiene un botón para la copia).
2. Lee lo que hace cada acción y lo que pierdes.
3. Si prefieres no depender de la app, guarda la acción como script `.ps1` y revísalo. Los mismos scripts están en [`scripts/`](../scripts).

**Exención de responsabilidad.** Optimizzzer se ofrece "tal cual", sin garantía de ningún tipo (ver [LICENSE](../LICENSE)). Sus autores y colaboradores no se hacen cargo de ningún daño, pérdida de datos, fallo de seguridad o problema de cualquier tipo causado por estas herramientas o sus scripts. Cada persona decide y responde por lo que ejecuta en su equipo. La app pide aceptar este aviso una vez antes de mostrar la zona.

**Pausar Windows Update hasta 2222** no es posible. La app escribe las mismas fechas de pausa que Configuración y sube el límite (`FlightSettingsMaxPauseDays`) al máximo que se sabe que Windows acepta, unos 20 años. Después lee la fecha que de verdad quedó y la muestra. Windows puede ignorarla en algunas ediciones o tras una actualización.

**Microsoft Defender** no se puede quitar de forma segura. Desde 2020, Windows ignora sus directivas si la Protección contra alteraciones está activa. Por eso la app primero te lleva a Seguridad de Windows para apagarla tú, y luego aplica las directivas de tiempo real. "Volver atrás" las borra y reactiva el tiempo real.

## English

Optimizzzer has a separate zone, **At your own risk**, at the end of the menu. From there you can:

- remove Copilot;
- remove Xbox;
- remove Microsoft Edge (with its official uninstaller; WebView2 is kept);
- remove the Microsoft Store;
- turn off Microsoft Defender real-time protection;
- pause Windows Update for about 20 years.

None of this is an optimization with evidence that it improves performance. These are personal choices, which is why none of it is part of a level or Customize.

**Risks.** These actions can:

- leave the PC without antivirus or security patches;
- break games, Game Pass, Store apps and features Windows opens with Edge;
- be reversed by a Windows update;
- not be undoable from the app: you have to reinstall.

**Before using it:**

1. Create a restore point and a registry backup.
2. Read what each action does and what you lose.
3. If you would rather not depend on the app, save the action as a `.ps1` script and review it. The same scripts are in [`scripts/`](../scripts).

**Disclaimer.** Optimizzzer is provided "as is", without warranty of any kind (see [LICENSE](../LICENSE)). Its authors and contributors accept no liability for any damage, data loss, security failure or any other problem caused by these tools or their scripts. Each person decides and is responsible for what they run on their PC. The app asks you to accept this notice once before showing the zone.
