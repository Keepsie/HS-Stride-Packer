// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using NUnit.Framework;
using FluentAssertions;
using System.Reflection;

namespace HS.Stride.Packer.Core.Tests
{
    [TestFixture]
    public class PathMappingTests
    {
        private PackageExporter _package_exporter;

        [SetUp]
        public void Setup()
        {
            var resource_validator = new ResourcePathValidator();
            var namespace_scanner = new NamespaceScanner();
            _package_exporter = new PackageExporter(resource_validator, namespace_scanner);
        }

        [Test]
        public void MapAssetFileToTempStructure_AssetFolderWithSpaces_ReturnCorrectMappedPath()
        {
            // Arrange
            var original_asset_file = @"E:\Github\Desert-Strides\desert_strides\desert_strides\Assets\UI\Dev Console\SpriteSheet.sdsheet";
            var library_path = @"E:\Github\Desert-Strides\desert_strides\";
            var temp_dir = @"C:\Temp\test123";
            var selected_asset_folders = new List<string> { "desert_strides/Assets/UI" };

            // Act
            var result = InvokeMapAssetFileToTempStructure(original_asset_file, library_path, temp_dir, selected_asset_folders);

            // Assert
            result.Should().Be(@"C:\Temp\test123\Assets\UI\Dev Console\SpriteSheet.sdsheet");
        }

        [Test]
        public void MapAssetFileToTempStructure_MixedPathSeparators_ReturnCorrectMappedPath()
        {
            // Arrange
            var original_asset_file = @"E:\Project\my_project\my_project\Assets\Materials\test.sdmat";
            var library_path = @"E:\Project\my_project\";
            var temp_dir = @"C:\Temp\abc123";
            var selected_asset_folders = new List<string> { "my_project/Assets/Materials" };

            // Act
            var result = InvokeMapAssetFileToTempStructure(original_asset_file, library_path, temp_dir, selected_asset_folders);

            // Assert
            result.Should().Be(@"C:\Temp\abc123\Assets\Materials\test.sdmat");
        }

        [Test]
        public void MapAssetFileToTempStructure_UnixStylePaths_ReturnCorrectMappedPath()
        {
            // Arrange
            var original_asset_file = "/home/user/project/my_project/Assets/Models/character.sdm3d";
            var library_path = "/home/user/project/";
            var temp_dir = "/tmp/test123";
            var selected_asset_folders = new List<string> { "my_project/Assets/Models" };

            // Act
            var result = InvokeMapAssetFileToTempStructure(original_asset_file, library_path, temp_dir, selected_asset_folders);

            // Assert
            result.Should().Be("/tmp/test123/Assets/Models/character.sdm3d");
        }

        [Test]
        public void MapAssetFileToTempStructure_NestedFolderStructure_ReturnPreservedStructure()
        {
            // Arrange
            var original_asset_file = @"C:\Project\game\game\Assets\UI\Menus\MainMenu\background.png";
            var library_path = @"C:\Project\game\";
            var temp_dir = @"C:\Temp\xyz789";
            var selected_asset_folders = new List<string> { "game/Assets/UI" };

            // Act
            var result = InvokeMapAssetFileToTempStructure(original_asset_file, library_path, temp_dir, selected_asset_folders);

            // Assert
            result.Should().Contain(@"Assets\UI\Menus\MainMenu\background.png");
        }

        [Test]
        public void MapAssetFileToTempStructure_NonAssetFile_ReturnOriginalStructure()
        {
            // Arrange
            var original_asset_file = @"E:\Project\game\game.Game\Components\PlayerController.cs";
            var library_path = @"E:\Project\game\";
            var temp_dir = @"C:\Temp\test456";
            var selected_asset_folders = new List<string> { "game/Assets/UI" };

            // Act
            var result = InvokeMapAssetFileToTempStructure(original_asset_file, library_path, temp_dir, selected_asset_folders);

            // Assert
            result.Should().Be(@"C:\Temp\test456\game.Game\Components\PlayerController.cs");
        }

        [Test]
        public void MapAssetFileToTempStructure_MultipleAssetFolders_ReturnCorrectMapping()
        {
            // Arrange
            var original_asset_file = @"E:\Project\game\game\Assets\Materials\stone.sdmat";
            var library_path = @"E:\Project\game\";
            var temp_dir = @"C:\Temp\multi123";
            var selected_asset_folders = new List<string> 
            { 
                "game/Assets/UI", 
                "game/Assets/Materials",
                "game/Assets/Models" 
            };

            // Act
            var result = InvokeMapAssetFileToTempStructure(original_asset_file, library_path, temp_dir, selected_asset_folders);

            // Assert
            result.Should().Contain(@"Assets\Materials\stone.sdmat");
        }

        [Test]
        public void MapAssetFileToTempStructure_CaseInsensitiveComparison_ReturnCorrectMapping()
        {
            // Arrange
            var original_asset_file = @"E:\Project\GAME\GAME\ASSETS\UI\button.sdprefab";
            var library_path = @"E:\Project\GAME\";
            var temp_dir = @"C:\Temp\case123";
            var selected_asset_folders = new List<string> { "game/assets/ui" };

            // Act
            var result = InvokeMapAssetFileToTempStructure(original_asset_file, library_path, temp_dir, selected_asset_folders);

            // Assert
            result.Should().Contain(@"Assets\ui\button.sdprefab");
        }

        [Test]
        public void MapAssetFileToTempStructure_EmptyAssetFoldersList_ReturnOriginalStructure()
        {
            // Arrange
            var original_asset_file = @"E:\Project\game\game\Assets\UI\button.sdprefab";
            var library_path = @"E:\Project\game\";
            var temp_dir = @"C:\Temp\empty123";
            var selected_asset_folders = new List<string>();

            // Act
            var result = InvokeMapAssetFileToTempStructure(original_asset_file, library_path, temp_dir, selected_asset_folders);

            // Assert
            result.Should().Be(@"C:\Temp\empty123\game\Assets\UI\button.sdprefab");
        }

        [Test]
        public void MapAssetFileToTempStructure_NullAssetFoldersList_ReturnOriginalStructure()
        {
            // Arrange
            var original_asset_file = @"E:\Project\game\game\Assets\UI\button.sdprefab";
            var library_path = @"E:\Project\game\";
            var temp_dir = @"C:\Temp\null123";
            List<string>? selected_asset_folders = null;

            // Act
            var result = InvokeMapAssetFileToTempStructure(original_asset_file, library_path, temp_dir, selected_asset_folders);

            // Assert
            result.Should().Be(@"C:\Temp\null123\game\Assets\UI\button.sdprefab");
        }

        [Test]
        public void MapAssetFileToTempStructure_ComplexProjectName_ReturnCorrectMapping()
        {
            // Arrange
            var original_asset_file = @"C:\Dev\my_awesome_game_2024\my_awesome_game_2024\Assets\VFX\explosion.sdfx";
            var library_path = @"C:\Dev\my_awesome_game_2024\";
            var temp_dir = @"C:\Temp\complex123";
            var selected_asset_folders = new List<string> { "my_awesome_game_2024/Assets/VFX" };

            // Act
            var result = InvokeMapAssetFileToTempStructure(original_asset_file, library_path, temp_dir, selected_asset_folders);

            // Assert
            result.Should().Contain(@"Assets\VFX\explosion.sdfx");
        }

        private string InvokeMapAssetFileToTempStructure(string original_asset_file, string library_path, string temp_dir, List<string>? selected_asset_folders)
        {
            var method = typeof(PackageExporter).GetMethod("MapAssetFileToTempStructure", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            return (string)method!.Invoke(_package_exporter, new object?[] { original_asset_file, library_path, temp_dir, selected_asset_folders })!;
        }
    }
}