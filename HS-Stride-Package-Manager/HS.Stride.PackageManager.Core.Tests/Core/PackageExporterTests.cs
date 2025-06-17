// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using NUnit.Framework;
using FluentAssertions;
using Moq;

namespace HS.Stride.Packer.Core.Tests
{
    [TestFixture]
    public class PackageExporterTests
    {
        private PackageExporter _packageExporter;
        private Mock<ResourcePathValidator> _mockResourceValidator;
        private Mock<NamespaceScanner> _mockNamespaceScanner;
        private ExportSettings _exportSettings;

        [SetUp]
        public void Setup()
        {
            _mockResourceValidator = new Mock<ResourcePathValidator>();
            _mockNamespaceScanner = new Mock<NamespaceScanner>();
            _packageExporter = new PackageExporter(_mockResourceValidator.Object, _mockNamespaceScanner.Object);

            _exportSettings = new ExportSettings
            {
                LibraryPath = @"C:\TestLibrary",
                OutputPath = @"C:\Output\test_package.stridepackage",
                Manifest = new PackageManifest
                {
                    Name = "TestPackage",
                    Version = "1.0.0",
                    Author = "Test Author",
                    Description = "Test Description"
                }
            };
        }

        [Test]
        public void Constructor_ValidDependencies_ReturnPackageExporter()
        {
            _packageExporter.Should().NotBeNull();
        }

        [Test]
        public void ValidateSettings_EmptyPackageName_ReturnValidationResultWithErrors()
        {
            _exportSettings.Manifest.Name = "";
            
            var result = _packageExporter.ValidateSettings(_exportSettings);
            
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain("Package name is required");
        }

        [Test]
        public void ValidateSettings_NullPackageName_ReturnValidationResultWithErrors()
        {
            _exportSettings.Manifest.Name = null;
            
            var result = _packageExporter.ValidateSettings(_exportSettings);
            
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain("Package name is required");
        }

        [Test]
        public void ValidateSettings_EmptyVersion_ReturnValidationResultWithErrors()
        {
            _exportSettings.Manifest.Version = "";
            
            var result = _packageExporter.ValidateSettings(_exportSettings);
            
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain("Package version is required");
        }

        [Test]
        public void ValidateSettings_NullVersion_ReturnValidationResultWithErrors()
        {
            _exportSettings.Manifest.Version = null;
            
            var result = _packageExporter.ValidateSettings(_exportSettings);
            
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain("Package version is required");
        }

        [Test]
        public void ValidateSettings_NonExistingLibraryPath_ReturnValidationResultWithErrors()
        {
            var nonExistingPath = @"C:\NonExisting\Path";
            _exportSettings.LibraryPath = nonExistingPath;
            
            var result = _packageExporter.ValidateSettings(_exportSettings);
            
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain($"Library path does not exist: {nonExistingPath}");
        }

        [Test]
        public void ValidateSettings_EmptyGeneratedOutputPath_ReturnValidationResultWithErrors()
        {
            var settingsWithBadPath = new ExportSettings
            {
                LibraryPath = "",  // This will cause Path.GetDirectoryName to return null
                OutputPath = null, // No explicit output path
                Manifest = new PackageManifest
                {
                    Name = "TestPackage",
                    Version = "1.0.0"
                }
            };
            
            var result = _packageExporter.ValidateSettings(settingsWithBadPath);
            
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain("Library path does not exist: ");
            result.Errors.Should().Contain("Cannot generate valid output path for package");
        }

        [Test]
        public async Task ExportPackageAsync_InvalidSettings_ThrowInvalidOperationException()
        {
            _exportSettings.Manifest.Name = "";
            
            Func<Task> act = async () => await _packageExporter.ExportPackageAsync(_exportSettings);
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Invalid export settings: Package name is required, Library path does not exist: C:\\TestLibrary");
        }


    }
}