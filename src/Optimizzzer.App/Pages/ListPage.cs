using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Optimizzzer.App.Shell;

namespace Optimizzzer.App.Pages
{
    /// <summary>
    /// Base for the manager screens (startup, services, tasks, apps): load in the background,
    /// show loading, error and empty states, filter with a search box, render rows lazily.
    /// </summary>
    public abstract class ListPage<T> : AppPage
    {
        protected IList<T> Items;
        private string _error;
        private bool _loading;
        protected string Query = "";
        private StackPanel _list;
        private FrameworkElement _empty;

        protected ListPage(AppState s, MainWindow w) : base(s, w) { }

        protected abstract IList<T> Load();
        protected abstract string Intro { get; }
        protected abstract string SearchHint { get; }
        protected abstract bool Matches(T item, string query);
        protected abstract FrameworkElement RowFor(T item);
        protected virtual FrameworkElement Toolbar() => null;
        /// <summary>Optional row above the search box (for example tabs), with its own spacing.</summary>
        protected virtual FrameworkElement TopBar() => null;
        protected virtual string EmptyText => Loc.S("No hay elementos.", "There are no items.");
        protected virtual IEnumerable<T> Filtered(IEnumerable<T> items) => items;
        /// <summary>Optional main action in the footer (for example "Uninstall 3 programs").</summary>
        protected virtual Button PrimaryAction() => null;

        public override void OnShown()
        {
            if (Items == null && !_loading && _error == null) _ = ReloadAsync();
        }

        protected async Task ReloadAsync()
        {
            _loading = true;
            _error = null;
            W.RefreshCurrentIf(this);
            try { Items = await Task.Run(() => Load()); }
            catch (Exception ex) { _error = ex.Message; }
            _loading = false;
            W.RefreshCurrentIf(this);
        }

        protected override FrameworkElement Build()
        {
            if (_error != null)
                return Frame(Intro, Ui.ErrorState(Loc.S("No se pudo cargar la lista.", "The list could not be loaded."), _error, () => _ = ReloadAsync()));
            if (Items == null)
            {
                var (v, _) = Ui.Loading(Loc.S("Cargando…", "Loading…"));
                return Frame(Intro, v);
            }

            var search = Ui.SearchBox(SearchHint, q => { Query = q; Refill(); });
            search.Box.Text = Query;
            var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
            bar.Children.Add(search);
            var extra = Toolbar();
            if (extra != null) { extra.Margin = new Thickness(16, 0, 0, 0); extra.VerticalAlignment = VerticalAlignment.Center; bar.Children.Add(extra); }

            _list = new StackPanel();
            _empty = Ui.Empty(EmptyText);
            Refill();

            var g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition());
            var top = TopBar();
            g.Children.Add(top == null ? (UIElement)bar : Ui.Stack(12, top, bar));
            var scroll = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, CanContentScroll = false, Focusable = false };
            Grid.SetRow(scroll, 1);
            g.Children.Add(scroll);
            return Frame(Intro, g, Footer(Ui.Secondary(Loc.S("Recargar", "Reload"), () => _ = ReloadAsync(), Icons.Restart), PrimaryAction()), scroll: false);
        }

        /// <summary>Rebuilds the visible rows. Long lists are capped and the rest reached by searching.</summary>
        protected void Refill()
        {
            if (_list == null || Items == null) return;
            _list.Children.Clear();
            var q = Query.Trim();
            var matches = Filtered(Items).Where(i => q.Length == 0 || Matches(i, q)).ToList();
            const int cap = 300;
            foreach (var item in matches.Take(cap)) _list.Children.Add(RowFor(item));
            if (matches.Count == 0) _list.Children.Add(_empty);
            if (matches.Count > cap)
                _list.Children.Add(Ui.Muted(Loc.S("Mostrando ", "Showing ") + cap + Loc.S(" de ", " of ") + matches.Count + Loc.S(". Escribe en el buscador para ver el resto.", ". Type in the search box to find the rest."), 12));
        }

        protected static bool Has(string s, string q) => !string.IsNullOrEmpty(s) && s.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0;
    }
}
