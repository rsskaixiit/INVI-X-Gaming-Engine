using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Invix.ViewModels;

namespace Invix;

public partial class MainWindow : Window
{
    private const double LogPanelExtent = 166;

    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();

        DataContext = _viewModel;
        _viewModel.ExitRequested += (_, _) => Close();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        Loaded += (_, _) =>
        {
            _viewModel.Start();
            Activate();
            Focus();
        };

        Closed += (_, _) =>
        {
            _viewModel.Stop();
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.LogText):
                LogBox.ScrollToEnd();
                break;

            case nameof(MainViewModel.IsLogExpanded):
                ResizeForLog();
                break;
        }
    }

    private void ResizeForLog()
    {
        var work = SystemParameters.WorkArea;

        Height = _viewModel.IsLogExpanded
            ? Math.Min(Height + LogPanelExtent, work.Height - 24)
            : Math.Max(Height - LogPanelExtent, MinHeight);
    }

    private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.F6:
                e.Handled = true;
                if (_viewModel.ToggleCommand.CanExecute(null))
                {
                    _viewModel.ToggleCommand.Execute(null);
                }

                break;

            case Key.Escape:
                e.Handled = true;
                Close();
                break;
        }

        base.OnPreviewKeyDown(e);
    }
}