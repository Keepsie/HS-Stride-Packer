// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

namespace HS.Stride.Packer.Core
{
    public class PackageManifest
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty; 
        public string Description { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string StrideVersion { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public List<NamespaceReference> Namespaces { get; set; } = new();
        public string PackageHash { get; set; } = string.Empty;  // SHA-256 
        public List<string> Tags { get; set; } = new(); // UI, effects, materials, templates, etc.
        
        // Project structure detection
        public ProjectStructureType StructureType { get; set; } = ProjectStructureType.Unknown;
        public string ProjectName { get; set; } = string.Empty;
        
        // Resource path mapping (original → clean paths)
        public Dictionary<string, string> ResourcePathMappings { get; set; } = new();
        public string ResourceTargetPath { get; set; } = string.Empty; // e.g., "Resources/ProjectName"
        
        // Registry metadata (optional - filled by user after export)
        public string? DownloadUrl { get; set; }
        public string? Homepage { get; set; }
        public string? Repository { get; set; }
        public string? License { get; set; }
    }
    
    public enum ProjectStructureType
    {
        Unknown,
        Fresh,    // ProjectName/Assets/ structure
        Template  // Assets/ at root structure
    }
}

