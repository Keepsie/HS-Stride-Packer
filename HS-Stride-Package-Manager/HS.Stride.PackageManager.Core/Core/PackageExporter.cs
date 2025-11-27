// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using System.IO.Compression;
using System.Text.Json;
using HS.Stride.Packer.Utilities;

namespace HS.Stride.Packer.Core
{
    public class PackageExporter
    {
        private readonly ResourcePathValidator _resourceValidator;
        private readonly NamespaceScanner _namespaceScanner;
        
        public PackageExporter(ResourcePathValidator resourceValidator, NamespaceScanner namespaceScanner)
        {
            _resourceValidator = resourceValidator;
            _namespaceScanner = namespaceScanner;
        }

        
        //Public API
        public async Task<string> ExportPackageAsync(ExportSettings settings)
        {
            
            //Validate Settings
            var validation = ValidateSettings(settings);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException($"Invalid export settings: {string.Join(", ", validation.Errors)}");
            }

            //Validate Resources and collect dependencies
            var resourceValidation = _resourceValidator.ValidateProject(settings.LibraryPath);
            if (resourceValidation.HasCriticalIssues)
            {
                    throw new InvalidOperationException($"Resource validation failed:\n{resourceValidation.GetReport()}");
            }
            
            // Scan for resource dependencies and set target resource path
            var dependencyResult = _resourceValidator.ValidateAndCollectDependencies(settings.LibraryPath, settings.SelectedAssetFolders);
            settings.ResourceDependencies = dependencyResult.ResourceDependencies;
            
            // Use package name (not project folder name) for resource organization
            var packageName = settings.Manifest.Name;
            settings.TargetResourcePath = $"Resources/{packageName}";
            
            //Scan all namespaces in .sd stuff in assets
            var namespaces = _namespaceScanner.ScanDirectory(settings.LibraryPath, settings.ExcludeNamespaces);
            settings.Manifest.Namespaces = namespaces;
            
            
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                Directory.CreateDirectory(tempDir);
                
                // Copy user structure and process .sd files to remove excluded namespaces
                await ProcessAndCopyUserStructureAsync(settings.LibraryPath, tempDir, settings);
                
                // Create our JSON manifest
                await CreateJsonManifestAsync(settings, tempDir);
                
                // Create the package
                if (File.Exists(settings.OutputPath))
                    File.Delete(settings.OutputPath);
                    
                ZipFile.CreateFromDirectory(tempDir, settings.OutputPath);
                
                // Create registry JSON file alongside the package if requested
                if (settings.ExportRegistryJson)
                {
                    var outputDirectory = Path.GetDirectoryName(settings.OutputPath) ?? "";
                    await CreateRegistryJsonAsync(settings, outputDirectory);
                }
                
                return settings.OutputPath;
            }
            finally
            {
                //Clean up our crap
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
        
        public ValidationResult ValidateSettings(ExportSettings settings)
        {
            var result = new ValidationResult();

            if (string.IsNullOrEmpty(settings.Manifest.Name))
            {
                result.Errors.Add("Package name is required");
            }

            if (string.IsNullOrEmpty(settings.Manifest.Version))
            {
                result.Errors.Add("Package version is required");
            }

            if (!Directory.Exists(settings.LibraryPath))
            {
                result.Errors.Add($"Library path does not exist: {settings.LibraryPath}");
            }

            // Validate output path can be generated
            var outputPath = settings.OutputPath ?? Path.Combine(
                Path.GetDirectoryName(settings.LibraryPath) ?? "",
                PathHelper.MakePackageFileName(settings.Manifest.Name, settings.Manifest.Version));
                
            if (string.IsNullOrEmpty(outputPath) || string.IsNullOrEmpty(Path.GetDirectoryName(outputPath)))
            {
                result.Errors.Add("Cannot generate valid output path for package");
            }

            return result;
        }
        
        public List<AssetFolderInfo> ScanForAssetFolders(string projectPath)
        {
            var assetFolders = new List<AssetFolderInfo>();
            
            // Check root level Assets/ (template structure)
            var rootAssetsPath = Path.Combine(projectPath, "Assets");
            if (Directory.Exists(rootAssetsPath))
            {
                var rootFolders = Directory.GetDirectories(rootAssetsPath, "*", SearchOption.TopDirectoryOnly)
                    .Select(path => new AssetFolderInfo
                    {
                        Name = Path.GetFileName(path),
                        FullPath = path,
                        RelativePath = $"Assets/{Path.GetFileName(path)}",
                        FileCount = Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length,
                        Location = "Root"
                    })
                    .Where(folder => folder.FileCount > 0)
                    .ToList();
                
                assetFolders.AddRange(rootFolders);
            }
            
            // Check for ProjectName/Assets/ (fresh structure)
            var projectName = Path.GetFileName(projectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var nestedAssetsPath = Path.Combine(projectPath, projectName, "Assets");
            if (Directory.Exists(nestedAssetsPath))
            {
                var nestedFolders = Directory.GetDirectories(nestedAssetsPath, "*", SearchOption.TopDirectoryOnly)
                    .Select(path => new AssetFolderInfo
                    {
                        Name = Path.GetFileName(path),
                        FullPath = path,
                        RelativePath = $"{projectName}/Assets/{Path.GetFileName(path)}",
                        FileCount = Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length,
                        Location = "Nested"
                    })
                    .Where(folder => folder.FileCount > 0)
                    .ToList();
                
                assetFolders.AddRange(nestedFolders);
            }
            
            return assetFolders;
        }

        public List<CodeFolderInfo> ScanForCodeFolders(string projectPath)
        {
            var projectName = Path.GetFileName(projectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var codeFolders = new List<CodeFolderInfo>();
            
            // Look for .Game folder (template structure)
            var gameFolder = Directory.GetDirectories(projectPath, "*", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(dir => Path.GetFileName(dir).EndsWith(".Game"));
                
            if (gameFolder != null)
            {
                var subFolders = GetCodeSubFolders(gameFolder);
                if (subFolders.Any())
                {
                    codeFolders.Add(new CodeFolderInfo 
                    { 
                        Name = Path.GetFileName(gameFolder), 
                        Path = gameFolder, 
                        Type = "Game (Shared Code - Template)",
                        SubFolders = subFolders
                    });
                }
            }
            
            // Look for ProjectName/ folder (fresh structure)
            var projectFolder = Path.Combine(projectPath, projectName);
            if (Directory.Exists(projectFolder) && gameFolder == null) // Only if no .Game folder exists
            {
                var subFolders = GetCodeSubFolders(projectFolder);
                if (subFolders.Any())
                {
                    codeFolders.Add(new CodeFolderInfo 
                    { 
                        Name = projectName, 
                        Path = projectFolder, 
                        Type = "Game (Shared Code - Fresh)",
                        SubFolders = subFolders
                    });
                }
            }
            
            // Platform folders are always at root level in both structures
            var platformFolders = Directory.GetDirectories(projectPath, "*", SearchOption.TopDirectoryOnly)
                .Where(dir => 
                {
                    var name = Path.GetFileName(dir);
                    return name.EndsWith(".Windows") || name.EndsWith(".Mac") || name.EndsWith(".Linux") || 
                           name.EndsWith(".iOS") || name.EndsWith(".Android") || name.EndsWith(".UWP");
                })
                .ToList();

            foreach (var platformFolder in platformFolders)
            {
                var subFolders = GetCodeSubFolders(platformFolder);
                if (subFolders.Any())
                {
                    var platformName = Path.GetFileName(platformFolder).Split('.').Last();
                    codeFolders.Add(new CodeFolderInfo 
                    { 
                        Name = Path.GetFileName(platformFolder), 
                        Path = platformFolder, 
                        Type = $"Platform ({platformName})",
                        SubFolders = subFolders
                    });
                }
            }
            
            return codeFolders;
        }
        

        //Private 
        
        //Generate Manifest
        private async Task CreateJsonManifestAsync(ExportSettings settings, string tempDir)
        {
          // Create our custom JSON manifest for package manager features
          var manifestPath = Path.Combine(tempDir, "manifest.json");

          // Generate package hash (excluding manifest.json itself)
          var packageHash = await GeneratePackageHashAsync(tempDir);
          settings.Manifest.PackageHash = packageHash;

          var manifestJson = JsonSerializer.Serialize(settings.Manifest, new JsonSerializerOptions 
          { 
              WriteIndented = true,
              Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
          });
          await File.WriteAllTextAsync(manifestPath, manifestJson);
        }

        private async Task CreateRegistryJsonAsync(ExportSettings settings, string outputDirectory)
        {
          // Create registry metadata JSON file alongside the .stridepackage
          var registryJsonPath = Path.Combine(outputDirectory, "stridepackage.json");
          
          var registryData = new
          {
              name = settings.Manifest.Name,
              version = settings.Manifest.Version,
              description = settings.Manifest.Description,
              author = settings.Manifest.Author,
              tags = settings.Manifest.Tags,
              stride_version = settings.Manifest.StrideVersion + "+",
              created = settings.Manifest.CreatedDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
              download_url = settings.Manifest.DownloadUrl ?? "",
              homepage = settings.Manifest.Homepage ?? "",
              repository = settings.Manifest.Repository ?? "",
              license = settings.Manifest.License ?? ""
          };

          var registryJson = JsonSerializer.Serialize(registryData, new JsonSerializerOptions 
          { 
              WriteIndented = true,
              Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
          });
          await File.WriteAllTextAsync(registryJsonPath, registryJson);
        }

        private async Task<string> GeneratePackageHashAsync(string tempDir)
        {
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

          return Convert.ToHexString(sha256.Hash ?? Array.Empty<byte>());
        }



        private async Task UpdateAssetReferencesDirectMappingAsync(string tempDir, ExportSettings settings)
        {
            // Only process dependencies that have a new location set
            var dependenciesWithNewLocation = settings.ResourceDependencies
                .Where(d => !string.IsNullOrEmpty(d.NewResourcePath))
                .ToList();

            // Group by asset file for efficient processing  
            var dependenciesByAsset = dependenciesWithNewLocation
                .SelectMany(dep => dep.References.Select(reference => new { Dependency = dep, Reference = reference }))
                .GroupBy(x => x.Reference.AssetFile)
                .ToList();

            foreach (var assetGroup in dependenciesByAsset)
            {
                var assetFile = assetGroup.Key;

                // Convert to temp directory path with proper folder mapping
                var tempAssetFile = MapAssetFileToTempStructure(assetFile, settings.LibraryPath, tempDir, settings.SelectedAssetFolders);

                var content = await File.ReadAllTextAsync(tempAssetFile);
                var modifiedContent = content;

                // Update all references for this asset file
                foreach (var item in assetGroup)
                {
                    // Calculate relative path from this specific asset to the new resource location
                    var assetDir = Path.GetDirectoryName(tempAssetFile)!;
                    var newRelativePath = Path.GetRelativePath(assetDir, Path.Combine(tempDir, item.Dependency.NewResourcePath));
                    newRelativePath = newRelativePath.Replace('\\', '/');

                    // More precise replacement based on reference type
                    switch (item.Reference.Type)
                    {
                        case ResourceReferenceType.SourceReference:
                            modifiedContent = modifiedContent.Replace($"Source: {item.Reference.ResourcePath}", $"Source: {newRelativePath}");
                            break;
                        case ResourceReferenceType.FileReference:
                            modifiedContent = modifiedContent.Replace($"!file \"{item.Reference.ResourcePath}\"", $"!file \"{newRelativePath}\"");
                            break;
                        default:
                            // Fallback to simple replacement for other types
                            modifiedContent = modifiedContent.Replace(item.Reference.ResourcePath, newRelativePath);
                            break;
                    }
                }

                // Write the updated content
                await File.WriteAllTextAsync(tempAssetFile, modifiedContent);
            }
        }
        
        private string MapAssetFileToTempStructure(string originalAssetFile, string libraryPath, string tempDir, List<string>? selectedAssetFolders)
        {
            var relativePath = Path.GetRelativePath(libraryPath, originalAssetFile);
            
            // Detect the separator style from the temp directory to match the expected format
            char separatorToUse = tempDir.Contains('/') ? '/' : Path.DirectorySeparatorChar;
            
            // Normalize paths for cross-platform comparison (Windows uses \, Unix uses /)
            var normalizedRelativePath = relativePath.Replace('\\', '/');
            
            // Check if this file is in any selected asset folders that get reorganized
            foreach (var assetFolder in selectedAssetFolders ?? new List<string>())
            {
                var normalizedAssetFolder = assetFolder.Replace('\\', '/');
                
                if (normalizedRelativePath.StartsWith(normalizedAssetFolder, StringComparison.OrdinalIgnoreCase))
                {
                    // Asset folders get flattened: ProjectName/Assets/UI → Assets/UI
                    var folderName = Path.GetFileName(assetFolder);
                    var remainingPath = normalizedRelativePath.Substring(normalizedAssetFolder.Length).TrimStart('/');
                    
                    // Convert remaining path to use the detected separator style
                    if (separatorToUse == '\\')
                    {
                        remainingPath = remainingPath.Replace('/', '\\');
                    }
                    
                    // Build path with the detected separator style
                    var parts = new[] { tempDir.TrimEnd('/', '\\'), "Assets", folderName, remainingPath };
                    return string.Join(separatorToUse.ToString(), parts.Where(p => !string.IsNullOrEmpty(p)));
                }
            }
            
            // For non-asset files, use original structure with detected separator
            var adjustedRelativePath = relativePath.Replace('\\', '/');
            if (separatorToUse == '\\')
            {
                adjustedRelativePath = adjustedRelativePath.Replace('/', '\\');
            }
            var nonAssetParts = new[] { tempDir.TrimEnd('/', '\\'), adjustedRelativePath };
            return string.Join(separatorToUse.ToString(), nonAssetParts.Where(p => !string.IsNullOrEmpty(p)));
        }

        private async Task CopyEssentialProjectFilesAsync(string sourcePath, string tempDir)
        {
            // No longer copying .sdpkg files - Stride generates those
            // Only copy essential non-Stride files if needed in future
            await Task.CompletedTask;
        }
        
        private void RemoveExcludedItems(string tempDir, List<string> excludeFiles)
        {
            foreach (var excludePath in excludeFiles)
            {
                var fullTempPath = Path.Combine(tempDir, excludePath);
                
                try
                {
                    if (File.Exists(fullTempPath))
                    {
                        File.Delete(fullTempPath);
                    }
                    else if (Directory.Exists(fullTempPath))
                    {
                        Directory.Delete(fullTempPath, true);
                    }
                }
                catch (Exception)
                {
                    // Skip if we can't delete (file in use, permissions, etc.)
                }
            }
        }
        
        private void KeepOnlyIncludedItems(string tempDir, List<string> includeFiles)
        {
            // Get all items in temp directory
            var allItems = Directory.GetFileSystemEntries(tempDir, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(tempDir, path))
                .ToList();
            
            // Find items to delete (not in include list and not parent directories of included items)
            var itemsToDelete = new List<string>();
            
            foreach (var item in allItems)
            {
                var shouldKeep = false;
                
                // Check if this item is explicitly included
                if (includeFiles.Contains(item))
                {
                    shouldKeep = true;
                }
                else
                {
                    // Check if this item is a parent directory of any included item
                    foreach (var includedItem in includeFiles)
                    {
                        if (includedItem.StartsWith(item + Path.DirectorySeparatorChar) || 
                            includedItem.StartsWith(item + Path.AltDirectorySeparatorChar))
                        {
                            shouldKeep = true;
                            break;
                        }
                    }
                }
                
                if (!shouldKeep)
                {
                    itemsToDelete.Add(item);
                }
            }
            
            // Delete items not in include list (delete files first, then empty directories)
            var filesToDelete = itemsToDelete.Where(item => File.Exists(Path.Combine(tempDir, item))).ToList();
            var dirsToDelete = itemsToDelete.Where(item => Directory.Exists(Path.Combine(tempDir, item))).ToList();
            
            // Delete files first
            foreach (var file in filesToDelete)
            {
                try
                {
                    File.Delete(Path.Combine(tempDir, file));
                }
                catch (Exception)
                {
                    // Skip if we can't delete
                }
            }
            
            // Delete directories (deepest first)
            foreach (var dir in dirsToDelete.OrderByDescending(d => d.Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar)))
            {
                try
                {
                    var dirPath = Path.Combine(tempDir, dir);
                    if (Directory.Exists(dirPath) && !Directory.EnumerateFileSystemEntries(dirPath).Any())
                    {
                        Directory.Delete(dirPath);
                    }
                }
                catch (Exception)
                {
                    // Skip if we can't delete
                }
            }
        }
        
        
        //Namespace Cleanup
        private async Task RemoveNamespacesFromFilesAsync(string directory, List<string> excludeNamespaces)
        {
            var strideFiles = Directory.GetFiles(directory, "*.sd*", SearchOption.AllDirectories);
            
            foreach (var file in strideFiles)
            {
                var content = await File.ReadAllTextAsync(file);
                var modifiedContent = RemoveNamespacesFromContent(content, excludeNamespaces);
                
                if (modifiedContent != content)
                {
                    await File.WriteAllTextAsync(file, modifiedContent);
                }
            }
        }
        
        private string RemoveNamespacesFromContent(string content, List<string> excludeNamespaces)
        {
            foreach (var excludeNs in excludeNamespaces)
            {
                // Remove assembly references (after comma, no !) - for .sdprefab/.sdscene
                content = content.Replace($",{excludeNs}", "");
                
                // Remove namespace references (with !) - for .sdprefab/.sdscene
                content = content.Replace($"!{excludeNs}.", "!");
                content = content.Replace($"!{excludeNs},", "!");
                
                // Remove namespace declarations - for .sdfx and other files
                content = content.Replace($"namespace {excludeNs}", "");
                content = content.Replace($"namespace {excludeNs}.", "namespace ");
            }
            
            return content;
        }

        
        //Folder Fetching
        private List<string> GetCodeSubFolders(string projectPath)
        {
            if (!Directory.Exists(projectPath))
                return new List<string>();

            return Directory.GetDirectories(projectPath, "*", SearchOption.TopDirectoryOnly)
                .Where(dir => Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories).Any())
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList();
        }
        
        
        //Folder Copying
        private async Task ProcessAndCopyUserStructureAsync(string sourcePath, string tempDir, ExportSettings settings)
        {
            // Copy only selected folders based on user's selections
            await CopySelectedFoldersAsync(sourcePath, tempDir, settings);
            
            // Remove excluded files/folders if specified
            if (settings.ExcludeFiles?.Any() == true)
            {
                RemoveExcludedItems(tempDir, settings.ExcludeFiles);
            }
            
            // If using include list, remove everything not included
            if (settings.IncludeFiles?.Any() == true)
            {
                KeepOnlyIncludedItems(tempDir, settings.IncludeFiles);
            }
            
            // If we have namespaces to exclude, process the .sd files
            if (settings.ExcludeNamespaces?.Any() == true)
            {
                await RemoveNamespacesFromFilesAsync(tempDir, settings.ExcludeNamespaces);
            }
        }

        private async Task CopySelectedFoldersAsync(string sourcePath, string tempDir, ExportSettings settings)
        {
            // Phase 1: Copy selected asset folders to root Assets/ level
            foreach (var assetFolder in settings.SelectedAssetFolders ?? new List<string>())
            {
                var sourceAssetPath = Path.Combine(sourcePath, assetFolder);
                
                // Extract just the final folder name and put under root Assets/
                var assetFolderName = Path.GetFileName(assetFolder);
                var targetAssetPath = Path.Combine(tempDir, "Assets", assetFolderName);
                
                if (Directory.Exists(sourceAssetPath))
                {
                    FileHelper.CopyDirectory(sourceAssetPath, targetAssetPath);
                }
            }

            // Phase 2: Copy selected code folders (shared)
            foreach (var codeFolder in settings.SelectedCodeFolders ?? new List<string>())
            {
                var sourceCodePath = Path.Combine(sourcePath, codeFolder);
                var codeFolderName = Path.GetFileName(codeFolder.TrimEnd('/'));
                var targetCodePath = Path.Combine(tempDir, settings.Manifest.Name, codeFolderName);

                if (Directory.Exists(sourceCodePath))
                {
                    FileHelper.CopyDirectory(sourceCodePath, targetCodePath);
                }
            }

            // Phase 2: Copy selected platform folders
            foreach (var platformFolder in settings.SelectedPlatformFolders ?? new List<string>())
            {
                var sourcePlatformPath = Path.Combine(sourcePath, platformFolder);
                var platformFolderName = Path.GetFileName(platformFolder.TrimEnd('/'));
                var targetPlatformPath = Path.Combine(tempDir, settings.Manifest.Name, platformFolderName);

                if (Directory.Exists(sourcePlatformPath))
                {
                    FileHelper.CopyDirectory(sourcePlatformPath, targetPlatformPath);
                }
            }

            // Phase 3: Copy and organize resources
            if ( settings.ResourceDependencies?.Any() == true)
            {
                await CopyAndOrganizeResourcesAsync(sourcePath, tempDir, settings);
            }
            

            // Copy essential project files (.sdpkg, etc.) that are needed for the package
            await CopyEssentialProjectFilesAsync(sourcePath, tempDir);
        }

        private async Task CopyAndOrganizeResourcesAsync(string sourcePath, string tempDir, ExportSettings settings)
        {
            if (string.IsNullOrEmpty(settings.TargetResourcePath))
                return;

            var targetResourceDir = Path.Combine(tempDir, settings.TargetResourcePath);
            Directory.CreateDirectory(targetResourceDir);

            // Get the package name to detect and prevent folder stacking
            var packageName = settings.Manifest.Name;

            foreach (var resourceDep in settings.ResourceDependencies)
            {
                if (File.Exists(resourceDep.ActualPath))
                {
                    // Calculate relative path from project root
                    var relativePath = Path.GetRelativePath(sourcePath, resourceDep.ActualPath);

                    // Strip project name and Resources folder to get clean content path
                    // From: "desert_strides/Resources/Textures/file.png"
                    // To: "Textures/file.png"
                    var pathParts = relativePath.Split('/', '\\');
                    if (pathParts.Length >= 3 && pathParts[1].Equals("Resources", StringComparison.OrdinalIgnoreCase))
                    {
                        // Skip first two parts (projectname/Resources/) and keep the rest
                        relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), pathParts.Skip(2));

                        // Prevent folder stacking: if the path already starts with the package name, strip it
                        // This handles re-exporting after a previous import created Resources/PackageName/...
                        // From: "PackageName/Textures/file.png" -> "Textures/file.png"
                        var remainingParts = relativePath.Split('/', '\\');
                        if (remainingParts.Length >= 1 && remainingParts[0].Equals(packageName, StringComparison.OrdinalIgnoreCase))
                        {
                            relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), remainingParts.Skip(1));
                        }
                    }

                    // Combine with project folder to maintain structure
                    var targetResourcePath = Path.Combine(targetResourceDir, relativePath);

                    // Create subdirectory structure as needed
                    Directory.CreateDirectory(Path.GetDirectoryName(targetResourcePath)!);

                    File.Copy(resourceDep.ActualPath, targetResourcePath, true);

                    // Set clean path maintaining the structure
                    var cleanRelativePath = Path.Combine(settings.TargetResourcePath, relativePath).Replace('\\', '/');
                    resourceDep.NewResourcePath = cleanRelativePath;
                }
            }

            await UpdateAssetReferencesDirectMappingAsync(tempDir, settings);
        }

    }

    public class AssetFolderInfo
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public int FileCount { get; set; }
        public string Location { get; set; } = string.Empty; // "Root" or "Nested"
    }

    public class CodeFolderInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public List<string> SubFolders { get; set; } = new();
    }
}

