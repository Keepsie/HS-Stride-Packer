// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using System.Text.RegularExpressions;


namespace HS.Stride.Packer.Core
{
    public class ResourcePathValidator
    {
        private AssetScanner? _assetScanner;
        private string? _scannerProjectPath;

        /// <summary>
        /// Gets or initializes the AssetScanner for robust source file resolution.
        /// The scanner indexes all source files in the project for fast filename-based lookups.
        /// </summary>
        private AssetScanner GetOrCreateScanner(string projectPath)
        {
            // Create new scanner if none exists or if project path changed
            if (_assetScanner == null || _scannerProjectPath != projectPath)
            {
                _assetScanner = new AssetScanner(projectPath);
                _assetScanner.Scan();
                _scannerProjectPath = projectPath;
            }
            return _assetScanner;
        }

        public ValidationResult ValidateProject(string projectPath)
        {
            var result = new ValidationResult();
            
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
                return result;
                
            var allAssets = Directory.GetFiles(projectPath, "*.sd*", SearchOption.AllDirectories);
            
            foreach (var assetFile in allAssets)
            {
                var resourceRefs = ScanForResourceReferences(assetFile);
                foreach (var resourceRef in resourceRefs)
                {
                    ValidateResourcePath(assetFile, resourceRef, projectPath, result);
                }
            }
            
            return result;
        }

        public ValidationResult ValidateAndCollectDependencies(string projectPath, List<string> selectedAssetFolders)
        {
            var result = new ValidationResult();
            
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
                return result;

            // Only scan the selected asset folders, not the entire project
            foreach (var assetFolderPath in selectedAssetFolders)
            {
                var fullAssetPath = Path.Combine(projectPath, assetFolderPath);
                if (!Directory.Exists(fullAssetPath))
                    continue;

                // Find all Stride asset files in this folder
                var assetFiles = Directory.GetFiles(fullAssetPath, "*.sd*", SearchOption.AllDirectories)
                    .ToList();

                foreach (var assetFile in assetFiles)
                {
                    var resourceRefs = ScanForResourceReferencesEnhanced(assetFile);
                    foreach (var resourceRef in resourceRefs)
                    {
                        ValidateAndCollectResourcePath(assetFile, resourceRef, projectPath, result);
                    }
                }
            }
            
            return result;
        }

        
        //Private
        private List<ResourceReference> ScanForResourceReferences(string filePath)
        {
            if (!File.Exists(filePath))
                return new List<ResourceReference>();
                
            var content = File.ReadAllText(filePath);
            var references = new List<ResourceReference>();
            
            // Fixed regex pattern to find !file references - capture until end of line or next !
            var fileRefRegex = new Regex(@"!file\s+(.+?)(?:\r?\n|!|$)");
            var matches = fileRefRegex.Matches(content);
            
            foreach (Match match in matches)
            {
                var resourcePath = match.Groups[1].Value.Trim();
                
                references.Add(new ResourceReference
                {
                    AssetFile = filePath,
                    ResourcePath = resourcePath,
                    LineNumber = GetLineNumber(content, match.Index),
                    Type = ResourceReferenceType.FileReference
                });
            }
            
            return references;
        }
        
        private void ValidateResourcePath(string assetFile, ResourceReference resourceRef,
                                         string projectPath, ValidationResult result)
        {
            // Use the robust FindActualResourceFile which leverages the AssetScanner
            var actualResourcePath = FindActualResourceFile(resourceRef.ResourcePath, assetFile, projectPath);

            if (!string.IsNullOrEmpty(actualResourcePath))
            {
                // Found the file - check if it's external to the project
                var projectFullPath = Path.GetFullPath(projectPath);
                if (!actualResourcePath.StartsWith(projectFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    result.ExternalResources.Add(new ExternalResourceIssue
                    {
                        AssetFile = assetFile,
                        ResourcePath = resourceRef.ResourcePath
                    });
                }
                // File found and is within project - valid
            }
            else
            {
                // Could not find the resource anywhere
                result.MissingResources.Add(new MissingResourceIssue
                {
                    AssetFile = assetFile,
                    ResourcePath = resourceRef.ResourcePath
                });
            }
        }
        private List<ResourceReference> ScanForResourceReferencesEnhanced(string filePath)
        {
            if (!File.Exists(filePath))
                return new List<ResourceReference>();
                
            var content = File.ReadAllText(filePath);
            var references = new List<ResourceReference>();
            
            // Pattern 1: !file "any/path/to/resource.ext"
            var filePattern = @"!file\s+""([^""]+)""";
            var fileMatches = Regex.Matches(content, filePattern);
            
            foreach (Match match in fileMatches)
            {
                var resourcePath = match.Groups[1].Value;
                references.Add(new ResourceReference
                {
                    AssetFile = filePath,
                    ResourcePath = resourcePath,
                    LineNumber = GetLineNumber(content, match.Index),
                    Type = ResourceReferenceType.FileReference
                });
            }
            
            // Pattern 2: Source: any/path/resource.ext (may include !file prefix)
            var sourcePattern = @"Source:\s*([^\r\n]+)";
            var sourceMatches = Regex.Matches(content, sourcePattern);

            foreach (Match match in sourceMatches)
            {
                var sourcePath = match.Groups[1].Value.Trim();

                // Strip !file prefix if present (Stride uses "Source: !file path/to/file")
                if (sourcePath.StartsWith("!file ", StringComparison.OrdinalIgnoreCase))
                {
                    sourcePath = sourcePath.Substring(6).Trim();
                }
                else if (sourcePath.StartsWith("!file", StringComparison.OrdinalIgnoreCase))
                {
                    sourcePath = sourcePath.Substring(5).Trim();
                }

                // Skip if it's a null reference or empty
                if (string.IsNullOrEmpty(sourcePath) || sourcePath == "null")
                    continue;

                references.Add(new ResourceReference
                {
                    AssetFile = filePath,
                    ResourcePath = sourcePath,
                    LineNumber = GetLineNumber(content, match.Index),
                    Type = ResourceReferenceType.SourceReference
                });
            }
            
            // Pattern 3: Any path containing common resource extensions
            var resourceExtensions = new[] { ".png", ".jpg", ".jpeg", ".tga", ".dds", ".fbx", ".obj", ".dae", ".wav", ".ogg", ".mp3" };
            foreach (var ext in resourceExtensions)
            {
                var pattern = $@"""([^""]*{Regex.Escape(ext)})""";
                var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
                
                foreach (Match match in matches)
                {
                    var resourcePath = match.Groups[1].Value;
                    references.Add(new ResourceReference
                    {
                        AssetFile = filePath,
                        ResourcePath = resourcePath,
                        LineNumber = GetLineNumber(content, match.Index),
                        Type = ResourceReferenceType.EmbeddedReference
                    });
                }
            }
            
            return references;
        }

        private void ValidateAndCollectResourcePath(string assetFile, ResourceReference resourceRef, string projectPath, ValidationResult result)
        {
            // Try to find the actual resource file
            var actualResourcePath = FindActualResourceFile(resourceRef.ResourcePath, assetFile, projectPath);

            if (!string.IsNullOrEmpty(actualResourcePath))
            {
                // Find existing dependency for this actual file path
                var existingDep = result.ResourceDependencies
                    .FirstOrDefault(d => d.ActualPath.Equals(actualResourcePath, StringComparison.OrdinalIgnoreCase));

                if (existingDep != null)
                {
                    // Add this reference to existing dependency (avoid duplicate resources)
                    existingDep.References.Add(resourceRef);
                }
                else
                {
                    // Create new dependency with first reference
                    var dependency = new ResourceDependency
                    {
                        FileName = Path.GetFileName(actualResourcePath),
                        ActualPath = actualResourcePath,
                        References = new List<ResourceReference> { resourceRef },

                        // Legacy properties for backward compatibility
                        OriginalPath = resourceRef.ResourcePath,
                        ReferencedInAsset = assetFile,
                        AssetFilePath = assetFile,
                        OriginalPathInAsset = resourceRef.ResourcePath
                    };

                    result.ResourceDependencies.Add(dependency);
                }

                // Also do validation
                var assetDir = Path.GetDirectoryName(assetFile);
                if (assetDir != null)
                {
                    var fullResourcePath = Path.GetFullPath(actualResourcePath);
                    var projectFullPath = Path.GetFullPath(projectPath);

                    if (!fullResourcePath.StartsWith(projectFullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        result.ExternalResources.Add(new ExternalResourceIssue
                        {
                            AssetFile = assetFile,
                            ResourcePath = resourceRef.ResourcePath
                        });
                    }
                }
            }
            else
            {
                // Resource not found - add to missing resources
                result.MissingResources.Add(new MissingResourceIssue
                {
                    AssetFile = assetFile,
                    ResourcePath = resourceRef.ResourcePath
                });
            }
        }

        private string FindActualResourceFile(string resourcePath, string assetFilePath, string projectPath)
        {
            // Use AssetScanner for robust source file resolution
            // This indexes all source files and can find them even when moved
            var scanner = GetOrCreateScanner(projectPath);
            var assetDir = Path.GetDirectoryName(assetFilePath);

            // The scanner tries multiple strategies:
            // 1. Relative to asset file directory
            // 2. Relative to project root
            // 3. Indexed path lookup
            // 4. Filename-based search with path similarity scoring
            var foundPath = scanner.FindSourceFileRobust(resourcePath, assetDir);

            if (!string.IsNullOrEmpty(foundPath))
                return foundPath;

            // Additional fallback: try as absolute path (for external references)
            if (Path.IsPathRooted(resourcePath) && File.Exists(resourcePath))
                return Path.GetFullPath(resourcePath);

            return string.Empty;
        }

        private static int GetLineNumber(string content, int position)
        {
            if (position < 0 || position >= content.Length)
                return 1;
                
            return content.Take(position).Count(c => c == '\n') + 1;
        }
    }
}

