// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using NUnit.Framework;
using FluentAssertions;
using System.IO.Compression;
using System.Text.Json;

namespace HS.Stride.Packer.Core.Tests
{
    [TestFixture]
    public class StridePackageManagerTests
    {
        private StridePackageManager _stridePackageManager;
        private ExportSettings _exportSettings;

        [SetUp]
        public void Setup()
        {
            _exportSettings = new ExportSettings
            {
                LibraryPath = @"C:\TestLibrary",
                Manifest = new PackageManifest
                {
                    Name = "TestPackage",
                    Version = "1.0.0",
                    Author = "Test Author",
                    Description = "Test Description"
                }
            };

            _stridePackageManager = new StridePackageManager(_exportSettings);
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up cache directory after each test
            try
            {
                var cacheDir = _stridePackageManager.GetCacheDirectory();
                if (Directory.Exists(cacheDir))
                {
                    Directory.Delete(cacheDir, true);
                }
            }
            catch (Exception)
            {
                // Ignore cleanup errors
            }
        }

        [Test]
        public void Constructor_ValidExportSettings_ReturnStridePackageManager()
        {
            _stridePackageManager.Should().NotBeNull();
        }

        [Test]
        public void Constructor_NullExportSettings_ThrowNullReferenceException()
        {
            Action act = () => new StridePackageManager(null);
            act.Should().Throw<NullReferenceException>()
                .WithMessage("null export settings");
        }

        [Test]
        public async Task ValidateForExportAsync_NonExistingPath_ReturnValidationResultWithErrors()
        {
            var nonExistingPath = @"C:\NonExisting\Path";
            var result = await _stridePackageManager.ValidateForExportAsync(nonExistingPath);
            
            result.Should().NotBeNull();
            result.Errors.Should().Contain($"Library path does not exist: {nonExistingPath}");
        }

        [Test]
        public async Task ValidateForImportAsync_NonExistingPackage_ReturnValidationResultWithErrors()
        {
            var nonExistingPackage = @"C:\NonExisting\package.stridepackage";
            var validProjectPath = @"C:\ValidProject";
            
            var result = await _stridePackageManager.ValidateForImportAsync(nonExistingPackage, validProjectPath);
            
            result.Should().NotBeNull();
            result.Errors.Should().Contain($"Package file does not exist: {nonExistingPackage}");
        }

        [Test]
        public async Task ValidateForImportAsync_NonExistingTargetPath_ReturnValidationResultWithErrors()
        {
            var validPackage = @"C:\ValidPackage\package.stridepackage";
            var nonExistingTarget = @"C:\NonExisting\Target";
            
            var result = await _stridePackageManager.ValidateForImportAsync(validPackage, nonExistingTarget);
            
            result.Should().NotBeNull();
            result.Errors.Should().Contain($"Package file does not exist: {validPackage}");
            result.Errors.Should().Contain($"Target project path does not exist: {nonExistingTarget}");
        }

        [Test]
        public async Task CreatePackageAsync_EmptyLibraryPath_ThrowArgumentException()
        {
            _exportSettings.LibraryPath = "";
            var manager = new StridePackageManager(_exportSettings);
            
            Func<Task> act = async () => await manager.CreatePackageAsync();
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("Library path cannot be empty*");
        }

        [Test]
        public async Task DownloadPackageAsync_EmptyUrl_ThrowArgumentException()
        {
            Func<Task> act = async () => await _stridePackageManager.DownloadPackageAsync("");
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("URL cannot be empty*");
        }

        [Test]
        public async Task DownloadPackageAsync_NullUrl_ThrowArgumentException()
        {
            Func<Task> act = async () => await _stridePackageManager.DownloadPackageAsync(null);
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("URL cannot be empty*");
        }

        [Test]
        public void SetRegistryUrl_ValidUrl_NoExceptionThrown()
        {
            var testUrl = "https://test.registry.com";
            
            Action act = () => _stridePackageManager.SetRegistryUrl(testUrl);
            act.Should().NotThrow();
        }

        [Test]
        public void ValidateImportSettings_ValidSettings_ReturnValidationResult()
        {
            var importSettings = new ImportSettings
            {
                PackagePath = @"C:\TestPackage\test.stridepackage",
                TargetProjectPath = @"C:\TestProject"
            };
            
            var result = _stridePackageManager.ValidateImportSettings(importSettings);
            
            result.Should().NotBeNull();
            result.Should().BeOfType<ValidationResult>();
        }

        // Cache Management Tests
        [Test]
        public void GetCacheDirectory_ReturnValidPath()
        {
            var result = _stridePackageManager.GetCacheDirectory();
            
            result.Should().NotBeNullOrEmpty();
            result.Should().EndWith("HSPacker");
        }

        [Test]
        public void IsPackageCached_NonExistentPackage_ReturnFalse()
        {
            var package = new PackageMetadata
            {
                Name = "NonExistentPackage",
                Version = "1.0.0"
            };

            var result = _stridePackageManager.IsPackageCached(package);

            result.Should().BeFalse();
        }

        [Test]
        public void GetCachedPackagePath_NonExistentPackage_ReturnNull()
        {
            var package = new PackageMetadata
            {
                Name = "NonExistentPackage",
                Version = "1.0.0"
            };

            var result = _stridePackageManager.GetCachedPackagePath(package);

            result.Should().BeNull();
        }

        [Test]
        public async Task InstallCachedPackageAsync_PackageNotCached_ThrowInvalidOperationException()
        {
            var package = new PackageMetadata
            {
                Name = "NotCachedPackage",
                Version = "1.0.0"
            };
            var targetProjectPath = @"C:\TestProject";

            Func<Task> act = async () => await _stridePackageManager.InstallCachedPackageAsync(package, targetProjectPath);
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Package 'NotCachedPackage' is not cached. Download it first.");
        }

        [Test]
        public async Task DownloadPackageToCacheAsync_EmptyDownloadUrl_ThrowArgumentException()
        {
            var package = new PackageMetadata
            {
                Name = "TestPackage",
                DownloadUrl = ""
            };

            Func<Task> act = async () => await _stridePackageManager.DownloadPackageToCacheAsync(package);
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("Package download URL cannot be empty");
        }

        [Test]
        public async Task ClearPackageCacheAsync_NonExistentPackage_ReturnFalse()
        {
            var package = new PackageMetadata
            {
                Name = "NonExistentPackage",
                Version = "1.0.0"
            };

            var result = await _stridePackageManager.ClearPackageCacheAsync(package);

            result.Should().BeFalse();
        }

        [Test]
        public async Task GetCachedPackagesAsync_EmptyCache_ReturnEmptyList()
        {
            // Clean cache first to ensure empty state
            var cacheDir = _stridePackageManager.GetCacheDirectory();
            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, true);
            }

            var result = await _stridePackageManager.GetCachedPackagesAsync();

            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Test]
        public async Task VerifyPackageIntegrityAsync_NonExistentPackage_ThrowFileNotFoundException()
        {
            var nonExistentPackage = @"C:\NonExistent\package.stridepackage";

            Func<Task> act = async () => await _stridePackageManager.VerifyPackageIntegrityAsync(nonExistentPackage);
            
            await act.Should().ThrowAsync<FileNotFoundException>()
                .WithMessage($"Package file not found: {nonExistentPackage}");
        }

        [Test]
        public async Task VerifyPackageIntegrityAsync_PackageWithoutManifest_ReturnFalse()
        {
            // Create ZIP file without manifest.json
            var packagePath = await CreatePackageWithoutManifest();
            
            try
            {
                var result = await _stridePackageManager.VerifyPackageIntegrityAsync(packagePath);
                result.Should().BeFalse();
            }
            finally
            {
                CleanupTestFile(packagePath);
            }
        }

        [Test]
        public async Task VerifyPackageIntegrityAsync_PackageWithoutHash_ReturnFalse()
        {
            // Create package with manifest but no hash - should return false
            var packagePath = await CreatePackageWithoutHash();
            
            try
            {
                var result = await _stridePackageManager.VerifyPackageIntegrityAsync(packagePath);
                result.Should().BeFalse();
            }
            finally
            {
                CleanupTestFile(packagePath);
            }
        }

        [Test]
        public async Task VerifyPackageIntegrityAsync_PackageWithValidHash_ReturnTrue()
        {
            // Create package with correct content and hash
            var packagePath = await CreatePackageWithValidHash();
            
            try
            {
                var result = await _stridePackageManager.VerifyPackageIntegrityAsync(packagePath);
                result.Should().BeTrue();
            }
            finally
            {
                CleanupTestFile(packagePath);
            }
        }

        [Test]
        public async Task VerifyPackageIntegrityAsync_PackageWithInvalidHash_ReturnFalse()
        {
            // Create package with corrupted content/hash
            var packagePath = await CreatePackageWithInvalidHash();
            
            try
            {
                var result = await _stridePackageManager.VerifyPackageIntegrityAsync(packagePath);
                result.Should().BeFalse();
            }
            finally
            {
                CleanupTestFile(packagePath);
            }
        }

        private async Task<string> CreatePackageWithoutManifest()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            // Create test content without manifest.json
            var assetsDir = Path.Combine(tempDir, "Assets");
            Directory.CreateDirectory(assetsDir);
            await File.WriteAllTextAsync(Path.Combine(assetsDir, "test.txt"), "Test content");

            var packagePath = Path.Combine(Path.GetTempPath(), $"NoManifest_{Guid.NewGuid()}.stridepackage");
            ZipFile.CreateFromDirectory(tempDir, packagePath);

            Directory.Delete(tempDir, true);
            return packagePath;
        }

        private async Task<string> CreatePackageWithoutHash()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            // Create test content
            var assetsDir = Path.Combine(tempDir, "Assets");
            Directory.CreateDirectory(assetsDir);
            await File.WriteAllTextAsync(Path.Combine(assetsDir, "test.txt"), "Test content for no hash");

            // Create manifest without hash
            var manifest = new PackageManifest
            {
                Name = "NoHashPackage",
                Version = "1.0.0",
                Author = "TestAuthor",
                Description = "Package without hash"
                // PackageHash intentionally omitted
            };

            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(tempDir, "manifest.json"), manifestJson);

            var packagePath = Path.Combine(Path.GetTempPath(), $"NoHash_{Guid.NewGuid()}.stridepackage");
            ZipFile.CreateFromDirectory(tempDir, packagePath);

            Directory.Delete(tempDir, true);
            return packagePath;
        }

        private async Task<string> CreatePackageWithValidHash()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            // Create test content
            var assetsDir = Path.Combine(tempDir, "Assets");
            Directory.CreateDirectory(assetsDir);
            await File.WriteAllTextAsync(Path.Combine(assetsDir, "test.txt"), "Test content for valid hash");

            // Generate correct hash
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
                Name = "ValidHashPackage",
                Version = "1.0.0",
                Author = "TestAuthor",
                Description = "Package with valid hash",
                PackageHash = hash
            };

            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(tempDir, "manifest.json"), manifestJson);

            var packagePath = Path.Combine(Path.GetTempPath(), $"ValidHash_{Guid.NewGuid()}.stridepackage");
            ZipFile.CreateFromDirectory(tempDir, packagePath);

            Directory.Delete(tempDir, true);
            return packagePath;
        }

        private async Task<string> CreatePackageWithInvalidHash()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            // Create test content
            var assetsDir = Path.Combine(tempDir, "Assets");
            Directory.CreateDirectory(assetsDir);
            await File.WriteAllTextAsync(Path.Combine(assetsDir, "test.txt"), "Test content for invalid hash");

            // Create manifest with intentionally wrong hash
            var manifest = new PackageManifest
            {
                Name = "InvalidHashPackage",
                Version = "1.0.0",
                Author = "TestAuthor",
                Description = "Package with invalid hash",
                PackageHash = "INTENTIONALLY_WRONG_HASH_12345"
            };

            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(tempDir, "manifest.json"), manifestJson);

            var packagePath = Path.Combine(Path.GetTempPath(), $"InvalidHash_{Guid.NewGuid()}.stridepackage");
            ZipFile.CreateFromDirectory(tempDir, packagePath);

            Directory.Delete(tempDir, true);
            return packagePath;
        }

        private void CleanupTestFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch (Exception)
            {
                // Ignore cleanup errors
            }
        }
    }
}