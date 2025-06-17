// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HS.Stride.Packer.Core
{
    public class PackageRegistry
    {
        private string? _currentRegistryUrl;
        private readonly HttpClient _httpClient;
        private readonly string? _customCacheDirectory;
        
        // Flow: Registry URL → Registry JSON → Package metadata URLs → Package metadata → Download URLs
        // Registry contains list of stridepackage.json URLs with full package metadata
        
        public PackageRegistry() : this(new HttpClient())
        {
        }
        
        public PackageRegistry(HttpClient httpClient, string? customCacheDirectory = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _customCacheDirectory = customCacheDirectory;
        }
        
        public void SetRegistryUrl(string registryUrl)
        {
            _currentRegistryUrl = registryUrl;
        }
        
        public async Task<RegistryInfo> GetRegistryInfoAsync(string? registryUrl = null)
        {
            var urlToUse = registryUrl ?? _currentRegistryUrl;
            
            if (string.IsNullOrEmpty(urlToUse))
                throw new ArgumentException("Registry URL cannot be empty. Set a registry URL first or provide one as parameter.");
            
            var response = await _httpClient.GetAsync(urlToUse);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var registryInfo = JsonSerializer.Deserialize<RegistryInfo>(content);
            
            return registryInfo ?? new RegistryInfo();
        }
        
        public async Task<List<PackageMetadata>> GetAllPackagesAsync(string? registryUrl = null)
        {
            var registryInfo = await GetRegistryInfoAsync(registryUrl);
            
            // Fetch all package metadata in parallel for much better performance
            var packageTasks = registryInfo.Packages.Select(async packageUrl =>
            {
                try
                {
                    return await GetPackageMetadataAsync(packageUrl);
                }
                catch (Exception)
                {
                    // Skip packages that fail to load
                    return null;
                }
            });
            
            var packageResults = await Task.WhenAll(packageTasks);
            
            return packageResults.Where(p => p != null).Cast<PackageMetadata>().ToList();
        }
        
        public async Task<PackageMetadata?> GetPackageMetadataAsync(string packageMetadataUrl)
        {
            var response = await _httpClient.GetAsync(packageMetadataUrl);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var packageMetadata = JsonSerializer.Deserialize<PackageMetadata>(content);
            
            return packageMetadata;
        }
        
        public async Task<List<PackageMetadata>> SearchPackagesAsync(string query, string? registryUrl = null)
        {
            var allPackages = await GetAllPackagesAsync(registryUrl);
            
            return allPackages.Where(p => 
                p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }
        
        public async Task<List<PackageMetadata>> FilterByTagsAsync(List<string> tags, string? registryUrl = null)
        {
            var allPackages = await GetAllPackagesAsync(registryUrl);
            
            return allPackages.Where(p => 
                tags.Any(tag => p.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            ).ToList();
        }
        
        // Local Package Cache Management
        public string GetCacheDirectory()
        {
            if (!string.IsNullOrEmpty(_customCacheDirectory))
                return _customCacheDirectory;
                
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(documentsPath, "HSPacker");
        }
        
        public async Task<string> DownloadPackageToCache(PackageMetadata package)
        {
            if (string.IsNullOrEmpty(package.DownloadUrl))
                throw new ArgumentException("Package download URL cannot be empty");
            
            var cacheDir = GetCacheDirectory();
            var packageDir = Path.Combine(cacheDir, SanitizeFileName(package.Name));
            
            Directory.CreateDirectory(packageDir);
            
            var packageFileName = $"{SanitizeFileName(package.Name)}.stridepackage";
            var packagePath = Path.Combine(packageDir, packageFileName);
            var metadataPath = Path.Combine(packageDir, "stridepackage.json");
            
            // Download .stridepackage file
            var response = await _httpClient.GetAsync(package.DownloadUrl);
            response.EnsureSuccessStatusCode();
            
            // Download and close file stream before verification
            await using (var fileStream = File.Create(packagePath))
            {
                await response.Content.CopyToAsync(fileStream);
            } // Explicitly close the file stream here
            
            // Small delay to ensure file handles are released
            await Task.Delay(50);
            
            // Verify package integrity after download
            var integrityValid = await VerifyDownloadedPackageAsync(packagePath);
            if (!integrityValid)
            {
                // Clean up the corrupted download
                try
                {
                    if (File.Exists(packagePath))
                        File.Delete(packagePath);
                }
                catch (IOException)
                {
                    // If file is still locked, wait a moment and try again
                    await Task.Delay(100);
                    try
                    {
                        if (File.Exists(packagePath))
                            File.Delete(packagePath);
                    }
                    catch (IOException)
                    {
                        // If still can't delete, leave it for cleanup later
                    }
                }
                    
                throw new InvalidOperationException($"Downloaded package '{package.Name}' failed integrity verification. The download may be corrupted.");
            }
            
            // Save metadata
            var metadataJson = JsonSerializer.Serialize(package, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(metadataPath, metadataJson);
            
            return packagePath;
        }
        
        public async Task<List<CachedPackage>> GetCachedPackages()
        {
            var cacheDir = GetCacheDirectory();
            var cachedPackages = new List<CachedPackage>();
            
            if (!Directory.Exists(cacheDir))
                return cachedPackages;
            
            var packageDirs = Directory.GetDirectories(cacheDir);
            
            foreach (var packageDir in packageDirs)
            {
                var metadataPath = Path.Combine(packageDir, "stridepackage.json");
                if (!File.Exists(metadataPath))
                    continue;
                
                try
                {
                    var metadataJson = await File.ReadAllTextAsync(metadataPath);
                    var package = JsonSerializer.Deserialize<PackageMetadata>(metadataJson);
                    
                    if (package != null)
                    {
                        var packageFileName = $"{SanitizeFileName(package.Name)}.stridepackage";
                        var packagePath = Path.Combine(packageDir, packageFileName);
                        
                        if (File.Exists(packagePath))
                        {
                            var fileInfo = new FileInfo(packagePath);
                            cachedPackages.Add(new CachedPackage
                            {
                                Metadata = package,
                                CachedPath = packagePath,
                                CachedDate = fileInfo.CreationTime,
                                Size = fileInfo.Length
                            });
                        }
                    }
                }
                catch (Exception)
                {
                    // Skip corrupted cache entries
                    continue;
                }
            }
            
            return cachedPackages.OrderByDescending(p => p.CachedDate).ToList();
        }
        
        public bool IsPackageCached(PackageMetadata package)
        {
            var cacheDir = GetCacheDirectory();
            var packageDir = Path.Combine(cacheDir, SanitizeFileName(package.Name));
            var packageFileName = $"{SanitizeFileName(package.Name)}.stridepackage";
            var packagePath = Path.Combine(packageDir, packageFileName);
            
            return File.Exists(packagePath);
        }
        
        public string? GetCachedPackagePath(PackageMetadata package)
        {
            var cacheDir = GetCacheDirectory();
            var packageDir = Path.Combine(cacheDir, SanitizeFileName(package.Name));
            var packageFileName = $"{SanitizeFileName(package.Name)}.stridepackage";
            var packagePath = Path.Combine(packageDir, packageFileName);
            
            return File.Exists(packagePath) ? packagePath : null;
        }
        
        public async Task<bool> ClearPackageCache(PackageMetadata package)
        {
            var cacheDir = GetCacheDirectory();
            var packageDir = Path.Combine(cacheDir, SanitizeFileName(package.Name));
            
            if (Directory.Exists(packageDir))
            {
                try
                {
                    Directory.Delete(packageDir, true);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            
            return false;
        }
        
        private static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return invalidChars.Aggregate(fileName, (current, c) => current.Replace(c, '_'));
        }
        
        private async Task<bool> VerifyDownloadedPackageAsync(string packagePath)
        {
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
                var manifest = JsonSerializer.Deserialize<PackageManifest>(manifestJson);

                if (manifest == null || string.IsNullOrEmpty(manifest.PackageHash))
                {
                    return false; // No hash means we can't verify
                }

                // Generate SHA-256 hash of all files except manifest.json
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var allFiles = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories)
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

                return string.Equals(computedHash, manifest.PackageHash, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
        
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
    
    public class RegistryInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
        
        [JsonPropertyName("updated")]
        public string Updated { get; set; } = DateTime.UtcNow.ToString("MM-dd-yyyy");
        
        [JsonPropertyName("packages")]
        public List<string> Packages { get; set; } = new();
    }
    
    public class PackageMetadata
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
        
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
        
        [JsonPropertyName("author")]
        public string Author { get; set; } = string.Empty;
        
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();
        
        [JsonPropertyName("stride_version")]
        public string StrideVersion { get; set; } = string.Empty;
        
        [JsonPropertyName("created")]
        public DateTime Created { get; set; } = DateTime.UtcNow;
        
        [JsonPropertyName("download_url")]
        public string DownloadUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("homepage")]
        public string Homepage { get; set; } = string.Empty;
        
        [JsonPropertyName("repository")]
        public string Repository { get; set; } = string.Empty;
        
        [JsonPropertyName("license")]
        public string License { get; set; } = string.Empty;
    }
    
    public class CachedPackage
    {
        public PackageMetadata Metadata { get; set; } = new();
        public string CachedPath { get; set; } = string.Empty;
        public DateTime CachedDate { get; set; } = DateTime.UtcNow;
        public long Size { get; set; }
        
        public string DisplaySize => FormatBytes(Size);
        
        private static string FormatBytes(long bytes)
        {
            return bytes switch
            {
                >= 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB",
                >= 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
                >= 1024 => $"{bytes / 1024.0:F1} KB",
                _ => $"{bytes} bytes"
            };
        }
    }
}