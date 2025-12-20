// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using HS.Stride.Packer.Core;
using MessageBox = System.Windows.MessageBox;

namespace HS.Stride.Packer.UI.ViewModels
{
    public class CleanupViewModel : INotifyPropertyChanged
    {
        private string _projectPath = "";
        private bool _isScanning;
        private bool _hasScanned;
        private string _statusMessage = "Select a Stride project to scan for cleanup opportunities";
        private double _progressValue;
        private CleanupAnalysis? _analysis;

        // Summary stats
        private int _totalAssets;
        private int _totalResources;
        private int _orphanedCount;
        private string _orphanedSize = "0 B";
        private int _misplacedCount;
        private int _emptyFolderCount;

        // Collections
        private ObservableCollection<OrphanedResourceItem> _orphanedResources = new();
        private ObservableCollection<MisplacedResourceItem> _misplacedResources = new();
        private ObservableCollection<OrphanedFolderItem> _orphanedFolders = new();

        // Selection states
        private bool? _selectAllOrphans = true;
        private bool? _selectAllMisplaced = true;

        public CleanupViewModel()
        {
            BrowseProjectCommand = new RelayCommand(BrowseProject);
            ScanProjectCommand = new RelayCommand(ScanProject, () => !string.IsNullOrEmpty(ProjectPath) && !IsScanning);
            DeleteOrphansCommand = new RelayCommand(DeleteOrphans, () => HasScanned && OrphanedResources.Any(o => o.IsSelected) && !IsScanning);
            DeleteFoldersCommand = new RelayCommand(DeleteFolders, () => HasScanned && OrphanedFolders.Any(f => f.IsSelected) && !IsScanning);
            ReorganizeCommand = new RelayCommand(ReorganizeResources, () => HasScanned && MisplacedResources.Any(m => m.IsSelected) && !IsScanning);
            CleanEmptyFoldersCommand = new RelayCommand(CleanEmptyFolders, () => HasScanned && EmptyFolderCount > 0 && !IsScanning);
        }

        #region Properties

        public string ProjectPath
        {
            get => _projectPath;
            set
            {
                if (SetProperty(ref _projectPath, value))
                {
                    HasScanned = false;
                    ClearResults();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsScanning
        {
            get => _isScanning;
            set
            {
                if (SetProperty(ref _isScanning, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool HasScanned
        {
            get => _hasScanned;
            set
            {
                if (SetProperty(ref _hasScanned, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        // Summary stats
        public int TotalAssets
        {
            get => _totalAssets;
            set => SetProperty(ref _totalAssets, value);
        }

        public int TotalResources
        {
            get => _totalResources;
            set => SetProperty(ref _totalResources, value);
        }

        public int OrphanedCount
        {
            get => _orphanedCount;
            set => SetProperty(ref _orphanedCount, value);
        }

        public string OrphanedSize
        {
            get => _orphanedSize;
            set => SetProperty(ref _orphanedSize, value);
        }

        public int MisplacedCount
        {
            get => _misplacedCount;
            set => SetProperty(ref _misplacedCount, value);
        }

        public int EmptyFolderCount
        {
            get => _emptyFolderCount;
            set => SetProperty(ref _emptyFolderCount, value);
        }

        // Collections
        public ObservableCollection<OrphanedResourceItem> OrphanedResources
        {
            get => _orphanedResources;
            set => SetProperty(ref _orphanedResources, value);
        }

        public ObservableCollection<MisplacedResourceItem> MisplacedResources
        {
            get => _misplacedResources;
            set => SetProperty(ref _misplacedResources, value);
        }

        public ObservableCollection<OrphanedFolderItem> OrphanedFolders
        {
            get => _orphanedFolders;
            set => SetProperty(ref _orphanedFolders, value);
        }

        // Selection states
        public bool? SelectAllOrphans
        {
            get => _selectAllOrphans;
            set
            {
                if (SetProperty(ref _selectAllOrphans, value) && value.HasValue)
                {
                    foreach (var item in OrphanedResources)
                        item.IsSelected = value.Value;
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool? SelectAllMisplaced
        {
            get => _selectAllMisplaced;
            set
            {
                if (SetProperty(ref _selectAllMisplaced, value) && value.HasValue)
                {
                    foreach (var item in MisplacedResources)
                        item.IsSelected = value.Value;
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        #endregion

        #region Commands

        public ICommand BrowseProjectCommand { get; }
        public ICommand ScanProjectCommand { get; }
        public ICommand DeleteOrphansCommand { get; }
        public ICommand DeleteFoldersCommand { get; }
        public ICommand ReorganizeCommand { get; }
        public ICommand CleanEmptyFoldersCommand { get; }

        #endregion

        #region Private Methods

        private void BrowseProject()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Stride Project Solution File",
                Filter = "Visual Studio Solution Files (*.sln)|*.sln|All Files (*.*)|*.*",
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                ProjectPath = Path.GetDirectoryName(dialog.FileName) ?? "";
            }
        }

        private async void ScanProject()
        {
            if (string.IsNullOrEmpty(ProjectPath) || !Directory.Exists(ProjectPath))
            {
                StatusMessage = "Please select a valid project path";
                return;
            }

            IsScanning = true;
            StatusMessage = "Scanning project...";
            ProgressValue = 0;
            ClearResults();

            try
            {
                ProgressValue = 10;
                StatusMessage = "Analyzing assets and resources...";

                var service = new ProjectCleanupService(ProjectPath);
                _analysis = await service.AnalyzeProjectAsync();

                ProgressValue = 80;
                StatusMessage = "Processing results...";

                // Update summary
                TotalAssets = _analysis.TotalAssets;
                TotalResources = _analysis.TotalResources;
                OrphanedCount = _analysis.OrphanedResources.Count;
                OrphanedSize = FormatSize(_analysis.TotalOrphanedSize);
                MisplacedCount = _analysis.MisplacedResources.Count;
                EmptyFolderCount = _analysis.EmptyFolders.Count;

                // Populate orphaned resources
                foreach (var orphan in _analysis.OrphanedResources)
                {
                    var item = new OrphanedResourceItem(orphan);
                    item.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(OrphanedResourceItem.IsSelected))
                        {
                            UpdateSelectAllOrphans();
                            CommandManager.InvalidateRequerySuggested();
                        }
                    };
                    OrphanedResources.Add(item);
                }

                // Populate misplaced resources
                foreach (var misplaced in _analysis.MisplacedResources)
                {
                    var item = new MisplacedResourceItem(misplaced);
                    item.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(MisplacedResourceItem.IsSelected))
                        {
                            UpdateSelectAllMisplaced();
                            CommandManager.InvalidateRequerySuggested();
                        }
                    };
                    MisplacedResources.Add(item);
                }

                // Populate orphaned folders (only those where all files are orphaned)
                foreach (var folder in _analysis.OrphanedFolders.Where(f => f.AllFilesOrphaned))
                {
                    OrphanedFolders.Add(new OrphanedFolderItem(folder));
                }

                ProgressValue = 100;
                HasScanned = true;

                if (_analysis.HasIssues)
                {
                    StatusMessage = $"Found {OrphanedCount} orphaned resources, {MisplacedCount} misplaced, {EmptyFolderCount} empty folders";
                }
                else
                {
                    StatusMessage = "Project is clean - no issues found";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Scan failed: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
                ProgressValue = 0;
            }
        }

        private async void DeleteOrphans()
        {
            var selected = OrphanedResources.Where(o => o.IsSelected).ToList();
            if (!selected.Any())
            {
                MessageBox.Show("No orphaned resources selected.", "Nothing Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to delete {selected.Count} orphaned resource(s)?\n\nThis cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            IsScanning = true;
            StatusMessage = "Deleting orphaned resources...";

            try
            {
                var service = new ProjectCleanupService(ProjectPath);
                var orphans = selected.Select(s => s.Resource).ToList();
                var cleanupResult = await service.DeleteOrphansAsync(orphans);

                // Remove deleted items from list
                foreach (var deleted in selected.Where(s => cleanupResult.DeletedFiles.Contains(s.Resource.RelativePath)))
                {
                    OrphanedResources.Remove(deleted);
                }

                OrphanedCount = OrphanedResources.Count;

                if (cleanupResult.HasErrors)
                {
                    StatusMessage = $"Deleted {cleanupResult.DeletedFiles.Count} files with {cleanupResult.Errors.Count} errors";
                }
                else
                {
                    StatusMessage = $"Successfully deleted {cleanupResult.DeletedFiles.Count} orphaned resources";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Delete failed: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        private async void DeleteFolders()
        {
            var selected = OrphanedFolders.Where(f => f.IsSelected).ToList();
            if (!selected.Any())
            {
                MessageBox.Show("No folders selected.", "Nothing Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to delete {selected.Count} folder(s) and ALL their contents?\n\nThis cannot be undone.",
                "Confirm Delete Folders",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            IsScanning = true;
            StatusMessage = "Deleting folders...";

            try
            {
                var service = new ProjectCleanupService(ProjectPath);
                var folders = selected.Select(s => s.Folder).ToList();
                var cleanupResult = await service.DeleteFoldersAsync(folders);

                // Remove deleted folders from list
                foreach (var deleted in selected.Where(s => cleanupResult.DeletedFolders.Contains(s.Folder.RelativePath)))
                {
                    OrphanedFolders.Remove(deleted);

                    // Also remove orphaned resources that were in this folder
                    var toRemove = OrphanedResources
                        .Where(o => o.Resource.RelativePath.StartsWith(deleted.Folder.RelativePath + "/"))
                        .ToList();
                    foreach (var item in toRemove)
                        OrphanedResources.Remove(item);
                }

                OrphanedCount = OrphanedResources.Count;

                if (cleanupResult.HasErrors)
                {
                    StatusMessage = $"Deleted {cleanupResult.DeletedFolders.Count} folders with {cleanupResult.Errors.Count} errors";
                }
                else
                {
                    StatusMessage = $"Successfully deleted {cleanupResult.DeletedFolders.Count} folders";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Delete failed: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        private async void ReorganizeResources()
        {
            var selected = MisplacedResources.Where(m => m.IsSelected).ToList();
            if (!selected.Any())
            {
                MessageBox.Show("No resources selected for reorganization.", "Nothing Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"This will move {selected.Count} resource(s) to better match their asset folder structure.\n\nAsset references will be updated automatically.\n\nContinue?",
                "Confirm Reorganize",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            IsScanning = true;
            StatusMessage = "Reorganizing resources...";

            try
            {
                var service = new ProjectCleanupService(ProjectPath);
                var resources = selected.Select(s => s.Resource).ToList();
                var cleanupResult = await service.ReorganizeResourcesAsync(resources);

                // Remove moved items from list
                foreach (var item in selected)
                {
                    MisplacedResources.Remove(item);
                }

                MisplacedCount = MisplacedResources.Count;

                if (cleanupResult.HasErrors)
                {
                    StatusMessage = $"Moved {cleanupResult.MovedFiles.Count} files with {cleanupResult.Errors.Count} errors";
                }
                else
                {
                    StatusMessage = $"Successfully reorganized {cleanupResult.MovedFiles.Count} resources";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Reorganize failed: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        private async void CleanEmptyFolders()
        {
            var result = MessageBox.Show(
                $"This will delete {EmptyFolderCount} empty folder(s) from the project.\n\nContinue?",
                "Confirm Clean Empty Folders",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            IsScanning = true;
            StatusMessage = "Cleaning empty folders...";

            try
            {
                var service = new ProjectCleanupService(ProjectPath);
                var cleanupResult = await service.CleanEmptyFoldersAsync();

                EmptyFolderCount = 0;

                if (cleanupResult.HasErrors)
                {
                    StatusMessage = $"Deleted {cleanupResult.DeletedFolders.Count} empty folders with {cleanupResult.Errors.Count} errors";
                }
                else
                {
                    StatusMessage = $"Successfully deleted {cleanupResult.DeletedFolders.Count} empty folders";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Clean failed: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        private void ClearResults()
        {
            OrphanedResources.Clear();
            MisplacedResources.Clear();
            OrphanedFolders.Clear();
            TotalAssets = 0;
            TotalResources = 0;
            OrphanedCount = 0;
            OrphanedSize = "0 B";
            MisplacedCount = 0;
            EmptyFolderCount = 0;
            _analysis = null;
        }

        private void UpdateSelectAllOrphans()
        {
            if (!OrphanedResources.Any())
            {
                _selectAllOrphans = false;
            }
            else if (OrphanedResources.All(o => o.IsSelected))
            {
                _selectAllOrphans = true;
            }
            else if (OrphanedResources.All(o => !o.IsSelected))
            {
                _selectAllOrphans = false;
            }
            else
            {
                _selectAllOrphans = null;
            }
            OnPropertyChanged(nameof(SelectAllOrphans));
        }

        private void UpdateSelectAllMisplaced()
        {
            if (!MisplacedResources.Any())
            {
                _selectAllMisplaced = false;
            }
            else if (MisplacedResources.All(m => m.IsSelected))
            {
                _selectAllMisplaced = true;
            }
            else if (MisplacedResources.All(m => !m.IsSelected))
            {
                _selectAllMisplaced = false;
            }
            else
            {
                _selectAllMisplaced = null;
            }
            OnPropertyChanged(nameof(SelectAllMisplaced));
        }

        private string FormatSize(long bytes)
        {
            return bytes switch
            {
                < 1024 => $"{bytes} B",
                < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
                < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
                _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
            };
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }

    // Wrapper classes for UI binding with selection support
    public class OrphanedResourceItem : INotifyPropertyChanged
    {
        private bool _isSelected = true;
        public OrphanedResource Resource { get; }

        public OrphanedResourceItem(OrphanedResource resource)
        {
            Resource = resource;
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public string FileName => Resource.FileName;
        public string RelativePath => Resource.RelativePath;
        public string SizeDisplay => Resource.SizeDisplay;
        public string Extension => Resource.Extension;

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class MisplacedResourceItem : INotifyPropertyChanged
    {
        private bool _isSelected = true;
        public MisplacedResource Resource { get; }

        public MisplacedResourceItem(MisplacedResource resource)
        {
            Resource = resource;
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public string FileName => Resource.FileName;
        public string CurrentPath => Resource.CurrentRelativePath;
        public string SuggestedPath => Resource.SuggestedRelativePath;
        public string ReferencingAsset => Resource.ReferencingAsset;

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class OrphanedFolderItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        public OrphanedFolder Folder { get; }

        public OrphanedFolderItem(OrphanedFolder folder)
        {
            Folder = folder;
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public string RelativePath => Folder.RelativePath;
        public int OrphanCount => Folder.OrphanCount;
        public string SizeDisplay => Folder.SizeDisplay;

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
