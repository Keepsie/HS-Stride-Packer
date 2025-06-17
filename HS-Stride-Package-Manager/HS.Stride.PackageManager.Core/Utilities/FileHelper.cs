// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

namespace HS.Stride.Packer.Utilities
{
    public static class FileHelper
    {
        public static bool SaveFile(string content, string filePath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? ".");
                File.WriteAllText(filePath, content);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string LoadFile(string filePath)
        {
            try
            {
                return File.Exists(filePath) ? File.ReadAllText(filePath) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool DeleteFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static List<string> GetFilesInDirectory(string dirPath, string pattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            try
            {
                return Directory.Exists(dirPath)
                    ? Directory.GetFiles(dirPath, pattern, searchOption).ToList()
                    : new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        public static bool EnsureDirectoryExists(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool MoveFile(string sourcePath, string destinationPath)
        {
            try
            {
                if (File.Exists(sourcePath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? ".");
                    File.Move(sourcePath, destinationPath);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static DateTime? GetFileLastModified(string filePath)
        {
            try
            {
                return File.Exists(filePath) ? File.GetLastWriteTime(filePath) : null;
            }
            catch
            {
                return null;
            }
        }

        public static bool CopyDirectory(string sourceDir, string destinationDir)
        {
            try
            {
                Directory.CreateDirectory(destinationDir);

                foreach (string file in Directory.GetFiles(sourceDir))
                {
                    string destFile = Path.Combine(destinationDir, Path.GetFileName(file));
                    File.Copy(file, destFile, true);
                }

                foreach (string dir in Directory.GetDirectories(sourceDir))
                {
                    string destDir = Path.Combine(destinationDir, Path.GetFileName(dir));
                    CopyDirectory(dir, destDir);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string GenerateUniqueFileName(string baseName, string extension = "", string directory = "")
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var milliseconds = DateTime.Now.Millisecond.ToString("D3");
            var random = new Random().Next(1000, 9999);
            var fileName = $"{baseName}_{timestamp}_{milliseconds}_{random}{extension}";

            if (!string.IsNullOrEmpty(directory))
            {
                return Path.Combine(directory, fileName);
            }

            return fileName;
        }
    }
}

