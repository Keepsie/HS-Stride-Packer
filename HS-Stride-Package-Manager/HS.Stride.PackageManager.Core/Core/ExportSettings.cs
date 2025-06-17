// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

namespace HS.Stride.Packer.Core
{
    public class ExportSettings
    {
        public string LibraryPath { get; set; } = string.Empty;
        public string OutputPath { get; set; } = string.Empty;
        public PackageManifest Manifest { get; set; } = new();
        public List<string> ExcludeNamespaces { get; set; } = new();
        public List<string> IncludeFiles { get; set; } = new();
        public List<string> ExcludeFiles { get; set; } = new();
        
        
        // Phase 1: Asset Selection
        public List<string> SelectedAssetFolders { get; set; } = new();
        
        
        // Phase 2: Code Selection
        public List<string> SelectedCodeFolders { get; set; } = new();
        public List<string> SelectedPlatformFolders { get; set; } = new();
        
        
        // Phase 3: Resource Organization
        public List<ResourceDependency> ResourceDependencies { get; set; } = new();
        public string TargetResourcePath { get; set; } = string.Empty;
        
        public bool ExportRegistryJson { get; set; } = true;
        
    }

    //Important less dependent on regex and searching if we just store and replace.
    public class ResourceDependency
    {
        public string FileName { get; set; } = string.Empty;
        public string OriginalPath { get; set; } = string.Empty;
        public string ActualPath { get; set; } = string.Empty;
        public string ReferencedInAsset { get; set; } = string.Empty;

        // List of all the ways this resource is referenced across assets
        public List<ResourceReference> References { get; set; } = new();
        
        public string AssetFilePath { get; set; } = string.Empty;
        public string OriginalPathInAsset { get; set; } = string.Empty;
        public string NewResourcePath { get; set; } = string.Empty;
        
        public override bool Equals(object? obj)
        {
            return obj is ResourceDependency other && ActualPath.Equals(other.ActualPath, StringComparison.OrdinalIgnoreCase);
        }
        
        public override int GetHashCode()
        {
            return ActualPath.GetHashCode(StringComparison.OrdinalIgnoreCase);
        }
    }
}

