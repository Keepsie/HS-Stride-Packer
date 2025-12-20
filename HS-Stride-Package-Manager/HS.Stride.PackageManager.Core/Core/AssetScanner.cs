// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

namespace HS.Stride.Packer.Core
{
    /// <summary>
    /// Scans a Stride project to build a cache of all assets and source files.
    /// - Assets (.sd* files): Indexed by GUID for robust resolution when assets move
    /// - Source files (.png, .fbx, .wav, etc.): Indexed by filename for fallback lookup
    /// Ported from HS.Stride.Editor.Toolkit.
    /// </summary>
    public class AssetScanner
    {
        private readonly string _projectPath;

        // Asset caches (.sd* files)
        private readonly Dictionary<string, ScannedAsset> _assetsByGuid = new();
        private readonly Dictionary<string, ScannedAsset> _assetsByPath = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<ScannedAsset> _allAssets = new();

        // Source file caches (raw resources)
        private readonly Dictionary<string, List<string>> _sourceFilesByName = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _sourceFilesByPath = new(StringComparer.OrdinalIgnoreCase);

        private bool _isScanned;

        // Common source file extensions
        private static readonly HashSet<string> SourceExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            // Images
            ".png", ".jpg", ".jpeg", ".tga", ".dds", ".bmp", ".gif", ".tiff", ".webp", ".hdr",
            // Models
            ".fbx", ".obj", ".dae", ".gltf", ".glb", ".3ds", ".blend",
            // Audio
            ".wav", ".ogg", ".mp3", ".flac", ".aiff",
            // Video
            ".mp4", ".avi", ".mov", ".wmv", ".webm",
            // Fonts
            ".ttf", ".otf",
            // Data
            ".json", ".xml", ".csv", ".txt", ".yaml", ".yml"
        };

        public AssetScanner(string projectPath)
        {
            _projectPath = projectPath;
        }

        /// <summary>
        /// Scans the project for all .sd* assets and source files
        /// </summary>
        public void Scan()
        {
            _assetsByGuid.Clear();
            _assetsByPath.Clear();
            _allAssets.Clear();
            _sourceFilesByName.Clear();
            _sourceFilesByPath.Clear();

            if (!Directory.Exists(_projectPath))
                return;

            // Scan all .sd* asset files in the project
            var assetFiles = Directory.GetFiles(_projectPath, "*.sd*", SearchOption.AllDirectories);

            foreach (var file in assetFiles)
            {
                var asset = ParseAssetFile(file);
                if (asset != null)
                {
                    _allAssets.Add(asset);

                    if (!string.IsNullOrEmpty(asset.Guid))
                        _assetsByGuid[asset.Guid] = asset;

                    if (!string.IsNullOrEmpty(asset.RelativePath))
                        _assetsByPath[asset.RelativePath] = asset;
                }
            }

            // Scan all source files (textures, models, audio, etc.)
            ScanSourceFiles();

            _isScanned = true;
        }

        /// <summary>
        /// Scans for all source files (raw resources) and indexes them by filename
        /// </summary>
        private void ScanSourceFiles()
        {
            try
            {
                var allFiles = Directory.GetFiles(_projectPath, "*.*", SearchOption.AllDirectories);

                foreach (var file in allFiles)
                {
                    var extension = Path.GetExtension(file);
                    if (string.IsNullOrEmpty(extension) || !SourceExtensions.Contains(extension))
                        continue;

                    // Skip files in obj/bin folders
                    if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) ||
                        file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) ||
                        file.Contains("/obj/") || file.Contains("/bin/"))
                        continue;

                    var fileName = Path.GetFileName(file);
                    var relativePath = Path.GetRelativePath(_projectPath, file).Replace('\\', '/');

                    // Index by filename (multiple files can have same name)
                    if (!_sourceFilesByName.TryGetValue(fileName, out var fileList))
                    {
                        fileList = new List<string>();
                        _sourceFilesByName[fileName] = fileList;
                    }
                    fileList.Add(file);

                    // Index by relative path (unique)
                    _sourceFilesByPath[relativePath] = file;
                }
            }
            catch (Exception)
            {
                // Ignore scanning errors
            }
        }

        /// <summary>
        /// Ensures the scanner has been run
        /// </summary>
        public void EnsureScanned()
        {
            if (!_isScanned)
                Scan();
        }

        /// <summary>
        /// Find asset by GUID (most robust for moved assets)
        /// </summary>
        public ScannedAsset? FindAssetByGuid(string guid)
        {
            EnsureScanned();
            return _assetsByGuid.TryGetValue(guid, out var asset) ? asset : null;
        }

        /// <summary>
        /// Find asset by relative path
        /// </summary>
        public ScannedAsset? FindAssetByPath(string relativePath)
        {
            EnsureScanned();

            // Normalize path separators
            var normalizedPath = relativePath.Replace('\\', '/');

            // Try exact match first
            if (_assetsByPath.TryGetValue(normalizedPath, out var asset))
                return asset;

            // Try without extension
            var withoutExt = Path.GetFileNameWithoutExtension(normalizedPath);
            var dir = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/') ?? "";
            var pathWithoutExt = string.IsNullOrEmpty(dir) ? withoutExt : $"{dir}/{withoutExt}";

            return _allAssets.FirstOrDefault(a =>
                a.RelativePath.Equals(pathWithoutExt, StringComparison.OrdinalIgnoreCase) ||
                a.RelativePathWithExtension.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Find asset by name (filename without extension)
        /// </summary>
        public ScannedAsset? FindAssetByName(string name)
        {
            EnsureScanned();
            return _allAssets.FirstOrDefault(a =>
                a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Find assets matching a pattern (supports * and ?)
        /// </summary>
        public List<ScannedAsset> FindAssets(string pattern)
        {
            EnsureScanned();

            var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";

            return _allAssets.Where(a =>
                System.Text.RegularExpressions.Regex.IsMatch(a.Name, regex,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase)).ToList();
        }

        /// <summary>
        /// Get all scanned assets
        /// </summary>
        public List<ScannedAsset> GetAllAssets()
        {
            EnsureScanned();
            return _allAssets.ToList();
        }

        // ==================== SOURCE FILE METHODS ====================

        /// <summary>
        /// Find a source file by its filename (e.g., "texture.png").
        /// Returns the first match if multiple files have the same name.
        /// Use FindSourceFilesByName to get all matches.
        /// </summary>
        public string? FindSourceFile(string fileName)
        {
            EnsureScanned();

            if (_sourceFilesByName.TryGetValue(fileName, out var files) && files.Count > 0)
                return files[0];

            return null;
        }

        /// <summary>
        /// Find all source files with a given filename.
        /// Useful when multiple files share the same name in different folders.
        /// </summary>
        public List<string> FindSourceFilesByName(string fileName)
        {
            EnsureScanned();

            if (_sourceFilesByName.TryGetValue(fileName, out var files))
                return files.ToList();

            return new List<string>();
        }

        /// <summary>
        /// Find a source file by its relative path from project root.
        /// </summary>
        public string? FindSourceFileByPath(string relativePath)
        {
            EnsureScanned();

            var normalizedPath = relativePath.Replace('\\', '/');
            if (_sourceFilesByPath.TryGetValue(normalizedPath, out var fullPath))
                return fullPath;

            return null;
        }

        /// <summary>
        /// Find a source file using multiple strategies:
        /// 1. Try the given path relative to a base directory
        /// 2. Try as relative path from project root
        /// 3. Fall back to filename search
        /// This is the main method for robust source file resolution.
        /// </summary>
        public string? FindSourceFileRobust(string resourcePath, string? relativeToDir = null)
        {
            EnsureScanned();

            // 1. Try relative to given directory
            if (!string.IsNullOrEmpty(relativeToDir))
            {
                var relativeFullPath = Path.GetFullPath(Path.Combine(relativeToDir, resourcePath));
                if (File.Exists(relativeFullPath))
                    return relativeFullPath;
            }

            // 2. Try as path relative to project root
            var fromProjectRoot = Path.Combine(_projectPath, resourcePath);
            if (File.Exists(fromProjectRoot))
                return Path.GetFullPath(fromProjectRoot);

            // 3. Try our indexed relative paths
            var normalizedPath = resourcePath.Replace('\\', '/');
            if (_sourceFilesByPath.TryGetValue(normalizedPath, out var indexedPath))
                return indexedPath;

            // 4. Fall back to filename search
            var fileName = Path.GetFileName(resourcePath);
            if (_sourceFilesByName.TryGetValue(fileName, out var files) && files.Count > 0)
            {
                // If there's only one file with this name, return it
                if (files.Count == 1)
                    return files[0];

                // If multiple, try to find best match based on path similarity
                var pathParts = normalizedPath.Split('/');
                var bestMatch = files
                    .Select(f => new { Path = f, Score = CalculatePathSimilarity(f, pathParts) })
                    .OrderByDescending(x => x.Score)
                    .First();

                return bestMatch.Path;
            }

            return null;
        }

        /// <summary>
        /// Calculate how similar a file path is to the expected path parts
        /// </summary>
        private int CalculatePathSimilarity(string fullPath, string[] expectedPathParts)
        {
            var relativePath = Path.GetRelativePath(_projectPath, fullPath).Replace('\\', '/');
            var actualParts = relativePath.Split('/');

            int score = 0;
            foreach (var expected in expectedPathParts)
            {
                if (actualParts.Any(a => a.Equals(expected, StringComparison.OrdinalIgnoreCase)))
                    score++;
            }

            return score;
        }

        /// <summary>
        /// Get count of indexed source files
        /// </summary>
        public int SourceFileCount
        {
            get
            {
                EnsureScanned();
                return _sourceFilesByPath.Count;
            }
        }

        /// <summary>
        /// Parses a Stride asset file to extract GUID and metadata
        /// </summary>
        private ScannedAsset? ParseAssetFile(string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                var assetType = GetAssetTypeFromExtension(extension);

                // Read first 20 lines to find the Id field
                var lines = File.ReadLines(filePath).Take(20).ToList();
                var idLine = lines.FirstOrDefault(l => l.Trim().StartsWith("Id: "));

                string guid = string.Empty;
                if (idLine != null)
                {
                    var idIndex = idLine.IndexOf("Id: ", StringComparison.Ordinal);
                    if (idIndex >= 0)
                        guid = idLine.Substring(idIndex + 4).Trim();
                }

                var name = Path.GetFileNameWithoutExtension(filePath);
                var relativePath = Path.GetRelativePath(_projectPath, filePath).Replace('\\', '/');

                // Path without extension (how Stride references assets)
                var lastDot = relativePath.LastIndexOf('.');
                var relativePathNoExt = lastDot != -1 ? relativePath.Substring(0, lastDot) : relativePath;

                return new ScannedAsset
                {
                    Guid = guid,
                    Name = name,
                    RelativePath = relativePathNoExt,
                    RelativePathWithExtension = relativePath,
                    FullPath = filePath,
                    AssetType = assetType,
                    Extension = extension
                };
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static string GetAssetTypeFromExtension(string extension)
        {
            return extension switch
            {
                ".sdprefab" => "Prefab",
                ".sdm3d" => "Model",
                ".sdmat" => "Material",
                ".sdtex" => "Texture",
                ".sdscene" => "Scene",
                ".sdsnd" => "Sound",
                ".sdanim" => "Animation",
                ".sdskel" => "Skeleton",
                ".sdsheet" => "SpriteSheet",
                ".sdsprite" => "Sprite",
                ".sdfx" => "Effect",
                ".sdpage" => "UIPage",
                ".sduilib" => "UILibrary",
                ".sdspritefnt" => "SpriteFont",
                ".sdfnt" => "SpriteFont",
                ".sdskybox" => "Skybox",
                ".sdvideo" => "Video",
                ".sdrendertex" => "RenderTexture",
                ".sdgamesettings" => "GameSettings",
                ".sdgfxcomp" => "GraphicsCompositor",
                ".sdarch" => "Archetype",
                ".sdphys" => "ColliderShape",
                ".sdconvex" => "ConvexHull",
                ".sdraw" => "RawAsset",
                _ => "Unknown"
            };
        }
    }

    /// <summary>
    /// Represents a scanned Stride asset with its GUID and path information
    /// </summary>
    public class ScannedAsset
    {
        public string Guid { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string RelativePathWithExtension { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string AssetType { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;

        /// <summary>
        /// Returns the reference string format used by Stride: "guid:path"
        /// </summary>
        public string Reference => $"{Guid}:{RelativePath}";

        public override string ToString() => $"{Name} ({AssetType}) - {Guid}";
    }
}
