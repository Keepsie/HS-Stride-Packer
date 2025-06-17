// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

namespace HS.Stride.Packer.Core
{
    public class NamespaceReference
    {
        public string Namespace { get; set; } = string.Empty;
        public List<string> FoundInFiles { get; set; } = new();
    }

}
