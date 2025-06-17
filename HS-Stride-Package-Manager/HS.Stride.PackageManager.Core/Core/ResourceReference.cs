// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

namespace HS.Stride.Packer.Core
{
    public class ResourceReference
    {
        public string AssetFile { get; set; } = string.Empty;
        public string ResourcePath { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public ResourceReferenceType Type { get; set; }
    }

    public enum ResourceReferenceType
    {
        FileReference,
        SourceReference,
        EmbeddedReference
    }
}

