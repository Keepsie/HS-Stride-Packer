// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using NUnit.Framework;
using FluentAssertions;

namespace HS.Stride.Packer.Core.Tests
{
    [TestFixture]
    public class ProjectScannerTests
    {
        private ProjectScanner _projectScanner;

        [SetUp]
        public void Setup()
        {
            _projectScanner = new ProjectScanner();
        }

        [Test]
        public void Constructor_CreateInstance_ReturnProjectScanner()
        {
            _projectScanner.Should().NotBeNull();
        }

        [Test]
        public void ScanProject_NullProjectPath_ThrowDirectoryNotFoundException()
        {
            Action act = () => _projectScanner.ScanProject(null);
            act.Should().Throw<DirectoryNotFoundException>()
                .WithMessage("Project path does not exist: ");
        }

        [Test]
        public void ScanProject_EmptyProjectPath_ThrowDirectoryNotFoundException()
        {
            Action act = () => _projectScanner.ScanProject("");
            act.Should().Throw<DirectoryNotFoundException>()
                .WithMessage("Project path does not exist: ");
        }

        [Test]
        public void ScanProject_NonExistentProjectPath_ThrowDirectoryNotFoundException()
        {
            var nonExistentPath = @"C:\NonExistent\Path";
            
            Action act = () => _projectScanner.ScanProject(nonExistentPath);
            act.Should().Throw<DirectoryNotFoundException>()
                .WithMessage($"Project path does not exist: {nonExistentPath}");
        }

        [Test]
        public void ProjectScanResult_DefaultConstructor_ReturnEmptyCollections()
        {
            var result = new ProjectScanResult();
            
            result.Should().NotBeNull();
            result.AllItems.Should().NotBeNull().And.BeEmpty();
            result.Files.Should().NotBeNull().And.BeEmpty();
            result.Folders.Should().NotBeNull().And.BeEmpty();
            result.TotalSize.Should().Be(0);
            result.FileCount.Should().Be(0);
            result.FolderCount.Should().Be(0);
        }

        [Test]
        public void ProjectItem_DefaultConstructor_ReturnEmptyProjectItem()
        {
            var item = new ProjectItem();
            
            item.Should().NotBeNull();
            item.Name.Should().Be(string.Empty);
            item.RelativePath.Should().Be(string.Empty);
            item.FullPath.Should().Be(string.Empty);
            item.IsFile.Should().BeFalse();
            item.Size.Should().Be(0);
            item.DisplaySize.Should().Be("");
        }

        [Test]
        public void ProjectItem_FileItem_ReturnCorrectDisplaySize()
        {
            var item = new ProjectItem
            {
                Name = "test.txt",
                RelativePath = "test.txt",
                FullPath = @"C:\test\test.txt",
                IsFile = true,
                Size = 1024
            };
            
            item.DisplaySize.Should().Be("1.0 KB");
        }

        [Test]
        public void ProjectItem_FolderItem_ReturnEmptyDisplaySize()
        {
            var item = new ProjectItem
            {
                Name = "TestFolder",
                RelativePath = "TestFolder",
                FullPath = @"C:\test\TestFolder",
                IsFile = false,
                Size = 0
            };
            
            item.DisplaySize.Should().Be("");
        }

        [Test]
        public void ProjectItem_ToString_FileItem_ReturnFormattedString()
        {
            var item = new ProjectItem
            {
                Name = "test.txt",
                RelativePath = "folder/test.txt",
                FullPath = @"C:\test\folder\test.txt",
                IsFile = true,
                Size = 512
            };
            
            item.ToString().Should().Be("folder/test.txt [512 B]");
        }

        [Test]
        public void ProjectItem_ToString_FolderItem_ReturnFormattedString()
        {
            var item = new ProjectItem
            {
                Name = "TestFolder",
                RelativePath = "TestFolder",
                FullPath = @"C:\test\TestFolder",
                IsFile = false,
                Size = 0
            };
            
            item.ToString().Should().Be("TestFolder/ [Folder]");
        }

        [Test]
        public void ProjectItem_FormatFileSize_BytesRange_ReturnBytesFormat()
        {
            var item = new ProjectItem { IsFile = true, Size = 512 };
            item.DisplaySize.Should().Be("512 B");
        }

        [Test]
        public void ProjectItem_FormatFileSize_KilobytesRange_ReturnKilobytesFormat()
        {
            var item = new ProjectItem { IsFile = true, Size = 2048 };
            item.DisplaySize.Should().Be("2.0 KB");
        }

        [Test]
        public void ProjectItem_FormatFileSize_MegabytesRange_ReturnMegabytesFormat()
        {
            var item = new ProjectItem { IsFile = true, Size = 2097152 }; // 2 MB
            item.DisplaySize.Should().Be("2.0 MB");
        }

        [Test]
        public void ProjectItem_FormatFileSize_GigabytesRange_ReturnGigabytesFormat()
        {
            var item = new ProjectItem { IsFile = true, Size = 2147483648 }; // 2 GB
            item.DisplaySize.Should().Be("2.0 GB");
        }

        [Test]
        public void ProjectItem_FormatFileSize_ZeroSize_ReturnZeroBytes()
        {
            var item = new ProjectItem { IsFile = true, Size = 0 };
            item.DisplaySize.Should().Be("0 B");
        }

        [Test]
        public void ProjectScanResult_TotalSize_EmptyFiles_ReturnZero()
        {
            var result = new ProjectScanResult();
            result.TotalSize.Should().Be(0);
        }

        [Test]
        public void ProjectScanResult_TotalSize_WithFiles_ReturnSum()
        {
            var result = new ProjectScanResult();
            result.Files.Add(new ProjectItem { Size = 100, IsFile = true });
            result.Files.Add(new ProjectItem { Size = 200, IsFile = true });
            result.Files.Add(new ProjectItem { Size = 300, IsFile = true });
            
            result.TotalSize.Should().Be(600);
        }

        [Test]
        public void ProjectScanResult_FileCount_EmptyFiles_ReturnZero()
        {
            var result = new ProjectScanResult();
            result.FileCount.Should().Be(0);
        }

        [Test]
        public void ProjectScanResult_FileCount_WithFiles_ReturnCount()
        {
            var result = new ProjectScanResult();
            result.Files.Add(new ProjectItem { IsFile = true });
            result.Files.Add(new ProjectItem { IsFile = true });
            
            result.FileCount.Should().Be(2);
        }

        [Test]
        public void ProjectScanResult_FolderCount_EmptyFolders_ReturnZero()
        {
            var result = new ProjectScanResult();
            result.FolderCount.Should().Be(0);
        }

        [Test]
        public void ProjectScanResult_FolderCount_WithFolders_ReturnCount()
        {
            var result = new ProjectScanResult();
            result.Folders.Add(new ProjectItem { IsFile = false });
            result.Folders.Add(new ProjectItem { IsFile = false });
            result.Folders.Add(new ProjectItem { IsFile = false });
            
            result.FolderCount.Should().Be(3);
        }
    }
}