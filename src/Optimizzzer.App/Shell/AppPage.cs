using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Optimizzzer.App.Shell
{
    public abstract class AppPage
    {
        protected AppPage(AppState state, MainWindow window) { S = state; W = window; }

        protected AppState S { get; }
        protected MainWindow W { get; }

        public abstract string Key { get; }
        public abstract string Title { get; }
        public abstract string Icon { get; }

        private FrameworkElement _view;
        public FrameworkElement View => _view ?? (_view = Build());

        /// <summary>Forces a rebuild next time (language change, data reload).</summary>
        public void Invalidate() => _view = null;

        protected abstract FrameworkElement Build();

        public virtual void OnShown() { }

        /// <summary>Standard page: heading, optional intro line, scrollable body, fixed footer with actions.</summary>
        protected Grid Frame(string intro, UIElement body, UIElement footer = null, bool scroll = true)
        {
            var g = new Grid { Margin = MainWindow.Compact ? new Thickness(20, 16, 20, 16) : new Thickness(32, 28, 32, 24) };
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition());
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var header = Ui.Stack(0, Ui.H1(Title), string.IsNullOrEmpty(intro) ? null : Ui.Muted(intro));
            header.Margin = new Thickness(0, 0, 0, 20);
            header.MaxWidth = 1040;
            header.HorizontalAlignment = HorizontalAlignment.Left;
            g.Children.Add(header);

            var content = body;
            if (scroll) content = Ui.Scroll(body);
            if (content is FrameworkElement fe) { fe.MaxWidth = 1060; fe.HorizontalAlignment = HorizontalAlignment.Stretch; }
            Grid.SetRow(content, 1);
            g.Children.Add(content);

            if (footer != null)
            {
                var bar = new Border { Child = footer, Padding = new Thickness(0, 16, 0, 0), BorderThickness = new Thickness(0, 1, 0, 0) };
                bar.SetResourceReference(Border.BorderBrushProperty, "Divider");
                Grid.SetRow(bar, 2);
                g.Children.Add(bar);
            }
            return g;
        }

        /// <summary>Footer with secondary actions on the left and one primary action on the right.</summary>
        protected static Grid Footer(UIElement left, UIElement right)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (left is FrameworkElement lf) lf.HorizontalAlignment = HorizontalAlignment.Left;
            if (left != null) g.Children.Add(left);
            if (right != null) { Grid.SetColumn(right, 1); g.Children.Add(right); }
            return g;
        }

        /// <summary>Runs work off the UI thread and reports errors in place instead of crashing.</summary>
        protected async Task Run(Func<Task> work, ContentControl errorHost, Action retry)
        {
            try { await work(); }
            catch (Exception ex)
            {
                if (errorHost != null)
                    errorHost.Content = Ui.ErrorState(Loc.S("Algo falló al cargar esta sección.", "Something failed while loading this section."), ex.Message, retry);
                else
                    W.ShowError(ex.Message);
            }
        }
    }
}
