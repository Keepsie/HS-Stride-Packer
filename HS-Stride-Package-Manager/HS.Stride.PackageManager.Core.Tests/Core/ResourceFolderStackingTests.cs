// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using NUnit.Framework;
using FluentAssertions;

namespace HS.Stride.Packer.Core.Tests
{
    /// <summary>
    /// Tests for the folder stacking prevention fix.
    /// When repacking a project that was previously imported, resources should not
    /// create nested folders like Resources/PackageName/PackageName/...
    /// </summary>
    [TestFixture]
    public class ResourceFolderStackingTests
    {
        /// <summary>
        /// Simulates the path stripping logic from CopyAndOrganizeResourcesAsync
        /// to test folder stacking prevention without needing file system access.
        /// </summary>
        private string StripResourcePath(string relativePath, string packageName)
        {
            // Strip project name and Resources folder to get clean content path
            var pathParts = relativePath.Split('/', '\\');
            if (pathParts.Length >= 3 && pathParts[1].Equals("Resources", StringComparison.OrdinalIgnoreCase))
            {
                // Skip first two parts (projectname/Resources/) and keep the rest
                relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), pathParts.Skip(2));

                // Prevent folder stacking: if the path already starts with the package name, strip it
                var remainingParts = relativePath.Split('/', '\\');
                if (remainingParts.Length >= 1 && remainingParts[0].Equals(packageName, StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), remainingParts.Skip(1));
                }
            }

            return relativePath;
        }

        [Test]
        public void StripResourcePath_FirstTimeExport_ReturnsCleanPath()
        {
            // First export: resource is at project/Resources/Textures/file.png
            var relativePath = "desert_strides/Resources/Textures/sand.png";
            var packageName = "MyLib";

            var result = StripResourcePath(relativePath, packageName);

            // Should strip to just Textures/sand.png
            result.Should().Be($"Textures{Path.DirectorySeparatorChar}sand.png");
        }

        [Test]
        public void StripResourcePath_ReExportAfterImport_PreventsStacking()
        {
            // Re-export scenario: resource was previously imported to Resources/MyLib/
            // so now it's at project/Resources/MyLib/Textures/file.png
            var relativePath = "desert_strides/Resources/MyLib/Textures/sand.png";
            var packageName = "MyLib";

            var result = StripResourcePath(relativePath, packageName);

            // Should strip the duplicate package name to prevent stacking
            result.Should().Be($"Textures{Path.DirectorySeparatorChar}sand.png");
        }

        [Test]
        public void StripResourcePath_MultipleReExports_StillPreventsStacking()
        {
            // Even if somehow the path got double-nested, we strip the first occurrence
            // This tests that we handle the immediate stacking case
            var relativePath = "project/Resources/MyLib/Textures/sand.png";
            var packageName = "MyLib";

            var result = StripResourcePath(relativePath, packageName);

            result.Should().Be($"Textures{Path.DirectorySeparatorChar}sand.png");
        }

        [Test]
        public void StripResourcePath_DifferentPackageName_DoesNotStrip()
        {
            // If the folder name doesn't match the package name, don't strip it
            var relativePath = "project/Resources/OtherLib/Textures/sand.png";
            var packageName = "MyLib";

            var result = StripResourcePath(relativePath, packageName);

            // Should keep OtherLib since it's not the same as MyLib
            result.Should().Be($"OtherLib{Path.DirectorySeparatorChar}Textures{Path.DirectorySeparatorChar}sand.png");
        }

        [Test]
        public void StripResourcePath_CaseInsensitive_StillPreventsStacking()
        {
            // Package name comparison should be case-insensitive
            var relativePath = "project/Resources/MYLIB/Textures/sand.png";
            var packageName = "MyLib";

            var result = StripResourcePath(relativePath, packageName);

            result.Should().Be($"Textures{Path.DirectorySeparatorChar}sand.png");
        }

        [Test]
        public void StripResourcePath_NestedSubfolders_PreservesStructure()
        {
            // Nested subfolders should be preserved after stripping
            var relativePath = "project/Resources/MyLib/Textures/Environment/Desert/sand.png";
            var packageName = "MyLib";

            var result = StripResourcePath(relativePath, packageName);

            result.Should().Be($"Textures{Path.DirectorySeparatorChar}Environment{Path.DirectorySeparatorChar}Desert{Path.DirectorySeparatorChar}sand.png");
        }

        [Test]
        public void StripResourcePath_MixedSlashes_HandlesCorrectly()
        {
            // Handle mixed forward/back slashes
            var relativePath = "project/Resources\\MyLib/Textures\\sand.png";
            var packageName = "MyLib";

            var result = StripResourcePath(relativePath, packageName);

            result.Should().Be($"Textures{Path.DirectorySeparatorChar}sand.png");
        }

        [Test]
        public void StripResourcePath_ShortPath_DoesNotCrash()
        {
            // Path that doesn't have enough parts shouldn't crash
            var relativePath = "Resources/file.png";
            var packageName = "MyLib";

            var result = StripResourcePath(relativePath, packageName);

            // Should return unchanged since it doesn't match the pattern
            result.Should().Be("Resources/file.png");
        }

        [Test]
        public void StripResourcePath_EmptyPackageName_DoesNotStrip()
        {
            var relativePath = "project/Resources/SomeFolder/Textures/sand.png";
            var packageName = "";

            var result = StripResourcePath(relativePath, packageName);

            // Empty package name won't match, keeps original structure after Resources strip
            result.Should().Be($"SomeFolder{Path.DirectorySeparatorChar}Textures{Path.DirectorySeparatorChar}sand.png");
        }

        [Test]
        public void StripResourcePath_ResourceAtRoot_HandlesCorrectly()
        {
            // File directly in Resources/PackageName/ with no subfolder
            var relativePath = "project/Resources/MyLib/readme.txt";
            var packageName = "MyLib";

            var result = StripResourcePath(relativePath, packageName);

            result.Should().Be("readme.txt");
        }
    }
}
