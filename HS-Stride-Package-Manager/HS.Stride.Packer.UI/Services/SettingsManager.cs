// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using System.IO;
using System.Text.Json;
using HS.Stride.Packer.Core;

namespace HS.Stride.Packer.UI.Services
{
    public class SettingsManager
    {
        private readonly string _settingsFilePath;
        private AppSettings _settings;
        
        public SettingsManager()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "HS Stride Packer");
            Directory.CreateDirectory(appFolder);
            _settingsFilePath = Path.Combine(appFolder, "settings.json");
            
            _settings = LoadSettings();
        }
        
        public AppSettings Settings => _settings;
        
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
}