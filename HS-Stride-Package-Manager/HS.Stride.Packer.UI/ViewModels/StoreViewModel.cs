// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HS.Stride.Packer.Core;
using HS.Stride.Packer.Utilities;
using HS.Stride.Packer.UI.Services;
using System.IO;
using System.Net.Http;

namespace HS.Stride.Packer.UI.ViewModels
{
    public class StoreViewModel : INotifyPropertyChanged
    {
        // Backend Services
        private readonly PackageRegistry _packageRegistry;
        private readonly StridePackageManager _packageManager;
        private readonly SettingsManager _settingsManager;
        
        // Package Collections
        private ObservableCollection<PackageMetadata> _packages = new();
        private ObservableCollection<PackageMetadata> _filteredPackages = new();
        private ObservableCollection<CachedPackage> _cachedPackages = new();
        private ObservableCollection<string> _categories = new();
        
        // Search and Filter
        private string _searchQuery = "";
        private string _selectedCategory = "All Categories";
        private string _sortBy = "Name A-Z";

        // Direct URL Installation
        private string _directUrl = "";
        // Registry Status
        private string _registryStatus = "Connecting to registry...";
        private int _packageCount = 0;
        private string _lastUpdated = "";
        private bool _isConnected = false;
        
        // Registry Configuration
        private string _registryUrl = "";
        private bool _isEditingRegistry = false;
        
        // UI State
        private bool _isLoading = false;
        private bool _isInstalling = false;
        private string _statusMessage = "Ready";
        private double _progressValue = 0;
        
        public StoreViewModel()
        {
            // Initialize settings manager and load saved settings
            _settingsManager = new SettingsManager();
            _registryUrl = _settingsManager.Settings.RegistryUrl;
            
            // Initialize backend services
            _packageRegistry = new PackageRegistry();
            _packageRegistry.SetRegistryUrl(_registryUrl);
            
            // Create default export settings for StridePackageManager
            var exportSettings = new ExportSettings();
            _packageManager = new StridePackageManager(exportSettings);
            
            // Initialize commands
            RefreshCommand = new RelayCommand(async () => await RefreshPackagesAsync());
            SearchCommand = new RelayCommand(async () => await SearchPackagesAsync());
            InstallFromUrlCommand = new RelayCommand(async () => await InstallFromUrlAsync(), () => !IsInstalling && !string.IsNullOrWhiteSpace(DirectUrl));
            InstallPackageCommand = new RelayCommand<PackageMetadata>(async (package) => await InstallPackageAsync(package), (package) => package != null && !IsInstalling);
            DownloadPackageCommand = new RelayCommand<PackageMetadata>(async (package) => await DownloadPackageAsync(package), (package) => package != null && !IsInstalling);
            ViewPackageCommand = new RelayCommand<PackageMetadata>((package) => ViewPackage(package), (package) => package != null);
            ChangeSortCommand = new RelayCommand<string>((sortType) => ChangeSortOrder(sortType));
            ChangeCategoryCommand = new RelayCommand<string>((category) => ChangeCategory(category));
            ToggleEditRegistryCommand = new RelayCommand(() => ToggleEditRegistry());
            SaveRegistryCommand = new RelayCommand(async () => await SaveRegistryUrlAsync(), () => !IsLoading && !string.IsNullOrWhiteSpace(RegistryUrl));
            ResetRegistryCommand = new RelayCommand(() => ResetRegistryToDefault());
            
            // Initialize categories
            Categories.Add("All Categories");
            
            // Load packages on startup
            _ = Task.Run(async () => await RefreshPackagesAsync());
        }
        
        #region Properties
        
        public ObservableCollection<PackageMetadata> Packages
        {
            get => _packages;
            set { _packages = value; OnPropertyChanged(); }
        }
        
        public ObservableCollection<PackageMetadata> FilteredPackages
        {
            get => _filteredPackages;
            set { _filteredPackages = value; OnPropertyChanged(); }
        }
        
        public ObservableCollection<CachedPackage> CachedPackages
        {
            get => _cachedPackages;
            set { _cachedPackages = value; OnPropertyChanged(); }
        }
        
        public ObservableCollection<string> Categories
        {
            get => _categories;
            set { _categories = value; OnPropertyChanged(); }
        }
        
        public string SearchQuery
        {
            get => _searchQuery;
            set 
            { 
                _searchQuery = value; 
                OnPropertyChanged();
                _ = Task.Run(async () => await SearchPackagesAsync());
            }
        }
        
        public string SelectedCategory
        {
            get => _selectedCategory;
            set 
            { 
                _selectedCategory = value; 
                OnPropertyChanged();
                FilterPackagesByCategory();
            }
        }
        
        public string SortBy
        {
            get => _sortBy;
            set 
            { 
                _sortBy = value; 
                OnPropertyChanged();
                SortPackages();
            }
        }
        
        public string DirectUrl
        {
            get => _directUrl;
            set 
            { 
                _directUrl = value; 
                OnPropertyChanged();
                ((RelayCommand)InstallFromUrlCommand).RaiseCanExecuteChanged();
            }
        }
        
        public string RegistryStatus
        {
            get => _registryStatus;
            set { _registryStatus = value; OnPropertyChanged(); }
        }
        
        public int PackageCount
        {
            get => _packageCount;
            set { _packageCount = value; OnPropertyChanged(); }
        }
        
        public string LastUpdated
        {
            get => _lastUpdated;
            set { _lastUpdated = value; OnPropertyChanged(); }
        }
        
        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(); }
        }
        
        public string RegistryUrl
        {
            get => _registryUrl;
            set 
            { 
                _registryUrl = value; 
                OnPropertyChanged();
                ((RelayCommand)SaveRegistryCommand).RaiseCanExecuteChanged();
            }
        }
        
        public bool IsEditingRegistry
        {
            get => _isEditingRegistry;
            set { _isEditingRegistry = value; OnPropertyChanged(); }
        }
        
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }
        
        public bool IsInstalling
        {
            get => _isInstalling;
            set 
            { 
                _isInstalling = value; 
                OnPropertyChanged();
                ((RelayCommand)InstallFromUrlCommand).RaiseCanExecuteChanged();
                ((RelayCommand<PackageMetadata>)InstallPackageCommand).RaiseCanExecuteChanged();
                ((RelayCommand<PackageMetadata>)DownloadPackageCommand).RaiseCanExecuteChanged();
            }
        }
        
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }
        
        public double ProgressValue
        {
            get => _progressValue;
            set { _progressValue = value; OnPropertyChanged(); }
        }
        
        #endregion
        
        #region Commands
        
        public ICommand RefreshCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand InstallFromUrlCommand { get; }
        public ICommand InstallPackageCommand { get; }
        public ICommand DownloadPackageCommand { get; }
        public ICommand ViewPackageCommand { get; }
        public ICommand ChangeSortCommand { get; }
        public ICommand ChangeCategoryCommand { get; }
        public ICommand ToggleEditRegistryCommand { get; }
        public ICommand SaveRegistryCommand { get; }
        public ICommand ResetRegistryCommand { get; }
        
        #endregion
        
        #region Methods
        
        private async Task RefreshPackagesAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading packages from registry...";
                ProgressValue = 0;
                
                // Get registry info
                var registryInfo = await _packageRegistry.GetRegistryInfoAsync();
                RegistryStatus = $"✅ Connected to {registryInfo.Name}";
                LastUpdated = registryInfo.Updated;
                IsConnected = true;
                ProgressValue = 25;
                
                // Load all packages
                var packages = await _packageRegistry.GetAllPackagesAsync();
                ProgressValue = 75;
                
                // Update UI on main thread
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Packages.Clear();
                    foreach (var package in packages)
                    {
                        Packages.Add(package);
                    }
                    
                    PackageCount = packages.Count;
                    
                    // Update categories
                    UpdateCategories();
                    
                    FilteredPackages.Clear();
                    foreach (var package in Packages)
                    {
                        FilteredPackages.Add(package);
                    }
                    
                    // Apply the default sort order to the initially populated list
                    SortPackages(); 
                    
                    StatusMessage = $"Loaded {PackageCount} packages. Displaying: {FilteredPackages.Count}";
                    ProgressValue = 100;
                });
                
                // Load cached packages
                await LoadCachedPackagesAsync();
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    RegistryStatus = "❌ Failed to connect to registry";
                    StatusMessage = $"Error: {ex.Message}";
                    IsConnected = false;
                });
            }
            finally
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    IsLoading = false;
                    ProgressValue = 0;
                });
            }
        }
        
        private async Task SearchPackagesAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                FilterPackagesByCategory();
                return;
            }
            
            try
            {
                StatusMessage = "Searching packages...";
                
                var searchResults = await _packageRegistry.SearchPackagesAsync(SearchQuery);
                
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    FilteredPackages.Clear();
                    foreach (var package in searchResults)
                    {
                        if (SelectedCategory == "All Categories" || package.Tags.Contains(SelectedCategory, StringComparer.OrdinalIgnoreCase))
                        {
                            FilteredPackages.Add(package);
                        }
                    }
                    
                    SortPackages();
                    StatusMessage = $"Found {FilteredPackages.Count} packages";
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Search error: {ex.Message}";
            }
        }
        
        private async Task InstallFromUrlAsync()
        {
            try
            {
                IsInstalling = true;
                StatusMessage = "Downloading package from URL...";
                
                // Download the package first
                string downloadedFilePath = await DownloadPackageFromUrl(DirectUrl);
                
                if (!string.IsNullOrEmpty(downloadedFilePath))
                {
                    // Navigate to Import tab and set the downloaded file path
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        NavigateToImportTabWithFile(downloadedFilePath);
                    });
                    
                    StatusMessage = "Package downloaded - switched to Import tab";
                }
                else
                {
                    StatusMessage = "Download failed";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Download error: {ex.Message}";
            }
            finally
            {
                IsInstalling = false;
            }
        }
        
        private async Task InstallPackageAsync(PackageMetadata package)
        {
            try
            {
                IsInstalling = true;
                StatusMessage = $"Installing {package.Name}...";
                
                // First download to cache
                await DownloadPackageAsync(package);
                
                // Find the cached package file
                var cachedPackages = await _packageRegistry.GetCachedPackages();
                var cachedPackage = cachedPackages.FirstOrDefault(cp => cp.Metadata.Name == package.Name && cp.Metadata.Version == package.Version);
                
                if (cachedPackage != null)
                {
                    // Navigate to Import tab and set the package file
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        NavigateToImportTab(cachedPackage.CachedPath);
                    });
                    
                    StatusMessage = $"{package.Name} ready for installation";
                }
                else
                {
                    StatusMessage = $"{package.Name} downloaded but not found in cache";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Installation error: {ex.Message}";
            }
            finally
            {
                IsInstalling = false;
            }
        }
        
        private async Task DownloadPackageAsync(PackageMetadata package)
        {
            try
            {
                StatusMessage = $"Downloading {package.Name}...";
                
                await _packageRegistry.DownloadPackageToCache(package);
                
                // Refresh cached packages
                await LoadCachedPackagesAsync();
                
                StatusMessage = $"{package.Name} downloaded to cache";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Download error: {ex.Message}";
            }
        }
        
        private void ViewPackage(PackageMetadata package)
        {
            if (!string.IsNullOrEmpty(package.Homepage))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = package.Homepage,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Could not open homepage: {ex.Message}";
                }
            }
        }
        
        private void ChangeSortOrder(string sortType)
        {
            SortBy = sortType;
        }
        
        private void ChangeCategory(string category)
        {
            SelectedCategory = category;
        }
        
        private void FilterPackagesByCategory()
        {
            FilteredPackages.Clear();
            
            var packagesToFilter = string.IsNullOrWhiteSpace(SearchQuery) ? Packages : Packages.Where(p => 
                p.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                p.Tags.Any(t => t.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
            );
            
            foreach (var package in packagesToFilter)
            {
                if (SelectedCategory == "All Categories" || package.Tags.Contains(SelectedCategory, StringComparer.OrdinalIgnoreCase))
                {
                    FilteredPackages.Add(package);
                }
            }
            
            SortPackages();
        }
        
        private void SortPackages()
        {
            var sorted = SortBy switch
            {
                "Name A-Z" => FilteredPackages.OrderBy(p => p.Name),
                "Size" => FilteredPackages.OrderBy(p => p.Name), // PackageMetadata doesn't have Size, sort by name instead
                "Newest" => FilteredPackages.OrderByDescending(p => p.Created),
                _ => FilteredPackages.AsEnumerable()
            };
            
            var sortedList = sorted.ToList();
            FilteredPackages.Clear();
            foreach (var package in sortedList)
            {
                FilteredPackages.Add(package);
            }
        }
        
        private void UpdateCategories()
        {
            var allTags = Packages.SelectMany(p => p.Tags).Distinct().OrderBy(t => t).ToList();
            
            Categories.Clear();
            Categories.Add("All Categories");
            foreach (var tag in allTags)
            {
                Categories.Add(tag);
            }
        }
        
        private async Task LoadCachedPackagesAsync()
        {
            try
            {
                var cached = await _packageRegistry.GetCachedPackages();
                
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    CachedPackages.Clear();
                    foreach (var package in cached)
                    {
                        CachedPackages.Add(package);
                    }
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading cached packages: {ex.Message}";
            }
        }
        
        private void NavigateToImportTab(string packageFilePath)
        {
            // Find the MainWindow and navigate to Import tab
            var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                // Switch to Import tab (index 1)
                var tabControl = mainWindow.FindName("MainTabControl") as System.Windows.Controls.TabControl;
                if (tabControl != null)
                {
                    tabControl.SelectedIndex = 1;
                    
                    // Update button styles
                    var method = mainWindow.GetType().GetMethod("SwitchTab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    method?.Invoke(mainWindow, new object[] { 1 });
                    
                    // Find the ImportView and set the package file path
                    var importTabItem = tabControl.Items[1] as System.Windows.Controls.TabItem;
                    if (importTabItem?.Content is Views.ImportView importView && importView.DataContext is ImportViewModel importViewModel)
                    {
                        importViewModel.PackageFilePath = packageFilePath;
                        importViewModel.SelectedSourceIndex = 0; // Set to local file source
                    }
                }
            }
        }
        
        private void NavigateToImportTabWithFile(string packageFilePath)
        {
            // Find the MainWindow and navigate to Import tab
            var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                // Switch to Import tab (index 1)
                var tabControl = mainWindow.FindName("MainTabControl") as System.Windows.Controls.TabControl;
                if (tabControl != null)
                {
                    tabControl.SelectedIndex = 1;
                    
                    // Update button styles
                    var method = mainWindow.GetType().GetMethod("SwitchTab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    method?.Invoke(mainWindow, new object[] { 1 });
                    
                    // Find the ImportView and set the package file path
                    var importTabItem = tabControl.Items[1] as System.Windows.Controls.TabItem;
                    if (importTabItem?.Content is Views.ImportView importView && importView.DataContext is ImportViewModel importViewModel)
                    {
                        importViewModel.PackageFilePath = packageFilePath;
                        importViewModel.SelectedSourceIndex = 0; // Set to local file source
                    }
                }
            }
        }

        private async Task<string> DownloadPackageFromUrl(string url)
        {
            try
            {
                if (url.EndsWith("stridepackage.json", StringComparison.OrdinalIgnoreCase))
                {
                    // Handle stridepackage.json metadata URL
                    var packageMetadata = await _packageRegistry.GetPackageMetadataAsync(url);
                    if (packageMetadata == null || string.IsNullOrEmpty(packageMetadata.DownloadUrl))
                    {
                        return string.Empty;
                    }
                    
                    // Download the actual package file
                    return await _packageRegistry.DownloadPackageToCache(packageMetadata);
                }
                else if (url.EndsWith(".stridepackage", StringComparison.OrdinalIgnoreCase))
                {
                    // Handle direct .stridepackage URL - download to temp location
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
                else
                {
                    // Invalid URL format
                    return string.Empty;
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
        
        private void ToggleEditRegistry()
        {
            IsEditingRegistry = !IsEditingRegistry;
            if (!IsEditingRegistry)
            {
                // Reset to current registry URL if cancelled
                RegistryUrl = _packageRegistry.GetType().GetField("_currentRegistryUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_packageRegistry) as string ?? RegistryUrl;
            }
        }
        
        private async Task SaveRegistryUrlAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(RegistryUrl))
                {
                    StatusMessage = "Registry URL cannot be empty";
                    return;
                }
                
                StatusMessage = "Updating registry...";
                
                // Update the registry URL
                _packageRegistry.SetRegistryUrl(RegistryUrl);
                
                // Exit edit mode
                IsEditingRegistry = false;
                
                // Refresh packages with new registry
                await RefreshPackagesAsync();
                
                // Save to persistent settings
                _settingsManager.Settings.RegistryUrl = RegistryUrl;
                _settingsManager.SaveSettings();
                
                StatusMessage = "Registry updated and saved";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to update registry: {ex.Message}";
                IsEditingRegistry = true; // Stay in edit mode
            }
        }
        
        private void ResetRegistryToDefault()
        {
            RegistryUrl = AppSettings.DefaultRegistryUrl;
        }
        
        #endregion
        
        #region INotifyPropertyChanged
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion
    }
}
