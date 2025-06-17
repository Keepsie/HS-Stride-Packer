// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using NUnit.Framework;
using FluentAssertions;

namespace HS.Stride.Packer.Core.Tests
{
    [TestFixture]
    public class ResourcePathValidatorTests
    {
        private ResourcePathValidator _resourcePathValidator;

        [SetUp]
        public void Setup()
        {
            _resourcePathValidator = new ResourcePathValidator();
        }

        [Test]
        public void Constructor_CreateInstance_ReturnResourcePathValidator()
        {
            _resourcePathValidator.Should().NotBeNull();
        }

        [Test]
        public void ValidateProject_NonExistentProjectPath_ReturnValidationResultWithNoIssues()
        {
            var nonExistentPath = @"C:\NonExistent\Path";
            
            var result = _resourcePathValidator.ValidateProject(nonExistentPath);
            
            result.Should().NotBeNull();
            result.ExternalResources.Should().BeEmpty();
            result.MissingResources.Should().BeEmpty();
        }

        [Test]
        public void ValidateProject_EmptyProjectPath_ReturnValidationResultWithNoIssues()
        {
            var result = _resourcePathValidator.ValidateProject("");
            
            result.Should().NotBeNull();
            result.ExternalResources.Should().BeEmpty();
            result.MissingResources.Should().BeEmpty();
        }

        [Test]
        public void ValidateProject_NullProjectPath_ReturnValidationResultWithNoIssues()
        {
            var result = _resourcePathValidator.ValidateProject(null);
            
            result.Should().NotBeNull();
            result.ExternalResources.Should().BeEmpty();
            result.MissingResources.Should().BeEmpty();
        }

    }
}