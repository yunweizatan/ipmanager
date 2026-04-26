using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using IPManage.Models;
using IPManage.Services;
using IPManage.Utilities;

namespace IPManage.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IpManagementService _service = new();
    private readonly List<IpRecord> _allRecords = [];

    private const string DashboardSection = "概览";
    private const string RecordsSection = "记录";
    private const string ImportSection = "导入导出";
    private const string BackupSection = "备份恢复";
    private const string SettingsSection = "系统设置";

    private string _currentSection = DashboardSection;
    private string _searchText = string.Empty;
    private string _selectedVlanFilter = "全部";
    private string _selectedStatusFilter = "全部";
    private IpRecord? _selectedRecord;
    private string _editorId = string.Empty;
    private string _ipAddress = string.Empty;
    private string _deviceName = string.Empty;
    private string _assetNumber = string.Empty;
    private string _owner = string.Empty;
    private string _password = string.Empty;
    private string _vlan = string.Empty;
    private string _subnet = string.Empty;
    private string _deviceType = string.Empty;
    private string _selectedStatus = "使用中";
    private string _applyTimeText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    private string _remark = string.Empty;
    private string _statusMessage = "就绪";
    private string _editorTitle = "新建资产";
    private string _lastImportSummary = "尚未导入";
    private string _lastBackupSummary = "尚未备份";
    private string _currentSectionTitle = "总览";
    private string _currentSectionDescription = "查看整体资产状态、快速入口和最近数据概览。";
    private bool _isBusy;
    private bool _showPasswords;
    private bool _isEditorPasswordVisible;
    private bool _lastSaveSucceeded;
    private bool _lastDeleteSucceeded;
    private int _selectedCount;
    private List<string> _selectedIds = [];

    public MainWindowViewModel()
    {
        FilteredRecords = new ObservableCollection<IpRecord>();
        RecentRecords = new ObservableCollection<IpRecord>();
        VlanOptions = new ObservableCollection<string> { "全部" };
        StatusOptions = new ObservableCollection<string>(["全部", "使用中", "空闲", "预留", "停用"]);
        EditorStatusOptions = new ObservableCollection<string>(["使用中", "空闲", "预留", "停用"]);

        NavigateDashboardCommand = new RelayCommand(() => ChangeSection(DashboardSection));
        NavigateRecordsCommand = new RelayCommand(() => ChangeSection(RecordsSection));
        NavigateImportCommand = new RelayCommand(() => ChangeSection(ImportSection));
        NavigateBackupCommand = new RelayCommand(() => ChangeSection(BackupSection));
        NavigateSettingsCommand = new RelayCommand(() => ChangeSection(SettingsSection));

        NewCommand = new RelayCommand(BeginCreateNew);
        OpenEditorCommand = new RelayCommand(OpenEditor, () => SelectedRecord is not null && !IsBusy);
        BatchDeleteCommand = new RelayCommand(BatchDeleteSelected, () => _selectedCount > 0 && !IsBusy);
        SaveCommand = new RelayCommand(SaveCurrentRecord, () => !IsBusy);
        DeleteCommand = new RelayCommand(DeleteSelectedRecord, () => SelectedRecord is not null && !IsBusy);
        RefreshCommand = new RelayCommand(ReloadFromDisk, () => !IsBusy);
        ClearFiltersCommand = new RelayCommand(ClearFilters, () => !IsBusy);
        BackupCommand = new RelayCommand(CreateBackup, () => !IsBusy);
        ExportCommand = new RelayCommand(ExportCurrentView, () => FilteredRecords.Count > 0 && !IsBusy);
        ImportCsvCommand = new RelayCommand(ImportCsvFile, () => !IsBusy);
        TogglePasswordVisibilityCommand = new RelayCommand(TogglePasswordVisibility);
        ToggleEditorPasswordVisibilityCommand = new RelayCommand(ToggleEditorPasswordVisibility);
        OpenDataFolderCommand = new RelayCommand(() => OpenFolder(DataLocation), () => !IsBusy);
        OpenExportsFolderCommand = new RelayCommand(() => OpenFolder(ExportsLocation), () => !IsBusy);
        OpenBackupsFolderCommand = new RelayCommand(() => OpenFolder(BackupsLocation), () => !IsBusy);

        ReloadFromDisk();
        ChangeSection(DashboardSection);
    }

    public ObservableCollection<IpRecord> FilteredRecords { get; }

    public ObservableCollection<IpRecord> RecentRecords { get; }

    public ObservableCollection<string> VlanOptions { get; }

    public ObservableCollection<string> StatusOptions { get; }

    public ObservableCollection<string> EditorStatusOptions { get; }

    public RelayCommand NavigateDashboardCommand { get; }

    public RelayCommand NavigateRecordsCommand { get; }

    public RelayCommand NavigateImportCommand { get; }

    public RelayCommand NavigateBackupCommand { get; }

    public RelayCommand NavigateSettingsCommand { get; }

    public RelayCommand NewCommand { get; }

    public RelayCommand OpenEditorCommand { get; }

    public RelayCommand BatchDeleteCommand { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand DeleteCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand ClearFiltersCommand { get; }

    public RelayCommand BackupCommand { get; }

    public RelayCommand ExportCommand { get; }

    public RelayCommand ImportCsvCommand { get; }

    public RelayCommand TogglePasswordVisibilityCommand { get; }

    public RelayCommand ToggleEditorPasswordVisibilityCommand { get; }

    public RelayCommand OpenDataFolderCommand { get; }

    public RelayCommand OpenExportsFolderCommand { get; }

    public RelayCommand OpenBackupsFolderCommand { get; }

    public string CurrentSectionTitle
    {
        get => _currentSectionTitle;
        private set => SetProperty(ref _currentSectionTitle, value);
    }

    public string CurrentSectionDescription
    {
        get => _currentSectionDescription;
        private set => SetProperty(ref _currentSectionDescription, value);
    }

    public bool IsEditingExisting => !string.IsNullOrWhiteSpace(_editorId);

    public int SelectedCount
    {
        get => _selectedCount;
        set
        {
            if (SetProperty(ref _selectedCount, value))
            {
                BatchDeleteCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(SelectionSummary));
            }
        }
    }

    public string SelectionSummary => _selectedCount > 0 ? $"已选 {_selectedCount} 条" : string.Empty;

    public bool ShowPasswords
    {
        get => _showPasswords;
        set
        {
            if (SetProperty(ref _showPasswords, value))
            {
                OnPropertyChanged(nameof(PasswordToggleText));
            }
        }
    }

    public bool IsEditorPasswordVisible
    {
        get => _isEditorPasswordVisible;
        set
        {
            if (SetProperty(ref _isEditorPasswordVisible, value))
            {
                OnPropertyChanged(nameof(EditorPasswordToggleText));
            }
        }
    }

    public string PasswordToggleText => ShowPasswords ? "隐藏密码" : "显示密码";

    public string EditorPasswordToggleText => IsEditorPasswordVisible ? "隐藏" : "显示";

    public bool LastSaveSucceeded
    {
        get => _lastSaveSucceeded;
        set => SetProperty(ref _lastSaveSucceeded, value);
    }

    public bool LastDeleteSucceeded
    {
        get => _lastDeleteSucceeded;
        set => SetProperty(ref _lastDeleteSucceeded, value);
    }

    public bool IsDashboardSelected => _currentSection == DashboardSection;

    public bool IsRecordsSelected => _currentSection == RecordsSection;

    public bool IsImportSelected => _currentSection == ImportSection;

    public bool IsBackupSelected => _currentSection == BackupSection;

    public bool IsSettingsSelected => _currentSection == SettingsSection;

    public Visibility DashboardVisibility => IsDashboardSelected ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RecordsVisibility => IsRecordsSelected ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ImportVisibility => IsImportSelected ? Visibility.Visible : Visibility.Collapsed;

    public Visibility BackupVisibility => IsBackupSelected ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SettingsVisibility => IsSettingsSelected ? Visibility.Visible : Visibility.Collapsed;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedVlanFilter
    {
        get => _selectedVlanFilter;
        set
        {
            if (SetProperty(ref _selectedVlanFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    public IpRecord? SelectedRecord
    {
        get => _selectedRecord;
        set
        {
            if (SetProperty(ref _selectedRecord, value))
            {
                LoadEditor(value);
                DeleteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string EditorTitle
    {
        get => _editorTitle;
        private set => SetProperty(ref _editorTitle, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string LastImportSummary
    {
        get => _lastImportSummary;
        private set => SetProperty(ref _lastImportSummary, value);
    }

    public string LastBackupSummary
    {
        get => _lastBackupSummary;
        private set => SetProperty(ref _lastBackupSummary, value);
    }

    public string DataLocation => _service.DataRoot;

    public string ExportsLocation => Path.Combine(DataLocation, "exports");

    public string BackupsLocation => Path.Combine(DataLocation, "backup");

    public string RuntimeSummary => ".NET 8 / WPF / 中文界面";

    public string SyncSummary => $"数据目录: {DataLocation}";

    public string ImportTemplateHint => "支持 UTF-8/GBK/Excel 保存后的 CSV。列顺序: IP地址,设备名称,资产编号,使用人,密码,VLAN,申请时间,设备类型,状态,备注,网段。";

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                SaveCommand.RaiseCanExecuteChanged();
                DeleteCommand.RaiseCanExecuteChanged();
                BatchDeleteCommand.RaiseCanExecuteChanged();
                RefreshCommand.RaiseCanExecuteChanged();
                ClearFiltersCommand.RaiseCanExecuteChanged();
                BackupCommand.RaiseCanExecuteChanged();
                ExportCommand.RaiseCanExecuteChanged();
                ImportCsvCommand.RaiseCanExecuteChanged();
                OpenDataFolderCommand.RaiseCanExecuteChanged();
                OpenExportsFolderCommand.RaiseCanExecuteChanged();
                OpenBackupsFolderCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int TotalRecords => _allRecords.Count;

    public int InUseCount => _allRecords.Count(record => record.Status == RecordStatus.InUse);

    public int IdleCount => _allRecords.Count(record => record.Status == RecordStatus.Idle);

    public int ReservedCount => _allRecords.Count(record => record.Status == RecordStatus.Reserved);

    public int DisabledCount => _allRecords.Count(record => record.Status == RecordStatus.Disabled);

    public int VlanCount => _allRecords.Select(record => record.Vlan)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    public string LastUpdatedSummary => _allRecords.Count == 0
        ? "暂无数据"
        : $"最后申请 {_allRecords.Max(record => record.ApplyTime):yyyy-MM-dd}";

    public string IpAddress
    {
        get => _ipAddress;
        set => SetProperty(ref _ipAddress, value);
    }

    public string DeviceName
    {
        get => _deviceName;
        set => SetProperty(ref _deviceName, value);
    }

    public string AssetNumber
    {
        get => _assetNumber;
        set => SetProperty(ref _assetNumber, value);
    }

    public string Owner
    {
        get => _owner;
        set => SetProperty(ref _owner, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string Vlan
    {
        get => _vlan;
        set => SetProperty(ref _vlan, value);
    }

    public string Subnet
    {
        get => _subnet;
        set => SetProperty(ref _subnet, value);
    }

    public string DeviceType
    {
        get => _deviceType;
        set => SetProperty(ref _deviceType, value);
    }

    public string SelectedStatus
    {
        get => _selectedStatus;
        set => SetProperty(ref _selectedStatus, value);
    }

    public string ApplyTimeText
    {
        get => _applyTimeText;
        set => SetProperty(ref _applyTimeText, value);
    }

    public string Remark
    {
        get => _remark;
        set => SetProperty(ref _remark, value);
    }

    private void ChangeSection(string section)
    {
        _currentSection = section;
        switch (section)
        {
            case DashboardSection:
                CurrentSectionTitle = "概览";
                CurrentSectionDescription = "查看整体资产状态、最近记录和常用操作入口。";
                break;
            case RecordsSection:
                CurrentSectionTitle = "记录管理";
                CurrentSectionDescription = "筛选、查看和编辑 IP 地址资产记录。";
                break;
            case ImportSection:
                CurrentSectionTitle = "导入导出";
                CurrentSectionDescription = "支持 CSV 导入和当前记录导出。";
                break;
            case BackupSection:
                CurrentSectionTitle = "备份恢复";
                CurrentSectionDescription = "手动备份当前数据，打开备份目录。";
                break;
            case SettingsSection:
                CurrentSectionTitle = "系统设置";
                CurrentSectionDescription = "查看数据目录、输出目录和当前应用说明。";
                break;
        }

        OnPropertyChanged(nameof(IsDashboardSelected));
        OnPropertyChanged(nameof(IsRecordsSelected));
        OnPropertyChanged(nameof(IsImportSelected));
        OnPropertyChanged(nameof(IsBackupSelected));
        OnPropertyChanged(nameof(IsSettingsSelected));
        OnPropertyChanged(nameof(DashboardVisibility));
        OnPropertyChanged(nameof(RecordsVisibility));
        OnPropertyChanged(nameof(ImportVisibility));
        OnPropertyChanged(nameof(BackupVisibility));
        OnPropertyChanged(nameof(SettingsVisibility));
    }

    private void ReloadFromDisk()
    {
        try
        {
            IsBusy = true;
            _allRecords.Clear();
            _allRecords.AddRange(_service.LoadRecords().Select(record => record.Clone()));
            RefreshFilterOptions();
            ApplyFilters();
            RefreshRecentRecords();
            ResetEditor();
            StatusMessage = $"已加载 {_allRecords.Count} 条记录。";
            OnPropertyChanged(nameof(TotalRecords));
            OnPropertyChanged(nameof(InUseCount));
            OnPropertyChanged(nameof(IdleCount));
            OnPropertyChanged(nameof(ReservedCount));
            OnPropertyChanged(nameof(DisabledCount));
            OnPropertyChanged(nameof(VlanCount));
            OnPropertyChanged(nameof(LastUpdatedSummary));
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败: {ex.Message}";
            MessageBox.Show(StatusMessage, "加载失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshRecentRecords()
    {
        RecentRecords.Clear();
        foreach (var record in _allRecords
                     .OrderByDescending(item => item.ApplyTime)
                     .Take(6))
        {
            RecentRecords.Add(record);
        }
    }

    private void ApplyFilters()
    {
        var search = SearchText.Trim();
        var filtered = _allRecords
            .Where(record => SelectedVlanFilter == "全部" || string.Equals(record.Vlan, SelectedVlanFilter, StringComparison.OrdinalIgnoreCase))
            .Where(record => SelectedStatusFilter == "全部" || string.Equals(record.StatusText, SelectedStatusFilter, StringComparison.OrdinalIgnoreCase))
            .Where(record =>
                string.IsNullOrWhiteSpace(search) ||
                record.IpAddress.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                record.DeviceName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                record.Owner.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                record.AssetNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                record.Vlan.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                record.Subnet.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                record.Remark.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                record.Password.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(record => record.IpAddress, StringComparer.OrdinalIgnoreCase)
            .ToList();

        FilteredRecords.Clear();
        foreach (var record in filtered)
        {
            FilteredRecords.Add(record);
        }

        ExportCommand.RaiseCanExecuteChanged();
    }

    private void RefreshFilterOptions()
    {
        var currentSelection = SelectedVlanFilter;
        VlanOptions.Clear();
        VlanOptions.Add("全部");

        foreach (var vlan in _allRecords
                     .Select(record => record.Vlan)
                     .Where(vlan => !string.IsNullOrWhiteSpace(vlan))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(vlan => vlan, StringComparer.CurrentCultureIgnoreCase))
        {
            VlanOptions.Add(vlan);
        }

        SelectedVlanFilter = VlanOptions.Contains(currentSelection) ? currentSelection : "全部";
    }

    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedVlanFilter = "全部";
        SelectedStatusFilter = "全部";
        StatusMessage = "筛选条件已清空。";
    }

    private void BeginCreateNew()
    {
        SelectedRecord = null;
        ResetEditor();
        ChangeSection(RecordsSection);
        StatusMessage = "已切换到新建模式。";
    }

    private void OpenEditor()
    {
        if (SelectedRecord is not null)
            LoadEditor(SelectedRecord);
    }

    public void PrepareNewRecord()
    {
        SelectedRecord = null;
        ResetEditor();
    }

    public void PrepareEditRecord(IpRecord record)
    {
        SelectedRecord = record;
        LoadEditor(record);
    }

    private void LoadEditor(IpRecord? record)
    {
        if (record is null)
        {
            return;
        }

        _editorId = record.Id;
        IpAddress = record.IpAddress;
        DeviceName = record.DeviceName;
        AssetNumber = record.AssetNumber;
        Owner = record.Owner;
        Password = record.Password;
        IsEditorPasswordVisible = false;
        Vlan = record.Vlan;
        Subnet = record.Subnet;
        DeviceType = record.DeviceType;
        SelectedStatus = record.StatusText;
        ApplyTimeText = record.ApplyTimeDisplay;
        Remark = record.Remark;
        EditorTitle = "编辑资产";
        OnPropertyChanged(nameof(IsEditingExisting));
    }

    private void ResetEditor()
    {
        _editorId = string.Empty;
        IpAddress = string.Empty;
        DeviceName = string.Empty;
        AssetNumber = string.Empty;
        Owner = string.Empty;
        Password = string.Empty;
        IsEditorPasswordVisible = false;
        Vlan = VlanOptions.Skip(1).FirstOrDefault() ?? "vlan1";
        Subnet = string.Empty;
        DeviceType = "PC";
        SelectedStatus = "使用中";
        ApplyTimeText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        Remark = string.Empty;
        EditorTitle = "新建资产";
        OnPropertyChanged(nameof(IsEditingExisting));
    }

    private void SaveCurrentRecord()
    {
        try
        {
            IsBusy = true;

            if (!DateTime.TryParse(ApplyTimeText, out var applyTime))
            {
                MessageBox.Show("申请时间格式不正确，请使用 yyyy-MM-dd HH:mm:ss。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                StatusMessage = "申请时间格式不正确。";
                return;
            }

            var status = RecordStatusExtensions.FromChineseText(SelectedStatus);
            var existing = _allRecords.FirstOrDefault(record => string.Equals(record.Id, _editorId, StringComparison.OrdinalIgnoreCase));

            var candidate = new IpRecord
            {
                Id = string.IsNullOrWhiteSpace(_editorId) ? Guid.NewGuid().ToString("N") : _editorId,
                IpAddress = IpAddress.Trim(),
                DeviceName = DeviceName.Trim(),
                AssetNumber = AssetNumber.Trim(),
                Owner = Owner.Trim(),
                Password = Password.Trim(),
                Vlan = Vlan.Trim(),
                Subnet = Subnet.Trim(),
                DeviceType = DeviceType.Trim(),
                Status = status,
                ApplyTime = applyTime,
                Remark = Remark.Trim(),
                CreatedAt = existing?.CreatedAt ?? DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            var errors = _service.Validate(candidate, _allRecords);
            if (errors.Count > 0)
            {
                var message = string.Join(Environment.NewLine, errors);
                MessageBox.Show(message, "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                StatusMessage = errors[0];
                return;
            }

            if (existing is null)
            {
                _allRecords.Add(candidate);
            }
            else
            {
                var index = _allRecords.IndexOf(existing);
                _allRecords[index] = candidate;
            }

            PersistAll(candidate.Id);
            StatusMessage = existing is null ? "记录已新增并保存。" : "记录已更新并保存。";
            LastSaveSucceeded = true;
        }
        catch (Exception ex)
        {
            LastSaveSucceeded = false;
            StatusMessage = $"保存失败: {ex.Message}";
            MessageBox.Show(StatusMessage, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void DeleteSelectedRecord()
    {
        if (SelectedRecord is null)
        {
            return;
        }

        var displayName = string.IsNullOrWhiteSpace(SelectedRecord.DeviceName)
            ? SelectedRecord.IpAddress
            : $"{SelectedRecord.IpAddress} / {SelectedRecord.DeviceName}";

        var result = MessageBox.Show(
            $"确认删除 {displayName} 吗？",
            "删除确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            IsBusy = true;
            _allRecords.RemoveAll(record => string.Equals(record.Id, SelectedRecord.Id, StringComparison.OrdinalIgnoreCase));
            PersistAll(null);
            ResetEditor();
            SelectedRecord = null;
            StatusMessage = "记录已删除。";
            LastDeleteSucceeded = true;
        }
        catch (Exception ex)
        {
            LastDeleteSucceeded = false;
            StatusMessage = $"删除失败: {ex.Message}";
            MessageBox.Show(StatusMessage, "删除失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void UpdateSelection(IEnumerable<IpRecord> selectedItems)
    {
        _selectedIds = selectedItems.Select(r => r.Id).ToList();
        SelectedCount = _selectedIds.Count;
    }

    private void BatchDeleteSelected()
    {
        if (_selectedIds.Count == 0) return;

        var result = MessageBox.Show(
            $"确认删除选中的 {_selectedIds.Count} 条记录吗？此操作不可撤销。",
            "批量删除确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            var idsToDelete = new HashSet<string>(_selectedIds, StringComparer.OrdinalIgnoreCase);
            _allRecords.RemoveAll(r => idsToDelete.Contains(r.Id));
            _selectedIds.Clear();
            SelectedCount = 0;
            SelectedRecord = null;
            PersistAll(null);
            StatusMessage = $"已删除 {idsToDelete.Count} 条记录。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"批量删除失败: {ex.Message}";
            MessageBox.Show(StatusMessage, "删除失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PersistAll(string? selectedRecordId)
    {
        _service.SaveAll(_allRecords);
        RefreshFilterOptions();
        ApplyFilters();
        RefreshRecentRecords();
        OnPropertyChanged(nameof(TotalRecords));
        OnPropertyChanged(nameof(InUseCount));
        OnPropertyChanged(nameof(IdleCount));
        OnPropertyChanged(nameof(ReservedCount));
        OnPropertyChanged(nameof(DisabledCount));
        OnPropertyChanged(nameof(VlanCount));
        OnPropertyChanged(nameof(LastUpdatedSummary));

        SelectedRecord = string.IsNullOrWhiteSpace(selectedRecordId)
            ? null
            : FilteredRecords.FirstOrDefault(record => string.Equals(record.Id, selectedRecordId, StringComparison.OrdinalIgnoreCase));
    }

    private void CreateBackup()
    {
        try
        {
            IsBusy = true;
            var backupPath = _service.CreateBackup();
            LastBackupSummary = $"最近备份: {backupPath}";
            StatusMessage = "备份完成。";
            MessageBox.Show(LastBackupSummary, "备份完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = $"备份失败: {ex.Message}";
            MessageBox.Show(StatusMessage, "备份失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ExportCurrentView()
    {
        try
        {
            IsBusy = true;
            var path = _service.ExportCsv(FilteredRecords);
            StatusMessage = "导出完成。";
            MessageBox.Show($"已导出到: {path}", "导出完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = $"导出失败: {ex.Message}";
            MessageBox.Show(StatusMessage, "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ImportCsvFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择 CSV 导入文件",
            Filter = "CSV 文件 (*.csv)|*.csv",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var preview = _service.PreviewCsv(dialog.FileName);
            var previewMessage = BuildImportPreviewMessage(dialog.FileName, preview);
            var previewResult = MessageBox.Show(previewMessage, "导入预览", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (previewResult != MessageBoxResult.Yes)
            {
                StatusMessage = "已取消导入。";
                return;
            }

            IsBusy = true;
            var result = _service.ImportCsv(dialog.FileName, _allRecords);
            if (result.AddedCount == 0 && result.UpdatedCount == 0 && result.Errors.Count > 0)
            {
                var failureMessage = BuildImportResultMessage(result, includeFailureReport: true);
                MessageBox.Show(failureMessage, "导入失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                LastImportSummary = $"导入失败: {result.Errors[0]}";
                StatusMessage = "导入失败。";
                return;
            }

            PersistAll(null);
            LastImportSummary = $"最近导入: {Path.GetFileName(dialog.FileName)}，{result.Summary}";
            StatusMessage = "导入完成。";

            MessageBox.Show(
                BuildImportResultMessage(result, includeFailureReport: true),
                "导入完成",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LastImportSummary = $"导入失败: {ex.Message}";
            StatusMessage = "导入失败。";
            MessageBox.Show(ex.Message, "导入失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private void TogglePasswordVisibility()
    {
        ShowPasswords = !ShowPasswords;
    }

    private void ToggleEditorPasswordVisibility()
    {
        IsEditorPasswordVisible = !IsEditorPasswordVisible;
    }

    private static string BuildImportPreviewMessage(string fileName, CsvPreviewResult preview)
    {
        var lines = new List<string>
        {
            $"文件: {Path.GetFileName(fileName)}",
            $"编码: {preview.SourceEncoding}",
            $"数据行数: {Math.Max(preview.TotalRowCount, 0)}"
        };

        if (preview.PreviewLines.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("预览前几行:");
            lines.AddRange(preview.PreviewLines);
        }

        if (preview.Warnings.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("解析提示:");
            lines.AddRange(preview.Warnings.Take(5));
        }

        lines.Add(string.Empty);
        lines.Add("确认继续导入吗？");
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildImportResultMessage(CsvImportResult result, bool includeFailureReport)
    {
        var lines = new List<string> { result.Summary };

        if (result.Errors.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("失败明细(最多展示 10 条):");
            lines.AddRange(result.Errors.Take(10));
        }

        if (includeFailureReport && !string.IsNullOrWhiteSpace(result.FailureReportPath))
        {
            lines.Add(string.Empty);
            lines.Add($"失败报告已导出到: {result.FailureReportPath}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
