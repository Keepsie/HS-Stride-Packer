// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using System.Text;

namespace HS.Stride.Packer.Core
{
    public class ValidationResult
    {
        public List<ExternalResourceIssue> ExternalResources { get; set; } = new();
        public List<MissingResourceIssue> MissingResources { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        
        // Combined validation + dependency detection
        public List<ResourceDependency> ResourceDependencies { get; set; } = new();
        
        public bool IsValid => !ExternalResources.Any() && !MissingResources.Any() && !Errors.Any();
        public bool HasCriticalIssues => ExternalResources.Any() || MissingResources.Any() || Errors.Any();
        
        public string GetReport()
        {
            var report = new StringBuilder();
            
            if (ExternalResources.Any())
            {
                report.AppendLine("EXTERNAL RESOURCES DETECTED:");
                report.AppendLine("These files are outside your project directory and may not work on other systems:");
                foreach (var issue in ExternalResources)
                {
                    report.AppendLine($"  • {Path.GetFileName(issue.AssetFile)}: {issue.ResourcePath}");
                }
                report.AppendLine("\nSOLUTION: Copy these files into your project's Resources folder and update the paths.");
                report.AppendLine();
            }
            
            if (MissingResources.Any())
            {
                report.AppendLine("MISSING RESOURCES:");
                foreach (var issue in MissingResources)
                {
                    report.AppendLine($"  • {Path.GetFileName(issue.AssetFile)}: {issue.ResourcePath}");
                }
                report.AppendLine();
            }
            
            if (Errors.Any())
            {
                report.AppendLine("CRITICAL ERRORS:");
                foreach (var error in Errors)
                {
                    report.AppendLine($"  • {error}");
                }
                report.AppendLine();
            }
            
            if (Warnings.Any())
            {
                report.AppendLine("WARNINGS:");
                foreach (var warning in Warnings)
                {
                    report.AppendLine($"  • {warning}");
                }
                report.AppendLine();
            }
            
            return report.ToString();
        }
    }

    public class ExternalResourceIssue
    {
        public string AssetFile { get; set; } = string.Empty;
        public string ResourcePath { get; set; } = string.Empty;
    }

    public class MissingResourceIssue
    {
        public string AssetFile { get; set; } = string.Empty;
        public string ResourcePath { get; set; } = string.Empty;
    }
}

