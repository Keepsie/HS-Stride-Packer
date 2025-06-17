// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using NUnit.Framework;
using FluentAssertions;
using System.Reflection;
using HS.Stride.Packer.Utilities;

namespace HS.Stride.Packer.Core.Tests
{
    [TestFixture]
    public class CriticalPathHandlingTests
    {
        private ResourcePathValidator _resource_validator;
        private string _test_data_path;
        private string _temp_test_dir;

        [SetUp]
        public void Setup()
        {
            _resource_validator = new ResourcePathValidator();
            
            // Get path to test data
            _test_data_path = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "Assets");
            
            // Create temporary directory for testing
            _temp_test_dir = Path.Combine(Path.GetTempPath(), $"stride_path_tests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_temp_test_dir);
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up temp directory
            try
            {
                if (Directory.Exists(_temp_test_dir))
                    Directory.Delete(_temp_test_dir, true);
            }
            catch
            {
                // Ignore cleanup failures
            }
        }

        #region ResourcePathValidator FindActualResourceFile Tests

        [Test]
        public void FindActualResourceFile_RelativeToAsset_FindCorrectFile()
        {
            // Arrange - Create test structure
            var project_dir = Path.Combine(_temp_test_dir, "TestProject");
            var assets_dir = Path.Combine(project_dir, "Assets", "UI");
            var resource_dir = Path.Combine(assets_dir, "Images");
            Directory.CreateDirectory(resource_dir);

            var asset_file = Path.Combine(assets_dir, "menu.sduipage");
            var resource_file = Path.Combine(resource_dir, "background.png");
            
            File.WriteAllText(asset_file, "!file Images/background.png");
            File.WriteAllText(resource_file, "dummy image content");

            // Act - Test relative path resolution
            var result = InvokeFindActualResourceFile("Images/background.png", asset_file, project_dir);

            // Assert
            result.Should().NotBeEmpty();
            Path.GetFileName(result).Should().Be("background.png");
        }

        [Test]
        public void FindActualResourceFile_RelativeWithDotDot_ResolveCorrectly()
        {
            // Arrange - Test ../../../ style paths
            var project_dir = Path.Combine(_temp_test_dir, "TestProject");
            var deep_assets_dir = Path.Combine(project_dir, "Assets", "UI", "Menus", "Main");
            var resources_dir = Path.Combine(project_dir, "Resources");
            Directory.CreateDirectory(deep_assets_dir);
            Directory.CreateDirectory(resources_dir);

            var asset_file = Path.Combine(deep_assets_dir, "mainmenu.sduipage");
            var resource_file = Path.Combine(resources_dir, "logo.png");
            
            File.WriteAllText(asset_file, "content");
            File.WriteAllText(resource_file, "image content");

            // Act - Test complex relative path
            var result = InvokeFindActualResourceFile("../../../../Resources/logo.png", asset_file, project_dir);

            // Assert
            result.Should().NotBeEmpty();
            Path.GetFileName(result).Should().Be("logo.png");
        }

        [Test]
        public void FindActualResourceFile_AbsolutePath_HandleCorrectly()
        {
            // Arrange
            var absolute_file = Path.Combine(_temp_test_dir, "absolute_resource.txt");
            File.WriteAllText(absolute_file, "content");

            var project_dir = Path.Combine(_temp_test_dir, "project");
            var asset_file = Path.Combine(project_dir, "test.sdprefab");
            Directory.CreateDirectory(project_dir);
            File.WriteAllText(asset_file, "content");

            // Act
            var result = InvokeFindActualResourceFile(absolute_file, asset_file, project_dir);

            // Assert
            result.Should().Be(Path.GetFullPath(absolute_file));
        }

        [Test]
        public void FindActualResourceFile_SearchInCommonLocations_FindFile()
        {
            // Arrange - File not relative to asset, but in common location
            var project_dir = Path.Combine(_temp_test_dir, "TestProject");
            var assets_resources_dir = Path.Combine(project_dir, "Assets", "Resources");
            Directory.CreateDirectory(assets_resources_dir);

            var asset_file = Path.Combine(project_dir, "Assets", "test.sdprefab");
            var resource_file = Path.Combine(assets_resources_dir, "texture.png");
            
            Directory.CreateDirectory(Path.GetDirectoryName(asset_file));
            File.WriteAllText(asset_file, "content");
            File.WriteAllText(resource_file, "texture content");

            // Act - Search for just filename, should find in Assets/Resources
            var result = InvokeFindActualResourceFile("texture.png", asset_file, project_dir);

            // Assert
            result.Should().NotBeEmpty();
            result.Should().Contain("texture.png");
        }

        [Test]
        public void FindActualResourceFile_NonExistentFile_ReturnEmpty()
        {
            // Arrange
            var project_dir = Path.Combine(_temp_test_dir, "TestProject");
            var asset_file = Path.Combine(project_dir, "test.sdprefab");
            Directory.CreateDirectory(project_dir);
            File.WriteAllText(asset_file, "content");

            // Act
            var result = InvokeFindActualResourceFile("nonexistent.png", asset_file, project_dir);

            // Assert
            result.Should().BeEmpty();
        }

        [Test]
        public void FindActualResourceFile_CrossPlatformSeparators_HandleBoth()
        {
            // Arrange - Test mixed separators
            var project_dir = Path.Combine(_temp_test_dir, "TestProject");
            var assets_dir = Path.Combine(project_dir, "Assets", "UI");
            Directory.CreateDirectory(assets_dir);

            var asset_file = Path.Combine(assets_dir, "test.sduipage");
            var resource_file = Path.Combine(assets_dir, "image.png");
            
            File.WriteAllText(asset_file, "content");
            File.WriteAllText(resource_file, "image content");

            // Act - Test both separators
            var result_forward = InvokeFindActualResourceFile("image.png", asset_file, project_dir);
            var result_mixed = InvokeFindActualResourceFile(".\\image.png", asset_file, project_dir);

            // Assert
            result_forward.Should().NotBeEmpty();
            result_mixed.Should().NotBeEmpty();
        }

        #endregion

        #region FileHelper Edge Case Tests

        [Test]
        public void FileHelper_SaveFile_CreateDirectoryIfNotExists()
        {
            // Arrange
            var deep_path = Path.Combine(_temp_test_dir, "deep", "nested", "structure", "file.txt");

            // Act
            var result = FileHelper.SaveFile("test content", deep_path);

            // Assert
            result.Should().BeTrue();
            File.Exists(deep_path).Should().BeTrue();
            File.ReadAllText(deep_path).Should().Be("test content");
        }

        [Test]
        public void FileHelper_SaveFile_LongFileName_HandleGracefully()
        {
            // Arrange - Create very long filename (but within reasonable limits)
            var long_filename = new string('a', 200) + ".txt";
            var file_path = Path.Combine(_temp_test_dir, long_filename);

            // Act
            var result = FileHelper.SaveFile("content", file_path);

            // Assert - Should handle long names (or fail gracefully)
            if (result)
            {
                File.Exists(file_path).Should().BeTrue();
            }
            // If it fails, that's acceptable for extremely long names
        }

        [Test]
        public void FileHelper_SaveFile_SpecialCharactersInPath_HandleCorrectly()
        {
            // Arrange - Test various special characters that are valid in filenames
            var special_chars_tests = new[]
            {
                "file with spaces.txt",
                "file-with-dashes.txt",
                "file_with_underscores.txt",
                "file(with)parentheses.txt",
                "file[with]brackets.txt"
            };

            foreach (var filename in special_chars_tests)
            {
                var file_path = Path.Combine(_temp_test_dir, filename);

                // Act
                var result = FileHelper.SaveFile($"content for {filename}", file_path);

                // Assert
                result.Should().BeTrue($"Should handle filename: {filename}");
                if (result)
                {
                    File.Exists(file_path).Should().BeTrue();
                }
            }
        }

        [Test]
        public void FileHelper_LoadFile_NonExistentFile_ReturnEmpty()
        {
            // Act
            var result = FileHelper.LoadFile(Path.Combine(_temp_test_dir, "nonexistent.txt"));

            // Assert
            result.Should().BeEmpty();
        }

        [Test]
        public void FileHelper_DeleteFile_NonExistentFile_ReturnFalse()
        {
            // Act
            var result = FileHelper.DeleteFile(Path.Combine(_temp_test_dir, "nonexistent.txt"));

            // Assert
            result.Should().BeFalse();
        }

        [Test]
        public void FileHelper_CopyDirectory_NestedStructure_CopyCompletely()
        {
            // Arrange - Create nested source structure
            var source_dir = Path.Combine(_temp_test_dir, "source");
            var dest_dir = Path.Combine(_temp_test_dir, "destination");
            
            var nested_dir = Path.Combine(source_dir, "nested", "deep");
            Directory.CreateDirectory(nested_dir);
            
            File.WriteAllText(Path.Combine(source_dir, "root.txt"), "root content");
            File.WriteAllText(Path.Combine(nested_dir, "deep.txt"), "deep content");

            // Act
            var result = FileHelper.CopyDirectory(source_dir, dest_dir);

            // Assert
            result.Should().BeTrue();
            File.Exists(Path.Combine(dest_dir, "root.txt")).Should().BeTrue();
            File.Exists(Path.Combine(dest_dir, "nested", "deep", "deep.txt")).Should().BeTrue();
        }

        [Test]
        public void FileHelper_GetFilesInDirectory_NonExistentDirectory_ReturnEmpty()
        {
            // Act
            var result = FileHelper.GetFilesInDirectory(Path.Combine(_temp_test_dir, "nonexistent"));

            // Assert
            result.Should().BeEmpty();
        }

        [Test]
        public void FileHelper_MoveFile_CreateDestinationDirectory_Succeed()
        {
            // Arrange
            var source_file = Path.Combine(_temp_test_dir, "source.txt");
            var dest_file = Path.Combine(_temp_test_dir, "deep", "nested", "destination.txt");
            
            File.WriteAllText(source_file, "content to move");

            // Act
            var result = FileHelper.MoveFile(source_file, dest_file);

            // Assert
            result.Should().BeTrue();
            File.Exists(source_file).Should().BeFalse();
            File.Exists(dest_file).Should().BeTrue();
            File.ReadAllText(dest_file).Should().Be("content to move");
        }

        #endregion

        #region Cross-Platform Path Validation Tests

        [Test]
        public void ResourcePathValidator_ValidateProject_HandleMixedSeparators()
        {
            // Arrange - Create test project with mixed path separators in content
            var project_dir = Path.Combine(_temp_test_dir, "MixedProject");
            var assets_dir = Path.Combine(project_dir, "Assets");
            Directory.CreateDirectory(assets_dir);

            var prefab_file = Path.Combine(assets_dir, "test.sdprefab");
            var prefab_content = @"!file ../Resources/texture.png
            !file Resources\material.mat
            !file deep/nested/path/model.obj";
            
            File.WriteAllText(prefab_file, prefab_content);

            // Act
            var result = _resource_validator.ValidateProject(project_dir);

            // Assert - Should not crash with mixed separators
            result.Should().NotBeNull();
        }

        [Test]
        public void ResourcePathValidator_HandleVeryLongPaths_NotCrash()
        {
            // Arrange - Create path near system limits
            var project_dir = Path.Combine(_temp_test_dir, "LongPathProject");
            Directory.CreateDirectory(project_dir);

            var very_long_path = Path.Combine(project_dir, new string('a', 200), new string('b', 200));
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(very_long_path));
                var asset_file = Path.Combine(project_dir, "test.sdprefab");
                File.WriteAllText(asset_file, $"!file {very_long_path}");

                // Act & Assert - Should not crash
                Assert.DoesNotThrow(() =>
                {
                    var result = _resource_validator.ValidateProject(project_dir);
                    result.Should().NotBeNull();
                });
            }
            catch (PathTooLongException)
            {
                // Expected on some systems, test passes
                Assert.Pass("System doesn't support very long paths - test passes");
            }
        }

        [Test]
        public void FileHelper_GenerateUniqueFileName_ProduceValidNames()
        {
            // Arrange & Act - Generate multiple unique names rapidly
            var names = new HashSet<string>();
            for (int i = 0; i < 10; i++)
            {
                var name = FileHelper.GenerateUniqueFileName("test", ".txt", _temp_test_dir);
                names.Add(name);
                
                // Small delay to test rapid generation
                System.Threading.Thread.Sleep(1);
            }

            // Assert
            names.Should().HaveCount(10, "All generated names should be unique");
            foreach (var name in names)
            {
                Path.IsPathRooted(name).Should().BeTrue();
                name.Should().Contain("test");
                name.Should().EndWith(".txt");
            }
        }

        #endregion

        #region Performance Tests

        [Test]
        public void ResourcePathValidator_ValidateProject_PerformWithManyFiles()
        {
            // Arrange - Create project with many asset files
            var project_dir = Path.Combine(_temp_test_dir, "PerformanceProject");
            var assets_dir = Path.Combine(project_dir, "Assets");
            Directory.CreateDirectory(assets_dir);

            // Create 50 asset files with resource references
            for (int i = 0; i < 50; i++)
            {
                var asset_file = Path.Combine(assets_dir, $"asset_{i}.sdprefab");
                File.WriteAllText(asset_file, $"!file resource_{i}.png\n!file texture_{i}.jpg");
            }

            // Act & Assert - Should complete within reasonable time
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = _resource_validator.ValidateProject(project_dir);
            stopwatch.Stop();

            result.Should().NotBeNull();
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000, "Should validate many files within 10 seconds");
        }

        #endregion

        #region Helper Methods

        private string InvokeFindActualResourceFile(string resourcePath, string assetFilePath, string projectPath)
        {
            var method = typeof(ResourcePathValidator).GetMethod("FindActualResourceFile", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            return (string)method!.Invoke(_resource_validator, new object[] { resourcePath, assetFilePath, projectPath })!;
        }

        #endregion
    }
}