using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IPManage.Models;
using IPManage.ViewModels;
using IPManage.Views;

namespace IPManage;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainWindowViewModel();
        DataContext = _vm;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ShowInTaskbar = true;
        Visibility = Visibility.Visible;
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    public void OpenNewRecordDialog()
    {
        _vm.PrepareNewRecord();
        _vm.LastSaveSucceeded = false;
        _vm.LastDeleteSucceeded = false;
        var dlg = new RecordEditDialog(_vm) { Owner = this };
        dlg.ShowDialog();
    }

    public void OpenEditRecordDialog(IpRecord record)
    {
        _vm.PrepareEditRecord(record);
        _vm.LastSaveSucceeded = false;
        _vm.LastDeleteSucceeded = false;
        var dlg = new RecordEditDialog(_vm) { Owner = this };
        dlg.ShowDialog();
    }

    private void OnDataGridRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm.SelectedRecord is { } record)
            OpenEditRecordDialog(record);
    }

    private void OnNewRecordClick(object sender, RoutedEventArgs e)
    {
        _vm.NavigateRecordsCommand.Execute(null);
        OpenNewRecordDialog();
    }

    private void OnDataGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is DataGrid grid)
            _vm.UpdateSelection(grid.SelectedItems.OfType<IpRecord>());
    }

    private void OnEditSelectedClick(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedRecord is { } record)
            OpenEditRecordDialog(record);
    }

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var dlg = new Views.AboutDialog { Owner = this };
        dlg.ShowDialog();
    }
}
