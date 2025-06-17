// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using System.Text.RegularExpressions;


namespace HS.Stride.Packer.Core
{
    public class NamespaceScanner
    {
        public List<NamespaceReference> ScanDirectory(string path)
        {
            return ScanDirectory(path, null);
        }
        
        public List<NamespaceReference> ScanDirectory(string path, List<string>? excludeNamespaces)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return new List<NamespaceReference>();
                
            var namespaceMap = new Dictionary<string, List<string>>();
            
            var strideFiles = Directory.GetFiles(path, "*.sd*", SearchOption.AllDirectories);
            
            foreach (var file in strideFiles)
            {
                var namespaces = ScanFile(file, excludeNamespaces);
                foreach (var ns in namespaces)
                {
                    if (!namespaceMap.ContainsKey(ns))
                    {
                        namespaceMap[ns] = new List<string>();
                    }
                    namespaceMap[ns].Add(Path.GetFileName(file));
                }
            }
            
            // Convert to NamespaceReference list
            return namespaceMap.Select(kvp => new NamespaceReference
            {
                Namespace = kvp.Key,
                FoundInFiles = kvp.Value.Distinct().ToList()
            }).ToList();
        }
        
        public List<string> ScanFile(string filePath)
        {
            return ScanFile(filePath, null);
        }

        
        //Private
        private List<string> ScanFile(string filePath, List<string>? excludeNamespaces)
        {
            if (!File.Exists(filePath))
                return new List<string>();
                
            var content = File.ReadAllText(filePath);
            var namespaces = new HashSet<string>();
            var extension = Path.GetExtension(filePath).ToLower();
            
            switch (extension)
            {
                case ".sdprefab":
                case ".sdscene":
                    namespaces.UnionWith(ScanPrefabFile(content));
                    break;
                case ".sdfx":
                    namespaces.UnionWith(ScanEffectFile(content));
                    break;
                case ".sdpkg":
                    namespaces.UnionWith(ScanPackageFile(content));
                    break;
                    //Might need to add a.sd*, General catchall if I havent seen all file types
            }
            
            return namespaces
                .Where(ns => !string.IsNullOrEmpty(ns) && !ns.StartsWith("Stride."))
                .Where(ns => !ShouldExcludeNamespace(ns, excludeNamespaces))
                .ToList();
        }
        
        private bool ShouldExcludeNamespace(string namespaceName, List<string>? excludeNamespaces)
        {
            if (excludeNamespaces == null || !excludeNamespaces.Any())
                return false;
                
            // Check for exact match
            if (excludeNamespaces.Contains(namespaceName))
                return true;
                
            // Check for prefix match (e.g., excluding "MyLib" should also exclude "MyLib.SubNamespace")
            return excludeNamespaces.Any(exclude => namespaceName.StartsWith(exclude + "."));
        }
        
        private List<string> ScanPrefabFile(string content)
        {
            var namespaces = new List<string>();
            
            // Look for script component references: !MyNamespace.MyScript,MyAssembly
            var regex = new Regex(@"!([a-zA-Z][a-zA-Z0-9_.]+),([a-zA-Z][a-zA-Z0-9_.]+)");
            var matches = regex.Matches(content);
            
            foreach (Match match in matches)
            {
                var namespaceName = match.Groups[1].Value;
                
                // Extract just the namespace part (before the class name)
                var namespaceParts = namespaceName.Split('.');
                if (namespaceParts.Length > 1)
                {
                    // Take all parts except the last (which is likely the class name)
                    var ns = string.Join(".", namespaceParts.Take(namespaceParts.Length - 1));
                    if (!string.IsNullOrEmpty(ns))
                    {
                        namespaces.Add(ns);
                    }
                }
                else
                {
                    // Single part, might be the namespace itself
                    namespaces.Add(namespaceName);
                }
            }
            
            return namespaces;
        }
        
        private List<string> ScanEffectFile(string content)
        {
            var namespaces = new List<string>();
            
            // Look for namespace declarations in shader files
            var regex = new Regex(@"namespace\s+([a-zA-Z][a-zA-Z0-9_.]+)");
            var matches = regex.Matches(content);
            
            foreach (Match match in matches)
            {
                var namespaceName = match.Groups[1].Value;
                namespaces.Add(namespaceName);
            }
            
            return namespaces;
        }
        
        private List<string> ScanPackageFile(string content)
        {
            var namespaces = new List<string>();
            
            // Look for package names which might indicate namespaces
            var regex = new Regex(@"Name:\s*([a-zA-Z][a-zA-Z0-9_.]+)");
            var matches = regex.Matches(content);
            
            foreach (Match match in matches)
            {
                var packageName = match.Groups[1].Value;
                namespaces.Add(packageName);
            }
            
            return namespaces;
        }
    }
}

