using System.Windows;
using IPManage.ViewModels;

namespace IPManage.Views;

public partial class RecordEditDialog : Window
{
    private readonly MainWindowViewModel _vm;

    public RecordEditDialog(MainWindowViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        DeleteBtn.Visibility = vm.IsEditingExisting ? Visibility.Visible : Visibility.Collapsed;
        SyncPasswordControls();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (_vm.SaveCommand.CanExecute(null))
        {
            _vm.SaveCommand.Execute(null);
            if (_vm.LastSaveSucceeded)
                DialogResult = true;
        }
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (_vm.DeleteCommand.CanExecute(null))
        {
            _vm.DeleteCommand.Execute(null);
            if (_vm.LastDeleteSucceeded)
                DialogResult = true;
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnTogglePasswordVisibilityClick(object sender, RoutedEventArgs e)
    {
        if (!_vm.IsEditorPasswordVisible)
        {
            _vm.Password = PasswordHiddenInput.Password;
        }

        if (_vm.ToggleEditorPasswordVisibilityCommand.CanExecute(null))
        {
            _vm.ToggleEditorPasswordVisibilityCommand.Execute(null);
        }

        SyncPasswordControls();
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_vm.IsEditorPasswordVisible)
        {
            _vm.Password = PasswordHiddenInput.Password;
        }
    }

    private void SyncPasswordControls()
    {
        PasswordHiddenInput.Password = _vm.Password ?? string.Empty;
    }
}
