// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using System.Text.RegularExpressions;

namespace HS.Stride.Packer.Core
{
    /// <summary>
    /// Service for cleaning up Stride projects by finding orphaned resources,
    /// reorganizing misplaced files, and removing empty folders.
    /// </summary>
    public class ProjectCleanupService
    {
        private readonly string _projectPath;
        private AssetScanner? _scanner;

        public ProjectCleanupService(string projectPath)
        {
            _projectPath = projectPath;
        }

        /// <summary>
        /// Scans the project and returns a complete cleanup analysis
        /// </summary>
        public async Task<CleanupAnalysis> AnalyzeProjectAsync()
        {
            return await Task.Run(() => AnalyzeProject());
        }

        private CleanupAnalysis AnalyzeProject()
        {
            var analysis = new CleanupAnalysis();

            if (!Directory.Exists(_projectPath))
            {
                analysis.Errors.Add($"Project path does not exist: {_projectPath}");
                return analysis;
            }

            // Initialize scanner
            _scanner = new AssetScanner(_projectPath);
            _scanner.Scan();

            // Step 1: Find all resource references from all assets
            var allReferences = CollectAllResourceReferences();
            analysis.TotalAssets = _scanner.GetAllAssets().Count;

            // Step 2: Get all source files in the project
            var allSourceFiles = GetAllSourceFiles();
            analysis.TotalResources = allSourceFiles.Count;

            // Step 3: Determine which files are referenced and which are orphaned
            var referencedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var reference in allReferences)
            {
                if (!string.IsNullOrEmpty(reference.ResolvedPath))
                {
                    referencedFiles.Add(reference.ResolvedPath);
                }
            }

            // Step 4: Find orphaned files (in Resources folder but not referenced)
            foreach (var sourceFile in allSourceFiles)
            {
                var relativePath = Path.GetRelativePath(_projectPath, sourceFile).Replace('\\', '/');

                // Only check files in Resources folder for orphan status
                if (!relativePath.Contains("/Resources/") && !relativePath.StartsWith("Resources/"))
                    continue;

                if (!referencedFiles.Contains(sourceFile))
                {
                    var fileInfo = new FileInfo(sourceFile);
                    analysis.OrphanedResources.Add(new OrphanedResource
                    {
                        FullPath = sourceFile,
                        RelativePath = relativePath,
                        FileName = Path.GetFileName(sourceFile),
                        SizeBytes = fileInfo.Length,
                        Extension = Path.GetExtension(sourceFile).ToLowerInvariant()
                    });
                }
            }

            // Step 5: Find misplaced resources (could be better organized)
            foreach (var reference in allReferences)
            {
                if (string.IsNullOrEmpty(reference.ResolvedPath))
                    continue;

                var suggestion = SuggestBetterLocation(reference);
                if (suggestion != null)
                {
                    analysis.MisplacedResources.Add(suggestion);
                }
            }

            // Step 6: Find empty folders
            analysis.EmptyFolders = FindEmptyFolders();

            // Step 7: Group orphans by folder for bulk delete option
            analysis.OrphanedFolders = GroupOrphansByFolder(analysis.OrphanedResources);

            return analysis;
        }

        /// <summary>
        /// Collects all resource references from all .sd* asset files
        /// </summary>
        private List<ResolvedReference> CollectAllResourceReferences()
        {
            var references = new List<ResolvedReference>();
            var assets = _scanner?.GetAllAssets() ?? new List<ScannedAsset>();

            foreach (var asset in assets)
            {
                var assetRefs = ScanAssetForReferences(asset.FullPath);
                foreach (var refPath in assetRefs)
                {
                    var resolved = _scanner?.FindSourceFileRobust(refPath, Path.GetDirectoryName(asset.FullPath));
                    references.Add(new ResolvedReference
                    {
                        OriginalPath = refPath,
                        ResolvedPath = resolved ?? "",
                        ReferencingAsset = asset.FullPath,
                        AssetRelativePath = asset.RelativePathWithExtension
                    });
                }
            }

            return references;
        }

        /// <summary>
        /// Scans a single asset file for resource references
        /// </summary>
        private List<string> ScanAssetForReferences(string assetPath)
        {
            var references = new List<string>();

            try
            {
                var content = File.ReadAllText(assetPath);

                // Pattern 1: Source: !file path or Source: path
                var sourcePattern = @"Source:\s*(?:!file\s+)?([^\r\n]+)";
                foreach (Match match in Regex.Matches(content, sourcePattern))
                {
                    var path = match.Groups[1].Value.Trim().Trim('"');
                    if (!string.IsNullOrEmpty(path) && path != "null")
                        references.Add(path);
                }

                // Pattern 2: !file "path"
                var filePattern = @"!file\s+""([^""]+)""";
                foreach (Match match in Regex.Matches(content, filePattern))
                {
                    var path = match.Groups[1].Value;
                    if (!string.IsNullOrEmpty(path))
                        references.Add(path);
                }

                // Pattern 3: Common resource paths in quotes with extensions
                var resourceExtensions = new[] { ".png", ".jpg", ".jpeg", ".tga", ".dds", ".fbx", ".obj", ".dae", ".wav", ".ogg", ".mp3" };
                foreach (var ext in resourceExtensions)
                {
                    var pattern = $@"""([^""]*{Regex.Escape(ext)})""";
                    foreach (Match match in Regex.Matches(content, pattern, RegexOptions.IgnoreCase))
                    {
                        var path = match.Groups[1].Value;
                        if (!string.IsNullOrEmpty(path))
                            references.Add(path);
                    }
                }
            }
            catch (Exception)
            {
                // Ignore read errors
            }

            return references.Distinct().ToList();
        }

        /// <summary>
        /// Gets all source files in the project
        /// </summary>
        private List<string> GetAllSourceFiles()
        {
            var files = new List<string>();
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png", ".jpg", ".jpeg", ".tga", ".dds", ".bmp", ".gif", ".hdr",
                ".fbx", ".obj", ".dae", ".gltf", ".glb", ".3ds",
                ".wav", ".ogg", ".mp3", ".flac",
                ".ttf", ".otf"
            };

            try
            {
                foreach (var file in Directory.GetFiles(_projectPath, "*.*", SearchOption.AllDirectories))
                {
                    // Skip bin/obj folders
                    if (file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) ||
                        file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                        continue;

                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (extensions.Contains(ext))
                        files.Add(file);
                }
            }
            catch (Exception)
            {
                // Ignore access errors
            }

            return files;
        }

        /// <summary>
        /// Suggests a better location for a resource based on the asset that uses it
        /// </summary>
        private MisplacedResource? SuggestBetterLocation(ResolvedReference reference)
        {
            if (string.IsNullOrEmpty(reference.ResolvedPath))
                return null;

            var resourceRelPath = Path.GetRelativePath(_projectPath, reference.ResolvedPath).Replace('\\', '/');
            var assetRelPath = reference.AssetRelativePath.Replace('\\', '/');

            // Extract the asset's folder structure (e.g., "Assets/Characters/Hero.sdmodel" -> "Characters")
            var assetFolder = GetAssetFolder(assetRelPath);
            if (string.IsNullOrEmpty(assetFolder))
                return null;

            // Check if resource is already in a matching folder
            if (resourceRelPath.Contains($"/{assetFolder}/") || resourceRelPath.Contains($"\\{assetFolder}\\"))
                return null;

            // Suggest moving to Resources/{assetFolder}/
            var fileName = Path.GetFileName(reference.ResolvedPath);
            var suggestedPath = $"Resources/{assetFolder}/{fileName}";

            // Don't suggest if already there
            if (resourceRelPath.Equals(suggestedPath, StringComparison.OrdinalIgnoreCase))
                return null;

            return new MisplacedResource
            {
                FullPath = reference.ResolvedPath,
                CurrentRelativePath = resourceRelPath,
                SuggestedRelativePath = suggestedPath,
                ReferencingAsset = reference.AssetRelativePath,
                FileName = fileName
            };
        }

        /// <summary>
        /// Extracts the main folder from an asset path
        /// </summary>
        private string GetAssetFolder(string assetPath)
        {
            // "Assets/Characters/Hero.sdmodel" -> "Characters"
            // "ProjectName/Assets/Items/Sword.sdtex" -> "Items"
            var parts = assetPath.Split('/');

            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (parts[i].Equals("Assets", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length - 1)
                {
                    return parts[i + 1];
                }
            }

            return "";
        }

        /// <summary>
        /// Finds all empty folders in the project
        /// </summary>
        private List<EmptyFolder> FindEmptyFolders()
        {
            var emptyFolders = new List<EmptyFolder>();

            try
            {
                // Get all directories, sorted by depth (deepest first for proper cleanup)
                var allDirs = Directory.GetDirectories(_projectPath, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Count(c => c == Path.DirectorySeparatorChar || c == '/'))
                    .ToList();

                foreach (var dir in allDirs)
                {
                    // Skip bin/obj folders
                    if (dir.Contains(Path.DirectorySeparatorChar + "bin") ||
                        dir.Contains(Path.DirectorySeparatorChar + "obj") ||
                        dir.Contains("/.git") || dir.Contains("\\.git"))
                        continue;

                    if (IsDirectoryEmpty(dir))
                    {
                        emptyFolders.Add(new EmptyFolder
                        {
                            FullPath = dir,
                            RelativePath = Path.GetRelativePath(_projectPath, dir).Replace('\\', '/')
                        });
                    }
                }
            }
            catch (Exception)
            {
                // Ignore access errors
            }

            return emptyFolders;
        }

        private bool IsDirectoryEmpty(string path)
        {
            try
            {
                return !Directory.EnumerateFileSystemEntries(path).Any();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Groups orphaned resources by their parent folder
        /// </summary>
        private List<OrphanedFolder> GroupOrphansByFolder(List<OrphanedResource> orphans)
        {
            return orphans
                .GroupBy(o => Path.GetDirectoryName(o.RelativePath)?.Replace('\\', '/') ?? "")
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .Select(g => new OrphanedFolder
                {
                    RelativePath = g.Key,
                    FullPath = Path.Combine(_projectPath, g.Key.Replace('/', Path.DirectorySeparatorChar)),
                    OrphanCount = g.Count(),
                    TotalSizeBytes = g.Sum(o => o.SizeBytes),
                    AllFilesOrphaned = AreAllFilesInFolderOrphaned(g.Key, orphans)
                })
                .OrderByDescending(f => f.OrphanCount)
                .ToList();
        }

        private bool AreAllFilesInFolderOrphaned(string folderRelPath, List<OrphanedResource> orphans)
        {
            var folderFullPath = Path.Combine(_projectPath, folderRelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(folderFullPath))
                return false;

            try
            {
                var filesInFolder = Directory.GetFiles(folderFullPath, "*.*", SearchOption.TopDirectoryOnly);
                var orphanPaths = new HashSet<string>(orphans.Select(o => o.FullPath), StringComparer.OrdinalIgnoreCase);

                return filesInFolder.All(f => orphanPaths.Contains(f));
            }
            catch
            {
                return false;
            }
        }

        // ==================== CLEANUP ACTIONS ====================

        /// <summary>
        /// Deletes the specified orphaned resources
        /// </summary>
        public async Task<CleanupResult> DeleteOrphansAsync(IEnumerable<OrphanedResource> orphans)
        {
            return await Task.Run(() =>
            {
                var result = new CleanupResult();

                foreach (var orphan in orphans)
                {
                    try
                    {
                        if (File.Exists(orphan.FullPath))
                        {
                            File.Delete(orphan.FullPath);
                            result.DeletedFiles.Add(orphan.RelativePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Failed to delete {orphan.RelativePath}: {ex.Message}");
                    }
                }

                return result;
            });
        }

        /// <summary>
        /// Deletes all files in the specified folders
        /// </summary>
        public async Task<CleanupResult> DeleteFoldersAsync(IEnumerable<OrphanedFolder> folders)
        {
            return await Task.Run(() =>
            {
                var result = new CleanupResult();

                foreach (var folder in folders)
                {
                    try
                    {
                        if (Directory.Exists(folder.FullPath))
                        {
                            Directory.Delete(folder.FullPath, true);
                            result.DeletedFolders.Add(folder.RelativePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Failed to delete folder {folder.RelativePath}: {ex.Message}");
                    }
                }

                return result;
            });
        }

        /// <summary>
        /// Removes all empty folders from the project
        /// </summary>
        public async Task<CleanupResult> CleanEmptyFoldersAsync()
        {
            return await Task.Run(() =>
            {
                var result = new CleanupResult();

                // Keep cleaning until no more empty folders are found
                // (deleting a folder may make its parent empty)
                int passes = 0;
                const int maxPasses = 50; // Safety limit

                while (passes < maxPasses)
                {
                    var emptyFolders = FindEmptyFolders();
                    if (!emptyFolders.Any())
                        break;

                    int deletedThisPass = 0;

                    // Delete in order (deepest first)
                    foreach (var folder in emptyFolders)
                    {
                        try
                        {
                            if (Directory.Exists(folder.FullPath) && IsDirectoryEmpty(folder.FullPath))
                            {
                                Directory.Delete(folder.FullPath);
                                result.DeletedFolders.Add(folder.RelativePath);
                                deletedThisPass++;
                            }
                        }
                        catch (Exception ex)
                        {
                            result.Errors.Add($"Failed to delete empty folder {folder.RelativePath}: {ex.Message}");
                        }
                    }

                    // If we didn't delete anything this pass, stop
                    if (deletedThisPass == 0)
                        break;

                    passes++;
                }

                return result;
            });
        }

        /// <summary>
        /// Moves resources to match their asset folder structure
        /// </summary>
        public async Task<CleanupResult> ReorganizeResourcesAsync(IEnumerable<MisplacedResource> resources)
        {
            return await Task.Run(() =>
            {
                var result = new CleanupResult();
                var resourceList = resources.ToList();

                if (!resourceList.Any())
                    return result;

                // Step 1: Build index of all asset files and their contents ONCE
                var allAssetFiles = Directory.GetFiles(_projectPath, "*.sd*", SearchOption.AllDirectories)
                    .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) &&
                               !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                    .ToList();

                // Create a dictionary mapping asset file -> its content
                var assetContents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var assetFile in allAssetFiles)
                {
                    try
                    {
                        assetContents[assetFile] = File.ReadAllText(assetFile);
                    }
                    catch
                    {
                        // Skip unreadable files
                    }
                }

                // Step 2: Build list of all filenames we're moving
                var filenamesToMove = resourceList.Select(r => Path.GetFileName(r.CurrentRelativePath)).ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Step 3: Build index of which asset files contain which filenames
                var filenamesToAssets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var (assetPath, content) in assetContents)
                {
                    foreach (var filename in filenamesToMove)
                    {
                        if (content.Contains(filename, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!filenamesToAssets.ContainsKey(filename))
                                filenamesToAssets[filename] = new List<string>();
                            filenamesToAssets[filename].Add(assetPath);
                        }
                    }
                }

                // Track which asset files were modified (we'll write them once at the end)
                var modifiedAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Step 4: Process each resource
                foreach (var resource in resourceList)
                {
                    try
                    {
                        var targetPath = Path.Combine(_projectPath, resource.SuggestedRelativePath.Replace('/', Path.DirectorySeparatorChar));
                        var targetDir = Path.GetDirectoryName(targetPath);

                        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }

                        if (File.Exists(resource.FullPath) && !File.Exists(targetPath))
                        {
                            File.Move(resource.FullPath, targetPath);
                            result.MovedFiles.Add($"{resource.CurrentRelativePath} -> {resource.SuggestedRelativePath}");

                            // Update references in all affected asset files
                            var oldFileName = Path.GetFileName(resource.CurrentRelativePath);
                            var newFullPath = targetPath;

                            if (filenamesToAssets.TryGetValue(oldFileName, out var affectedAssets))
                            {
                                foreach (var assetPath in affectedAssets)
                                {
                                    if (assetContents.TryGetValue(assetPath, out var content))
                                    {
                                        var updatedContent = UpdateAssetContent(content, assetPath, oldFileName, newFullPath);
                                        if (updatedContent != content)
                                        {
                                            assetContents[assetPath] = updatedContent;
                                            modifiedAssets.Add(assetPath);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Failed to move {resource.FileName}: {ex.Message}");
                    }
                }

                // Step 5: Write back all modified asset files
                foreach (var assetPath in modifiedAssets)
                {
                    try
                    {
                        if (assetContents.TryGetValue(assetPath, out var content))
                        {
                            File.WriteAllText(assetPath, content);
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Failed to update asset {Path.GetFileName(assetPath)}: {ex.Message}");
                    }
                }

                return result;
            });
        }

        /// <summary>
        /// Updates asset content with new resource path. Returns updated content string.
        /// Uses line-by-line parsing for reliability (similar to HS.Stride.Editor.Toolkit approach)
        /// </summary>
        private string UpdateAssetContent(string content, string assetFullPath, string oldFileName, string newFullPath)
        {
            // Quick check - does this file even mention the filename?
            if (!content.Contains(oldFileName, StringComparison.OrdinalIgnoreCase))
                return content;

            var assetDir = Path.GetDirectoryName(assetFullPath)!;
            var newRelative = Path.GetRelativePath(assetDir, newFullPath).Replace('\\', '/');

            var lines = content.Split('\n');
            bool modified = false;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.TrimStart();

                // Check if this line contains the filename we're looking for
                if (!trimmed.Contains(oldFileName, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Pattern 1: Source: !file path/to/file.ext
                if (trimmed.StartsWith("Source:", StringComparison.OrdinalIgnoreCase))
                {
                    var indent = line.Substring(0, line.Length - line.TrimStart().Length);

                    if (trimmed.Contains("!file", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = $"{indent}Source: !file {newRelative}";
                    }
                    else
                    {
                        lines[i] = $"{indent}Source: {newRelative}";
                    }
                    modified = true;
                    continue;
                }

                // Pattern 2: !file "path/to/file.ext" (quoted)
                if (trimmed.Contains("!file", StringComparison.OrdinalIgnoreCase) &&
                    trimmed.Contains("\"", StringComparison.Ordinal))
                {
                    var newLine = Regex.Replace(line,
                        $@"(!file\s+"")[^""]*{Regex.Escape(oldFileName)}("")",
                        $"$1{newRelative}$2",
                        RegexOptions.IgnoreCase);

                    if (newLine != line)
                    {
                        lines[i] = newLine;
                        modified = true;
                    }
                }
            }

            return modified ? string.Join("\n", lines) : content;
        }
    }

    // ==================== DATA MODELS ====================

    public class CleanupAnalysis
    {
        public int TotalAssets { get; set; }
        public int TotalResources { get; set; }
        public List<OrphanedResource> OrphanedResources { get; set; } = new();
        public List<MisplacedResource> MisplacedResources { get; set; } = new();
        public List<EmptyFolder> EmptyFolders { get; set; } = new();
        public List<OrphanedFolder> OrphanedFolders { get; set; } = new();
        public List<string> Errors { get; set; } = new();

        public long TotalOrphanedSize => OrphanedResources.Sum(o => o.SizeBytes);
        public bool HasIssues => OrphanedResources.Any() || MisplacedResources.Any() || EmptyFolders.Any();
    }

    public class OrphanedResource
    {
        public string FullPath { get; set; } = "";
        public string RelativePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public long SizeBytes { get; set; }
        public string Extension { get; set; } = "";
        public bool IsSelected { get; set; } = true;

        public string SizeDisplay => SizeBytes switch
        {
            < 1024 => $"{SizeBytes} B",
            < 1024 * 1024 => $"{SizeBytes / 1024.0:F1} KB",
            _ => $"{SizeBytes / (1024.0 * 1024.0):F1} MB"
        };
    }

    public class MisplacedResource
    {
        public string FullPath { get; set; } = "";
        public string CurrentRelativePath { get; set; } = "";
        public string SuggestedRelativePath { get; set; } = "";
        public string ReferencingAsset { get; set; } = "";
        public string FileName { get; set; } = "";
        public bool IsSelected { get; set; } = true;
    }

    public class EmptyFolder
    {
        public string FullPath { get; set; } = "";
        public string RelativePath { get; set; } = "";
    }

    public class OrphanedFolder
    {
        public string FullPath { get; set; } = "";
        public string RelativePath { get; set; } = "";
        public int OrphanCount { get; set; }
        public long TotalSizeBytes { get; set; }
        public bool AllFilesOrphaned { get; set; }
        public bool IsSelected { get; set; }

        public string SizeDisplay => TotalSizeBytes switch
        {
            < 1024 => $"{TotalSizeBytes} B",
            < 1024 * 1024 => $"{TotalSizeBytes / 1024.0:F1} KB",
            _ => $"{TotalSizeBytes / (1024.0 * 1024.0):F1} MB"
        };
    }

    public class CleanupResult
    {
        public List<string> DeletedFiles { get; set; } = new();
        public List<string> DeletedFolders { get; set; } = new();
        public List<string> MovedFiles { get; set; } = new();
        public List<string> Errors { get; set; } = new();

        public bool HasErrors => Errors.Any();
        public int TotalActions => DeletedFiles.Count + DeletedFolders.Count + MovedFiles.Count;
    }

    public class ResolvedReference
    {
        public string OriginalPath { get; set; } = "";
        public string ResolvedPath { get; set; } = "";
        public string ReferencingAsset { get; set; } = "";
        public string AssetRelativePath { get; set; } = "";
    }
}
