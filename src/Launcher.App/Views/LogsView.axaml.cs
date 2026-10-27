using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using Launcher.App.ViewModels;

namespace Launcher.App.Views;

public partial class LogsView : UserControl
{
    private INotifyCollectionChanged? _observed;

    public LogsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachLiveAutoScroll();
    }

    private void AttachLiveAutoScroll()
    {
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnLiveChanged;
            _observed = null;
        }

        if (DataContext is LogsViewModel viewModel)
        {
            _observed = viewModel.Live.Lines;
            viewModel.Live.Lines.CollectionChanged += OnLiveChanged;
        }
    }

    private void OnLiveChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.UIThread.Post(() => LiveScroll.ScrollToEnd());
}
