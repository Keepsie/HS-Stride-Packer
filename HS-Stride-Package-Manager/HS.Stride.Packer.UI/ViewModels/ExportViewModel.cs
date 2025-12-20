// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using HS.Stride.Packer.Core;
using HS.Stride.Packer.Utilities;

namespace HS.Stride.Packer.UI.ViewModels
{
    public class ExportViewModel : INotifyPropertyChanged
    {
        // Backend Services
        private readonly PackageExporter _packageExporter;
        private readonly NamespaceScanner _namespaceScanner;
        private readonly ResourcePathValidator _resourceValidator;
        private StridePackageManager? _packageManager;

        // Package Information
        private string _packageName = "";
        private string _version = "";
        private string _strideVersion = "";
        private string _author = "";
        private string _description = "";
        private string _tags = "";

        // Project Setup
        private string _projectPath = "";
        private string _exportLocation = "";
        private bool _isProjectValid;
        private string _projectValidationMessage = "";

        // Phase Collections
        private ObservableCollection<AssetFolderItem> _assetFolders = new();
        private ObservableCollection<CodeProjectItem> _codeProjects = new();
        private ObservableCollection<NamespaceItem> _namespaces = new();

        // Progress and Status
        private bool _isProcessing;
        private string _statusMessage = "Ready to create package";
        private string _errorMessage = "";
        private double _progressValue;
        private bool _canCreatePackage;

        public ExportViewModel()
        {
            // Initialize backend services
            _resourceValidator = new ResourcePathValidator();
            _namespaceScanner = new NamespaceScanner();
            _packageExporter = new PackageExporter(_resourceValidator, _namespaceScanner);

            // Initialize commands
            BrowseProjectCommand = new RelayCommand(BrowseProject);
            BrowseExportLocationCommand = new RelayCommand(BrowseExportLocation);
            CreatePackageCommand = new RelayCommand(CreatePackage, () => CanCreatePackage);

            // Initialize default values
            ExportLocation = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads";
        }

        #region Properties

        // Package Information
        public string PackageName
        {
            get => _packageName;
            set
            {
                if (SetProperty(ref _packageName, value))
                {
                    UpdateCanCreatePackage();
                }
            }
        }

        public string Version
        {
            get => _version;
            set
            {
                if (SetProperty(ref _version, value))
                {
                    UpdateCanCreatePackage();
                }
            }
        }

        public string StrideVersion
        {
            get => _strideVersion;
            set => SetProperty(ref _strideVersion, value);
        }

        public string Author
        {
            get => _author;
            set => SetProperty(ref _author, value);
        }

        public string Description
        {
            get => _description;
            set
            {
                if (SetProperty(ref _description, value))
                {
                    UpdateCanCreatePackage();
                }
            }
        }

        public string Tags
        {
            get => _tags;
            set => SetProperty(ref _tags, value);
        }

        // Project Setup
        public string ProjectPath
        {
            get => _projectPath;
            set
            {
                if (SetProperty(ref _projectPath, value))
                {
                    ValidateProjectPath();
                    RefreshProject();
                }
            }
        }

        public string ExportLocation
        {
            get => _exportLocation;
            set => SetProperty(ref _exportLocation, value);
        }

        public bool IsProjectValid
        {
            get => _isProjectValid;
            set
            {
                if (SetProperty(ref _isProjectValid, value))
                {
                    UpdateCanCreatePackage();
                }
            }
        }

        public string ProjectValidationMessage
        {
            get => _projectValidationMessage;
            set => SetProperty(ref _projectValidationMessage, value);
        }

        // Collections
        public ObservableCollection<AssetFolderItem> AssetFolders
        {
            get => _assetFolders;
            set => SetProperty(ref _assetFolders, value);
        }

        public ObservableCollection<CodeProjectItem> CodeProjects
        {
            get => _codeProjects;
            set => SetProperty(ref _codeProjects, value);
        }

        public ObservableCollection<NamespaceItem> Namespaces
        {
            get => _namespaces;
            set => SetProperty(ref _namespaces, value);
        }

        // Select All Assets Property
        private bool? _selectAllAssets;
        public bool? SelectAllAssets
        {
            get => _selectAllAssets;
            set
            {
                if (SetProperty(ref _selectAllAssets, value))
                {
                    if (value.HasValue)
                    {
                        foreach (var asset in AssetFolders)
                        {
                            asset.IsSelected = value.Value;
                        }
                    }
                }
            }
        }

        // Exclude All Namespaces Property (tri-state toggle like Select All Assets)
        private bool? _excludeAllNamespaces;
        public bool? ExcludeAllNamespaces
        {
            get => _excludeAllNamespaces;
            set
            {
                if (SetProperty(ref _excludeAllNamespaces, value))
                {
                    if (value.HasValue)
                    {
                        foreach (var ns in Namespaces)
                        {
                            ns.IsExcluded = value.Value;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Called by NamespaceItem when IsExcluded changes to update the tri-state checkbox
        /// </summary>
        public void UpdateExcludeAllNamespaces()
        {
            if (Namespaces.Count == 0)
            {
                _excludeAllNamespaces = false;
                OnPropertyChanged(nameof(ExcludeAllNamespaces));
                return;
            }

            var excludedCount = Namespaces.Count(ns => ns.IsExcluded);

            if (excludedCount == 0)
                _excludeAllNamespaces = false;      // None excluded
            else if (excludedCount == Namespaces.Count)
                _excludeAllNamespaces = true;       // All excluded
            else
                _excludeAllNamespaces = null;       // Partial (indeterminate)

            OnPropertyChanged(nameof(ExcludeAllNamespaces));
        }

        // Progress and Status
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    UpdateCanCreatePackage();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (SetProperty(ref _errorMessage, value))
                {
                    OnPropertyChanged(nameof(HasError));
                }
            }
        }

        public bool HasError => !string.IsNullOrEmpty(_errorMessage);

        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        public bool CanCreatePackage
        {
            get => _canCreatePackage;
            set => SetProperty(ref _canCreatePackage, value);
        }

        #endregion

        #region Commands

        public ICommand BrowseProjectCommand { get; }
        public ICommand BrowseExportLocationCommand { get; }
        public ICommand CreatePackageCommand { get; }

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
                var selectedPath = Path.GetDirectoryName(dialog.FileName);
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    ProjectPath = selectedPath;
                }
            }
        }

        private void BrowseExportLocation()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select Export Location",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ExportLocation = dialog.SelectedPath;
            }
        }

        private async void RefreshProject()
        {
            if (string.IsNullOrEmpty(ProjectPath) || !IsProjectValid)
                return;

            IsProcessing = true;
            StatusMessage = "Scanning project...";
            ProgressValue = 0;

            try
            {
                await ScanAssetFoldersAsync();
                ProgressValue = 25;

                await ScanCodeProjectsAsync();
                ProgressValue = 50;

                await ScanNamespacesAsync();
                ProgressValue = 100;

                StatusMessage = "Project scan complete";
                UpdateCanCreatePackage();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error scanning project: {ex.Message}";
                System.Windows.MessageBox.Show($"Failed to scan project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task ScanAssetFoldersAsync()
        {
            var assetFolders = await Task.Run(() => _packageExporter.ScanForAssetFolders(ProjectPath));

            AssetFolders.Clear();
            foreach (var folder in assetFolders)
            {
                AssetFolders.Add(new AssetFolderItem
                {
                    Name = folder.Name,
                    RelativePath = folder.RelativePath,  // Full path for export
                    FileCount = folder.FileCount,
                    Depth = folder.Depth,  // For hierarchical display
                    IsSelected = true,
                    FullPath = folder.FullPath,
                    Parent = this
                });
            }
        }

        private async Task ScanCodeProjectsAsync()
        {
            var codeProjects = await Task.Run(() => _packageExporter.ScanForCodeFolders(ProjectPath));
            
            CodeProjects.Clear();
            foreach (var project in codeProjects)
            {
                var projectItem = new CodeProjectItem
                {
                    Name = project.Name,
                    Type = project.Type,
                    IsSelected = false, // Start all projects unselected
                    SubFolders = new ObservableCollection<CodeSubFolderItem>()
                };

                foreach (var subFolder in project.SubFolders)
                {
                    var subFolderItem = new CodeSubFolderItem
                    {
                        Name = subFolder,
                        Parent = projectItem,
                        IsSelected = false  // Start unselected
                    };
                    projectItem.SubFolders.Add(subFolderItem);
                }

                CodeProjects.Add(projectItem);
            }
        }

        private async Task ScanNamespacesAsync()
        {
            var namespaceRefs = await Task.Run(() => _namespaceScanner.ScanDirectory(ProjectPath));

            Namespaces.Clear();
            foreach (var ns in namespaceRefs)
            {
                var shouldExclude = ns.Namespace.Contains("Test") || ns.Namespace.Contains("Debug") || ns.Namespace.Contains("Temp");

                Namespaces.Add(new NamespaceItem
                {
                    Name = ns.Namespace,
                    FileCount = ns.FoundInFiles.Count,
                    IsExcluded = shouldExclude,
                    Files = ns.FoundInFiles,
                    Category = shouldExclude ? "Remove" : "Keep",
                    Parent = this
                });
            }

            // Update the tri-state checkbox after loading
            UpdateExcludeAllNamespaces();
        }

        private void ValidateProjectPath()
        {
            if (string.IsNullOrEmpty(ProjectPath))
            {
                IsProjectValid = false;
                ProjectValidationMessage = "Please select a Stride project path";
                return;
            }

            var validation = PathHelper.ValidateStrideProject(ProjectPath);
            
            IsProjectValid = validation.IsValid;
            
            if (validation.IsValid)
            {
                ProjectValidationMessage = validation.SuccessMessage;
                
                // Auto-populate package information if fields are empty (like console app)
                AutoPopulatePackageInformation();
                
                // Ensure button state is updated after auto-population
                UpdateCanCreatePackage();
            }
            else
            {
                ProjectValidationMessage = validation.ErrorMessage;
                
                // Add suggestions as additional context (for console output later if needed)
                if (validation.Suggestions.Any())
                {
                    ProjectValidationMessage += $"\nSuggestions: {string.Join("; ", validation.Suggestions)}";
                }
            }
        }

        private void AutoPopulatePackageInformation()
        {
            // Only auto-populate if fields are empty to preserve user input
            var defaultName = Path.GetFileName(ProjectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            
            if (string.IsNullOrWhiteSpace(PackageName))
            {
                PackageName = defaultName;
            }
            
            if (string.IsNullOrWhiteSpace(Version))
            {
                Version = "1.0.0";
            }
            
            if (string.IsNullOrWhiteSpace(StrideVersion))
            {
                StrideVersion = "4.2.0";
            }
            
            if (string.IsNullOrWhiteSpace(Author))
            {
                Author = Environment.UserName;
            }
            
            if (string.IsNullOrWhiteSpace(Description))
            {
                Description = $"Package created from {PackageName}";
            }
        }

        private void UpdateCanCreatePackage()
        {
            CanCreatePackage = IsProjectValid &&
                              !string.IsNullOrWhiteSpace(PackageName) &&
                              !string.IsNullOrWhiteSpace(Version) &&
                              !string.IsNullOrWhiteSpace(Description) &&
                              !IsProcessing;
            
            // Force command reevaluation
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private async void CreatePackage()
        {
            if (!CanCreatePackage)
                return;

            IsProcessing = true;
            StatusMessage = "Creating package...";
            ErrorMessage = "";  // Clear previous errors
            ProgressValue = 0;

            try
            {
                var exportSettings = BuildExportSettings();
                _packageManager = new StridePackageManager(exportSettings);

                var result = await _packageManager.CreatePackageAsync();

                ProgressValue = 100;
                StatusMessage = "Package created successfully!";
                ErrorMessage = "";  // Clear errors on success

                System.Windows.MessageBox.Show($"Package created successfully!\nFile: {result}", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                // Clear all areas for next package creation
                ClearAllAreas();
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to create package";

                // Build detailed error message for the copyable text area
                var errorBuilder = new System.Text.StringBuilder();
                errorBuilder.AppendLine("=== PACKAGE CREATION FAILED ===");
                errorBuilder.AppendLine();
                errorBuilder.AppendLine(ex.Message);

                if (ex.InnerException != null)
                {
                    errorBuilder.AppendLine();
                    errorBuilder.AppendLine("--- Details ---");
                    errorBuilder.AppendLine(ex.InnerException.Message);
                }

                ErrorMessage = errorBuilder.ToString();
            }
            finally
            {
                IsProcessing = false;
                UpdateCanCreatePackage();
            }
        }

        private ExportSettings BuildExportSettings()
        {
            var outputPath = Path.Combine(ExportLocation, PathHelper.MakePackageFileName(PackageName, Version));
            
            var settings = new ExportSettings
            {
                LibraryPath = ProjectPath,
                OutputPath = outputPath,
                ExportRegistryJson = true,
                Manifest = new PackageManifest
                {
                    Name = PackageName,
                    Version = Version,
                    StrideVersion = StrideVersion,
                    Description = Description,
                    Author = Author,
                    CreatedDate = DateTime.UtcNow,
                    Tags = Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                              .Select(t => t.Trim())
                              .Where(t => !string.IsNullOrEmpty(t))
                              .ToList()
                }
            };

            // Collect selected asset folders (use RelativePath for full subfolder structure)
            settings.SelectedAssetFolders = AssetFolders
                .Where(af => af.IsSelected)
                .Select(af => af.RelativePath)
                .ToList();

            // Collect selected code folders
            settings.SelectedCodeFolders = CodeProjects
                .Where(cp => cp.IsSelected)
                .SelectMany(cp => cp.SubFolders.Where(sf => sf.IsSelected).Select(sf => $"{cp.Name}/{sf.Name}"))
                .ToList();

            // Collect excluded namespaces
            settings.ExcludeNamespaces = Namespaces
                .Where(ns => ns.IsExcluded)
                .Select(ns => ns.Name)
                .ToList();

            return settings;
        }

        private void ClearAllAreas()
        {
            // Clear package information
            PackageName = "";
            Version = "";
            StrideVersion = "";
            Author = "";
            Description = "";
            Tags = "";

            // Clear project setup
            ProjectPath = "";
            IsProjectValid = false;
            ProjectValidationMessage = "";

            // Clear collections
            AssetFolders.Clear();
            CodeProjects.Clear();
            Namespaces.Clear();

            // Reset status
            StatusMessage = "Ready to create package";
            ErrorMessage = "";
            ProgressValue = 0;
            CanCreatePackage = false;
        }

        public void UpdateSelectAllAssets()
        {
            if (AssetFolders.Count == 0)
            {
                _selectAllAssets = false;
                OnPropertyChanged(nameof(SelectAllAssets));
                return;
            }

            var selectedCount = AssetFolders.Count(af => af.IsSelected);
            
            if (selectedCount == 0)
                _selectAllAssets = false;
            else if (selectedCount == AssetFolders.Count)
                _selectAllAssets = true;
            else
                _selectAllAssets = null; // Indeterminate state
                
            OnPropertyChanged(nameof(SelectAllAssets));
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

    #region Helper Classes

    public class AssetFolderItem : INotifyPropertyChanged
    {
        private bool _isSelected = true;

        public string Name { get; set; } = "";
        public string RelativePath { get; set; } = "";  // Full relative path for export
        public int FileCount { get; set; }
        public string FullPath { get; set; } = "";
        public int Depth { get; set; }  // Depth level for hierarchy display
        public ExportViewModel? Parent { get; set; }

        /// <summary>
        /// Display name with indentation for hierarchical view
        /// </summary>
        public string DisplayName => Depth > 0 ? $"{"   ".PadLeft(Depth * 3)}└─ {Name}" : Name;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                    Parent?.UpdateSelectAllAssets();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class CodeProjectItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public ObservableCollection<CodeSubFolderItem> SubFolders { get; set; } = new();

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                    
                    // Update all subfolders
                    foreach (var subFolder in SubFolders)
                    {
                        subFolder.IsSelected = value;
                    }
                }
            }
        }

        public void UpdateParentSelection()
        {
            var selectedCount = SubFolders.Count(sf => sf.IsSelected);
            var newSelection = false;
            
            // Update parent selection based on children
            if (selectedCount == 0)
            {
                newSelection = false;
            }
            else if (selectedCount == SubFolders.Count)
            {
                newSelection = true;
            }
            else
            {
                // Partial selection - set to true to show something is selected
                newSelection = true;
            }
            
            // Only update if it actually changed
            if (_isSelected != newSelection)
            {
                _isSelected = newSelection;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class CodeSubFolderItem : INotifyPropertyChanged
    {
        private bool _isSelected = false;

        public string Name { get; set; } = "";
        public CodeProjectItem? Parent { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                    Parent?.UpdateParentSelection();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class NamespaceItem : INotifyPropertyChanged
    {
        private bool _isExcluded;

        public string Name { get; set; } = "";
        public int FileCount { get; set; }
        public List<string> Files { get; set; } = new();
        public string Category { get; set; } = "Keep";
        public ExportViewModel? Parent { get; set; }

        public bool IsExcluded
        {
            get => _isExcluded;
            set
            {
                if (_isExcluded != value)
                {
                    _isExcluded = value;
                    Category = value ? "Remove" : "Keep";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Category));
                    Parent?.UpdateExcludeAllNamespaces();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    #endregion
}