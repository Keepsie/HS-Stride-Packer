// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0


using HS.Stride.Packer.Core;
using HS.Stride.Packer.Utilities;

namespace HS.Stride.Packer.Console;

public class StrideCommandHandler
{
    private readonly PackageRegistry _registry;
    private static readonly string DEFAULT_REGISTRY_URL = StridePackageManager.DefaultRegistryUrl;

    public StrideCommandHandler()
    {
        _registry = new PackageRegistry();
        _registry.SetRegistryUrl(DEFAULT_REGISTRY_URL);
    }

    
    //Public
    public async Task<bool> HandleInput(string input)
    {
        return await ProcessCommand(input);
    }
    
    
    //Private

    // Command Processing
    private async Task<bool> ProcessCommand(string input)
    {
        // Split command and arguments
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLower();
        var args = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : string.Empty;

        switch (command)
        {
            case "help":
                ShowHelp();
                return true;

            case "export":
                return await HandleExport(args);

            case "import":
                return await HandleImport(args);

            case "registry":
                return await HandleRegistry(args);

            case "download":
                return await HandleDownload(args);

            case "install":
                return await HandleInstall(args);

            case "search":
                return await HandleSearch(args);

            case "cache":
                return await HandleCache(args);

            case "config":
                return HandleConfig(args);

            case "clear":
                Helper.Clear();
                Helper.ShowTitle();
                Helper.AddSpace();
                return true;

            default:
                Helper.ShowError($"Unknown command: {command}");
                Helper.ShowInfo("Type 'help' for available commands.");
                return false;
        }
    }

    private void ShowHelp()
    {
        Helper.AddSpace();
        Helper.ShowInfo("Available Commands:");
        Helper.Write("help                - Show this help message");
        Helper.Write("export [path]       - Export Stride library to .stridepackage (4-phase selection)");
        Helper.Write("import [file]       - Import a .stridepackage file");
        Helper.Write("registry            - Browse available packages in registry");
        Helper.Write("search [query]      - Search packages by name/description");
        Helper.Write("download [name]     - Download a package from registry to cache");
        Helper.Write("install [name|url]  - Install package from registry or direct URL");
        Helper.Write("cache [list|clear]  - Manage local package cache");
        Helper.Write("config [show|set-registry] - Configure registry settings");
        Helper.Write("clear               - Clear the console");
        Helper.Write("exit                - Exit the application");
        Helper.AddSpace();
        Helper.ShowInfo("Examples:");
        Helper.Write("export C:\\MyProject");
        Helper.Write("import MyPackage.stridepackage");
        Helper.Write("install MyLibrary");
        Helper.Write("install https://github.com/author/package/releases/download/v1.0/stridepackage.json");
        Helper.Write("search ui-kit");
        Helper.Write("cache list");
        Helper.AddSpace();
    }

    
    //Import / Export
    private async Task<bool> HandleExport(string args)
    {
        try
        {
            Helper.ShowInfo("=== Package Export ===");
            Helper.AddSpace();

            // Get project path
            string projectPath = GetProjectPath(args);
            if (string.IsNullOrEmpty(projectPath)) return false;

            // Get export settings
            var exportSettings = SetupExportSettings(projectPath);

            // Phase 1: Asset Selection
            SelectAssetFolders(exportSettings);

            // Phase 2: Code Selection  
            SelectCodeFolders(exportSettings);

            // Phase 3: Resource Detection (automatic)
            AutoDetectAndOrganizeResources(exportSettings);

            // Phase 4: Namespace Selection (optional cleanup)
            SelectNamespacesToRemove(exportSettings);

            // Create package (Resource path updates happen automatically during export)
            await CreatePackageAsync(exportSettings);

            return true;
        }
        catch (Exception ex)
        {
            Helper.ShowError($"Export failed: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> HandleImport(string args)
    {
        try
        {
            Helper.ShowInfo("=== Package Import ===");
            Helper.AddSpace();

            string packagePath = GetPackagePath(args);
            if (string.IsNullOrEmpty(packagePath)) return false;

            string targetPath = GetImportTargetPath();
            if (string.IsNullOrEmpty(targetPath)) return false;

            // Create import settings (simple 3-step process)
            var importSettings = new ImportSettings
            {
                PackagePath = packagePath,
                TargetProjectPath = targetPath,
                OverwriteExistingFiles = true,
            };

            Helper.ShowInfo("Import Summary:");
            Helper.Write($"  Package: {Path.GetFileName(packagePath)}");
            Helper.Write($"  Target: {targetPath}");
            Helper.Write($"  Mode: Direct merge (Assets → Assets, Code → Code, Resources → Resources)");
            Helper.Write($"  Overwrite: {importSettings.OverwriteExistingFiles}");
            Helper.AddSpace();

            if (!Helper.ReadBool("Proceed with import?"))
            {
                Helper.ShowWarning("Import cancelled by user");
                return false;
            }

            Helper.Write("Verifying package integrity... Please wait...");

            // Create a dummy export settings for the constructor
            var dummyExportSettings = new ExportSettings
            {
                LibraryPath = targetPath,
                OutputPath = "",
                Manifest = new PackageManifest()
            };
            
            var packageManager = new StridePackageManager(dummyExportSettings);
            
            // Verify package integrity before import
            try
            {
                var isValid = await packageManager.VerifyPackageIntegrityAsync(packagePath);
                if (isValid)
                {
                    Helper.ShowSuccess("✓ Package integrity verified");
                }
                else
                {
                    Helper.ShowError("✗ Package integrity verification failed");
                    Helper.ShowError("This package is corrupted, tampered with, or missing required security information.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Helper.ShowError("✗ Package integrity verification failed");
                Helper.ShowError($"Error: {ex.Message}");
                return false;
            }

            Helper.Write("Importing package... Please wait...");
            var result = await packageManager.ImportPackageAsync(importSettings);

            Helper.AddSpace();
            Helper.ShowSuccess("Package imported successfully!");
            Helper.ShowInfo($"Imported to: {result.ImportPath}");
            Helper.ShowInfo($"Files imported: {result.TotalFilesImported}");
            if (result.CreatedDirectories.Any())
            {
                Helper.ShowInfo($"Created directories: {string.Join(", ", result.CreatedDirectories)}");
            }
            if (result.HasConflicts)
            {
                Helper.ShowWarning($"Overwritten files: {result.OverwrittenItems.Count}");
                Helper.ShowWarning($"Skipped files: {result.SkippedItems.Count}");
            }

            return true;
        }
        catch (Exception ex)
        {
            Helper.ShowError($"Import failed: {ex.Message}");
            return false;
        }
    }


    //Pathing
    
    private string GetProjectPath(string args)
    {
        if (!string.IsNullOrWhiteSpace(args))
        {
            if (Directory.Exists(args))
            {
                return args;
            }
            Helper.ShowError($"Directory does not exist: {args}");
        }

        Helper.ShowInfo("Select Stride Project Root Directory:");
        Helper.ShowInfo("Please select the Visual Studio solution folder containing the .sln file");
        Helper.AddSpace();

        while (true)
        {
            var projectPath = Helper.ReadString("Enter your Stride project root path", allowEmpty: false);

            var validation = PathHelper.ValidateStrideProject(projectPath);
            
            if (validation.IsValid)
            {
                Helper.ShowSuccess(validation.SuccessMessage);
                Helper.ShowWarning("IMPORTANT: Close Stride GameStudio before importing/exporting packages");
                Helper.AddSpace();
                return projectPath;
            }
            else
            {
                Helper.ShowError(validation.ErrorMessage);
                
                if (validation.Suggestions.Any())
                {
                    Helper.ShowInfo("Suggestions:");
                    foreach (var suggestion in validation.Suggestions)
                    {
                        Helper.Write($"  • {suggestion}");
                    }
                }
                
                Helper.AddSpace();
                if (!Helper.ReadBool("Would you like to try another path?"))
                {
                    return string.Empty;
                }
            }
        }
    }

    private string GetPackagePath(string args)
    {
        if (!string.IsNullOrWhiteSpace(args))
        {
            if (File.Exists(args) && args.EndsWith(".stridepackage"))
            {
                return args;
            }
            Helper.ShowError($"Package file not found: {args}");
        }

        Helper.ShowInfo("Select Package File:");
        Helper.AddSpace();

        while (true)
        {
            var packagePath = Helper.ReadString("Enter path to .stridepackage file", allowEmpty: false);

            if (!File.Exists(packagePath))
            {
                Helper.ShowError($"File does not exist: {packagePath}");
                if (!Helper.ReadBool("Would you like to try another path?"))
                {
                    return string.Empty;
                }
                continue;
            }

            if (!packagePath.EndsWith(".stridepackage"))
            {
                Helper.ShowError("File must have .stridepackage extension");
                if (!Helper.ReadBool("Would you like to try another file?"))
                {
                    return string.Empty;
                }
                continue;
            }

            Helper.ShowSuccess($"Package file validated: {packagePath}");
            Helper.AddSpace();
            return packagePath;
        }
    }

    private string GetImportTargetPath()
    {
        Helper.ShowInfo("Select Target Project:");
        Helper.AddSpace();

        while (true)
        {
            var targetPath = Helper.ReadString("Enter target Stride project path", allowEmpty: false);

            if (!Directory.Exists(targetPath))
            {
                Helper.ShowError($"Directory does not exist: {targetPath}");
                if (!Helper.ReadBool("Would you like to try another path?"))
                {
                    return string.Empty;
                }
                continue;
            }

            if (!PathHelper.IsStrideProject(targetPath))
            {
                Helper.ShowWarning("Target directory may not be a Stride project");
                if (!Helper.ReadBool("Continue anyway?"))
                {
                    continue;
                }
            }

            Helper.ShowSuccess($"Target path validated: {targetPath}");
            Helper.ShowWarning("IMPORTANT: Close Stride GameStudio before importing/exporting packages");
            Helper.AddSpace();
            return targetPath;
        }
    }

    private ExportSettings SetupExportSettings(string projectPath)
    {
        Helper.ShowInfo("Package Information:");
        Helper.AddSpace();

        var defaultName = Path.GetFileName(projectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        
        // Detect project structure type
        var projectStructureType = DetectProjectStructure(projectPath);
        Helper.ShowInfo($"Detected project structure: {projectStructureType}");

        var packageName = Helper.ReadString($"Package name (default: {defaultName})", allowEmpty: true);
        if (string.IsNullOrWhiteSpace(packageName))
        {
            packageName = defaultName;
        }

        var version = Helper.ReadString("Version (default: 1.0.0)", allowEmpty: true);
        if (string.IsNullOrWhiteSpace(version))
        {
            version = "1.0.0";
        }

        var description = Helper.ReadString("Description (optional)", allowEmpty: true);
        if (string.IsNullOrWhiteSpace(description))
        {
            description = $"Package created from {packageName}";
        }

        var author = Helper.ReadString($"Author (default: {Environment.UserName})", allowEmpty: true);
        if (string.IsNullOrWhiteSpace(author))
        {
            author = Environment.UserName;
        }

        var strideVersion = Helper.ReadString("Stride version (optional)", allowEmpty: true);
        if (string.IsNullOrWhiteSpace(strideVersion))
        {
            strideVersion = "Unknown";
        }

        var tagsInput = Helper.ReadString("Tags (comma-separated, e.g. 'ui,effects,templates')", allowEmpty: true);
        var tags = new List<string>();
        if (!string.IsNullOrWhiteSpace(tagsInput))
        {
            tags = tagsInput.Split(',', StringSplitOptions.RemoveEmptyEntries)
                          .Select(t => t.Trim())
                          .Where(t => !string.IsNullOrEmpty(t))
                          .ToList();
        }
        
        string? downloadUrl = null;
        string? homepage = null;
        string? repository = null;
        string? license = null;
        

        Helper.AddSpace();

        var defaultExportPath = Path.GetDirectoryName(projectPath) ?? ".";
        Helper.Write($"Default export location: {defaultExportPath}");

        var useDefault = Helper.ReadBool("Use default export location?");
        string exportLocation;

        if (useDefault)
        {
            exportLocation = defaultExportPath;
        }
        else
        {
            exportLocation = Helper.ReadString("Enter custom export location", allowEmpty: false);

            if (!Directory.Exists(exportLocation))
            {
                Helper.ShowInfo($"Creating export directory: {exportLocation}");
                Directory.CreateDirectory(exportLocation);
            }
        }

        var outputPath = Path.Combine(exportLocation, PathHelper.MakePackageFileName(packageName, version));

        Helper.AddSpace();
        Helper.ShowSuccess("✓ Package information collected");
        Helper.ShowInfo($"Output file: {outputPath}");
        Helper.AddSpace();

        return new ExportSettings
        {
            LibraryPath = projectPath,
            OutputPath = outputPath,
            ExportRegistryJson = true,
            Manifest = new PackageManifest
            {
                Name = packageName,
                Version = version,
                Description = description,
                Author = author,
                StrideVersion = strideVersion,
                CreatedDate = DateTime.UtcNow,
                StructureType = projectStructureType,
                ProjectName = defaultName,
                Tags = tags,
                DownloadUrl = downloadUrl,
                Homepage = homepage,
                Repository = repository,
                License = license
            }
        };
    }
    
    private void SelectAssetFolders(ExportSettings exportSettings)
    {
        Helper.ShowInfo("=== Phase 1: Asset Selection ===");
        Helper.AddSpace();

        // Use PackageExporter service to scan for assets
        var packageExporter = new PackageExporter(new ResourcePathValidator(), new NamespaceScanner());
        var allAssetFolders = packageExporter.ScanForAssetFolders(exportSettings.LibraryPath);

        if (!allAssetFolders.Any())
        {
            Helper.ShowWarning("No asset folders found in project");
            Helper.ShowInfo("Checked both root Assets/ and ProjectName/Assets/ locations");
            Helper.AddSpace();
            return;
        }

        Helper.ShowInfo($"Found {allAssetFolders.Count} asset folder(s):");
        Helper.AddSpace();

        // Display available asset folders with their locations
        for (int i = 0; i < allAssetFolders.Count; i++)
        {
            var folder = allAssetFolders[i];
            Helper.Write($"  {i + 1}. {folder.RelativePath}/ ({folder.FileCount} files)");
        }
        Helper.AddSpace();

        if (!Helper.ReadBool("Do you want to include any asset folders?"))
        {
            Helper.ShowInfo("No asset folders selected");
            Helper.AddSpace();
            return;
        }

        // Let user select which asset folders to include
        var selectedAssetFolders = new List<string>();
        
        while (true)
        {
            Helper.ShowInfo("Enter folder numbers to include (comma-separated), or 'done' to finish:");
            var input = Helper.ReadString("Include", allowEmpty: true);
            
            if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "done")
                break;
                
            try
            {
                var indices = input.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(x => int.Parse(x.Trim()) - 1)
                                  .Where(x => x >= 0 && x < allAssetFolders.Count)
                                  .ToList();
                
                foreach (var index in indices)
                {
                    var folder = allAssetFolders[index];
                    if (!selectedAssetFolders.Contains(folder.RelativePath))
                    {
                        selectedAssetFolders.Add(folder.RelativePath);
                        Helper.ShowSuccess($"Added '{folder.RelativePath}/' to export");
                    }
                }
                
                if (selectedAssetFolders.Any())
                {
                    Helper.ShowInfo($"Selected: {string.Join(", ", selectedAssetFolders.Select(f => $"{f}/"))}");
                }
                
                if (!Helper.ReadBool("Add more asset folders?"))
                    break;
            }
            catch
            {
                Helper.ShowError("Invalid input. Please enter valid numbers separated by commas.");
            }
        }
        
        // Store selected asset folders in export settings
        exportSettings.SelectedAssetFolders = selectedAssetFolders;
        
        if (selectedAssetFolders.Any())
        {
            Helper.ShowSuccess($"✓ Phase 1 Complete: {selectedAssetFolders.Count} asset folder(s) selected");
        }
        else
        {
            Helper.ShowInfo("✓ Phase 1 Complete: No asset folders selected");
        }
        Helper.AddSpace();
    }

    private void SelectCodeFolders(ExportSettings exportSettings)
    {
        Helper.ShowInfo("=== Phase 2: Code Selection ===");
        Helper.AddSpace();

        if (!Helper.ReadBool("Does this export include any code?"))
        {
            Helper.ShowInfo("✓ Phase 2 Complete: No code selected");
            Helper.AddSpace();
            return;
        }

        // Use PackageExporter service to scan for code folders
        var packageExporter = new PackageExporter(new ResourcePathValidator(), new NamespaceScanner());
        var codeFolders = packageExporter.ScanForCodeFolders(exportSettings.LibraryPath);

        if (!codeFolders.Any())
        {
            Helper.ShowWarning("No code folders found in project");
            Helper.ShowInfo("Expected folders like: MyProject.Game/, MyProject.Windows/, etc.");
            Helper.AddSpace();
            return;
        }

        Helper.ShowInfo($"Found {codeFolders.Count} code project(s):");
        Helper.AddSpace();

        // Display available code projects
        for (int i = 0; i < codeFolders.Count; i++)
        {
            var folder = codeFolders[i];
            Helper.Write($"  {i + 1}. {folder.Name}/ - {folder.Type}");
            Helper.Write($"     Sub-folders: {string.Join(", ", folder.SubFolders.Take(3))}" + 
                        (folder.SubFolders.Count > 3 ? $" (and {folder.SubFolders.Count - 3} more)" : ""));
        }
        Helper.AddSpace();

        // Let user select which code projects to scan
        var selectedCodeProjects = new List<string>();
        
        while (true)
        {
            Helper.ShowInfo("Enter project numbers to scan (comma-separated), or 'done' to finish:");
            var input = Helper.ReadString("Include", allowEmpty: true);
            
            if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "done")
                break;
                
            try
            {
                var indices = input.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(x => int.Parse(x.Trim()) - 1)
                                  .Where(x => x >= 0 && x < codeFolders.Count)
                                  .ToList();
                
                foreach (var index in indices)
                {
                    var folderName = codeFolders[index].Name;
                    if (!selectedCodeProjects.Contains(folderName))
                    {
                        selectedCodeProjects.Add(folderName);
                        Helper.ShowSuccess($"Added '{folderName}/' to code scan");
                    }
                }
                
                if (selectedCodeProjects.Any())
                {
                    Helper.ShowInfo($"Selected: {string.Join(", ", selectedCodeProjects)}");
                }
                
                if (!Helper.ReadBool("Add more code projects?"))
                    break;
            }
            catch
            {
                Helper.ShowError("Invalid input. Please enter valid numbers separated by commas.");
            }
        }

        if (!selectedCodeProjects.Any())
        {
            Helper.ShowInfo("✓ Phase 2 Complete: No code projects selected");
            Helper.AddSpace();
            return;
        }

        // Now let user select specific code folders from the selected projects
        var selectedCodeFolders = new List<string>();
        var selectedPlatformFolders = new List<string>();

        foreach (var selectedProject in selectedCodeProjects)
        {
            var project = codeFolders.First(f => f.Name == selectedProject);
            
            Helper.ShowInfo($"Select code folders from {project.Name}:");
            Helper.AddSpace();

            for (int i = 0; i < project.SubFolders.Count; i++)
            {
                Helper.Write($"  {i + 1}. {project.SubFolders[i]}/");
            }
            Helper.AddSpace();

            while (true)
            {
                Helper.ShowInfo("Enter folder numbers to include (comma-separated), or 'done' to finish:");
                var input = Helper.ReadString("Include", allowEmpty: true);
                
                if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "done")
                    break;
                    
                try
                {
                    var indices = input.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                      .Select(x => int.Parse(x.Trim()) - 1)
                                      .Where(x => x >= 0 && x < project.SubFolders.Count)
                                      .ToList();
                    
                    foreach (var index in indices)
                    {
                        var folderPath = $"{project.Name}/{project.SubFolders[index]}";
                        
                        if (project.Type.Contains("Game"))
                        {
                            if (!selectedCodeFolders.Contains(folderPath))
                            {
                                selectedCodeFolders.Add(folderPath);
                                Helper.ShowSuccess($"Added '{folderPath}' to code export");
                            }
                        }
                        else
                        {
                            if (!selectedPlatformFolders.Contains(folderPath))
                            {
                                selectedPlatformFolders.Add(folderPath);
                                Helper.ShowSuccess($"Added '{folderPath}' to platform export");
                            }
                        }
                    }
                    
                    if (!Helper.ReadBool("Add more folders from this project?"))
                        break;
                }
                catch
                {
                    Helper.ShowError("Invalid input. Please enter valid numbers separated by commas.");
                }
            }
            Helper.AddSpace();
        }

        // Store selected code folders in export settings
        exportSettings.SelectedCodeFolders = selectedCodeFolders;
        exportSettings.SelectedPlatformFolders = selectedPlatformFolders;
        
        var totalSelected = selectedCodeFolders.Count + selectedPlatformFolders.Count;
        if (totalSelected > 0)
        {
            Helper.ShowSuccess($"✓ Phase 2 Complete: {selectedCodeFolders.Count} shared code folder(s), {selectedPlatformFolders.Count} platform folder(s) selected");
        }
        else
        {
            Helper.ShowInfo("✓ Phase 2 Complete: No code folders selected");
        }
        Helper.AddSpace();
    }
    
    private void AutoDetectAndOrganizeResources(ExportSettings exportSettings)
    {
        Helper.ShowInfo("=== Phase 3: Resource Detection (Automatic) ===");
        Helper.AddSpace();

        if (!exportSettings.SelectedAssetFolders.Any())
        {
            Helper.ShowInfo("No assets selected - skipping resource detection");
            Helper.ShowInfo("✓ Phase 3 Complete: No resources to organize");
            Helper.AddSpace();
            return;
        }

        Helper.ShowInfo("✓ Phase 3 Complete: Resource dependencies will be handled during export");
        Helper.AddSpace();
        
    }
    
    private void SelectNamespacesToRemove(ExportSettings exportSettings)
    {
        Helper.ShowInfo("=== Phase 4: Namespace Cleanup (Optional) ===");
        Helper.AddSpace();

        var scanner = new NamespaceScanner();
        var namespaceReferences = scanner.ScanDirectory(exportSettings.LibraryPath);

        if (!namespaceReferences.Any())
        {
            Helper.ShowInfo("No namespaces found in project files");
            Helper.AddSpace();
            return;
        }

        Helper.ShowInfo($"Found {namespaceReferences.Count} namespace(s) in your project:");
        Helper.AddSpace();

        // Display all found namespaces
        for (int i = 0; i < namespaceReferences.Count; i++)
        {
            var ns = namespaceReferences[i];
            Helper.Write($"  {i + 1}. {ns.Namespace}");
            Helper.Write($"     Found in: {string.Join(", ", ns.FoundInFiles.Take(3))}" + 
                        (ns.FoundInFiles.Count > 3 ? $" (and {ns.FoundInFiles.Count - 3} more)" : ""));
        }
        Helper.AddSpace();
        
        if (!Helper.ReadBool("Do you want to exclude any namespaces from the package?"))
        {
            Helper.ShowInfo("All namespaces will be included in the package");
            Helper.AddSpace();
            return;
        }

        // Let user select namespaces to exclude
        var excludeNamespaces = new List<string>();
        
        while (true)
        {
            Helper.ShowInfo("Enter namespace numbers to exclude (comma-separated), or 'done' to finish:");
            var input = Helper.ReadString("Exclude", allowEmpty: true);
            
            if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "done")
                break;
                
            try
            {
                var indices = input.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(x => int.Parse(x.Trim()) - 1)
                                  .Where(x => x >= 0 && x < namespaceReferences.Count)
                                  .ToList();
                
                foreach (var index in indices)
                {
                    var nsToExclude = namespaceReferences[index].Namespace;
                    if (!excludeNamespaces.Contains(nsToExclude))
                    {
                        excludeNamespaces.Add(nsToExclude);
                        Helper.ShowSuccess($"Added '{nsToExclude}' to exclusion list");
                    }
                }
                
                if (excludeNamespaces.Any())
                {
                    Helper.ShowInfo($"Currently excluding: {string.Join(", ", excludeNamespaces)}");
                }
                
                if (!Helper.ReadBool("Add more namespaces to exclude?"))
                    break;
            }
            catch
            {
                Helper.ShowError("Invalid input. Please enter valid numbers separated by commas.");
            }
        }
        
        exportSettings.ExcludeNamespaces = excludeNamespaces;
        
        if (excludeNamespaces.Any())
        {
            Helper.ShowSuccess($"✓ Phase 4 Complete: Will exclude {excludeNamespaces.Count} namespace(s) from the package");
        }
        else
        {
            Helper.ShowInfo("✓ Phase 4 Complete: All namespaces will be included in the package");
        }
        Helper.AddSpace();
    }

    private async Task CreatePackageAsync(ExportSettings exportSettings)
    {
        Helper.ShowInfo("Creating Package:");
        Helper.AddSpace();

        try
        {
            var packageManager = new StridePackageManager(exportSettings);

            Helper.ShowInfo("Final Package Summary:");
            Helper.Write($"  📦 Name: {exportSettings.Manifest.Name} v{exportSettings.Manifest.Version}");
            Helper.Write($"  👤 Author: {exportSettings.Manifest.Author}");
            Helper.Write($"  📄 Format: .stridepackage");
            
            // Content summary
            var contentParts = new List<string>();
            if (exportSettings.SelectedAssetFolders.Any())
                contentParts.Add($"{exportSettings.SelectedAssetFolders.Count} asset folder(s)");
            if (exportSettings.SelectedCodeFolders.Any() || exportSettings.SelectedPlatformFolders.Any())
                contentParts.Add($"{exportSettings.SelectedCodeFolders.Count + exportSettings.SelectedPlatformFolders.Count} code folder(s)");
            if (exportSettings.ResourceDependencies.Any())
                contentParts.Add("organized resources");
            
            Helper.Write($"  📂 Content: {(contentParts.Any() ? string.Join(", ", contentParts) : "No content selected")}");
            Helper.Write($"  💾 Output: {exportSettings.OutputPath}");
            Helper.AddSpace();

            if (!Helper.ReadBool("Proceed with package creation?"))
            {
                Helper.ShowWarning("Package creation cancelled by user");
                return;
            }

            Helper.Write("Creating package... Please wait...");

            var result = await packageManager.CreatePackageAsync();

            Helper.AddSpace();
            Helper.ShowSuccess("Package created successfully!");
            Helper.ShowSuccess($"Location: {result}");

            var fileInfo = new FileInfo(result);
            Helper.ShowInfo($"Size: {fileInfo.Length / 1024.0:F1} KB");
        }
        catch (Exception ex)
        {
            Helper.AddSpace();
            Helper.ShowError($"Failed to create package: {ex.Message}");
        }
    }

    private ProjectStructureType DetectProjectStructure(string projectPath)
    {
        var projectName = Path.GetFileName(projectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        
        // Check for Fresh structure: ProjectName/Assets/ folder exists
        var nestedAssetsPath = Path.Combine(projectPath, projectName, "Assets");
        if (Directory.Exists(nestedAssetsPath))
        {
            return ProjectStructureType.Fresh;
        }
        
        // Check for Template structure: Assets/ at root level
        var rootAssetsPath = Path.Combine(projectPath, "Assets");
        if (Directory.Exists(rootAssetsPath))
        {
            return ProjectStructureType.Template;
        }
        
        return ProjectStructureType.Unknown;
    }
    
    
    //Registry
    
    private async Task<bool> HandleRegistry(string args)
    {
        try
        {
            Helper.ShowInfo("=== Package Registry ===");
            Helper.AddSpace();
            Helper.Write("Connecting to registry... Please wait...");
            Helper.AddSpace();

            var packages = await _registry.GetAllPackagesAsync();
            
            if (!packages.Any())
            {
                Helper.ShowWarning("No packages available in registry");
                Helper.ShowInfo("The registry may be empty or unreachable.");
                return true;
            }

            Helper.ShowInfo($"Available packages ({packages.Count}):");
            Helper.AddSpace();

            foreach (var package in packages)
            {
                Helper.Write($"📦 {package.Name} v{package.Version}");
                Helper.Write($"   Author: {package.Author}");
                Helper.Write($"   {package.Description}");
                if (package.Tags.Any())
                {
                    Helper.Write($"   Tags: {string.Join(", ", package.Tags)}");
                }
                Helper.Write($"   Stride: {package.StrideVersion}");
                Helper.AddSpace();
            }

            return true;
        }
        catch (Exception ex)
        {
            Helper.ShowError($"Failed to connect to registry: {ex.Message}");
            Helper.ShowInfo("Please check your internet connection and registry URL.");
            return false;
        }
    }

    private async Task<bool> HandleDownload(string args)
    {
        try
        {
            Helper.ShowInfo("=== Package Download ===");
            Helper.AddSpace();

            if (string.IsNullOrWhiteSpace(args))
            {
                Helper.ShowError("Please specify a package name to download");
                Helper.ShowInfo("Example: download MyLibrary");
                return false;
            }

            Helper.Write("Searching registry... Please wait...");
            var packages = await _registry.GetAllPackagesAsync();
            var package = packages.FirstOrDefault(p => 
                p.Name.Equals(args, StringComparison.OrdinalIgnoreCase));

            if (package == null)
            {
                Helper.ShowError($"Package '{args}' not found in registry");
                Helper.ShowInfo("Use 'registry' to see available packages or 'search <query>' to find packages");
                return false;
            }

            Helper.AddSpace();
            Helper.ShowInfo("Package Details:");
            Helper.Write($"  Name: {package.Name}");
            Helper.Write($"  Version: {package.Version}");
            Helper.Write($"  Author: {package.Author}");
            Helper.Write($"  Description: {package.Description}");
            if (package.Tags.Any())
            {
                Helper.Write($"  Tags: {string.Join(", ", package.Tags)}");
            }
            Helper.Write($"  Stride Version: {package.StrideVersion}");
            Helper.AddSpace();

            // Check if already cached
            if (_registry.IsPackageCached(package))
            {
                var cachedPath = _registry.GetCachedPackagePath(package);
                Helper.ShowInfo($"Package already cached at: {cachedPath}");
                Helper.ShowInfo("Use 'import' command to install from cache");
                return true;
            }

            if (!Helper.ReadBool("Download this package to cache?"))
            {
                Helper.ShowWarning("Download cancelled");
                return false;
            }

            Helper.Write("Downloading package... Please wait...");
            
            var downloadPath = await _registry.DownloadPackageToCache(package);
            
            // Verify downloaded package integrity
            try
            {
                var dummyExportSettings = new ExportSettings
                {
                    LibraryPath = "",
                    OutputPath = "",
                    Manifest = new PackageManifest()
                };
                var packageManager = new StridePackageManager(dummyExportSettings);
                var isValid = await packageManager.VerifyPackageIntegrityAsync(downloadPath);
                if (isValid)
                {
                    Helper.ShowSuccess("✓ Download integrity verified");
                }
                else
                {
                    Helper.ShowWarning("⚠ Download integrity could not be verified");
                }
            }
            catch (Exception)
            {
                Helper.ShowWarning("⚠ Download integrity verification failed");
            }

            Helper.AddSpace();
            Helper.ShowSuccess("Package downloaded successfully!");
            Helper.ShowInfo($"Cached at: {downloadPath}");
            Helper.ShowInfo("Use 'import' command to install this package");

            return true;
        }
        catch (Exception ex)
        {
            Helper.ShowError($"Download failed: {ex.Message}");
            Helper.ShowInfo("Please check your internet connection and try again.");
            return false;
        }
    }
    
    private async Task<bool> HandleInstall(string args)
    {
        try
        {
            Helper.ShowInfo("=== Package Installation ===");
            Helper.AddSpace();

            if (string.IsNullOrWhiteSpace(args))
            {
                Helper.ShowError("Please specify a package name or URL to install");
                Helper.ShowInfo("Examples:");
                Helper.Write("  install MyLibrary");
                Helper.Write("  install https://github.com/author/package/releases/download/v1.0/stridepackage.json");
                return false;
            }

            string packagePath;
            
            // Check if it's a URL
            if (args.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                if (args.EndsWith("stridepackage.json", StringComparison.OrdinalIgnoreCase))
                {
                    // Handle stridepackage.json metadata URL
                    Helper.Write("Loading package metadata... Please wait...");
                    packagePath = await InstallFromMetadataUrl(args);
                    if (string.IsNullOrEmpty(packagePath))
                    {
                        return false;
                    }
                }
                else if (args.EndsWith(".stridepackage", StringComparison.OrdinalIgnoreCase))
                {
                    // Handle direct .stridepackage URL
                    Helper.Write("Downloading package file... Please wait...");
                    packagePath = await DownloadFromUrl(args);
                    if (string.IsNullOrEmpty(packagePath))
                    {
                        return false;
                    }
                }
                else
                {
                    Helper.ShowError("URL must point to either a 'stridepackage.json' metadata file or a '.stridepackage' file");
                    Helper.ShowInfo("Examples:");
                    Helper.Write("  https://github.com/author/package/releases/download/v1.0/stridepackage.json");
                    Helper.Write("  https://github.com/author/package/releases/download/v1.0/Package.stridepackage");
                    return false;
                }
            }
            else
            {
                // Search registry for package name
                Helper.Write("Searching registry... Please wait...");
                var packages = await _registry.GetAllPackagesAsync();
                var package = packages.FirstOrDefault(p => 
                    p.Name.Equals(args, StringComparison.OrdinalIgnoreCase));

                if (package == null)
                {
                    Helper.ShowError($"Package '{args}' not found in registry");
                    Helper.ShowInfo("Use 'registry' to see available packages or 'search <query>' to find packages");
                    return false;
                }

                // Check if cached, download if not
                if (_registry.IsPackageCached(package))
                {
                    packagePath = _registry.GetCachedPackagePath(package)!;
                    Helper.ShowInfo($"Using cached package: {Path.GetFileName(packagePath)}");
                }
                else
                {
                    Helper.Write("Downloading package... Please wait...");
                    packagePath = await _registry.DownloadPackageToCache(package);
                    Helper.ShowSuccess("Package downloaded to cache");
                }
            }

            // Now import the package
            Helper.AddSpace();
            Helper.Write($"Package file: {Path.GetFileName(packagePath)}");
            
            string targetPath = GetImportTargetPath();
            if (string.IsNullOrEmpty(targetPath))
            {
                return false;
            }

            // Use existing import logic
            var importSettings = new ImportSettings
            {
                PackagePath = packagePath,
                TargetProjectPath = targetPath,
                OverwriteExistingFiles = true,
            };

            Helper.ShowInfo("Import Summary:");
            Helper.Write($"  Package: {Path.GetFileName(packagePath)}");
            Helper.Write($"  Target: {targetPath}");
            Helper.Write($"  Mode: Direct merge (Assets → Assets, Code → Code, Resources → Resources)");
            Helper.AddSpace();

            if (!Helper.ReadBool("Proceed with installation?"))
            {
                Helper.ShowWarning("Installation cancelled by user");
                return false;
            }

            Helper.Write("Verifying package integrity... Please wait...");

            var dummyExportSettings = new ExportSettings
            {
                LibraryPath = targetPath,
                OutputPath = "",
                Manifest = new PackageManifest()
            };
            
            var packageManager = new StridePackageManager(dummyExportSettings);
            
            // Verify package integrity before installation
            try
            {
                var isValid = await packageManager.VerifyPackageIntegrityAsync(packagePath);
                if (isValid)
                {
                    Helper.ShowSuccess("✓ Package integrity verified");
                }
                else
                {
                    Helper.ShowError("✗ Package integrity verification failed");
                    Helper.ShowError("This package is corrupted, tampered with, or missing required security information.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Helper.ShowError("✗ Package integrity verification failed");
                Helper.ShowError($"Error: {ex.Message}");
                return false;
            }

            Helper.Write("Installing package... Please wait...");
            var result = await packageManager.ImportPackageAsync(importSettings);

            Helper.AddSpace();
            Helper.ShowSuccess("Package installed successfully!");
            Helper.ShowInfo($"Installed to: {result.ImportPath}");
            Helper.ShowInfo($"Files imported: {result.TotalFilesImported}");
            if (result.CreatedDirectories.Any())
            {
                Helper.ShowInfo($"Created directories: {string.Join(", ", result.CreatedDirectories)}");
            }

            return true;
        }
        catch (Exception ex)
        {
            Helper.ShowError($"Installation failed: {ex.Message}");
            return false;
        }
    }

    private async Task<string> DownloadFromUrl(string url)
    {
        try
        {
            if (!url.EndsWith(".stridepackage", StringComparison.OrdinalIgnoreCase))
            {
                Helper.ShowError("URL must point to a .stridepackage file");
                return string.Empty;
            }

            var uri = new Uri(url);
            var filename = Path.GetFileName(uri.LocalPath);
            var cacheDir = _registry.GetCacheDirectory();
            var downloadsDir = Path.Combine(cacheDir, "Downloads");
            Directory.CreateDirectory(downloadsDir);
            var localPath = Path.Combine(downloadsDir, filename);

            using var client = new HttpClient();
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            await using var fileStream = File.Create(localPath);
            await response.Content.CopyToAsync(fileStream);

            Helper.ShowSuccess($"Downloaded: {filename}");
            return localPath;
        }
        catch (Exception ex)
        {
            Helper.ShowError($"Failed to download from URL: {ex.Message}");
            return string.Empty;
        }
    }

    private async Task<string> InstallFromMetadataUrl(string metadataUrl)
    {
        try
        {
            // Download and parse the stridepackage.json metadata
            var packageMetadata = await _registry.GetPackageMetadataAsync(metadataUrl);
            if (packageMetadata == null)
            {
                Helper.ShowError("Failed to load package metadata");
                return string.Empty;
            }

            Helper.AddSpace();
            Helper.ShowInfo("Package Details:");
            Helper.Write($"  Name: {packageMetadata.Name}");
            Helper.Write($"  Version: {packageMetadata.Version}");
            Helper.Write($"  Author: {packageMetadata.Author}");
            Helper.Write($"  Description: {packageMetadata.Description}");
            if (packageMetadata.Tags.Any())
            {
                Helper.Write($"  Tags: {string.Join(", ", packageMetadata.Tags)}");
            }
            Helper.Write($"  Stride Version: {packageMetadata.StrideVersion}");
            if (!string.IsNullOrEmpty(packageMetadata.License))
            {
                Helper.Write($"  License: {packageMetadata.License}");
            }
            Helper.AddSpace();

            if (string.IsNullOrEmpty(packageMetadata.DownloadUrl))
            {
                Helper.ShowError("Package metadata does not contain a download URL");
                return string.Empty;
            }

            if (!Helper.ReadBool("Download and install this package?"))
            {
                Helper.ShowWarning("Installation cancelled");
                return string.Empty;
            }

            // Check if already cached by metadata
            if (_registry.IsPackageCached(packageMetadata))
            {
                var cachedPath = _registry.GetCachedPackagePath(packageMetadata);
                Helper.ShowInfo($"Package already cached: {Path.GetFileName(cachedPath)}");
                return cachedPath!;
            }

            Helper.Write("Downloading package... Please wait...");
            
            // Use the registry to download and cache the package
            var downloadPath = await _registry.DownloadPackageToCache(packageMetadata);
            Helper.ShowSuccess($"Package downloaded: {Path.GetFileName(downloadPath)}");
            
            return downloadPath;
        }
        catch (Exception ex)
        {
            Helper.ShowError($"Failed to install from metadata URL: {ex.Message}");
            return string.Empty;
        }
    }

    private async Task<bool> HandleSearch(string args)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Helper.ShowError("Please specify a search query");
                Helper.ShowInfo("Example: search ui-kit");
                return false;
            }

            Helper.ShowInfo($"=== Searching for '{args}' ===");
            Helper.AddSpace();
            Helper.Write("Searching registry... Please wait...");

            var packages = await _registry.SearchPackagesAsync(args);
            
            if (!packages.Any())
            {
                Helper.ShowWarning($"No packages found matching '{args}'");
                Helper.ShowInfo("Try different search terms or use 'registry' to see all available packages");
                return true;
            }

            Helper.AddSpace();
            Helper.ShowInfo($"Found {packages.Count} package(s):");
            Helper.AddSpace();

            foreach (var package in packages)
            {
                Helper.Write($"📦 {package.Name} v{package.Version}");
                Helper.Write($"   Author: {package.Author}");
                Helper.Write($"   {package.Description}");
                if (package.Tags.Any())
                {
                    Helper.Write($"   Tags: {string.Join(", ", package.Tags)}");
                }
                Helper.AddSpace();
            }

            return true;
        }
        catch (Exception ex)
        {
            Helper.ShowError($"Search failed: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> HandleCache(string args)
    {
        try
        {
            var command = args.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLower();
            
            switch (command)
            {
                case "list":
                case "":
                case null:
                    return await HandleCacheList();
                    
                case "clear":
                    var packageName = string.Join(" ", args.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1));
                    return await HandleCacheClear(packageName);
                    
                default:
                    Helper.ShowError($"Unknown cache command: {command}");
                    Helper.ShowInfo("Available commands: cache list, cache clear <package>");
                    return false;
            }
        }
        catch (Exception ex)
        {
            Helper.ShowError($"Cache operation failed: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> HandleCacheList()
    {
        Helper.ShowInfo("=== Package Cache ===");
        Helper.AddSpace();
        
        var cachedPackages = await _registry.GetCachedPackages();
        
        if (!cachedPackages.Any())
        {
            Helper.ShowInfo("No packages in cache");
            Helper.ShowInfo("Use 'download <package>' to cache packages locally");
            return true;
        }

        Helper.ShowInfo($"Cached packages ({cachedPackages.Count}):");
        Helper.AddSpace();

        foreach (var cached in cachedPackages)
        {
            Helper.Write($"📦 {cached.Metadata.Name} v{cached.Metadata.Version}");
            Helper.Write($"   Size: {cached.DisplaySize}");
            Helper.Write($"   Cached: {cached.CachedDate:yyyy-MM-dd HH:mm}");
            Helper.Write($"   Path: {cached.CachedPath}");
            Helper.AddSpace();
        }
        
        Helper.ShowInfo($"Cache directory: {_registry.GetCacheDirectory()}");
        return true;
    }

    private async Task<bool> HandleCacheClear(string packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName))
        {
            Helper.ShowError("Please specify a package name to clear from cache");
            Helper.ShowInfo("Example: cache clear MyLibrary");
            return false;
        }

        var cachedPackages = await _registry.GetCachedPackages();
        var cached = cachedPackages.FirstOrDefault(p => 
            p.Metadata.Name.Equals(packageName, StringComparison.OrdinalIgnoreCase));

        if (cached == null)
        {
            Helper.ShowError($"Package '{packageName}' not found in cache");
            Helper.ShowInfo("Use 'cache list' to see cached packages");
            return false;
        }

        Helper.ShowInfo($"Clear cached package: {cached.Metadata.Name} v{cached.Metadata.Version}");
        if (!Helper.ReadBool("Are you sure?"))
        {
            Helper.ShowInfo("Operation cancelled");
            return true;
        }

        var success = await _registry.ClearPackageCache(cached.Metadata);
        
        if (success)
        {
            Helper.ShowSuccess($"Cleared {cached.Metadata.Name} from cache");
        }
        else
        {
            Helper.ShowError($"Failed to clear {cached.Metadata.Name} from cache");
        }
        
        return success;
    }

    private bool HandleConfig(string args)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts.FirstOrDefault()?.ToLower();
        
        switch (command)
        {
            case "show":
            case "":
            case null:
                return HandleConfigShow();
                
            case "set-registry":
                var url = parts.Length > 1 ? parts[1] : null;
                return HandleConfigSetRegistry(url);
                
            default:
                Helper.ShowError($"Unknown config command: {command}");
                Helper.ShowInfo("Available commands: config show, config set-registry <url>");
                return false;
        }
    }

    private bool HandleConfigShow()
    {
        Helper.ShowInfo("=== Configuration ===");
        Helper.AddSpace();
        Helper.Write($"Registry URL: {DEFAULT_REGISTRY_URL}");
        Helper.Write($"Cache Directory: {_registry.GetCacheDirectory()}");
        Helper.AddSpace();
        return true;
    }

    private bool HandleConfigSetRegistry(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Helper.ShowError("Please specify a registry URL");
            Helper.ShowInfo("Example: config set-registry https://your-domain.com/registry.json");
            return false;
        }

        try
        {
            var uri = new Uri(url);
            _registry.SetRegistryUrl(url);
            Helper.ShowSuccess($"Registry URL updated to: {url}");
            Helper.ShowInfo("Use 'registry' to test the new URL");
            return true;
        }
        catch (Exception ex)
        {
            Helper.ShowError($"Invalid URL: {ex.Message}");
            return false;
        }
    }

}

