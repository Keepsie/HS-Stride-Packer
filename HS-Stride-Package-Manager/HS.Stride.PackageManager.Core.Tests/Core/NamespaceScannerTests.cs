// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using NUnit.Framework;
using FluentAssertions;

namespace HS.Stride.Packer.Core.Tests
{
    [TestFixture]
    public class NamespaceScannerTests
    {
        private NamespaceScanner _namespaceScanner;

        [SetUp]
        public void Setup()
        {
            _namespaceScanner = new NamespaceScanner();
        }

        [Test]
        public void Constructor_CreateInstance_ReturnNamespaceScanner()
        {
            _namespaceScanner.Should().NotBeNull();
        }

        [Test]
        public void ScanDirectory_NonExistentDirectory_ReturnEmptyNamespaceList()
        {
            var nonExistentPath = @"C:\NonExistent\Path";
            
            var result = _namespaceScanner.ScanDirectory(nonExistentPath);
            
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Test]
        public void ScanDirectory_EmptyPath_ReturnEmptyNamespaceList()
        {
            var result = _namespaceScanner.ScanDirectory("");
            
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Test]
        public void ScanDirectory_NullPath_ReturnEmptyNamespaceList()
        {
            var result = _namespaceScanner.ScanDirectory(null);
            
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Test]
        public void ScanFile_NonExistentFile_ReturnEmptyNamespaceList()
        {
            var nonExistentFile = @"C:\NonExistent\file.sdprefab";
            
            var result = _namespaceScanner.ScanFile(nonExistentFile);
            
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Test]
        public void ScanFile_EmptyFilePath_ReturnEmptyNamespaceList()
        {
            var result = _namespaceScanner.ScanFile("");
            
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Test]
        public void ScanFile_NullFilePath_ReturnEmptyNamespaceList()
        {
            var result = _namespaceScanner.ScanFile(null);
            
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

    }
}