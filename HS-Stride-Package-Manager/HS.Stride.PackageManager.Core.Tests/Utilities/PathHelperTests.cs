using HS.Stride.PackageManager.Utilities;
using NUnit.Framework;
using FluentAssertions;

namespace HS.Stride.PackageManager.Core.Tests.Utilities
{
    [TestFixture]
    public class PathHelperTests
    {
        [Test]
        public void IsPathWithinDirectory_NullFilePath_ReturnFalse()
        {
            var result = PathHelper.IsPathWithinDirectory(null, @"C:\TestDir");
            result.Should().BeFalse();
        }

        [Test]
        public void IsPathWithinDirectory_EmptyFilePath_ReturnFalse()
        {
            var result = PathHelper.IsPathWithinDirectory("", @"C:\TestDir");
            result.Should().BeFalse();
        }

        [Test]
        public void IsPathWithinDirectory_NullDirectoryPath_ReturnFalse()
        {
            var result = PathHelper.IsPathWithinDirectory(@"C:\TestDir\file.txt", null);
            result.Should().BeFalse();
        }

        [Test]
        public void IsPathWithinDirectory_EmptyDirectoryPath_ReturnFalse()
        {
            var result = PathHelper.IsPathWithinDirectory(@"C:\TestDir\file.txt", "");
            result.Should().BeFalse();
        }

        [Test]
        public void IsPathWithinDirectory_InvalidFilePath_ReturnFalse()
        {
            var result = PathHelper.IsPathWithinDirectory(@"C:\<<invalid>>path\file.txt", @"C:\TestDir");
            result.Should().BeFalse();
        }

        [Test]
        public void IsPathWithinDirectory_InvalidDirectoryPath_ReturnFalse()
        {
            var result = PathHelper.IsPathWithinDirectory(@"C:\TestDir\file.txt", @"C:\<<invalid>>dir");
            result.Should().BeFalse();
        }

        [Test]
        public void IsPathWithinDirectory_FileNotWithinDirectory_ReturnFalse()
        {
            var result = PathHelper.IsPathWithinDirectory(@"C:\OtherDir\file.txt", @"C:\TestDir");
            result.Should().BeFalse();
        }

        [Test]
        public void IsPathWithinDirectory_FileWithinDirectory_ReturnTrue()
        {
            var result = PathHelper.IsPathWithinDirectory(@"C:\TestDir\subdir\file.txt", @"C:\TestDir");
            result.Should().BeTrue();
        }

        [Test]
        public void IsPathWithinDirectory_SamePath_ReturnTrue()
        {
            var result = PathHelper.IsPathWithinDirectory(@"C:\TestDir", @"C:\TestDir");
            result.Should().BeTrue();
        }

        [Test]
        public void NormalizePath_NullPath_ReturnOriginalPath()
        {
            var result = PathHelper.NormalizePath(null);
            result.Should().BeNull();
        }

        [Test]
        public void NormalizePath_EmptyPath_ReturnOriginalPath()
        {
            var result = PathHelper.NormalizePath("");
            result.Should().Be("");
        }

        [Test]
        public void NormalizePath_InvalidPath_ReturnOriginalPath()
        {
            var invalidPath = @"C:\<<invalid>>path";
            var result = PathHelper.NormalizePath(invalidPath);
            result.Should().Be(invalidPath);
        }

        [Test]
        public void NormalizePath_ValidPath_ReturnNormalizedPath()
        {
            var result = PathHelper.NormalizePath(@"C:\TestDir\SubDir");
            result.Should().NotBeNull();
            result.Should().Contain("/"); // Should convert backslashes to forward slashes
        }

        [Test]
        public void GetRelativePathFromTo_NullFromPath_ReturnToPath()
        {
            var toPath = @"C:\TestDir\file.txt";
            var result = PathHelper.GetRelativePathFromTo(null, toPath);
            result.Should().Be(toPath);
        }

        [Test]
        public void GetRelativePathFromTo_EmptyFromPath_ReturnToPath()
        {
            var toPath = @"C:\TestDir\file.txt";
            var result = PathHelper.GetRelativePathFromTo("", toPath);
            result.Should().Be(toPath);
        }

        [Test]
        public void GetRelativePathFromTo_NullToPath_ReturnToPath()
        {
            var result = PathHelper.GetRelativePathFromTo(@"C:\TestDir", null);
            result.Should().BeNull();
        }

        [Test]
        public void GetRelativePathFromTo_EmptyToPath_ReturnToPath()
        {
            var result = PathHelper.GetRelativePathFromTo(@"C:\TestDir", "");
            result.Should().Be("");
        }

        [Test]
        public void GetRelativePathFromTo_InvalidFromPath_ReturnToPath()
        {
            var toPath = @"C:\TestDir\file.txt";
            var result = PathHelper.GetRelativePathFromTo(@"C:\<<invalid>>path", toPath);
            result.Should().Be(toPath);
        }

        [Test]
        public void GetRelativePathFromTo_InvalidToPath_ReturnToPath()
        {
            var toPath = @"C:\<<invalid>>path";
            var result = PathHelper.GetRelativePathFromTo(@"C:\TestDir", toPath);
            result.Should().Be(toPath);
        }

        [Test]
        public void GetRelativePathFromTo_ValidPaths_ReturnNormalizedRelativePath()
        {
            var result = PathHelper.GetRelativePathFromTo(@"C:\TestDir", @"C:\TestDir\SubDir\file.txt");
            result.Should().NotBeNull();
            result.Should().Contain("/"); // Should convert backslashes to forward slashes
        }

        [Test]
        public void IsStrideProject_NullDirectoryPath_ReturnFalse()
        {
            var result = PathHelper.IsStrideProject(null);
            result.Should().BeFalse();
        }

        [Test]
        public void IsStrideProject_EmptyDirectoryPath_ReturnFalse()
        {
            var result = PathHelper.IsStrideProject("");
            result.Should().BeFalse();
        }

        [Test]
        public void IsStrideProject_InvalidDirectoryPath_ReturnFalse()
        {
            var result = PathHelper.IsStrideProject(@"C:\<<invalid>>path");
            result.Should().BeFalse();
        }

        [Test]
        public void IsStrideProject_NonExistentDirectory_ReturnFalse()
        {
            var result = PathHelper.IsStrideProject(@"C:\NonExistent\Directory");
            result.Should().BeFalse();
        }

        [Test]
        public void IsStrideAsset_NullFilePath_ReturnFalse()
        {
            var result = PathHelper.IsStrideAsset(null);
            result.Should().BeFalse();
        }

        [Test]
        public void IsStrideAsset_EmptyFilePath_ReturnFalse()
        {
            var result = PathHelper.IsStrideAsset("");
            result.Should().BeFalse();
        }

        [Test]
        public void IsStrideAsset_NonStrideExtension_ReturnFalse()
        {
            var result = PathHelper.IsStrideAsset("test.txt");
            result.Should().BeFalse();
        }

        [Test]
        public void IsStrideAsset_StrideExtension_ReturnTrue()
        {
            var result = PathHelper.IsStrideAsset("test.sdprefab");
            result.Should().BeTrue();
        }

        [Test]
        public void IsStrideAsset_StrideExtensionUpperCase_ReturnTrue()
        {
            var result = PathHelper.IsStrideAsset("test.SDPREFAB");
            result.Should().BeTrue();
        }

        [Test]
        public void GetAssetTypeFromExtension_NullFilePath_ReturnUnknown()
        {
            var result = PathHelper.GetAssetTypeFromExtension(null);
            result.Should().Be("Unknown");
        }

        [Test]
        public void GetAssetTypeFromExtension_EmptyFilePath_ReturnUnknown()
        {
            var result = PathHelper.GetAssetTypeFromExtension("");
            result.Should().Be("Unknown");
        }

        [Test]
        public void GetAssetTypeFromExtension_NoExtension_ReturnUnknown()
        {
            var result = PathHelper.GetAssetTypeFromExtension("testfile");
            result.Should().Be("Unknown");
        }

        [Test]
        public void GetAssetTypeFromExtension_UnknownExtension_ReturnUnknown()
        {
            var result = PathHelper.GetAssetTypeFromExtension("test.txt");
            result.Should().Be("Unknown");
        }

        [Test]
        public void GetAssetTypeFromExtension_PrefabExtension_ReturnPrefab()
        {
            var result = PathHelper.GetAssetTypeFromExtension("test.sdprefab");
            result.Should().Be("Prefab");
        }

        [Test]
        public void GetAssetTypeFromExtension_SceneExtension_ReturnScene()
        {
            var result = PathHelper.GetAssetTypeFromExtension("test.sdscene");
            result.Should().Be("Scene");
        }

        [Test]
        public void GetAssetTypeFromExtension_MaterialExtension_ReturnMaterial()
        {
            var result = PathHelper.GetAssetTypeFromExtension("test.sdmat");
            result.Should().Be("Material");
        }

        [Test]
        public void GetAssetTypeFromExtension_EffectExtension_ReturnEffect()
        {
            var result = PathHelper.GetAssetTypeFromExtension("test.sdfx");
            result.Should().Be("Effect");
        }

        [Test]
        public void GetAssetTypeFromExtension_TextureExtension_ReturnTexture()
        {
            var result = PathHelper.GetAssetTypeFromExtension("test.sdtex");
            result.Should().Be("Texture");
        }

        [Test]
        public void GetAssetTypeFromExtension_ModelExtension_ReturnModel()
        {
            var result = PathHelper.GetAssetTypeFromExtension("test.sdm3d");
            result.Should().Be("Model");
        }

        [Test]
        public void GetAssetTypeFromExtension_SoundExtension_ReturnSound()
        {
            var result = PathHelper.GetAssetTypeFromExtension("test.sdsnd");
            result.Should().Be("Sound");
        }

        [Test]
        public void GetAssetTypeFromExtension_UIPageExtension_ReturnUIPage()
        {
            var result = PathHelper.GetAssetTypeFromExtension("test.sdpage");
            result.Should().Be("UIPage");
        }

        [Test]
        public void GetAssetTypeFromExtension_PackageExtension_ReturnPackage()
        {
            var result = PathHelper.GetAssetTypeFromExtension("test.sdpkg");
            result.Should().Be("Package");
        }

        [Test]
        public void GetAssetTypeFromExtension_SkeletonExtension_ReturnSkeleton()
        {
            var result = PathHelper.GetAssetTypeFromExtension("test.sdskel");
            result.Should().Be("Skeleton");
        }

        [Test]
        public void GetAssetTypeFromExtension_AnimationExtension_ReturnAnimation()
        {
            var result = PathHelper.GetAssetTypeFromExtension("test.sdanim");
            result.Should().Be("Animation");
        }

        [Test]
        public void GetAssetTypeFromExtension_SpriteSheetExtension_ReturnSpriteSheet()
        {
            var result = PathHelper.GetAssetTypeFromExtension("test.sdsheet");
            result.Should().Be("SpriteSheet");
        }

        [Test]
        public void GetAssetTypeFromExtension_UpperCaseExtension_ReturnCorrectType()
        {
            var result = PathHelper.GetAssetTypeFromExtension("test.SDPREFAB");
            result.Should().Be("Prefab");
        }

        [Test]
        public void MakePackageFileName_NullPackageName_ReturnSafeFileName()
        {
            var result = PathHelper.MakePackageFileName(null, "1.0.0");
            result.Should().NotBeNull();
            result.Should().EndWith("-1_0_0.stridepackage");
        }

        [Test]
        public void MakePackageFileName_EmptyPackageName_ReturnSafeFileName()
        {
            var result = PathHelper.MakePackageFileName("", "1.0.0");
            result.Should().NotBeNull();
            result.Should().EndWith("-1_0_0.stridepackage");
        }

        [Test]
        public void MakePackageFileName_NullVersion_ReturnSafeFileName()
        {
            var result = PathHelper.MakePackageFileName("TestPackage", null);
            result.Should().NotBeNull();
            result.Should().StartWith("TestPackage-");
            result.Should().EndWith(".stridepackage");
        }

        [Test]
        public void MakePackageFileName_EmptyVersion_ReturnSafeFileName()
        {
            var result = PathHelper.MakePackageFileName("TestPackage", "");
            result.Should().NotBeNull();
            result.Should().StartWith("TestPackage-");
            result.Should().EndWith(".stridepackage");
        }

        [Test]
        public void MakePackageFileName_ValidInputs_ReturnFormattedFileName()
        {
            var result = PathHelper.MakePackageFileName("TestPackage", "1.0.0");
            result.Should().Be("TestPackage-1_0_0.stridepackage");
        }

        [Test]
        public void MakePackageFileName_InvalidCharacters_ReturnSafeFileName()
        {
            var result = PathHelper.MakePackageFileName("Test<>Package", "1.0/0");
            result.Should().NotBeNull();
            result.Should().NotContain("<");
            result.Should().NotContain(">");
            result.Should().NotContain("/");
            result.Should().EndWith(".stridepackage");
        }

        [Test]
        public void GetProjectRootFromAsset_NullAssetPath_ReturnEmptyString()
        {
            var result = PathHelper.GetProjectRootFromAsset(null);
            result.Should().Be(string.Empty);
        }

        [Test]
        public void GetProjectRootFromAsset_EmptyAssetPath_ReturnEmptyString()
        {
            var result = PathHelper.GetProjectRootFromAsset("");
            result.Should().Be(string.Empty);
        }

        [Test]
        public void GetProjectRootFromAsset_InvalidAssetPath_ReturnEmptyString()
        {
            var result = PathHelper.GetProjectRootFromAsset(@"C:\<<invalid>>path\asset.sdprefab");
            result.Should().Be(string.Empty);
        }

        [Test]
        public void GetProjectRootFromAsset_NonExistentAssetPath_ReturnEmptyString()
        {
            var result = PathHelper.GetProjectRootFromAsset(@"C:\NonExistent\Directory\asset.sdprefab");
            result.Should().Be(string.Empty);
        }
    }
}