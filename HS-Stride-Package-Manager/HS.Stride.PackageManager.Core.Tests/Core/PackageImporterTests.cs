// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0


using NUnit.Framework;
using FluentAssertions;
using System.IO.Compression;
using System.Text.Json;

namespace HS.Stride.Packer.Core.Tests
{
    [TestFixture]
    public class PackageImporterTests
    {
        private PackageImporter _packageImporter;
        private ImportSettings _importSettings;

        [SetUp]
        public void Setup()
        {
            _packageImporter = new PackageImporter();

            _importSettings = new ImportSettings
            {
                PackagePath = @"C:\TestPackage\test.stridepackage",
                TargetProjectPath = @"C:\TestProject",
                OverwriteExistingFiles = false,
            };
        }

        [Test]
        public void Constructor_CreateInstance_ReturnPackageImporter()
        {
            _packageImporter.Should().NotBeNull();
        }

        [Test]
        public void ValidateSettings_NonExistentPackage_ReturnValidationResultWithErrors()
        {
            _importSettings.PackagePath = @"C:\NonExistent\package.stridepackage";
            
            var result = _packageImporter.ValidateSettings(_importSettings);
            
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain($"Package file does not exist: {_importSettings.PackagePath}");
        }

        [Test]
        public void ValidateSettings_NonExistentTargetProject_ReturnValidationResultWithErrors()
        {
            _importSettings.TargetProjectPath = @"C:\NonExistent\Target";
            
            var result = _packageImporter.ValidateSettings(_importSettings);
            
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain($"Target project path does not exist: {_importSettings.TargetProjectPath}");
        }

        [Test]
        public void ValidateSettings_InvalidPackageExtension_ReturnValidationResultWithWarnings()
        {
            _importSettings.PackagePath = @"C:\TestPackage\package.zip";
            
            var result = _packageImporter.ValidateSettings(_importSettings);
            
            result.Should().NotBeNull();
            result.Warnings.Should().Contain("Package file does not have .stridepackage extension");
        }

        [Test]
        public async Task ImportPackageAsync_InvalidSettings_ThrowInvalidOperationException()
        {
            _importSettings.PackagePath = "nonexistent.stridepackage";
            
            Func<Task> act = async () => await _packageImporter.ImportPackageAsync(_importSettings);
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Invalid import settings: *");
        }

        [Test]
        public async Task ImportPackageAsync_ValidPackageWithValidHash_ShouldSucceed()
        {
            // Create test package with valid hash
            var testPackagePath = await CreateTestPackageWithValidHash();
            var testProjectPath = CreateTestProjectDirectory();
            
            try
            {
                _importSettings.PackagePath = testPackagePath;
                _importSettings.TargetProjectPath = testProjectPath;

                var result = await _packageImporter.ImportPackageAsync(_importSettings);

                result.Should().NotBeNull();
                result.Manifest.Should().NotBeNull();
                result.Manifest.PackageHash.Should().NotBeNullOrEmpty();
                result.ImportedFiles.Should().NotBeEmpty();
            }
            finally
            {
                CleanupTestFiles(testPackagePath, testProjectPath);
            }
        }

        [Test]
        public async Task ImportPackageAsync_PackageWithCorruptedHash_ShouldThrowException()
        {
            // Create test package with intentionally wrong hash
            var testPackagePath = await CreateTestPackageWithCorruptedHash();
            var testProjectPath = CreateTestProjectDirectory();
            
            try
            {
                _importSettings.PackagePath = testPackagePath;
                _importSettings.TargetProjectPath = testProjectPath;

                Func<Task> act = async () => await _packageImporter.ImportPackageAsync(_importSettings);
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Package integrity verification failed. The package may be corrupted or tampered with.");
            }
            finally
            {
                CleanupTestFiles(testPackagePath, testProjectPath);
            }
        }

        [Test]
        public async Task ImportPackageAsync_PackageWithNoHash_ShouldThrowException()
        {
            // Create test package without hash - should now FAIL
            var testPackagePath = await CreateTestPackageWithoutHash();
            var testProjectPath = CreateTestProjectDirectory();
            
            try
            {
                _importSettings.PackagePath = testPackagePath;
                _importSettings.TargetProjectPath = testProjectPath;

                Func<Task> act = async () => await _packageImporter.ImportPackageAsync(_importSettings);
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Package manifest is missing integrity hash. This package may be corrupted or was created with an outdated version of the packer.");
            }
            finally
            {
                CleanupTestFiles(testPackagePath, testProjectPath);
            }
        }

        [Test]
        public async Task ImportPackageAsync_PackageWithNoManifest_ShouldThrowException()
        {
            // Create test package without manifest.json - should FAIL
            var testPackagePath = await CreateTestPackageWithoutManifest();
            var testProjectPath = CreateTestProjectDirectory();
            
            try
            {
                _importSettings.PackagePath = testPackagePath;
                _importSettings.TargetProjectPath = testProjectPath;

                Func<Task> act = async () => await _packageImporter.ImportPackageAsync(_importSettings);
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Package is missing manifest.json file. This package may be corrupted or is not a valid .stridepackage file.");
            }
            finally
            {
                CleanupTestFiles(testPackagePath, testProjectPath);
            }
        }

        private async Task<string> CreateTestPackageWithoutManifest()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            // Create test content WITHOUT manifest.json
            var assetsDir = Path.Combine(tempDir, "Assets");
            Directory.CreateDirectory(assetsDir);
            await File.WriteAllTextAsync(Path.Combine(assetsDir, "test.txt"), "Test content for package without manifest");

            // Create ZIP package without manifest
            var packagePath = Path.Combine(Path.GetTempPath(), $"TestPackage_NoManifest_{Guid.NewGuid()}.stridepackage");
            ZipFile.CreateFromDirectory(tempDir, packagePath);

            Directory.Delete(tempDir, true);
            return packagePath;
        }

        private async Task<string> CreateTestPackageWithValidHash()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            // Create test content
            var assetsDir = Path.Combine(tempDir, "Assets");
            Directory.CreateDirectory(assetsDir);
            await File.WriteAllTextAsync(Path.Combine(assetsDir, "test.txt"), "Test content for hash verification");

            // Generate proper hash
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var files = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(f).Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f)
                .ToList();

            foreach (var file in files)
            {
                var fileBytes = await File.ReadAllBytesAsync(file);
                sha256.TransformBlock(fileBytes, 0, fileBytes.Length, null, 0);
            }
            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            var hash = Convert.ToHexString(sha256.Hash ?? Array.Empty<byte>());

            // Create manifest with correct hash
            var manifest = new PackageManifest
            {
                Name = "TestPackage",
                Version = "1.0.0",
                Author = "TestAuthor",
                Description = "Test package with valid hash",
                PackageHash = hash
            };

            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(tempDir, "manifest.json"), manifestJson);

            // Create ZIP package
            var packagePath = Path.Combine(Path.GetTempPath(), $"TestPackage_Valid_{Guid.NewGuid()}.stridepackage");
            ZipFile.CreateFromDirectory(tempDir, packagePath);

            Directory.Delete(tempDir, true);
            return packagePath;
        }

        private async Task<string> CreateTestPackageWithCorruptedHash()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            // Create test content
            var assetsDir = Path.Combine(tempDir, "Assets");
            Directory.CreateDirectory(assetsDir);
            await File.WriteAllTextAsync(Path.Combine(assetsDir, "test.txt"), "Test content for corrupted hash");

            // Create manifest with wrong hash
            var manifest = new PackageManifest
            {
                Name = "TestPackage",
                Version = "1.0.0",
                Author = "TestAuthor", 
                Description = "Test package with corrupted hash",
                PackageHash = "INTENTIONALLY_WRONG_HASH_VALUE_12345"
            };

            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(tempDir, "manifest.json"), manifestJson);

            // Create ZIP package
            var packagePath = Path.Combine(Path.GetTempPath(), $"TestPackage_Corrupted_{Guid.NewGuid()}.stridepackage");
            ZipFile.CreateFromDirectory(tempDir, packagePath);

            Directory.Delete(tempDir, true);
            return packagePath;
        }

        private async Task<string> CreateTestPackageWithoutHash()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            // Create test content
            var assetsDir = Path.Combine(tempDir, "Assets");
            Directory.CreateDirectory(assetsDir);
            await File.WriteAllTextAsync(Path.Combine(assetsDir, "test.txt"), "Test content for legacy package");

            // Create manifest without hash (legacy package)
            var manifest = new PackageManifest
            {
                Name = "TestPackage",
                Version = "1.0.0",
                Author = "TestAuthor",
                Description = "Legacy test package without hash"
                // PackageHash is intentionally omitted
            };

            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(tempDir, "manifest.json"), manifestJson);

            // Create ZIP package
            var packagePath = Path.Combine(Path.GetTempPath(), $"TestPackage_Legacy_{Guid.NewGuid()}.stridepackage");
            ZipFile.CreateFromDirectory(tempDir, packagePath);

            Directory.Delete(tempDir, true);
            return packagePath;
        }

        private string CreateTestProjectDirectory()
        {
            var testProjectPath = Path.Combine(Path.GetTempPath(), $"TestProject_{Guid.NewGuid()}");
            Directory.CreateDirectory(testProjectPath);
            Directory.CreateDirectory(Path.Combine(testProjectPath, "Assets"));
            return testProjectPath;
        }

        private void CleanupTestFiles(string packagePath, string projectPath)
        {
            try
            {
                if (File.Exists(packagePath))
                    File.Delete(packagePath);
                if (Directory.Exists(projectPath))
                    Directory.Delete(projectPath, true);
            }
            catch (Exception)
            {
                // Ignore cleanup errors
            }
        }

    }
}