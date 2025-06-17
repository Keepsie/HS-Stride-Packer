// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using System.IO.Compression;
using System.Text.Json;
using HS.Stride.Packer.Core;
using HS.Stride.Packer.Utilities;

namespace HS.Stride.Packer.Core
{
    public class PackageImporter
    {
        public ValidationResult ValidateSettings(ImportSettings settings)
        {
            var result = new ValidationResult();

            if (!File.Exists(settings.PackagePath))
            {
                result.Errors.Add($"Package file does not exist: {settings.PackagePath}");
            }

            if (!Directory.Exists(settings.TargetProjectPath))
            {
                result.Errors.Add($"Target project path does not exist: {settings.TargetProjectPath}");
            }

            if (!settings.PackagePath.EndsWith(".stridepackage", StringComparison.OrdinalIgnoreCase))
            {
                result.Warnings.Add("Package file does not have .stridepackage extension");
            }

            return result;
        }

        public async Task<ImportResult> ImportPackageAsync(ImportSettings settings)
        {
            var validation = ValidateSettings(settings);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException($"Invalid import settings: {string.Join(", ", validation.Errors)}");
            }

            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                // Extract package to temp directory
                Directory.CreateDirectory(tempDir);
                ZipFile.ExtractToDirectory(settings.PackagePath, tempDir);

                var manifest = await ReadPackageManifestAsync(tempDir);
                
                // All packages must have a manifest
                if (manifest == null)
                {
                    throw new InvalidOperationException("Package is missing manifest.json file. This package may be corrupted or is not a valid .stridepackage file.");
                }
                
                // All packages must have integrity hash
                if (string.IsNullOrEmpty(manifest.PackageHash))
                {
                    throw new InvalidOperationException("Package manifest is missing integrity hash. This package may be corrupted or was created with an outdated version of the packer.");
                }
                
                // Verify package integrity - MANDATORY
                var integrityValid = await VerifyPackageIntegrityAsync(tempDir, manifest.PackageHash);
                if (!integrityValid)
                {
                    throw new InvalidOperationException("Package integrity verification failed. The package may be corrupted or tampered with.");
                }

                // Simple 3-step import process
                var result = await ImportWithSimpleStepsAsync(settings, tempDir, manifest);

                result.Manifest = manifest;
                return result;
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        
        //Private
        private async Task<bool> VerifyPackageIntegrityAsync(string extractedDir, string expectedHash)
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
                // If we can't compute the hash, fail verification
                return false;
            }
        }

        private async Task<PackageManifest?> ReadPackageManifestAsync(string extractedDir)
        {
            var manifestPath = Path.Combine(extractedDir, "manifest.json");
            if (!File.Exists(manifestPath))
                return null;

            var manifestJson = await File.ReadAllTextAsync(manifestPath);
            return JsonSerializer.Deserialize<PackageManifest>(manifestJson);
        }

        private async Task<ImportResult> ImportWithSimpleStepsAsync(ImportSettings settings, string tempDir, PackageManifest? manifest)
        {
            var result = new ImportResult();
            
            // Detect target project structure (Fresh vs Template)
            var targetStructure = DetectTargetProjectStructure(settings.TargetProjectPath);
            
            // Simple scan-based import: find all folders in package and categorize them
            await ScanAndImportPackageContentsAsync(tempDir, settings.TargetProjectPath, targetStructure, result, settings.OverwriteExistingFiles);

            result.ImportPath = settings.TargetProjectPath;
            return result;
        }

        private async Task ScanAndImportPackageContentsAsync(string tempDir, string targetProjectPath, TargetProjectStructure targetStructure, ImportResult result, bool overwrite)
        {
            // Scan all directories in the package (recursively to find Assets, Resources, and code folders)
            await ScanDirectoryForImportAsync(tempDir, tempDir, targetProjectPath, targetStructure, result, overwrite);
        }

        private async Task ScanDirectoryForImportAsync(string currentDir, string packageRoot, string targetProjectPath, TargetProjectStructure targetStructure, ImportResult result, bool overwrite)
        {
            try
            {
                var directories = Directory.GetDirectories(currentDir, "*", SearchOption.TopDirectoryOnly);
                
                foreach (var dir in directories)
                {
                    var folderName = Path.GetFileName(dir);
                    
                    // Skip manifest.json and other files at root
                    if (folderName.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                        continue;
                    
                    if (folderName.Equals("Assets", StringComparison.OrdinalIgnoreCase))
                    {
                        // Found Assets folder - copy contents to user's Assets
                        await CopyAssetsToTarget(dir, targetProjectPath, targetStructure, result, overwrite);
                    }
                    else if (folderName.Equals("Resources", StringComparison.OrdinalIgnoreCase))
                    {
                        // Found Resources folder - copy contents to user's Resources
                        await CopyResourcesToTarget(dir, targetProjectPath, targetStructure, result, overwrite);
                    }
                    else
                    {
                        // Any other folder is assumed to be code - copy to user's code area
                        await CopyCodeToTarget(dir, folderName, targetProjectPath, targetStructure, result, overwrite);
                    }
                    
                }
            }
            catch (Exception ex)
            {
                result.SkippedItems.Add($"Error scanning directory {currentDir}: {ex.Message}");
            }
        }

        private async Task CopyAssetsToTarget(string assetsDir, string targetProjectPath, TargetProjectStructure targetStructure, ImportResult result, bool overwrite)
        {
            var targetAssetsDir = Path.Combine(targetProjectPath, targetStructure.AssetsPath);
            
            // Create target Assets folder if it doesn't exist
            if (!Directory.Exists(targetAssetsDir))
            {
                Directory.CreateDirectory(targetAssetsDir);
                result.CreatedDirectories.Add(targetStructure.AssetsPath);
            }

            // Copy contents of package Assets/ directly into user's Assets/
            await CopyDirectoryContentsAsync(assetsDir, targetAssetsDir, result, targetProjectPath, overwrite);
        }

        private async Task CopyResourcesToTarget(string resourcesDir, string targetProjectPath, TargetProjectStructure targetStructure, ImportResult result, bool overwrite)
        {
            var targetResourcesDir = Path.Combine(targetProjectPath, targetStructure.ResourcesPath);
            
            // Create target Resources folder if it doesn't exist
            if (!Directory.Exists(targetResourcesDir))
            {
                Directory.CreateDirectory(targetResourcesDir);
                result.CreatedDirectories.Add(targetStructure.ResourcesPath);
            }

            // Copy contents of package Resources/ directly into user's Resources/
            await CopyDirectoryContentsAsync(resourcesDir, targetResourcesDir, result, targetProjectPath, overwrite);
        }

        private async Task CopyCodeToTarget(string codeDir, string folderName, string targetProjectPath, TargetProjectStructure targetStructure, ImportResult result, bool overwrite)
        {
            // Determine where to put code
            var targetCodeDir = DetermineTargetCodePath(folderName, targetProjectPath, targetStructure);
            
            if (!string.IsNullOrEmpty(targetCodeDir))
            {
                // Create target code area if it doesn't exist
                if (!Directory.Exists(targetCodeDir))
                {
                    Directory.CreateDirectory(targetCodeDir);
                    var relativeDir = Path.GetRelativePath(targetProjectPath, targetCodeDir);
                    result.CreatedDirectories.Add(relativeDir);
                }

                // Copy CONTENTS of the code folder to target, not the folder itself
                // So desert_strides/Happenstance/ becomes .Game/Happenstance/, not .Game/desert_strides/Happenstance/
                var subFolders = Directory.GetDirectories(codeDir);
                foreach (var subFolder in subFolders)
                {
                    var subFolderName = Path.GetFileName(subFolder);
                    var targetSubDir = Path.Combine(targetCodeDir, subFolderName);
                    
                    if (!Directory.Exists(targetSubDir))
                    {
                        Directory.CreateDirectory(targetSubDir);
                        var relativeSubDir = Path.GetRelativePath(targetProjectPath, targetSubDir);
                        result.CreatedDirectories.Add(relativeSubDir);
                    }

                    // Copy contents of each subfolder
                    await CopyDirectoryContentsAsync(subFolder, targetSubDir, result, targetProjectPath, overwrite);
                }
            }
        }

        private async Task CopyDirectoryContentsAsync(string sourceDir, string targetDir, ImportResult result, string targetProjectPath, bool overwrite)
        {
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDir, file);
                var targetFile = Path.Combine(targetDir, relativePath);
                var targetFileDir = Path.GetDirectoryName(targetFile);

                // Create subdirectories as needed
                if (!string.IsNullOrEmpty(targetFileDir) && !Directory.Exists(targetFileDir))
                {
                    Directory.CreateDirectory(targetFileDir);
                }

                // Handle file conflicts
                if (File.Exists(targetFile))
                {
                    if (overwrite)
                    {
                        File.Copy(file, targetFile, true);
                        result.OverwrittenItems.Add(Path.GetRelativePath(targetProjectPath, targetFile));
                    }
                    else
                    {
                        result.SkippedItems.Add(Path.GetRelativePath(targetProjectPath, targetFile));
                        continue;
                    }
                }
                else
                {
                    File.Copy(file, targetFile);
                }

                result.ImportedFiles.Add(Path.GetRelativePath(targetProjectPath, targetFile));
            }

            await Task.CompletedTask;
        }

        private TargetProjectStructure DetectTargetProjectStructure(string projectPath)
        {
            var projectName = Path.GetFileName(projectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            
            // Check for Fresh structure: ProjectName/Assets/ folder exists
            var nestedAssetsPath = Path.Combine(projectPath, projectName, "Assets");
            if (Directory.Exists(nestedAssetsPath))
            {
                return new TargetProjectStructure
                {
                    Type = ProjectStructureType.Fresh,
                    AssetsPath = Path.Combine(projectName, "Assets"),
                    ResourcesPath = Path.Combine(projectName, "Resources"),
                    CodePath = projectName
                };
            }
            
            // Check for Template structure: Assets/ at root level
            var rootAssetsPath = Path.Combine(projectPath, "Assets");
            if (Directory.Exists(rootAssetsPath))
            {
                return new TargetProjectStructure
                {
                    Type = ProjectStructureType.Template,
                    AssetsPath = "Assets",
                    ResourcesPath = "Resources",
                    CodePath = "" // Code goes in .Game folders at root
                };
            }
            
            // Default to Template structure if unclear
            return new TargetProjectStructure
            {
                Type = ProjectStructureType.Template,
                AssetsPath = "Assets",
                ResourcesPath = "Resources", 
                CodePath = ""
            };
        }


        private string DetermineTargetCodePath(string packageCodeFolderName, string targetProjectPath, TargetProjectStructure targetStructure)
        {
            if (targetStructure.Type == ProjectStructureType.Fresh)
            {
                // Fresh structure: code goes in ProjectName/ folder
                return Path.Combine(targetProjectPath, targetStructure.CodePath);
            }
            
            // Template structure: code goes at root level (like .Game folders)
            // For any code folder, try to find existing .Game folder, otherwise put at root
            var gameFolder = Directory.GetDirectories(targetProjectPath, "*.Game", SearchOption.TopDirectoryOnly).FirstOrDefault();
            return gameFolder ?? targetProjectPath;
            
        }
    }

    public class ImportResult
    {
        public string ImportPath { get; set; } = string.Empty;
        public List<string> ImportedFiles { get; set; } = new();
        public List<string> CreatedDirectories { get; set; } = new();
        public List<string> OverwrittenItems { get; set; } = new();
        public List<string> SkippedItems { get; set; } = new();
        public PackageManifest? Manifest { get; set; }

        public bool HasConflicts => OverwrittenItems.Any() || SkippedItems.Any();
        public int TotalFilesImported => ImportedFiles.Count;
    }

    public class TargetProjectStructure
    {
        public ProjectStructureType Type { get; set; }
        public string AssetsPath { get; set; } = string.Empty;
        public string ResourcesPath { get; set; } = string.Empty;
        public string CodePath { get; set; } = string.Empty;
    }
}