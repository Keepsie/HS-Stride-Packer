// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using NUnit.Framework;
using FluentAssertions;

namespace HS.Stride.Packer.Core.Tests
{
    [TestFixture]
    public class ValidationResultTests
    {
        private ValidationResult _validationResult;

        [SetUp]
        public void Setup()
        {
            _validationResult = new ValidationResult();
        }

        [Test]
        public void Constructor_CreateInstance_ReturnValidationResult()
        {
            _validationResult.Should().NotBeNull();
            _validationResult.ExternalResources.Should().NotBeNull().And.BeEmpty();
            _validationResult.MissingResources.Should().NotBeNull().And.BeEmpty();
            _validationResult.Errors.Should().NotBeNull().And.BeEmpty();
            _validationResult.Warnings.Should().NotBeNull().And.BeEmpty();
        }

        [Test]
        public void IsValid_NoIssues_ReturnTrue()
        {
            _validationResult.IsValid.Should().BeTrue();
        }

        [Test]
        public void IsValid_WithExternalResources_ReturnFalse()
        {
            _validationResult.ExternalResources.Add(new ExternalResourceIssue
            {
                AssetFile = "test.sdpage",
                ResourcePath = "../../external.png"
            });

            _validationResult.IsValid.Should().BeFalse();
        }

        [Test]
        public void IsValid_WithMissingResources_ReturnFalse()
        {
            _validationResult.MissingResources.Add(new MissingResourceIssue
            {
                AssetFile = "test.sdpage",
                ResourcePath = "missing.png"
            });

            _validationResult.IsValid.Should().BeFalse();
        }

        [Test]
        public void IsValid_WithErrors_ReturnFalse()
        {
            _validationResult.Errors.Add("Critical error occurred");

            _validationResult.IsValid.Should().BeFalse();
        }

        [Test]
        public void IsValid_WithWarningsOnly_ReturnTrue()
        {
            _validationResult.Warnings.Add("Warning message");

            _validationResult.IsValid.Should().BeTrue();
        }

        [Test]
        public void HasCriticalIssues_NoIssues_ReturnFalse()
        {
            _validationResult.HasCriticalIssues.Should().BeFalse();
        }

        [Test]
        public void HasCriticalIssues_WithExternalResources_ReturnTrue()
        {
            _validationResult.ExternalResources.Add(new ExternalResourceIssue
            {
                AssetFile = "test.sdpage",
                ResourcePath = "../../external.png"
            });

            _validationResult.HasCriticalIssues.Should().BeTrue();
        }

        [Test]
        public void HasCriticalIssues_WithMissingResources_ReturnTrue()
        {
            _validationResult.MissingResources.Add(new MissingResourceIssue
            {
                AssetFile = "test.sdpage",
                ResourcePath = "missing.png"
            });

            _validationResult.HasCriticalIssues.Should().BeTrue();
        }

        [Test]
        public void HasCriticalIssues_WithErrors_ReturnTrue()
        {
            _validationResult.Errors.Add("Critical error occurred");

            _validationResult.HasCriticalIssues.Should().BeTrue();
        }

        [Test]
        public void HasCriticalIssues_WithWarningsOnly_ReturnFalse()
        {
            _validationResult.Warnings.Add("Warning message");

            _validationResult.HasCriticalIssues.Should().BeFalse();
        }

        [Test]
        public void GetReport_NoIssues_ReturnEmptyString()
        {
            var report = _validationResult.GetReport();

            report.Should().Be("");
        }

        [Test]
        public void GetReport_WithExternalResources_ReturnFormattedReport()
        {
            _validationResult.ExternalResources.Add(new ExternalResourceIssue
            {
                AssetFile = @"C:\Project\UI\MainMenu.sdpage",
                ResourcePath = "../../Resources/logo.png"
            });

            var report = _validationResult.GetReport();

            report.Should().Contain("EXTERNAL RESOURCES DETECTED:");
            report.Should().Contain("These files are outside your project directory");
            report.Should().Contain("MainMenu.sdpage: ../../Resources/logo.png");
            report.Should().Contain("SOLUTION: Copy these files into your project's Resources folder");
        }

        [Test]
        public void GetReport_WithMissingResources_ReturnFormattedReport()
        {
            _validationResult.MissingResources.Add(new MissingResourceIssue
            {
                AssetFile = @"C:\Project\Scenes\Level1.sdscene",
                ResourcePath = "missing_texture.png"
            });

            var report = _validationResult.GetReport();

            report.Should().Contain("MISSING RESOURCES:");
            report.Should().Contain("Level1.sdscene: missing_texture.png");
        }

        [Test]
        public void GetReport_WithErrors_ReturnFormattedReport()
        {
            _validationResult.Errors.Add("Package name is required");
            _validationResult.Errors.Add("Invalid version format");

            var report = _validationResult.GetReport();

            report.Should().Contain("CRITICAL ERRORS:");
            report.Should().Contain("Package name is required");
            report.Should().Contain("Invalid version format");
        }

        [Test]
        public void GetReport_WithWarnings_ReturnFormattedReport()
        {
            _validationResult.Warnings.Add("Deprecated feature used");
            _validationResult.Warnings.Add("Performance warning");

            var report = _validationResult.GetReport();

            report.Should().Contain("WARNINGS:");
            report.Should().Contain("Deprecated feature used");
            report.Should().Contain("Performance warning");
        }

        [Test]
        public void GetReport_WithAllIssueTypes_ReturnCompleteReport()
        {
            _validationResult.ExternalResources.Add(new ExternalResourceIssue
            {
                AssetFile = "test.sdpage",
                ResourcePath = "../../external.png"
            });
            _validationResult.MissingResources.Add(new MissingResourceIssue
            {
                AssetFile = "test.sdscene",
                ResourcePath = "missing.png"
            });
            _validationResult.Errors.Add("Critical error");
            _validationResult.Warnings.Add("Warning message");

            var report = _validationResult.GetReport();

            report.Should().Contain("EXTERNAL RESOURCES DETECTED:");
            report.Should().Contain("MISSING RESOURCES:");
            report.Should().Contain("CRITICAL ERRORS:");
            report.Should().Contain("WARNINGS:");
        }

        [Test]
        public void GetReport_MultipleExternalResources_ReturnAllListed()
        {
            _validationResult.ExternalResources.Add(new ExternalResourceIssue
            {
                AssetFile = "page1.sdpage",
                ResourcePath = "../../external1.png"
            });
            _validationResult.ExternalResources.Add(new ExternalResourceIssue
            {
                AssetFile = "page2.sdpage",
                ResourcePath = "../../external2.png"
            });

            var report = _validationResult.GetReport();

            report.Should().Contain("page1.sdpage: ../../external1.png");
            report.Should().Contain("page2.sdpage: ../../external2.png");
        }

        [Test]
        public void ExternalResourceIssue_DefaultConstructor_ReturnEmptyProperties()
        {
            var issue = new ExternalResourceIssue();

            issue.Should().NotBeNull();
            issue.AssetFile.Should().Be(string.Empty);
            issue.ResourcePath.Should().Be(string.Empty);
        }

        [Test]
        public void ExternalResourceIssue_SetProperties_ReturnCorrectValues()
        {
            var issue = new ExternalResourceIssue
            {
                AssetFile = "test.sdpage",
                ResourcePath = "../../external.png"
            };

            issue.AssetFile.Should().Be("test.sdpage");
            issue.ResourcePath.Should().Be("../../external.png");
        }

        [Test]
        public void MissingResourceIssue_DefaultConstructor_ReturnEmptyProperties()
        {
            var issue = new MissingResourceIssue();

            issue.Should().NotBeNull();
            issue.AssetFile.Should().Be(string.Empty);
            issue.ResourcePath.Should().Be(string.Empty);
        }

        [Test]
        public void MissingResourceIssue_SetProperties_ReturnCorrectValues()
        {
            var issue = new MissingResourceIssue
            {
                AssetFile = "scene.sdscene",
                ResourcePath = "missing.png"
            };

            issue.AssetFile.Should().Be("scene.sdscene");
            issue.ResourcePath.Should().Be("missing.png");
        }
    }
}