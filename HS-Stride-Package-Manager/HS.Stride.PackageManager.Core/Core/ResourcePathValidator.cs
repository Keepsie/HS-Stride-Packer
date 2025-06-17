// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using System.Text.RegularExpressions;


namespace HS.Stride.Packer.Core
{
    public class ResourcePathValidator
    {
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
            var assetDir = Path.GetDirectoryName(assetFile);
            if (assetDir == null) return;
            
            var fullResourcePath = Path.GetFullPath(Path.Combine(assetDir, resourceRef.ResourcePath));
            var projectFullPath = Path.GetFullPath(projectPath);
            
            if (!fullResourcePath.StartsWith(projectFullPath, StringComparison.OrdinalIgnoreCase))
            {
                result.ExternalResources.Add(new ExternalResourceIssue
                {
                    AssetFile = assetFile,
                    ResourcePath = resourceRef.ResourcePath
                });
            }
            else if (!File.Exists(fullResourcePath))
            {
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
            
            // Pattern 2: Source: any/path/resource.ext
            var sourcePattern = @"Source:\s*([^\r\n]+)";
            var sourceMatches = Regex.Matches(content, sourcePattern);
            
            foreach (Match match in sourceMatches)
            {
                var sourcePath = match.Groups[1].Value.Trim();
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
            // Try different ways to resolve the resource path
            
            // 1. Try relative to the asset file
            var assetDir = Path.GetDirectoryName(assetFilePath) ?? "";
            var relativeToAsset = Path.Combine(assetDir, resourcePath);
            if (File.Exists(relativeToAsset))
                return Path.GetFullPath(relativeToAsset);
            
            // 2. Try relative to project root
            var relativeToProject = Path.Combine(projectPath, resourcePath);
            if (File.Exists(relativeToProject))
                return Path.GetFullPath(relativeToProject);
            
            // 3. Try as absolute path
            if (Path.IsPathRooted(resourcePath) && File.Exists(resourcePath))
                return Path.GetFullPath(resourcePath);
            
            // 4. Search in common resource locations
            var fileName = Path.GetFileName(resourcePath);
            var searchPaths = new[]
            {
                Path.Combine(projectPath, "Resources"),
                Path.Combine(projectPath, "Assets", "Resources"),
                Path.Combine(projectPath, "Assets"),
                projectPath
            };
            
            foreach (var searchPath in searchPaths)
            {
                if (Directory.Exists(searchPath))
                {
                    var foundFile = Directory.GetFiles(searchPath, fileName, SearchOption.AllDirectories)
                        .FirstOrDefault();
                    if (foundFile != null)
                        return foundFile;
                }
            }
            
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

