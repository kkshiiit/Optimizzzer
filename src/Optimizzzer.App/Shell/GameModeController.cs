using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Optimizzzer.Core.Tools;

namespace Optimizzzer.App.Shell
{
    /// <summary>
    /// The header's temporary game mode. The state lives in one small file so the mode survives closing the app
    /// (you usually close Optimizzzer and then play) and is undone exactly when turned off. The file is deleted then.
    /// </summary>
    public sealed class GameModeController
    {
        private readonly AppState _s;
        private readonly MainWindow _w;
        private readonly string _file;
        private GameSessionState _active;

        public GameModeController(AppState s, MainWindow w)
        {
            _s = s; _w = w;
            _file = Path.Combine(s.Win.DataDir, "gamemode.txt");
            try { if (File.Exists(_file)) _active = GameSessionState.Parse(File.ReadAllText(_file)); }
            catch (Exception) { }
            w.GameModeRequested += () => _ = OpenAsync();
            w.SetGameModeIndicator(_active != null);
        }

        public bool Active => _active != null;

        private async Task OpenAsync()
        {
            await _s.EnsureReadyAsync();
            if (_active != null) { ShowActive(); return; }
            var running = await Task.Run(() => GameSession.Running(_s.Ctx));
            ShowStart(running);
        }

        private static FrameworkElement Line(string icon, string text) =>
            Ui.Row(10, Ui.Icon(icon, 16, "AccentText"), Ui.Text(text, 14));

        private void ShowStart(IList<BackgroundApp> running)
        {
            var body = Ui.Stack(12,
                Ui.Muted(Loc.S("Mientras está activo:", "While it is on:")),
                Line(Icons.Update, Loc.S("Windows Update no empieza descargas nuevas (pausa de 1 día como máximo).", "Windows Update starts no new downloads (a 1-day pause at most).")),
                Line(Icons.Power, Loc.S("Se usa el plan de energía Alto rendimiento.", "The High performance power plan is used.")),
                Line(Icons.Close, Loc.S("Se cierran las apps en segundo plano que marques abajo.", "The background apps you tick below are closed.")));

            var checks = new List<(CheckBox Box, BackgroundApp App)>();
            if (running.Count == 0)
                body.Children.Add(Ui.Muted(Loc.S("No hay apps conocidas en segundo plano abiertas ahora.", "No known background apps are open right now."), 13));
            else
            {
                body.Children.Add(Ui.Text(Loc.S("Cerrar ahora", "Close now"), 14, true));
                foreach (var a in running)
                {
                    var cb = new CheckBox { Content = a.Name, IsChecked = true };
                    checks.Add((cb, a));
                    body.Children.Add(cb);
                }
            }
            body.Children.Add(Ui.Notice(Icons.Info, "FgMuted", Loc.S(
                "Al desactivarlo vuelven tu plan de energía y Windows Update como estaban. Las apps cerradas no se reabren solas: vuelven al abrirlas o al reiniciar. Sigue activo aunque cierres Optimizzzer.",
                "Turning it off brings your power plan and Windows Update back as they were. Closed apps do not reopen by themselves: they come back when you open them or restart. It stays on even if you close Optimizzzer.")));

            var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = Ui.Secondary(Loc.S("Cancelar", "Cancel"), _w.CloseOverlay);
            var start = Ui.Primary(Loc.S("Activar", "Turn on"), null, Icons.Gaming);
            start.Margin = new Thickness(8, 0, 0, 0);
            start.Click += async (_, __) =>
            {
                start.IsEnabled = false;
                var close = checks.Where(c => c.Box.IsChecked == true).Select(c => c.App.Exe).ToList();
                try
                {
                    var st = await Task.Run(() => GameSession.Start(_s.Ctx, close, DateTime.UtcNow));
                    File.WriteAllText(_file, st.Serialize());
                    _active = st;
                    _w.CloseOverlay();
                    _w.SetGameModeIndicator(true);
                    var parts = new List<string>();
                    if (st.UpdatesPaused) parts.Add(Loc.S("actualizaciones en pausa", "updates paused"));
                    if (st.SchemeChanged || st.PreviousScheme == GameSession.HighPerformance) parts.Add(Loc.S("Alto rendimiento", "High performance"));
                    if (st.Closed.Count > 0) parts.Add(st.Closed.Count + Loc.S(" apps cerradas", " apps closed"));
                    _w.Toast(ToastKind.Success, Loc.S("Modo juego activo", "Game mode on") + (parts.Count > 0 ? ": " + string.Join(", ", parts) + "." : "."));
                }
                catch (Exception ex)
                {
                    start.IsEnabled = true;
                    _w.Toast(ToastKind.Error, Loc.S("No se pudo activar: ", "Could not turn it on: ") + ex.Message);
                }
            };
            buttons.Children.Add(cancel); buttons.Children.Add(start);
            var head = Ui.Stack(4, Ui.H2(Loc.S("Modo juego temporal", "Temporary game mode")),
                Ui.Muted(Loc.S("Menos cosas en segundo plano mientras juegas. Se desactiva con este mismo botón.", "Less in the background while you play. Turn it off with this same button.")));
            _w.ShowOverlay(MainWindow.Sheet(head, body, buttons), _w.CloseOverlay, 560);
        }

        private void ShowActive()
        {
            var since = _active.StartedUtc.ToLocalTime().ToString("t");
            var body = Ui.Stack(10,
                Ui.Text(Loc.S("Activo desde las ", "On since ") + since + ".", 14),
                _active.Closed.Count > 0 ? Ui.Muted(Loc.S("Se cerraron: ", "Closed: ") + string.Join(", ", _active.Closed), 13) : null);
            var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            var keep = Ui.Secondary(Loc.S("Seguir jugando", "Keep playing"), _w.CloseOverlay);
            var stop = Ui.Primary(Loc.S("Desactivar", "Turn off"), null, Icons.Close);
            stop.Margin = new Thickness(8, 0, 0, 0);
            stop.Click += async (_, __) =>
            {
                stop.IsEnabled = false;
                try { await StopAsync(); _w.CloseOverlay(); _w.Toast(ToastKind.Success, Loc.S("Modo juego desactivado: todo volvió a como estaba.", "Game mode off: everything is back as it was.")); }
                catch (Exception ex) { stop.IsEnabled = true; _w.Toast(ToastKind.Error, Loc.S("No se pudo desactivar: ", "Could not turn it off: ") + ex.Message); }
            };
            buttons.Children.Add(keep); buttons.Children.Add(stop);
            _w.ShowOverlay(MainWindow.Sheet(Ui.H2(Loc.S("Modo juego activo", "Game mode is on")), body, buttons), _w.CloseOverlay, 480);
        }

        public async Task StopAsync()
        {
            if (_active == null) return;
            await _s.EnsureReadyAsync();
            var st = _active;
            await Task.Run(() => GameSession.Stop(_s.Ctx, st));
            _active = null;
            try { File.Delete(_file); } catch (Exception) { }
            _w.SetGameModeIndicator(false);
        }

        /// <summary>Self-test hook: on and off without the sheet.</summary>
        public async Task<GameSessionState> StartForTestAsync()
        {
            await _s.EnsureReadyAsync();
            _active = await Task.Run(() => GameSession.Start(_s.Ctx, new string[0], DateTime.UtcNow));
            File.WriteAllText(_file, _active.Serialize());
            _w.SetGameModeIndicator(true);
            return _active;
        }
    }
}
