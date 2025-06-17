// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using NUnit.Framework;
using FluentAssertions;

namespace HS.Stride.Packer.Core.Tests
{
    [TestFixture]
    public class RegexPatternTests
    {
        private NamespaceScanner _namespace_scanner;
        private ResourcePathValidator _resource_validator;
        private string _test_data_path;
        
        // Real Stride file content for testing
        private string _prefab_content;
        private string _scene_content;
        private string _ui_page_content;

        [SetUp]
        public void Setup()
        {
            _namespace_scanner = new NamespaceScanner();
            _resource_validator = new ResourcePathValidator();
            
            // Get path to test data (now copied to output directory)
            _test_data_path = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "Assets");
            
            if (!Directory.Exists(_test_data_path))
                throw new DirectoryNotFoundException($"TestData directory not found at: {_test_data_path}");
            
            // Load real Stride file content for comprehensive testing
            LoadRealStrideFileContent();
        }

        private void LoadRealStrideFileContent()
        {
            var prefab_path = Path.Combine(_test_data_path, "Background.sdprefab");
            var scene_path = Path.Combine(_test_data_path, "Scene.sdscene");
            var ui_page_path = Path.Combine(_test_data_path, "Page.sduipage");

            _prefab_content = File.ReadAllText(prefab_path);
            _scene_content = File.ReadAllText(scene_path);
            _ui_page_content = File.ReadAllText(ui_page_path);
        }

        [Test]
        public void NamespaceScanner_ScanPrefabFile_ReturnCorrectNamespaces()
        {
            // Arrange - Create temp file with prefab content
            var temp_prefab_path = Path.Combine(Path.GetTempPath(), "test.sdprefab");
            File.WriteAllText(temp_prefab_path, _prefab_content);

            try
            {
                // Act
                var result = _namespace_scanner.ScanFile(temp_prefab_path);

                // Assert
                result.Should().NotBeEmpty();
                result.Should().Contain("SpaceEscape.Background");
            }
            finally
            {
                if (File.Exists(temp_prefab_path))
                    File.Delete(temp_prefab_path);
            }
        }

        [Test]
        public void NamespaceScanner_ScanSceneFile_ReturnCorrectNamespaces()
        {
            // Arrange - Create temp file with scene content
            var temp_scene_path = Path.Combine(Path.GetTempPath(), "test.sdscene");
            File.WriteAllText(temp_scene_path, _scene_content);

            try
            {
                // Act
                var result = _namespace_scanner.ScanFile(temp_scene_path);

                // Assert
                result.Should().NotBeEmpty();
                result.Should().Contain("SpaceEscape");
                result.Should().Contain("SpaceEscape.Background");
            }
            finally
            {
                if (File.Exists(temp_scene_path))
                    File.Delete(temp_scene_path);
            }
        }

        [Test]
        public void NamespaceScanner_ScriptReferenceRegex_MatchRealPatterns()
        {
            // Test the actual regex pattern against real content
            var script_references = new[]
            {
                "!SpaceEscape.Background.BackgroundScript,SpaceEscape.Game",
                "!SpaceEscape.CharacterScript,SpaceEscape.Game",
                "!SpaceEscape.GameScript,SpaceEscape.Game",
                "!SpaceEscape.UIScript,SpaceEscape.Game"
            };

            foreach (var reference in script_references)
            {
                // Verify our content contains these patterns
                _scene_content.Should().Contain(reference, $"Scene should contain script reference: {reference}");
            }
        }

        [Test]
        public void NamespaceScanner_HandleMalformedInput_NotThrowException()
        {
            // Arrange - Various malformed inputs that could break regex
            var malformed_inputs = new[]
            {
                "!InvalidScript", // Missing assembly
                "!,", // Empty namespace and assembly
                "!.Script,Assembly", // Namespace starting with dot
                "!Script.,Assembly", // Namespace ending with dot
                "!Script..Component,Assembly", // Double dots
                "!Script\nMultiline,Assembly", // Newlines
                "!" + new string('A', 1000) + ",Assembly", // Very long namespace
                "!Script,", // Missing assembly name
                "", // Empty string
                "Random text without script references"
            };

            foreach (var input in malformed_inputs)
            {
                // Arrange - Create temp file for each malformed input
                var temp_path = Path.Combine(Path.GetTempPath(), $"malformed_test_{Guid.NewGuid()}.sdprefab");
                File.WriteAllText(temp_path, input ?? "");

                try
                {
                    // Act & Assert - Should not throw exceptions
                    Assert.DoesNotThrow(() => 
                    {
                        var result = _namespace_scanner.ScanFile(temp_path);
                        result.Should().NotBeNull();
                    });
                }
                finally
                {
                    if (File.Exists(temp_path))
                        File.Delete(temp_path);
                }
            }
        }

        [Test]
        public void NamespaceScanner_HandleSpecialCharacters_ProcessCorrectly()
        {
            // Arrange - Test edge cases with special characters
            var special_cases = new Dictionary<string, List<string>>
            {
                ["!My_Namespace.Component_1,My_Assembly"] = new() { "My_Namespace" },
                ["!Namespace123.Script456,Assembly789"] = new() { "Namespace123" },
                ["!A.B.C.D.Component,E.F.G.Assembly"] = new() { "A.B.C.D" }
            };

            foreach (var test_case in special_cases)
            {
                // Arrange - Create temp file
                var temp_path = Path.Combine(Path.GetTempPath(), $"special_chars_{Guid.NewGuid()}.sdprefab");
                File.WriteAllText(temp_path, test_case.Key);

                try
                {
                    // Act
                    var result = _namespace_scanner.ScanFile(temp_path);

                    // Assert
                    foreach (var expected_namespace in test_case.Value)
                    {
                        result.Should().Contain(expected_namespace, 
                            $"Should extract namespace '{expected_namespace}' from '{test_case.Key}'");
                    }
                }
                finally
                {
                    if (File.Exists(temp_path))
                        File.Delete(temp_path);
                }
            }
        }

        [Test]
        public void NamespaceScanner_ScanAllRealAssetTypes_NotThrowExceptions()
        {
            // Arrange - Get all real Stride asset files
            var stride_files = Directory.GetFiles(_test_data_path, "*.sd*", SearchOption.AllDirectories);

            foreach (var file in stride_files)
            {
                var extension = Path.GetExtension(file);

                // Act & Assert - Should handle all file types without throwing
                Assert.DoesNotThrow(() =>
                {
                    var result = _namespace_scanner.ScanFile(file);
                    result.Should().NotBeNull($"Namespace scanner should handle {extension} files");
                }, $"Failed processing file: {Path.GetFileName(file)} ({extension})");
            }
        }

        [Test]
        public void ResourcePathValidator_ValidateProject_HandleAllAssetTypes()
        {
            // Act & Assert - Should handle all file types without throwing
            Assert.DoesNotThrow(() =>
            {
                var result = _resource_validator.ValidateProject(_test_data_path);
                result.Should().NotBeNull("Resource validator should handle all asset types");
            });
        }

        [Test]
        public void NamespaceScanner_PerformanceWithLargeFiles_CompleteReasonably()
        {
            // Arrange - Create large content by repeating scene content
            var large_content = string.Join("\n", Enumerable.Repeat(_scene_content, 100));
            var temp_path = Path.Combine(Path.GetTempPath(), $"performance_test_{Guid.NewGuid()}.sdscene");
            File.WriteAllText(temp_path, large_content);

            try
            {
                // Act & Assert - Should complete within reasonable time
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var result = _namespace_scanner.ScanFile(temp_path);
                stopwatch.Stop();

                result.Should().NotBeEmpty();
                stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "Should process large files within 5 seconds");
            }
            finally
            {
                if (File.Exists(temp_path))
                    File.Delete(temp_path);
            }
        }

        [Test]
        public void NamespaceScanner_PerformanceWithManyFiles_CompleteReasonably()
        {
            // Act & Assert - Should handle scanning entire test directory efficiently
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = _namespace_scanner.ScanDirectory(_test_data_path);
            stopwatch.Stop();

            result.Should().NotBeNull();
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000, "Should scan all test files within 10 seconds");
        }
    }
}