// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using System.IO;
using System.Text.Json;
using HS.Stride.Packer.Core;

namespace HS.Stride.Packer.UI.Services
{
    public class SettingsManager
    {
        private readonly string _settingsFilePath;
        private readonly string _presetsFolder;
        private AppSettings _settings;

        public SettingsManager()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "HS Stride Packer");
            Directory.CreateDirectory(appFolder);
            _settingsFilePath = Path.Combine(appFolder, "settings.json");
            _presetsFolder = Path.Combine(appFolder, "Presets");
            Directory.CreateDirectory(_presetsFolder);

            _settings = LoadSettings();
        }

        public AppSettings Settings => _settings;
        public string PresetsFolder => _presetsFolder;

        public void SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception)
            {
                // Fail silently - settings are not critical
            }
        }

        private AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    return settings ?? new AppSettings();
                }
            }
            catch (Exception)
            {
                // Fail silently and use defaults
            }

            return new AppSettings();
        }

        public void ResetToDefaults()
        {
            _settings = new AppSettings();
            SaveSettings();
        }

        /// <summary>
        /// Save an export preset by package name
        /// </summary>
        public void SavePreset(ExportPreset preset)
        {
            try
            {
                var fileName = SanitizeFileName(preset.PackageName) + ".json";
                var filePath = Path.Combine(_presetsFolder, fileName);
                var json = JsonSerializer.Serialize(preset, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch (Exception)
            {
                // Fail silently
            }
        }

        /// <summary>
        /// Load an export preset by file path
        /// </summary>
        public ExportPreset? LoadPreset(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    return JsonSerializer.Deserialize<ExportPreset>(json);
                }
            }
            catch (Exception)
            {
                // Fail silently
            }
            return null;
        }

        /// <summary>
        /// Get all saved presets
        /// </summary>
        public List<string> GetPresetFiles()
        {
            try
            {
                return Directory.GetFiles(_presetsFolder, "*.json").ToList();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Delete a preset file
        /// </summary>
        public void DeletePreset(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception)
            {
                // Fail silently
            }
        }

        private string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        }
    }

    public class AppSettings
    {
        public string RegistryUrl { get; set; } = StridePackageManager.DefaultRegistryUrl;
        public string LastUsedProjectPath { get; set; } = "";
        public string LastUsedExportPath { get; set; } = "";
        public bool RememberWindowSize { get; set; } = true;
        public double WindowWidth { get; set; } = 700;
        public double WindowHeight { get; set; } = 600;

        // Default registry URL for reset functionality
        public static string DefaultRegistryUrl => StridePackageManager.DefaultRegistryUrl;
    }

    /// <summary>
    /// Stores all export settings for a package preset
    /// </summary>
    public class ExportPreset
    {
        public string PackageName { get; set; } = "";
        public string Version { get; set; } = "";
        public string StrideVersion { get; set; } = "";
        public string Author { get; set; } = "";
        public string Description { get; set; } = "";
        public string Tags { get; set; } = "";
        public string ProjectPath { get; set; } = "";
        public string ExportLocation { get; set; } = "";

        // Selected folders (by relative path)
        public List<string> SelectedAssetFolders { get; set; } = new();
        public List<string> SelectedCodeFolders { get; set; } = new();

        // Excluded namespaces
        public List<string> ExcludedNamespaces { get; set; } = new();

        // Timestamp for reference
        public DateTime SavedDate { get; set; } = DateTime.UtcNow;
    }
}