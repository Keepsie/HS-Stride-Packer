using HS.Stride.PackageManager.Utilities;
using NUnit.Framework;
using FluentAssertions;

namespace HS.Stride.PackageManager.Core.Tests.Utilities
{
    [TestFixture]
    public class FileHelperTests
    {
        [Test]
        public void SaveFile_NullContent_ReturnFalse()
        {
            var result = FileHelper.SaveFile(null, @"C:\NonExistent\test.txt");
            result.Should().BeFalse();
        }

        [Test]
        public void SaveFile_EmptyContent_ReturnFalse()
        {
            var result = FileHelper.SaveFile("", @"C:\NonExistent\test.txt");
            result.Should().BeFalse();
        }

        [Test]
        public void SaveFile_NullFilePath_ReturnFalse()
        {
            var result = FileHelper.SaveFile("test content", null);
            result.Should().BeFalse();
        }

        [Test]
        public void SaveFile_EmptyFilePath_ReturnFalse()
        {
            var result = FileHelper.SaveFile("test content", "");
            result.Should().BeFalse();
        }

        [Test]
        public void SaveFile_InvalidPath_ReturnFalse()
        {
            var result = FileHelper.SaveFile("test content", @"C:\<<invalid>>path\test.txt");
            result.Should().BeFalse();
        }

        [Test]
        public void LoadFile_NullFilePath_ReturnEmptyString()
        {
            var result = FileHelper.LoadFile(null);
            result.Should().Be(string.Empty);
        }

        [Test]
        public void LoadFile_EmptyFilePath_ReturnEmptyString()
        {
            var result = FileHelper.LoadFile("");
            result.Should().Be(string.Empty);
        }

        [Test]
        public void LoadFile_NonExistentFile_ReturnEmptyString()
        {
            var result = FileHelper.LoadFile(@"C:\NonExistent\test.txt");
            result.Should().Be(string.Empty);
        }

        [Test]
        public void LoadFile_InvalidPath_ReturnEmptyString()
        {
            var result = FileHelper.LoadFile(@"C:\<<invalid>>path\test.txt");
            result.Should().Be(string.Empty);
        }

        [Test]
        public void DeleteFile_NullFilePath_ReturnFalse()
        {
            var result = FileHelper.DeleteFile(null);
            result.Should().BeFalse();
        }

        [Test]
        public void DeleteFile_EmptyFilePath_ReturnFalse()
        {
            var result = FileHelper.DeleteFile("");
            result.Should().BeFalse();
        }

        [Test]
        public void DeleteFile_NonExistentFile_ReturnFalse()
        {
            var result = FileHelper.DeleteFile(@"C:\NonExistent\test.txt");
            result.Should().BeFalse();
        }

        [Test]
        public void DeleteFile_InvalidPath_ReturnFalse()
        {
            var result = FileHelper.DeleteFile(@"C:\<<invalid>>path\test.txt");
            result.Should().BeFalse();
        }

        [Test]
        public void GetFilesInDirectory_NullPath_ReturnEmptyList()
        {
            var result = FileHelper.GetFilesInDirectory(null);
            result.Should().NotBeNull().And.BeEmpty();
        }

        [Test]
        public void GetFilesInDirectory_EmptyPath_ReturnEmptyList()
        {
            var result = FileHelper.GetFilesInDirectory("");
            result.Should().NotBeNull().And.BeEmpty();
        }

        [Test]
        public void GetFilesInDirectory_NonExistentDirectory_ReturnEmptyList()
        {
            var result = FileHelper.GetFilesInDirectory(@"C:\NonExistent\Directory");
            result.Should().NotBeNull().And.BeEmpty();
        }

        [Test]
        public void GetFilesInDirectory_InvalidPath_ReturnEmptyList()
        {
            var result = FileHelper.GetFilesInDirectory(@"C:\<<invalid>>path");
            result.Should().NotBeNull().And.BeEmpty();
        }

        [Test]
        public void GetFilesInDirectory_WithPattern_ReturnEmptyList()
        {
            var result = FileHelper.GetFilesInDirectory(@"C:\NonExistent", "*.txt");
            result.Should().NotBeNull().And.BeEmpty();
        }

        [Test]
        public void GetFilesInDirectory_WithSearchOption_ReturnEmptyList()
        {
            var result = FileHelper.GetFilesInDirectory(@"C:\NonExistent", "*", SearchOption.AllDirectories);
            result.Should().NotBeNull().And.BeEmpty();
        }

        [Test]
        public void EnsureDirectoryExists_NullPath_ReturnFalse()
        {
            var result = FileHelper.EnsureDirectoryExists(null);
            result.Should().BeFalse();
        }

        [Test]
        public void EnsureDirectoryExists_EmptyPath_ReturnFalse()
        {
            var result = FileHelper.EnsureDirectoryExists("");
            result.Should().BeFalse();
        }

        [Test]
        public void EnsureDirectoryExists_InvalidPath_ReturnFalse()
        {
            var result = FileHelper.EnsureDirectoryExists(@"C:\<<invalid>>path");
            result.Should().BeFalse();
        }

        [Test]
        public void MoveFile_NullSourcePath_ReturnFalse()
        {
            var result = FileHelper.MoveFile(null, @"C:\dest\test.txt");
            result.Should().BeFalse();
        }

        [Test]
        public void MoveFile_EmptySourcePath_ReturnFalse()
        {
            var result = FileHelper.MoveFile("", @"C:\dest\test.txt");
            result.Should().BeFalse();
        }

        [Test]
        public void MoveFile_NullDestinationPath_ReturnFalse()
        {
            var result = FileHelper.MoveFile(@"C:\source\test.txt", null);
            result.Should().BeFalse();
        }

        [Test]
        public void MoveFile_EmptyDestinationPath_ReturnFalse()
        {
            var result = FileHelper.MoveFile(@"C:\source\test.txt", "");
            result.Should().BeFalse();
        }

        [Test]
        public void MoveFile_NonExistentSourceFile_ReturnFalse()
        {
            var result = FileHelper.MoveFile(@"C:\NonExistent\test.txt", @"C:\dest\test.txt");
            result.Should().BeFalse();
        }

        [Test]
        public void MoveFile_InvalidSourcePath_ReturnFalse()
        {
            var result = FileHelper.MoveFile(@"C:\<<invalid>>source\test.txt", @"C:\dest\test.txt");
            result.Should().BeFalse();
        }

        [Test]
        public void MoveFile_InvalidDestinationPath_ReturnFalse()
        {
            var result = FileHelper.MoveFile(@"C:\source\test.txt", @"C:\<<invalid>>dest\test.txt");
            result.Should().BeFalse();
        }

        [Test]
        public void GetFileLastModified_NullFilePath_ReturnNull()
        {
            var result = FileHelper.GetFileLastModified(null);
            result.Should().BeNull();
        }

        [Test]
        public void GetFileLastModified_EmptyFilePath_ReturnNull()
        {
            var result = FileHelper.GetFileLastModified("");
            result.Should().BeNull();
        }

        [Test]
        public void GetFileLastModified_NonExistentFile_ReturnNull()
        {
            var result = FileHelper.GetFileLastModified(@"C:\NonExistent\test.txt");
            result.Should().BeNull();
        }

        [Test]
        public void GetFileLastModified_InvalidPath_ReturnNull()
        {
            var result = FileHelper.GetFileLastModified(@"C:\<<invalid>>path\test.txt");
            result.Should().BeNull();
        }

        [Test]
        public void CopyDirectory_NullSourceDir_ReturnFalse()
        {
            var result = FileHelper.CopyDirectory(null, @"C:\dest");
            result.Should().BeFalse();
        }

        [Test]
        public void CopyDirectory_EmptySourceDir_ReturnFalse()
        {
            var result = FileHelper.CopyDirectory("", @"C:\dest");
            result.Should().BeFalse();
        }

        [Test]
        public void CopyDirectory_NullDestinationDir_ReturnFalse()
        {
            var result = FileHelper.CopyDirectory(@"C:\source", null);
            result.Should().BeFalse();
        }

        [Test]
        public void CopyDirectory_EmptyDestinationDir_ReturnFalse()
        {
            var result = FileHelper.CopyDirectory(@"C:\source", "");
            result.Should().BeFalse();
        }

        [Test]
        public void CopyDirectory_NonExistentSourceDir_ReturnFalse()
        {
            var result = FileHelper.CopyDirectory(@"C:\NonExistent\Source", @"C:\dest");
            result.Should().BeFalse();
        }

        [Test]
        public void CopyDirectory_InvalidSourcePath_ReturnFalse()
        {
            var result = FileHelper.CopyDirectory(@"C:\<<invalid>>source", @"C:\dest");
            result.Should().BeFalse();
        }

        [Test]
        public void CopyDirectory_InvalidDestinationPath_ReturnFalse()
        {
            var result = FileHelper.CopyDirectory(@"C:\source", @"C:\<<invalid>>dest");
            result.Should().BeFalse();
        }

        [Test]
        public void GenerateUniqueFileName_NullBaseName_ReturnTimestampedFileName()
        {
            var result = FileHelper.GenerateUniqueFileName(null);
            result.Should().NotBeNull();
            result.Should().StartWith("_"); // null becomes empty, so starts with underscore
            result.Should().EndWith(".txt"); // No extension specified, but timestamp format should be present
        }

        [Test]
        public void GenerateUniqueFileName_EmptyBaseName_ReturnTimestampedFileName()
        {
            var result = FileHelper.GenerateUniqueFileName("");
            result.Should().NotBeNull();
            result.Should().StartWith("_"); // Empty becomes underscore prefix
        }

        [Test]
        public void GenerateUniqueFileName_ValidBaseName_ReturnTimestampedFileName()
        {
            var result = FileHelper.GenerateUniqueFileName("test");
            result.Should().NotBeNull();
            result.Should().StartWith("test_");
            result.Should().MatchRegex(@"test_\d{8}_\d{6}"); // Should match timestamp format
        }

        [Test]
        public void GenerateUniqueFileName_WithExtension_ReturnTimestampedFileNameWithExtension()
        {
            var result = FileHelper.GenerateUniqueFileName("test", ".txt");
            result.Should().NotBeNull();
            result.Should().StartWith("test_");
            result.Should().EndWith(".txt");
        }

        [Test]
        public void GenerateUniqueFileName_WithDirectory_ReturnFullPath()
        {
            var result = FileHelper.GenerateUniqueFileName("test", ".txt", @"C:\temp");
            result.Should().NotBeNull();
            result.Should().StartWith(@"C:\temp\test_");
            result.Should().EndWith(".txt");
        }

        [Test]
        public void GenerateUniqueFileName_WithEmptyDirectory_ReturnFileNameOnly()
        {
            var result = FileHelper.GenerateUniqueFileName("test", ".txt", "");
            result.Should().NotBeNull();
            result.Should().StartWith("test_");
            result.Should().EndWith(".txt");
            result.Should().NotContain(@"\"); // Should not contain path separators
        }

        [Test]
        public void GenerateUniqueFileName_WithNullDirectory_ReturnFileNameOnly()
        {
            var result = FileHelper.GenerateUniqueFileName("test", ".txt", null);
            result.Should().NotBeNull();
            result.Should().StartWith("test_");
            result.Should().EndWith(".txt");
            result.Should().NotContain(@"\"); // Should not contain path separators
        }

        [Test]
        public void GenerateUniqueFileName_MultipleCallsSameSecond_ReturnSameTimestamp()
        {
            var result1 = FileHelper.GenerateUniqueFileName("test");
            var result2 = FileHelper.GenerateUniqueFileName("test");
            
            // Both should have same timestamp if called in same second
            var timestamp1 = result1.Substring(5); // Remove "test_" prefix
            var timestamp2 = result2.Substring(5); // Remove "test_" prefix
            
            timestamp1.Should().Be(timestamp2);
        }
    }
}