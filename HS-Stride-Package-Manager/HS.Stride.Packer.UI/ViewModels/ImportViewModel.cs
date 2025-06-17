// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using HS.Stride.Packer.Core;
using HS.Stride.Packer.Utilities;
using HS.Stride.Packer.UI.Services;
using MessageBox = System.Windows.MessageBox;

namespace HS.Stride.Packer.UI.ViewModels
{
    public class ImportViewModel : INotifyPropertyChanged
    {
        // Backend Services
        private readonly PackageRegistry _registry;
        private readonly SettingsManager _settingsManager;
        private StridePackageManager? _packageManager;

        // Package Source
        private string _packageFilePath = "";
        private string _packageUrl = "";
        private string _registryPackageName = "";
        private int _selectedSourceIndex = 0; // 0=Local, 1=URL, 2=Registry

        // Target Project
        private string _targetProjectPath = "";
        private bool _isTargetValid;
        private string _targetValidationMessage = "";

        // Package Validation
        private bool _isPackageValid;
        private string _packageValidationMessage = "";
        private PackageMetadata? _packageMetadata;

        // Import Settings
        private bool _overwriteExistingFiles = true;

        // Progress and Status
        private bool _isProcessing;
        private string _statusMessage = "Ready to import package";
        private double _progressValue;
        private bool _canImport;

        // Results
        private ImportResult? _lastImportResult;
        private ObservableCollection<string> _importResults = new();
        private ObservableCollection<string> _packageContents = new();

        public ImportViewModel()
        {
            // Initialize settings manager and load saved settings
            _settingsManager = new SettingsManager();
            
            // Initialize backend services
            _registry = new PackageRegistry();
            _registry.SetRegistryUrl(_settingsManager.Settings.RegistryUrl);

            // Initialize commands
            BrowsePackageFileCommand = new RelayCommand(BrowsePackageFile);
            ValidateUrlCommand = new RelayCommand(ValidateUrl);
            SearchRegistryCommand = new RelayCommand(SearchRegistry);
            BrowseTargetProjectCommand = new RelayCommand(BrowseTargetProject);
            ImportPackageCommand = new RelayCommand(ImportPackage, () => CanImport);
        }

        #region Properties

        // Package Source
        public string PackageFilePath
        {
            get => _packageFilePath;
            set
            {
                if (SetProperty(ref _packageFilePath, value))
                {
                    if (SelectedSourceIndex == 0) // Local file
                    {
                        ValidatePackageSource();
                        AutoValidateIfReady();
                    }
                }
            }
        }

        public string PackageUrl
        {
            get => _packageUrl;
            set
            {
                if (SetProperty(ref _packageUrl, value))
                {
                    if (SelectedSourceIndex == 1) // URL
                    {
                        ResetPackageValidation();
                        // Auto-validate URLs after user finishes typing (could add debouncing here)
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            ValidateUrl();
                        }
                    }
                }
            }
        }

        public string RegistryPackageName
        {
            get => _registryPackageName;
            set
            {
                if (SetProperty(ref _registryPackageName, value))
                {
                    if (SelectedSourceIndex == 2) // Registry
                    {
                        ResetPackageValidation();
                    }
                }
            }
        }

        public int SelectedSourceIndex
        {
            get => _selectedSourceIndex;
            set
            {
                if (SetProperty(ref _selectedSourceIndex, value))
                {
                    ResetPackageValidation();
                    ValidatePackageSource();
                    AutoValidateIfReady();
                }
            }
        }

        // Target Project
        public string TargetProjectPath
        {
            get => _targetProjectPath;
            set
            {
                if (SetProperty(ref _targetProjectPath, value))
                {
                    ValidateTargetProject();
                    AutoValidateIfReady();
                }
            }
        }

        public bool IsTargetValid
        {
            get => _isTargetValid;
            set
            {
                if (SetProperty(ref _isTargetValid, value))
                {
                    UpdateCanImport();
                }
            }
        }

        public string TargetValidationMessage
        {
            get => _targetValidationMessage;
            set => SetProperty(ref _targetValidationMessage, value);
        }

        // Package Validation
        public bool IsPackageValid
        {
            get => _isPackageValid;
            set
            {
                if (SetProperty(ref _isPackageValid, value))
                {
                    UpdateCanImport();
                }
            }
        }

        public string PackageValidationMessage
        {
            get => _packageValidationMessage;
            set => SetProperty(ref _packageValidationMessage, value);
        }

        public PackageMetadata? PackageMetadata
        {
            get => _packageMetadata;
            set => SetProperty(ref _packageMetadata, value);
        }

        // Import Settings
        public bool OverwriteExistingFiles
        {
            get => _overwriteExistingFiles;
            set => SetProperty(ref _overwriteExistingFiles, value);
        }

        // Progress and Status
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    UpdateCanImport();
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

        public bool CanImport
        {
            get => _canImport;
            set => SetProperty(ref _canImport, value);
        }


        // Results
        public ImportResult? LastImportResult
        {
            get => _lastImportResult;
            set => SetProperty(ref _lastImportResult, value);
        }

        public ObservableCollection<string> ImportResults
        {
            get => _importResults;
            set => SetProperty(ref _importResults, value);
        }

        public ObservableCollection<string> PackageContents
        {
            get => _packageContents;
            set => SetProperty(ref _packageContents, value);
        }

        #endregion

        #region Commands

        public ICommand BrowsePackageFileCommand { get; }
        public ICommand ValidateUrlCommand { get; }
        public ICommand SearchRegistryCommand { get; }
        public ICommand BrowseTargetProjectCommand { get; }
        public ICommand ImportPackageCommand { get; }

        #endregion

        #region Private Methods

        private void BrowsePackageFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Stride Package File",
                Filter = "Stride Package Files (*.stridepackage)|*.stridepackage|All Files (*.*)|*.*",
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                PackageFilePath = dialog.FileName;
            }
        }

        private async void ValidateUrl()
        {
            if (string.IsNullOrWhiteSpace(PackageUrl))
            {
                PackageValidationMessage = "Please enter a URL";
                IsPackageValid = false;
                return;
            }

            IsProcessing = true;
            StatusMessage = "Validating URL...";

            try
            {
                if (PackageUrl.EndsWith("stridepackage.json", StringComparison.OrdinalIgnoreCase))
                {
                    // Validate metadata URL
                    var metadata = await _registry.GetPackageMetadataAsync(PackageUrl);
                    if (metadata != null)
                    {
                        PackageMetadata = metadata;
                        PackageValidationMessage = $"✓ Valid metadata URL - {metadata.Name} v{metadata.Version}";
                        IsPackageValid = true;
                        AutoValidateIfReady();
                    }
                    else
                    {
                        PackageValidationMessage = "Failed to load package metadata from URL";
                        IsPackageValid = false;
                    }
                }
                else if (PackageUrl.EndsWith(".stridepackage", StringComparison.OrdinalIgnoreCase))
                {
                    // Validate direct package URL
                    using var client = new HttpClient();
                    var response = await client.GetAsync(PackageUrl, HttpCompletionOption.ResponseHeadersRead);
                    if (response.IsSuccessStatusCode)
                    {
                        PackageValidationMessage = "✓ Valid package URL";
                        IsPackageValid = true;
                        AutoValidateIfReady();
                    }
                    else
                    {
                        PackageValidationMessage = "Package URL is not accessible";
                        IsPackageValid = false;
                    }
                }
                else
                {
                    PackageValidationMessage = "URL must point to a .stridepackage file or stridepackage.json metadata";
                    IsPackageValid = false;
                }
            }
            catch (Exception ex)
            {
                PackageValidationMessage = $"URL validation failed: {ex.Message}";
                IsPackageValid = false;
            }
            finally
            {
                IsProcessing = false;
                StatusMessage = "Ready to import package";
            }
        }

        private async void SearchRegistry()
        {
            if (string.IsNullOrWhiteSpace(RegistryPackageName))
            {
                PackageValidationMessage = "Please enter a package name";
                IsPackageValid = false;
                return;
            }

            IsProcessing = true;
            StatusMessage = "Searching registry...";

            try
            {
                var packages = await _registry.SearchPackagesAsync(RegistryPackageName);
                var package = packages.FirstOrDefault(p => 
                    p.Name.Equals(RegistryPackageName, StringComparison.OrdinalIgnoreCase));

                if (package != null)
                {
                    PackageMetadata = package;
                    PackageValidationMessage = $"✓ Found in registry - {package.Name} v{package.Version}";
                    IsPackageValid = true;
                    AutoValidateIfReady();
                }
                else
                {
                    PackageValidationMessage = $"Package '{RegistryPackageName}' not found in registry";
                    IsPackageValid = false;
                }
            }
            catch (Exception ex)
            {
                PackageValidationMessage = $"Registry search failed: {ex.Message}";
                IsPackageValid = false;
            }
            finally
            {
                IsProcessing = false;
                StatusMessage = "Ready to import package";
            }
        }

        private void BrowseTargetProject()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select Target Stride Project Directory",
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TargetProjectPath = dialog.SelectedPath;
            }
        }

        private async void AutoValidateIfReady()
        {
            // Only auto-validate if we have both package source and target, and not already processing
            if (!HasValidPackageSource() || !IsTargetValid || IsProcessing)
                return;

            IsProcessing = true;
            StatusMessage = "Analyzing package contents...";
            ProgressValue = 0;
            PackageContents.Clear();

            try
            {
                string packagePath = await GetPackagePathAsync();
                if (string.IsNullOrEmpty(packagePath))
                {
                    StatusMessage = "Failed to obtain package";
                    return;
                }

                ProgressValue = 25;

                // Create dummy export settings for the constructor
                var dummyExportSettings = new ExportSettings
                {
                    LibraryPath = TargetProjectPath,
                    OutputPath = "",
                    Manifest = new PackageManifest()
                };

                _packageManager = new StridePackageManager(dummyExportSettings);

                ProgressValue = 50;

                // Verify integrity first
                var isValid = await _packageManager.VerifyPackageIntegrityAsync(packagePath);
                
                if (!isValid)
                {
                    PackageValidationMessage = "✗ Package integrity verification failed";
                    StatusMessage = "Package validation failed";
                    return;
                }

                ProgressValue = 75;

                // Extract and analyze package contents
                await AnalyzePackageContentsAsync(packagePath);
                
                ProgressValue = 100;

                StatusMessage = "Ready to import package";
            }
            catch (Exception ex)
            {
                PackageValidationMessage = $"✗ Validation error: {ex.Message}";
                StatusMessage = "Package validation failed";
                PackageContents.Clear();
            }
            finally
            {
                IsProcessing = false;
                ProgressValue = 0;
            }
        }

        private async Task AnalyzePackageContentsAsync(string packagePath)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                // Extract package to temp directory
                Directory.CreateDirectory(tempDir);
                System.IO.Compression.ZipFile.ExtractToDirectory(packagePath, tempDir);

                // Read manifest if available
                var manifestPath = Path.Combine(tempDir, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    var manifestJson = await File.ReadAllTextAsync(manifestPath);
                    var manifest = System.Text.Json.JsonSerializer.Deserialize<PackageManifest>(manifestJson);
                    
                    if (manifest != null)
                    {
                        PackageContents.Add("📦 Package Information:");
                        PackageContents.Add($"  Name: {manifest.Name}");
                        PackageContents.Add($"  Version: {manifest.Version}");
                        PackageContents.Add($"  Author: {manifest.Author ?? "Unknown"}");
                        PackageContents.Add($"  Description: {manifest.Description ?? "No description"}");
                        if (!string.IsNullOrEmpty(manifest.StrideVersion))
                        {
                            PackageContents.Add($"  Stride Version: {manifest.StrideVersion}");
                        }
                        if (manifest.Tags?.Any() == true)
                        {
                            PackageContents.Add($"  Tags: {string.Join(", ", manifest.Tags)}");
                        }
                        PackageContents.Add("");
                    }
                }

                // Analyze folder structure
                PackageContents.Add("📁 Package Contents:");
                await AnalyzeDirectoryStructure(tempDir, tempDir, "  ");
            }
            finally
            {
                // Clean up temp directory
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        private async Task AnalyzeDirectoryStructure(string currentDir, string rootDir, string indent)
        {
            try
            {
                var directories = Directory.GetDirectories(currentDir, "*", SearchOption.TopDirectoryOnly)
                    .Where(d => !Path.GetFileName(d).Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(d => Path.GetFileName(d));

                var files = Directory.GetFiles(currentDir, "*", SearchOption.TopDirectoryOnly)
                    .Where(f => !Path.GetFileName(f).Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => Path.GetFileName(f));

                foreach (var dir in directories)
                {
                    var folderName = Path.GetFileName(dir);
                    var fileCount = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length;
                    
                    // Determine folder type
                    string icon = "📁";
                    if (folderName.Equals("Assets", StringComparison.OrdinalIgnoreCase))
                        icon = "🖼️";
                    else if (folderName.Equals("Resources", StringComparison.OrdinalIgnoreCase))
                        icon = "📚";
                    else if (folderName.Contains(".Game") || folderName.Contains("Game"))
                        icon = "🎮";
                    else if (folderName.Contains("Windows") || folderName.Contains("Platform"))
                        icon = "💻";

                    PackageContents.Add($"{indent}{icon} {folderName}/ ({fileCount} files)");

                    // Show key subfolders for Assets
                    if (folderName.Equals("Assets", StringComparison.OrdinalIgnoreCase))
                    {
                        var subDirs = Directory.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly);
                        foreach (var subDir in subDirs.Take(5)) // Limit to first 5
                        {
                            var subFolderName = Path.GetFileName(subDir);
                            var subFileCount = Directory.GetFiles(subDir, "*", SearchOption.AllDirectories).Length;
                            PackageContents.Add($"{indent}  📁 {subFolderName}/ ({subFileCount} files)");
                        }
                        if (subDirs.Length > 5)
                        {
                            PackageContents.Add($"{indent}  ... and {subDirs.Length - 5} more folders");
                        }
                    }
                }

                // Show important files at current level
                foreach (var file in files.Take(3)) // Limit to first 3 files
                {
                    var fileName = Path.GetFileName(file);
                    string icon = "📄";
                    if (fileName.EndsWith(".cs"))
                        icon = "💻";
                    else if (fileName.EndsWith(".sdpkg"))
                        icon = "📦";
                    else if (fileName.EndsWith(".sln"))
                        icon = "🔧";
                    
                    PackageContents.Add($"{indent}{icon} {fileName}");
                }

                if (files.Count() > 3)
                {
                    PackageContents.Add($"{indent}... and {files.Count() - 3} more files");
                }
            }
            catch (Exception)
            {
                PackageContents.Add($"{indent}❌ Error reading directory contents");
            }
        }

        private async void ImportPackage()
        {
            if (!CanImport)
                return;

            IsProcessing = true;
            StatusMessage = "Importing package...";
            ProgressValue = 0;
            ImportResults.Clear();

            try
            {
                string packagePath = await GetPackagePathAsync();
                if (string.IsNullOrEmpty(packagePath))
                {
                    StatusMessage = "Failed to obtain package";
                    return;
                }

                ProgressValue = 25;

                var importSettings = new ImportSettings
                {
                    PackagePath = packagePath,
                    TargetProjectPath = TargetProjectPath,
                    OverwriteExistingFiles = OverwriteExistingFiles
                };

                // Create package manager if not already created
                if (_packageManager == null)
                {
                    var dummyExportSettings = new ExportSettings
                    {
                        LibraryPath = TargetProjectPath,
                        OutputPath = "",
                        Manifest = new PackageManifest()
                    };
                    _packageManager = new StridePackageManager(dummyExportSettings);
                }

                ProgressValue = 50;

                var result = await _packageManager.ImportPackageAsync(importSettings);
                LastImportResult = result;

                ProgressValue = 100;

                // Display results
                ImportResults.Add($"✓ Package imported successfully!");
                ImportResults.Add($"  Imported to: {result.ImportPath}");
                ImportResults.Add($"  Files imported: {result.TotalFilesImported}");
                
                if (result.CreatedDirectories.Any())
                {
                    ImportResults.Add($"  Created directories: {string.Join(", ", result.CreatedDirectories)}");
                }
                
                if (result.HasConflicts)
                {
                    ImportResults.Add($"  ⚠ Overwritten files: {result.OverwrittenItems.Count}");
                    ImportResults.Add($"  ⚠ Skipped files: {result.SkippedItems.Count}");
                }

                StatusMessage = "Import completed successfully";

                MessageBox.Show($"Package imported successfully!\n\nFiles imported: {result.TotalFilesImported}\nLocation: {result.ImportPath}", 
                    "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ImportResults.Add($"✗ Import failed: {ex.Message}");
                StatusMessage = "Import failed";
                MessageBox.Show($"Import failed: {ex.Message}", "Import Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
                ProgressValue = 0;
            }
        }

        private async Task<string> GetPackagePathAsync()
        {
            switch (SelectedSourceIndex)
            {
                case 0: // Local file
                    return PackageFilePath;

                case 1: // URL
                    if (PackageUrl.EndsWith("stridepackage.json", StringComparison.OrdinalIgnoreCase))
                    {
                        // Download from metadata URL
                        var metadata = await _registry.GetPackageMetadataAsync(PackageUrl);
                        if (metadata != null)
                        {
                            return await _registry.DownloadPackageToCache(metadata);
                        }
                    }
                    else if (PackageUrl.EndsWith(".stridepackage", StringComparison.OrdinalIgnoreCase))
                    {
                        // Download direct package URL
                        return await DownloadPackageFromUrl(PackageUrl);
                    }
                    break;

                case 2: // Registry
                    if (PackageMetadata != null)
                    {
                        return await _registry.DownloadPackageToCache(PackageMetadata);
                    }
                    break;
            }

            return string.Empty;
        }

        private async Task<string> DownloadPackageFromUrl(string url)
        {
            var uri = new Uri(url);
            var filename = Path.GetFileName(uri.LocalPath);
            var tempDir = Path.Combine(Path.GetTempPath(), "HSPacker", "Downloads");
            Directory.CreateDirectory(tempDir);
            var localPath = Path.Combine(tempDir, filename);

            using var client = new HttpClient();
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            await using var fileStream = File.Create(localPath);
            await response.Content.CopyToAsync(fileStream);

            return localPath;
        }

        private void ValidatePackageSource()
        {
            switch (SelectedSourceIndex)
            {
                case 0: // Local file
                    if (string.IsNullOrWhiteSpace(PackageFilePath))
                    {
                        PackageValidationMessage = "Please select a package file";
                        IsPackageValid = false;
                    }
                    else if (!File.Exists(PackageFilePath))
                    {
                        PackageValidationMessage = "Package file does not exist";
                        IsPackageValid = false;
                    }
                    else if (!PackageFilePath.EndsWith(".stridepackage", StringComparison.OrdinalIgnoreCase))
                    {
                        PackageValidationMessage = "File must have .stridepackage extension";
                        IsPackageValid = false;
                    }
                    else
                    {
                        PackageValidationMessage = "✓ Package file selected";
                        IsPackageValid = true;
                    }
                    break;

                case 1: // URL
                case 2: // Registry
                    ResetPackageValidation();
                    break;
            }
        }

        private void ValidateTargetProject()
        {
            if (string.IsNullOrWhiteSpace(TargetProjectPath))
            {
                IsTargetValid = false;
                TargetValidationMessage = "Please select a target project directory";
                return;
            }

            if (!Directory.Exists(TargetProjectPath))
            {
                IsTargetValid = false;
                TargetValidationMessage = "Target directory does not exist";
                return;
            }

            if (!PathHelper.IsStrideProject(TargetProjectPath))
            {
                IsTargetValid = false;
                TargetValidationMessage = "Target directory may not be a Stride project";
                return;
            }

            IsTargetValid = true;
            TargetValidationMessage = "✓ Valid Stride project selected";
        }

        private void ResetPackageValidation()
        {
            IsPackageValid = false;
            PackageValidationMessage = "";
            PackageMetadata = null;
        }

        private bool HasValidPackageSource()
        {
            switch (SelectedSourceIndex)
            {
                case 0: return !string.IsNullOrWhiteSpace(PackageFilePath);
                case 1: return !string.IsNullOrWhiteSpace(PackageUrl);
                case 2: return !string.IsNullOrWhiteSpace(RegistryPackageName);
                default: return false;
            }
        }

        private void UpdateCanImport()
        {
            CanImport = IsTargetValid && IsPackageValid && !IsProcessing;
            
            // Force command reevaluation
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
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
}