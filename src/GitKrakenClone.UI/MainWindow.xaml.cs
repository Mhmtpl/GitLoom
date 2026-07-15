using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GitKrakenClone.UI.ViewModels;

namespace GitKrakenClone.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private ScrollViewer? _listScrollViewer;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Recursively find ScrollViewer inside CommitListBox
        _listScrollViewer = FindScrollViewer(CommitListBox);
        if (_listScrollViewer != null)
        {
            _listScrollViewer.ScrollChanged += ListScrollViewer_ScrollChanged;

            // Initialize scroll positions on the graph control
            GraphControl.VerticalScrollOffset = _listScrollViewer.VerticalOffset;
            GraphControl.ViewportHeight = _listScrollViewer.ViewportHeight;
        }

        // Check if there is a command line argument (e.g. repo path passed on startup)
        var args = System.Environment.GetCommandLineArgs();
        if (args.Length > 1)
        {
            var potentialPath = args[1];
            if (System.IO.Directory.Exists(potentialPath))
            {
                _viewModel.LoadRepository(potentialPath);
            }
        }
    }

    private void ListScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // Forward scroll changes to the Skia graph control
        if (e.VerticalChange != 0 || e.ViewportHeightChange != 0)
        {
            GraphControl.VerticalScrollOffset = e.VerticalOffset;
            GraphControl.ViewportHeight = e.ViewportHeight;
        }
    }

    private ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        if (parent is ScrollViewer sv)
        {
            return sv;
        }

        int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childrenCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            var result = FindScrollViewer(child);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}