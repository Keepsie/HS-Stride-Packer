// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

namespace HS.Stride.Packer.Utilities
{
    public static class PathHelper
    {
        public static bool IsPathWithinDirectory(string filePath, string directoryPath)
        {
            try
            {
                var fullFilePath = Path.GetFullPath(filePath);
                var fullDirectoryPath = Path.GetFullPath(directoryPath);

                return fullFilePath.StartsWith(fullDirectoryPath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static string NormalizePath(string path)
        {
            try
            {
                return Path.GetFullPath(path).Replace('\\', '/');
            }
            catch
            {
                return path;
            }
        }

        public static string GetRelativePathFromTo(string fromPath, string toPath)
        {
            try
            {
                return Path.GetRelativePath(fromPath, toPath).Replace('\\', '/');
            }
            catch
            {
                return toPath;
            }
        }

        public static bool IsStrideProject(string directoryPath)
        {
            var validation = ValidateStrideProject(directoryPath);
            return validation.IsValid;
        }

        public static ProjectValidationResult ValidateStrideProject(string directoryPath)
        {
            var result = new ProjectValidationResult();

            try
            {
                if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
                {
                    result.IsValid = false;
                    result.ErrorMessage = "Directory does not exist";
                    return result;
                }

                // Check for .sln files in the root directory
                var slnFiles = Directory.GetFiles(directoryPath, "*.sln", SearchOption.TopDirectoryOnly);
                result.HasSolutionFile = slnFiles.Any();

                // Check for .sdpkg files (can be in subdirectories)
                var sdpkgFiles = Directory.GetFiles(directoryPath, "*.sdpkg", SearchOption.AllDirectories);
                result.HasStridePackages = sdpkgFiles.Any();

                // Determine validity and create helpful messages
                if (result.HasSolutionFile && result.HasStridePackages)
                {
                    result.IsValid = true;
                    result.SuccessMessage = "✓ Valid Stride project (Visual Studio solution with Stride packages)";
                }
                else if (!result.HasSolutionFile && !result.HasStridePackages)
                {
                    result.IsValid = false;
                    result.ErrorMessage = "❌ Not a Stride project root. Please select the Visual Studio solution folder containing the .sln file";
                    result.Suggestions.Add("Look for a folder containing a .sln file (Visual Studio solution)");
                    result.Suggestions.Add("The packer will automatically find Stride packages in the project");
                }
                else if (!result.HasSolutionFile)
                {
                    result.IsValid = false;
                    result.ErrorMessage = "❌ Found Stride packages but no Visual Studio solution. Please select the folder containing the .sln file";
                    result.Suggestions.Add("Look for the directory containing the .sln file");
                }
                else // !result.HasStridePackages
                {
                    result.IsValid = false;
                    result.ErrorMessage = "❌ Found Visual Studio solution but no Stride packages. This may not be a Stride project";
                    result.Suggestions.Add("Ensure this is a Stride game project, not just any Visual Studio solution");
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = $"Error validating project: {ex.Message}";
            }

            return result;
        }

        public static bool IsStrideAsset(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            return extension.StartsWith(".sd");
        }

        public static string GetAssetTypeFromExtension(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            return extension switch
            {
                ".sdprefab" => "Prefab",
                ".sdscene" => "Scene",
                ".sdmat" => "Material",
                ".sdfx" => "Effect",
                ".sdtex" => "Texture",
                ".sdm3d" => "Model",
                ".sdsnd" => "Sound",
                ".sdpage" => "UIPage",
                ".sdpkg" => "Package",
                ".sdskel" => "Skeleton",
                ".sdanim" => "Animation",
                ".sdsheet" => "SpriteSheet",
                _ => "Unknown"
            };
        }

        public static string MakePackageFileName(string packageName, string version)
        {
            var safeName = string.Join("_", packageName.Split(Path.GetInvalidFileNameChars()));
            var safeVersion = string.Join("_", version.Split(Path.GetInvalidFileNameChars()));
            return $"{safeName}-{safeVersion}.stridepackage";
        }

        public static string GetProjectRootFromAsset(string assetPath)
        {
            try
            {
                var directory = new DirectoryInfo(Path.GetDirectoryName(assetPath) ?? ".");

                while (directory != null)
                {
                    if (Directory.GetFiles(directory.FullName, "*.sdpkg").Any())
                    {
                        return directory.FullName;
                    }
                    directory = directory.Parent;
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    public class ProjectValidationResult
    {
        public bool IsValid { get; set; }
        public bool HasSolutionFile { get; set; }
        public bool HasStridePackages { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string SuccessMessage { get; set; } = string.Empty;
        public List<string> Suggestions { get; set; } = new();
    }
}

