// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using System.IO.Compression;
using System.Text.Json;

namespace HS.Stride.Packer.Core
{
    public class StridePackageManager
    {
        public static readonly string DefaultRegistryUrl = "https://raw.githubusercontent.com/Keepsie/HS-Stride-Packer/main/stride_registry.json";
        
        private readonly ResourcePathValidator _resourceValidator;
        private readonly NamespaceScanner _namespaceScanner;
        private readonly PackageExporter _packageExporter;
        private readonly PackageImporter _packageImporter;
        private readonly ProjectScanner _projectScanner;
        private readonly PackageRegistry _packageRegistry;
        private readonly ExportSettings _exportSettings;
        
        public StridePackageManager(ExportSettings exportSettings)
        {
            if (exportSettings == null) throw new NullReferenceException("null export settings");
            
            _exportSettings = exportSettings;
            _namespaceScanner = new NamespaceScanner();
            _resourceValidator = new ResourcePathValidator();
            _projectScanner = new ProjectScanner();
            _packageExporter = new PackageExporter(_resourceValidator, _namespaceScanner);
            _packageImporter = new PackageImporter();
            _packageRegistry = new PackageRegistry();
        }
        
        //Validations
        public async Task<ValidationResult> ValidateForExportAsync(string libraryPath)
        {
            return await Task.Run(() =>
            {
                if (!Directory.Exists(libraryPath))
                {
                    return new ValidationResult
                    {
                        Errors = { $"Library path does not exist: {libraryPath}" }
                    };
                }

                // Only validate the SELECTED asset folders, not the entire project
                // This prevents errors from unrelated assets the user didn't select
                var selectedFolders = _exportSettings?.SelectedAssetFolders ?? new List<string>();

                ValidationResult validation;
                if (selectedFolders.Any())
                {
                    // Validate only selected folders using the robust resource finder
                    validation = _resourceValidator.ValidateAndCollectDependencies(libraryPath, selectedFolders);
                }
                else
                {
                    // No folders selected - just return empty validation with warning
                    validation = new ValidationResult();
                    validation.Warnings.Add("No asset folders selected");
                }

                // Check if any Stride files exist in selected folders
                var hasStrideFiles = selectedFolders.Any(folder =>
                {
                    var fullPath = Path.Combine(libraryPath, folder);
                    return Directory.Exists(fullPath) &&
                           Directory.GetFiles(fullPath, "*.sd*", SearchOption.AllDirectories).Any();
                });

                if (!hasStrideFiles && selectedFolders.Any())
                {
                    validation.Warnings.Add("No Stride asset files found in the selected folders");
                }

                return validation;
            });
        }
        
        public async Task<ValidationResult> ValidateForImportAsync(string packagePath, string targetProjectPath)
        {
            return await Task.Run(() =>
            {
                var result = new ValidationResult();
                
                if (!File.Exists(packagePath))
                {
                    result.Errors.Add($"Package file does not exist: {packagePath}");
                }
                
                if (!Directory.Exists(targetProjectPath))
                {
                    result.Errors.Add($"Target project path does not exist: {targetProjectPath}");
                }
                
                // Only check for .sdpkg files if target directory exists
                if (Directory.Exists(targetProjectPath))
                {
                    var packageFiles = Directory.GetFiles(targetProjectPath, "*.sdpkg", SearchOption.AllDirectories);
                    if (!packageFiles.Any())
                    {
                        result.Warnings.Add("No Stride package (.sdpkg) files found - this may not be a Stride project");
                    }
                }
                
                return result;
            });
        }
        
        
        //Scanning
        public ProjectScanResult ScanProject(string projectPath)
        {
            return _projectScanner.ScanProject(projectPath);
        }
        
        
        //Create
        public async Task<string> CreatePackageAsync()
        {
            if (string.IsNullOrEmpty(_exportSettings.LibraryPath))
                throw new ArgumentException("Library path cannot be empty", nameof(_exportSettings));
            
            var validation = await ValidateForExportAsync(_exportSettings.LibraryPath);
            if (validation.HasCriticalIssues)
            {
                throw new InvalidOperationException($"Cannot create package due to validation errors:\n{validation.GetReport()}");
            }
            
            return await _packageExporter.ExportPackageAsync(_exportSettings);
        }
        
        
        //Download
        public async Task<string> DownloadPackageAsync(string url)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentException("URL cannot be empty", nameof(url));

            var downloadFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            downloadFolder = Path.Combine(downloadFolder, "Downloads");
            Directory.CreateDirectory(downloadFolder);

            using var httpClient = new HttpClient();
            
            var fileName = Path.GetFileName(new Uri(url).LocalPath);
            if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".stridepackage"))
            {
                fileName = $"package_{DateTime.Now:yyyyMMdd_HHmmss}.stridepackage";
            }

            var filePath = Path.Combine(downloadFolder, fileName);
            
            using var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            await using var fileStream = File.Create(filePath);
            await response.Content.CopyToAsync(fileStream);
            
            return filePath;
        }

        public async Task<bool> VerifyPackageIntegrityAsync(string packagePath)
        {
            if (!File.Exists(packagePath))
                throw new FileNotFoundException($"Package file not found: {packagePath}");

            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                // Extract package to temp directory
                Directory.CreateDirectory(tempDir);
                System.IO.Compression.ZipFile.ExtractToDirectory(packagePath, tempDir);

                // Read package manifest
                var manifestPath = Path.Combine(tempDir, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    return false; // No manifest means we can't verify
                }

                var manifestJson = await File.ReadAllTextAsync(manifestPath);
                var manifest = System.Text.Json.JsonSerializer.Deserialize<PackageManifest>(manifestJson);

                if (manifest == null || string.IsNullOrEmpty(manifest.PackageHash))
                {
                    return false; // No hash means we can't verify
                }

                // Verify integrity using the same logic as PackageImporter
                return await VerifyPackageHashAsync(tempDir, manifest.PackageHash);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        private async Task<bool> VerifyPackageHashAsync(string extractedDir, string expectedHash)
        {
            try
            {
                // Generate SHA-256 hash of all files except manifest.json (same as export)
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var allFiles = Directory.GetFiles(extractedDir, "*", SearchOption.AllDirectories)
                    .Where(f => !Path.GetFileName(f).Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f)
                    .ToList();

                foreach (var file in allFiles)
                {
                    var fileBytes = await File.ReadAllBytesAsync(file);
                    sha256.TransformBlock(fileBytes, 0, fileBytes.Length, null, 0);
                }

                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                var computedHash = Convert.ToHexString(sha256.Hash ?? Array.Empty<byte>());

                return string.Equals(computedHash, expectedHash, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        
        //Registry
        public void SetRegistryUrl(string registryUrl)
        {
            _packageRegistry.SetRegistryUrl(registryUrl);
        }
        
        public async Task<List<PackageMetadata>> GetRegistryPackagesAsync(string? registryUrl = null)
        {
            return await _packageRegistry.GetAllPackagesAsync(registryUrl);
        }
        
        public async Task<RegistryInfo> GetRegistryInfoAsync(string? registryUrl = null)
        {
            return await _packageRegistry.GetRegistryInfoAsync(registryUrl);
        }
        
        public async Task<List<PackageMetadata>> SearchPackagesAsync(string query, string? registryUrl = null)
        {
            return await _packageRegistry.SearchPackagesAsync(query, registryUrl);
        }
        
        public async Task<List<PackageMetadata>> FilterPackagesByTagsAsync(List<string> tags, string? registryUrl = null)
        {
            return await _packageRegistry.FilterByTagsAsync(tags, registryUrl);
        }
        
        
        // Package Cache Management - UI Methods
        public async Task<string> DownloadPackageToCacheAsync(PackageMetadata package)
        {
            return await _packageRegistry.DownloadPackageToCache(package);
        }
        
        public async Task<List<CachedPackage>> GetCachedPackagesAsync()
        {
            return await _packageRegistry.GetCachedPackages();
        }
        
        public bool IsPackageCached(PackageMetadata package)
        {
            return _packageRegistry.IsPackageCached(package);
        }
        
        public string? GetCachedPackagePath(PackageMetadata package)
        {
            return _packageRegistry.GetCachedPackagePath(package);
        }
        
        public async Task<ImportResult> InstallCachedPackageAsync(PackageMetadata package, string targetProjectPath)
        {
            var cachedPath = _packageRegistry.GetCachedPackagePath(package);
            if (cachedPath == null)
                throw new InvalidOperationException($"Package '{package.Name}' is not cached. Download it first.");
            
            var importSettings = new ImportSettings
            {
                PackagePath = cachedPath,
                TargetProjectPath = targetProjectPath
            };
            
            return await _packageImporter.ImportPackageAsync(importSettings);
        }
        
        public async Task<bool> ClearPackageCacheAsync(PackageMetadata package)
        {
            return await _packageRegistry.ClearPackageCache(package);
        }
        
        public string GetCacheDirectory()
        {
            return _packageRegistry.GetCacheDirectory();
        }

        
        //Import
        public async Task<ImportResult> ImportPackageAsync(ImportSettings importSettings)
        {
            return await _packageImporter.ImportPackageAsync(importSettings);
        }
        
        public ValidationResult ValidateImportSettings(ImportSettings importSettings)
        {
            return _packageImporter.ValidateSettings(importSettings);
        }
        
    }
}
