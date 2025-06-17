// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

namespace HS.Stride.Packer.Core
{
    public class ImportSettings
    {
        public string PackagePath { get; set; } = string.Empty;
        public string TargetProjectPath { get; set; } = string.Empty;
        public bool OverwriteExistingFiles { get; set; } = true;
    }
}