// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

namespace HS.Stride.Packer.Core
{
    public class ProjectScanner
    {
        public ProjectScanResult ScanProject(string projectPath)
        {
            if (!Directory.Exists(projectPath))
                throw new DirectoryNotFoundException($"Project path does not exist: {projectPath}");

            var result = new ProjectScanResult();
            
            // Get all files and folders recursively
            var allItems = GetAllItems(projectPath, projectPath);
            result.AllItems = allItems;
            
            // Separate files and folders
            result.Files = allItems.Where(item => item.IsFile).ToList();
            result.Folders = allItems.Where(item => !item.IsFile).ToList();
            
            return result;
        }
        
        private List<ProjectItem> GetAllItems(string currentPath, string rootPath)
        {
            var items = new List<ProjectItem>();
            
            try
            {
                // Add current directory (except root)
                if (currentPath != rootPath)
                {
                    var relativePath = Path.GetRelativePath(rootPath, currentPath);
                    items.Add(new ProjectItem
                    {
                        Name = Path.GetFileName(currentPath),
                        RelativePath = relativePath,
                        FullPath = currentPath,
                        IsFile = false,
                        Size = 0
                    });
                }
                
                // Add all files in current directory
                foreach (var file in Directory.GetFiles(currentPath))
                {
                    var fileInfo = new FileInfo(file);
                    var relativePath = Path.GetRelativePath(rootPath, file);
                    
                    items.Add(new ProjectItem
                    {
                        Name = Path.GetFileName(file),
                        RelativePath = relativePath,
                        FullPath = file,
                        IsFile = true,
                        Size = fileInfo.Length
                    });
                }
                
                // Recursively add subdirectories
                foreach (var directory in Directory.GetDirectories(currentPath))
                {
                    items.AddRange(GetAllItems(directory, rootPath));
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we can't access
            }
            catch (DirectoryNotFoundException)
            {
                // Skip if directory was deleted during scan
            }
            
            return items;
        }
    }
    
    public class ProjectScanResult
    {
        public List<ProjectItem> AllItems { get; set; } = new();
        public List<ProjectItem> Files { get; set; } = new();
        public List<ProjectItem> Folders { get; set; } = new();
        
        public long TotalSize => Files.Sum(f => f.Size);
        public int FileCount => Files.Count;
        public int FolderCount => Folders.Count;
    }
    
    public class ProjectItem
    {
        public string Name { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsFile { get; set; }
        public long Size { get; set; }
        
        public string DisplaySize => IsFile ? FormatFileSize(Size) : "";
        
        public override string ToString()
        {
            if (IsFile)
            {
                return $"{RelativePath} [{DisplaySize}]";
            }
            else
            {
                return $"{RelativePath}/ [Folder]";
            }
        }
        
        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }
    }
}