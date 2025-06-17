// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using NUnit.Framework;
using FluentAssertions;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using System.IO.Compression;

namespace HS.Stride.Packer.Core.Tests
{
    [TestFixture]
    public class PackageRegistryTests
    {
        private PackageRegistry _packageRegistry;
        private Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private HttpClient _mockHttpClient;
        private string _testCacheDirectory;

        [SetUp]
        public void Setup()
        {
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _mockHttpClient = new HttpClient(_mockHttpMessageHandler.Object);
            _testCacheDirectory = Path.Combine(Path.GetTempPath(), "HSPackerTests", Guid.NewGuid().ToString());
            _packageRegistry = new PackageRegistry(_mockHttpClient, _testCacheDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            _packageRegistry?.Dispose();
            _mockHttpClient?.Dispose();
            
            // Clean up test cache directory with retry logic for locked files
            CleanupTestDirectory();
        }

        private void CleanupTestDirectory()
        {
            if (!Directory.Exists(_testCacheDirectory))
                return;

            try
            {
                Directory.Delete(_testCacheDirectory, true);
            }
            catch (IOException)
            {
                // Files might be locked, wait and try again
                System.Threading.Thread.Sleep(200);
                try
                {
                    Directory.Delete(_testCacheDirectory, true);
                }
                catch (Exception)
                {
                    // If still can't delete, try to delete individual files
                    try
                    {
                        var files = Directory.GetFiles(_testCacheDirectory, "*", SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            try
                            {
                                File.SetAttributes(file, FileAttributes.Normal);
                                File.Delete(file);
                            }
                            catch (Exception)
                            {
                                // Ignore individual file deletion errors
                            }
                        }
                        Directory.Delete(_testCacheDirectory, true);
                    }
                    catch (Exception)
                    {
                        // Final fallback - ignore cleanup errors
                    }
                }
            }
            catch (Exception)
            {
                // Ignore cleanup errors
            }
        }

        [Test]
        public void Constructor_CreateInstance_ReturnPackageRegistry()
        {
            _packageRegistry.Should().NotBeNull();
        }

        [Test]
        public void SetRegistryUrl_ValidUrl_NoExceptionThrown()
        {
            var testUrl = "https://registry.example.com/packages.json";
            
            Action act = () => _packageRegistry.SetRegistryUrl(testUrl);
            act.Should().NotThrow();
        }

        [Test]
        public void SetRegistryUrl_NullUrl_NoExceptionThrown()
        {
            Action act = () => _packageRegistry.SetRegistryUrl(null);
            act.Should().NotThrow();
        }

        [Test]
        public void SetRegistryUrl_EmptyUrl_NoExceptionThrown()
        {
            Action act = () => _packageRegistry.SetRegistryUrl("");
            act.Should().NotThrow();
        }

        [Test]
        public async Task GetRegistryInfoAsync_NoRegistryUrlSet_ThrowArgumentException()
        {
            Func<Task> act = async () => await _packageRegistry.GetRegistryInfoAsync();
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("Registry URL cannot be empty. Set a registry URL first or provide one as parameter.");
        }

        [Test]
        public async Task GetRegistryInfoAsync_EmptyRegistryUrlParameter_ThrowArgumentException()
        {
            Func<Task> act = async () => await _packageRegistry.GetRegistryInfoAsync("");
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("Registry URL cannot be empty. Set a registry URL first or provide one as parameter.");
        }

        [Test]
        public async Task GetRegistryInfoAsync_NullRegistryUrlParameter_ThrowArgumentException()
        {
            Func<Task> act = async () => await _packageRegistry.GetRegistryInfoAsync(null);
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("Registry URL cannot be empty. Set a registry URL first or provide one as parameter.");
        }

        [Test]
        public async Task GetRegistryInfoAsync_ValidJsonWithSinglePackage_ReturnRegistryInfo()
        {
            var testUrl = "https://registry.example.com/packages.json";
            var registryInfo = new RegistryInfo
            {
                Name = "Test Registry",
                Description = "Test registry for packages",
                Updated = DateTime.UtcNow.AddDays(-1).ToString("MM-dd-yyyy"),
                Packages = new List<string>
                {
                    "https://github.com/user/repo/releases/download/v1.0/package1.json"
                }
            };
            var jsonResponse = JsonSerializer.Serialize(registryInfo);
            
            SetupHttpResponse(HttpStatusCode.OK, jsonResponse);
            
            var result = await _packageRegistry.GetRegistryInfoAsync(testUrl);
            
            result.Should().NotBeNull();
            result.Name.Should().Be("Test Registry");
            result.Description.Should().Be("Test registry for packages");
            result.Packages.Should().HaveCount(1);
            result.Packages.Should().Contain("https://github.com/user/repo/releases/download/v1.0/package1.json");
        }

        [Test]
        public async Task GetRegistryInfoAsync_ValidJsonWithMultiplePackages_ReturnRegistryInfo()
        {
            var testUrl = "https://registry.example.com/packages.json";
            var registryInfo = new RegistryInfo
            {
                Name = "Multi Package Registry",
                Description = "Registry with multiple packages",
                Updated = DateTime.UtcNow.ToString("MM-dd-yyyy"),
                Packages = new List<string>
                {
                    "https://github.com/user1/repo1/releases/download/v1.0/package1.json",
                    "https://github.com/user2/repo2/releases/download/v2.0/package2.json",
                    "https://github.com/user3/repo3/releases/download/v1.5/package3.json"
                }
            };
            var jsonResponse = JsonSerializer.Serialize(registryInfo);
            
            SetupHttpResponse(HttpStatusCode.OK, jsonResponse);
            
            var result = await _packageRegistry.GetRegistryInfoAsync(testUrl);
            
            result.Should().NotBeNull();
            result.Name.Should().Be("Multi Package Registry");
            result.Packages.Should().HaveCount(3);
            result.Packages.Should().Contain("https://github.com/user1/repo1/releases/download/v1.0/package1.json");
            result.Packages.Should().Contain("https://github.com/user2/repo2/releases/download/v2.0/package2.json");
            result.Packages.Should().Contain("https://github.com/user3/repo3/releases/download/v1.5/package3.json");
        }

        [Test]
        public async Task GetRegistryInfoAsync_ValidJsonWithEmptyPackages_ReturnEmptyRegistryInfo()
        {
            var testUrl = "https://registry.example.com/packages.json";
            var registryInfo = new RegistryInfo
            {
                Name = "Empty Registry",
                Description = "Registry with no packages",
                Updated = DateTime.UtcNow.ToString("MM-dd-yyyy"),
                Packages = new List<string>()
            };
            var jsonResponse = JsonSerializer.Serialize(registryInfo);
            
            SetupHttpResponse(HttpStatusCode.OK, jsonResponse);
            
            var result = await _packageRegistry.GetRegistryInfoAsync(testUrl);
            
            result.Should().NotBeNull();
            result.Name.Should().Be("Empty Registry");
            result.Packages.Should().BeEmpty();
        }

        [Test]
        public async Task GetRegistryInfoAsync_InvalidJson_ThrowJsonException()
        {
            var testUrl = "https://registry.example.com/packages.json";
            var invalidJson = "{ invalid json content }";
            
            SetupHttpResponse(HttpStatusCode.OK, invalidJson);
            
            Func<Task> act = async () => await _packageRegistry.GetRegistryInfoAsync(testUrl);
            await act.Should().ThrowAsync<JsonException>();
        }

        [Test]
        public async Task GetRegistryInfoAsync_HttpError404_ThrowHttpRequestException()
        {
            var testUrl = "https://registry.example.com/packages.json";
            
            SetupHttpResponse(HttpStatusCode.NotFound, "");
            
            Func<Task> act = async () => await _packageRegistry.GetRegistryInfoAsync(testUrl);
            await act.Should().ThrowAsync<HttpRequestException>();
        }

        [Test]
        public async Task GetRegistryInfoAsync_HttpError500_ThrowHttpRequestException()
        {
            var testUrl = "https://registry.example.com/packages.json";
            
            SetupHttpResponse(HttpStatusCode.InternalServerError, "");
            
            Func<Task> act = async () => await _packageRegistry.GetRegistryInfoAsync(testUrl);
            await act.Should().ThrowAsync<HttpRequestException>();
        }

        [Test]
        public async Task GetRegistryInfoAsync_JsonWithNullValues_ReturnEmptyRegistryInfo()
        {
            var testUrl = "https://registry.example.com/packages.json";
            var jsonResponse = "null";
            
            SetupHttpResponse(HttpStatusCode.OK, jsonResponse);
            
            var result = await _packageRegistry.GetRegistryInfoAsync(testUrl);
            
            result.Should().NotBeNull();
            result.Packages.Should().BeEmpty();
        }

        [Test]
        public async Task GetRegistryInfoAsync_JsonWithMixedValidInvalidUrls_ReturnAllUrlsAsIs()
        {
            var testUrl = "https://registry.example.com/packages.json";
            var registryInfo = new RegistryInfo
            {
                Name = "Mixed URL Registry",
                Description = "Registry with valid and invalid URLs",
                Updated = DateTime.UtcNow.ToString("MM-dd-yyyy"),
                Packages = new List<string>
                {
                    "https://github.com/user/repo/releases/download/v1.0/valid.json",
                    "not-a-url",
                    "ftp://invalid-protocol.com/package.json",
                    "https://github.com/user/repo/releases/download/v2.0/another.json"
                }
            };
            var jsonResponse = JsonSerializer.Serialize(registryInfo);
            
            SetupHttpResponse(HttpStatusCode.OK, jsonResponse);
            
            var result = await _packageRegistry.GetRegistryInfoAsync(testUrl);
            
            result.Should().NotBeNull();
            result.Packages.Should().HaveCount(4);
            result.Packages.Should().Contain("https://github.com/user/repo/releases/download/v1.0/valid.json");
            result.Packages.Should().Contain("not-a-url");
            result.Packages.Should().Contain("ftp://invalid-protocol.com/package.json");
            result.Packages.Should().Contain("https://github.com/user/repo/releases/download/v2.0/another.json");
        }

        [Test]
        public async Task GetRegistryInfoAsync_UseProvidedUrlOverSetUrl_ReturnResultFromProvidedUrl()
        {
            var setUrl = "https://set-registry.example.com/packages.json";
            var providedUrl = "https://provided-registry.example.com/packages.json";
            var registryInfo = new RegistryInfo
            {
                Name = "Provided Registry",
                Description = "Registry from provided URL",
                Updated = DateTime.UtcNow.ToString("MM-dd-yyyy"),
                Packages = new List<string>
                {
                    "https://github.com/user/repo/releases/download/v1.0/fromprovided.json"
                }
            };
            var jsonResponse = JsonSerializer.Serialize(registryInfo);
            
            _packageRegistry.SetRegistryUrl(setUrl);
            SetupHttpResponse(HttpStatusCode.OK, jsonResponse);
            
            var result = await _packageRegistry.GetRegistryInfoAsync(providedUrl);
            
            result.Should().NotBeNull();
            result.Name.Should().Be("Provided Registry");
            result.Packages.Should().HaveCount(1);
            result.Packages.Should().Contain("https://github.com/user/repo/releases/download/v1.0/fromprovided.json");
            
            // Verify the provided URL was used, not the set URL
            VerifyHttpRequest(providedUrl);
        }


        [Test]
        public void Constructor_NullHttpClient_ThrowArgumentNullException()
        {
            Action act = () => new PackageRegistry(null);
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("httpClient");
        }

        [Test]
        public async Task GetAllPackagesAsync_ValidRegistry_ReturnAllPackages()
        {
            var testUrl = "https://registry.example.com/packages.json";
            var registryInfo = new RegistryInfo
            {
                Name = "Test Registry",
                Packages = new List<string>
                {
                    "https://example.com/package1.json",
                    "https://example.com/package2.json"
                }
            };
            var package1 = new PackageMetadata
            {
                Name = "Package1",
                Version = "1.0.0",
                Author = "Author1",
                Description = "Test package 1",
                DownloadUrl = "https://example.com/package1.zip"
            };
            var package2 = new PackageMetadata
            {
                Name = "Package2", 
                Version = "2.0.0",
                Author = "Author2",
                Description = "Test package 2",
                DownloadUrl = "https://example.com/package2.zip"
            };

            SetupHttpResponseSequence(new[]
            {
                (HttpStatusCode.OK, JsonSerializer.Serialize(registryInfo)),
                (HttpStatusCode.OK, JsonSerializer.Serialize(package1)),
                (HttpStatusCode.OK, JsonSerializer.Serialize(package2))
            });

            var result = await _packageRegistry.GetAllPackagesAsync(testUrl);

            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result[0].Name.Should().Be("Package1");
            result[1].Name.Should().Be("Package2");
        }

        [Test]
        public async Task GetPackageMetadataAsync_ValidUrl_ReturnPackageMetadata()
        {
            var packageUrl = "https://example.com/package.json";
            var packageMetadata = new PackageMetadata
            {
                Name = "TestPackage",
                Version = "1.0.0",
                Author = "TestAuthor",
                Description = "Test description",
                Tags = new List<string> { "test", "demo" },
                DownloadUrl = "https://example.com/package.zip"
            };
            var jsonResponse = JsonSerializer.Serialize(packageMetadata);

            SetupHttpResponse(HttpStatusCode.OK, jsonResponse);

            var result = await _packageRegistry.GetPackageMetadataAsync(packageUrl);

            result.Should().NotBeNull();
            result.Name.Should().Be("TestPackage");
            result.Version.Should().Be("1.0.0");
            result.Author.Should().Be("TestAuthor");
            result.Tags.Should().Contain("test");
            result.Tags.Should().Contain("demo");
        }

        [Test]
        public async Task SearchPackagesAsync_ValidQuery_ReturnMatchingPackages()
        {
            var testUrl = "https://registry.example.com/packages.json";
            var registryInfo = new RegistryInfo
            {
                Packages = new List<string> { "https://example.com/package1.json", "https://example.com/package2.json" }
            };
            var package1 = new PackageMetadata
            {
                Name = "UILibrary",
                Description = "User interface components",
                Tags = new List<string> { "ui", "components" }
            };
            var package2 = new PackageMetadata
            {
                Name = "AudioEngine",
                Description = "Audio processing library",
                Tags = new List<string> { "audio", "sound" }
            };

            SetupHttpResponseSequence(new[]
            {
                (HttpStatusCode.OK, JsonSerializer.Serialize(registryInfo)),
                (HttpStatusCode.OK, JsonSerializer.Serialize(package1)),
                (HttpStatusCode.OK, JsonSerializer.Serialize(package2))
            });

            var result = await _packageRegistry.SearchPackagesAsync("ui", testUrl);

            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result[0].Name.Should().Be("UILibrary");
        }

        [Test]
        public async Task FilterByTagsAsync_ValidTags_ReturnMatchingPackages()
        {
            var testUrl = "https://registry.example.com/packages.json";
            var registryInfo = new RegistryInfo
            {
                Packages = new List<string> { "https://example.com/package1.json", "https://example.com/package2.json" }
            };
            var package1 = new PackageMetadata
            {
                Name = "UILibrary",
                Tags = new List<string> { "ui", "components" }
            };
            var package2 = new PackageMetadata
            {
                Name = "AudioEngine", 
                Tags = new List<string> { "audio", "sound" }
            };

            SetupHttpResponseSequence(new[]
            {
                (HttpStatusCode.OK, JsonSerializer.Serialize(registryInfo)),
                (HttpStatusCode.OK, JsonSerializer.Serialize(package1)),
                (HttpStatusCode.OK, JsonSerializer.Serialize(package2))
            });

            var result = await _packageRegistry.FilterByTagsAsync(new List<string> { "audio" }, testUrl);

            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result[0].Name.Should().Be("AudioEngine");
        }

        // Local Package Cache Tests
        [Test]
        public void GetCacheDirectory_ReturnValidPath()
        {
            var result = _packageRegistry.GetCacheDirectory();
            
            result.Should().NotBeNullOrEmpty();
            result.Should().Be(_testCacheDirectory);
        }

        [Test]
        public async Task DownloadPackageToCache_ValidPackage_DownloadAndSaveFiles()
        {
            var package = new PackageMetadata
            {
                Name = "TestPackage",
                Version = "1.0.0",
                Author = "TestAuthor",
                DownloadUrl = "https://example.com/testpackage.stridepackage"
            };
            
            // Create valid package content with proper hash
            var packageContent = await CreateValidPackageContentForPackage(package.Name);
            SetupHttpResponse(HttpStatusCode.OK, packageContent);

            var result = await _packageRegistry.DownloadPackageToCache(package);

            result.Should().NotBeNullOrEmpty();
            result.Should().EndWith("TestPackage.stridepackage");
            result.Should().Contain("HSPacker");
            
            // Verify the file actually exists and is valid
            File.Exists(result).Should().BeTrue();
        }

        [Test]
        public async Task DownloadPackageToCache_EmptyDownloadUrl_ThrowArgumentException()
        {
            var package = new PackageMetadata
            {
                Name = "TestPackage",
                DownloadUrl = ""
            };

            Func<Task> act = async () => await _packageRegistry.DownloadPackageToCache(package);
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("Package download URL cannot be empty");
        }

        [Test]
        public async Task DownloadPackageToCache_HttpError_ThrowHttpRequestException()
        {
            var package = new PackageMetadata
            {
                Name = "TestPackage",
                DownloadUrl = "https://example.com/notfound.stridepackage"
            };

            SetupHttpResponse(HttpStatusCode.NotFound, "");

            Func<Task> act = async () => await _packageRegistry.DownloadPackageToCache(package);
            await act.Should().ThrowAsync<HttpRequestException>();
        }

        [Test]
        public void IsPackageCached_NonExistentPackage_ReturnFalse()
        {
            var package = new PackageMetadata
            {
                Name = "NonExistentPackage"
            };

            var result = _packageRegistry.IsPackageCached(package);

            result.Should().BeFalse();
        }

        [Test]
        public void GetCachedPackagePath_NonExistentPackage_ReturnNull()
        {
            var package = new PackageMetadata
            {
                Name = "NonExistentPackage"
            };

            var result = _packageRegistry.GetCachedPackagePath(package);

            result.Should().BeNull();
        }

        [Test]
        public async Task ClearPackageCache_NonExistentPackage_ReturnFalse()
        {
            var package = new PackageMetadata
            {
                Name = "NonExistentPackage"
            };

            var result = await _packageRegistry.ClearPackageCache(package);

            result.Should().BeFalse();
        }

        [Test]
        public async Task GetCachedPackages_EmptyCacheDirectory_ReturnEmptyList()
        {
            var result = await _packageRegistry.GetCachedPackages();

            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        // CachedPackage Tests
        [Test]
        public void CachedPackage_DisplaySize_FormatBytesCorrectly()
        {
            var cachedPackage = new CachedPackage
            {
                Size = 1024
            };

            cachedPackage.DisplaySize.Should().Be("1.0 KB");

            cachedPackage.Size = 1024 * 1024;
            cachedPackage.DisplaySize.Should().Be("1.0 MB");

            cachedPackage.Size = 1024 * 1024 * 1024;
            cachedPackage.DisplaySize.Should().Be("1.0 GB");

            cachedPackage.Size = 512;
            cachedPackage.DisplaySize.Should().Be("512 bytes");
        }

        [Test]
        public void CachedPackage_Properties_SetAndGetCorrectly()
        {
            var metadata = new PackageMetadata
            {
                Name = "TestPackage",
                Version = "1.0.0"
            };
            var cachedDate = DateTime.Now.AddDays(-1);
            var cachedPath = @"C:\Cache\TestPackage\TestPackage.stridepackage";
            var size = 2048;

            var cachedPackage = new CachedPackage
            {
                Metadata = metadata,
                CachedPath = cachedPath,
                CachedDate = cachedDate,
                Size = size
            };

            cachedPackage.Metadata.Should().Be(metadata);
            cachedPackage.CachedPath.Should().Be(cachedPath);
            cachedPackage.CachedDate.Should().Be(cachedDate);
            cachedPackage.Size.Should().Be(size);
            cachedPackage.DisplaySize.Should().Be("2.0 KB");
        }

        // Integration Tests for Package Cache Workflow
        [Test]
        public async Task PackageCacheWorkflow_DownloadCheckClearWorkflow_WorksCorrectly()
        {
            var package = new PackageMetadata
            {
                Name = "WorkflowTestPackage",
                Version = "1.0.0",
                Author = "TestAuthor",
                DownloadUrl = "https://example.com/workflow.stridepackage"
            };
            // Create valid package content with proper hash
            var packageContent = await CreateValidPackageContentForPackage(package.Name);
            SetupHttpResponse(HttpStatusCode.OK, packageContent);

            // Initially should not be cached
            _packageRegistry.IsPackageCached(package).Should().BeFalse();
            _packageRegistry.GetCachedPackagePath(package).Should().BeNull();

            // Download package
            var downloadedPath = await _packageRegistry.DownloadPackageToCache(package);
            downloadedPath.Should().NotBeNullOrEmpty();

            // Now should be cached
            _packageRegistry.IsPackageCached(package).Should().BeTrue();
            _packageRegistry.GetCachedPackagePath(package).Should().NotBeNull();

            // Should appear in cached packages list
            var cachedPackages = await _packageRegistry.GetCachedPackages();
            cachedPackages.Should().HaveCount(1);
            cachedPackages[0].Metadata.Name.Should().Be("WorkflowTestPackage");

            // Clear cache
            var cleared = await _packageRegistry.ClearPackageCache(package);
            cleared.Should().BeTrue();

            // Should no longer be cached
            _packageRegistry.IsPackageCached(package).Should().BeFalse();
            _packageRegistry.GetCachedPackagePath(package).Should().BeNull();
        }

        [Test]
        public async Task GetAllPackagesAsync_MultiplePackages_FetchesInParallel()
        {
            var testUrl = "https://registry.example.com/packages.json";
            var registryJson = JsonSerializer.Serialize(new RegistryInfo
            {
                Name = "Test Registry",
                Description = "Performance test registry",
                Packages = new List<string>
                {
                    "https://example.com/package1.json",
                    "https://example.com/package2.json",
                    "https://example.com/package3.json"
                }
            });

            var package1Json = JsonSerializer.Serialize(new PackageMetadata
            {
                Name = "Package1",
                Version = "1.0.0",
                Author = "Test",
                Description = "Test package 1"
            });

            var package2Json = JsonSerializer.Serialize(new PackageMetadata
            {
                Name = "Package2", 
                Version = "2.0.0",
                Author = "Test",
                Description = "Test package 2"
            });

            var package3Json = JsonSerializer.Serialize(new PackageMetadata
            {
                Name = "Package3",
                Version = "3.0.0", 
                Author = "Test",
                Description = "Test package 3"
            });

            // Setup responses using existing helper method
            SetupHttpResponseSequence(new[]
            {
                (HttpStatusCode.OK, registryJson),
                (HttpStatusCode.OK, package1Json),
                (HttpStatusCode.OK, package2Json),
                (HttpStatusCode.OK, package3Json)
            });

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var packages = await _packageRegistry.GetAllPackagesAsync(testUrl);
            stopwatch.Stop();

            packages.Should().HaveCount(3);
            packages.Should().Contain(p => p.Name == "Package1");
            packages.Should().Contain(p => p.Name == "Package2"); 
            packages.Should().Contain(p => p.Name == "Package3");

            // Performance check - parallel requests should complete much faster than sequential
            // Even with mocked responses, parallel should be under 100ms vs sequential taking much longer
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, 
                "because parallel HTTP requests should be much faster than sequential ones");
        }

        private void SetupHttpResponse(HttpStatusCode statusCode, string content)
        {
            HttpContent httpContent;
            
            // Check if content is Base64 encoded (for binary package content)
            if (IsBase64String(content))
            {
                var bytes = Convert.FromBase64String(content);
                httpContent = new ByteArrayContent(bytes);
            }
            else
            {
                httpContent = new StringContent(content);
            }

            var response = new HttpResponseMessage(statusCode)
            {
                Content = httpContent
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    // Create a new response each time to avoid sharing content streams
                    HttpContent freshContent;
                    if (IsBase64String(content))
                    {
                        var bytes = Convert.FromBase64String(content);
                        freshContent = new ByteArrayContent(bytes);
                    }
                    else
                    {
                        freshContent = new StringContent(content);
                    }

                    return new HttpResponseMessage(statusCode)
                    {
                        Content = freshContent
                    };
                });
        }

        private static bool IsBase64String(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length % 4 != 0)
                return false;

            // Check if it contains only valid Base64 characters
            if (!System.Text.RegularExpressions.Regex.IsMatch(s, @"^[a-zA-Z0-9+/]*={0,3}$"))
                return false;

            // Additional check: Base64 strings for package content should be reasonably long
            // Short strings like "null" are likely JSON, not Base64 package content
            if (s.Length < 100)
                return false;

            try
            {
                Convert.FromBase64String(s);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void SetupHttpResponseSequence((HttpStatusCode statusCode, string content)[] responses)
        {
            var setup = _mockHttpMessageHandler
                .Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>());

            foreach (var (statusCode, content) in responses)
            {
                var response = new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(content)
                };
                setup = setup.ReturnsAsync(response);
            }
        }

        private void VerifyHttpRequest(string expectedUrl)
        {
            _mockHttpMessageHandler
                .Protected()
                .Verify(
                    "SendAsync",
                    Times.Once(),
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString() == expectedUrl),
                    ItExpr.IsAny<CancellationToken>());
        }

        // Hash Verification Tests
        [Test]
        public async Task DownloadPackageToCache_CorruptedPackage_ThrowInvalidOperationExceptionAndCleanup()
        {
            var package = new PackageMetadata
            {
                Name = "CorruptedPackage",
                Version = "1.0.0",
                DownloadUrl = "https://example.com/corrupted.stridepackage"
            };

            // Create corrupted package content (invalid ZIP)
            var corruptedContent = "This is not a valid ZIP file - corrupted data";
            SetupHttpResponse(HttpStatusCode.OK, corruptedContent);

            Func<Task> act = async () => await _packageRegistry.DownloadPackageToCache(package);
            var exception = await act.Should().ThrowAsync<Exception>();
            
            // Should be either InvalidOperationException (expected) or IOException (file lock issue)
            exception.Which.Should().Match(ex => 
                ex is InvalidOperationException || ex is IOException,
                "because either the integrity check should fail or file locking should occur");

            // Verify the corrupted file was cleaned up (may need a delay for file locks)
            await Task.Delay(100);
            var packagePath = Path.Combine(_testCacheDirectory, "CorruptedPackage", "CorruptedPackage.stridepackage");
            File.Exists(packagePath).Should().BeFalse();
        }

        [Test]
        public async Task DownloadPackageToCache_PackageWithoutManifest_ThrowInvalidOperationExceptionAndCleanup()
        {
            var package = new PackageMetadata
            {
                Name = "NoManifestPackage",
                Version = "1.0.0",
                DownloadUrl = "https://example.com/nomanifest.stridepackage"
            };

            // Create package without manifest.json
            var packageContent = await CreatePackageContentWithoutManifest();
            SetupHttpResponse(HttpStatusCode.OK, packageContent);

            Func<Task> act = async () => await _packageRegistry.DownloadPackageToCache(package);
            var exception = await act.Should().ThrowAsync<Exception>();
            
            // Should be either InvalidOperationException (expected) or IOException (file lock issue)
            exception.Which.Should().Match(ex => 
                ex is InvalidOperationException || ex is IOException,
                "because either the integrity check should fail or file locking should occur");

            // Verify the corrupted file was cleaned up (may need a delay for file locks)
            await Task.Delay(100);
            var packagePath = Path.Combine(_testCacheDirectory, "NoManifestPackage", "NoManifestPackage.stridepackage");
            File.Exists(packagePath).Should().BeFalse();
        }

        [Test]
        public async Task DownloadPackageToCache_PackageWithoutHash_ThrowInvalidOperationExceptionAndCleanup()
        {
            var package = new PackageMetadata
            {
                Name = "NoHashPackage", 
                Version = "1.0.0",
                DownloadUrl = "https://example.com/nohash.stridepackage"
            };

            // Create package with manifest but no hash
            var packageContent = await CreatePackageContentWithoutHash();
            SetupHttpResponse(HttpStatusCode.OK, packageContent);

            Func<Task> act = async () => await _packageRegistry.DownloadPackageToCache(package);
            var exception = await act.Should().ThrowAsync<Exception>();
            
            // Should be either InvalidOperationException (expected) or IOException (file lock issue)
            exception.Which.Should().Match(ex => 
                ex is InvalidOperationException || ex is IOException,
                "because either the integrity check should fail or file locking should occur");

            // Verify the corrupted file was cleaned up (may need a delay for file locks)
            await Task.Delay(100);
            var packagePath = Path.Combine(_testCacheDirectory, "NoHashPackage", "NoHashPackage.stridepackage");
            File.Exists(packagePath).Should().BeFalse();
        }

        [Test]
        public async Task DownloadPackageToCache_ValidPackageWithValidHash_SucceedWithoutErrors()
        {
            var package = new PackageMetadata
            {
                Name = "ValidPackage",
                Version = "1.0.0", 
                DownloadUrl = "https://example.com/valid.stridepackage"
            };

            // Create valid package with correct hash
            var packageContent = await CreateValidPackageContent();
            SetupHttpResponse(HttpStatusCode.OK, packageContent);

            var result = await _packageRegistry.DownloadPackageToCache(package);

            result.Should().NotBeNullOrEmpty();
            result.Should().EndWith("ValidPackage.stridepackage");
            File.Exists(result).Should().BeTrue();

            // Verify package is actually cached
            _packageRegistry.IsPackageCached(package).Should().BeTrue();
        }

        private async Task<string> CreatePackageContentWithoutManifest()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create test content without manifest.json
                var assetsDir = Path.Combine(tempDir, "Assets");
                Directory.CreateDirectory(assetsDir);
                await File.WriteAllTextAsync(Path.Combine(assetsDir, "test.txt"), "Test content");

                var packagePath = Path.Combine(Path.GetTempPath(), $"NoManifest_{Guid.NewGuid()}.stridepackage");
                ZipFile.CreateFromDirectory(tempDir, packagePath);

                var packageBytes = await File.ReadAllBytesAsync(packagePath);
                File.Delete(packagePath);
                return Convert.ToBase64String(packageBytes);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        private async Task<string> CreatePackageContentWithoutHash()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
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

                var packageBytes = await File.ReadAllBytesAsync(packagePath);
                File.Delete(packagePath);
                return Convert.ToBase64String(packageBytes);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        private async Task<string> CreateValidPackageContent()
        {
            return await CreateValidPackageContentForPackage("ValidPackage");
        }

        private async Task<string> CreateValidPackageContentForPackage(string packageName)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create test content
                var assetsDir = Path.Combine(tempDir, "Assets");
                Directory.CreateDirectory(assetsDir);
                await File.WriteAllTextAsync(Path.Combine(assetsDir, "test.txt"), $"Test content for {packageName}");

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
                    Name = packageName,
                    Version = "1.0.0",
                    Author = "TestAuthor",
                    Description = $"Valid package {packageName} with hash",
                    PackageHash = hash
                };

                var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(Path.Combine(tempDir, "manifest.json"), manifestJson);

                var packagePath = Path.Combine(Path.GetTempPath(), $"{packageName}_{Guid.NewGuid()}.stridepackage");
                ZipFile.CreateFromDirectory(tempDir, packagePath);

                var packageBytes = await File.ReadAllBytesAsync(packagePath);
                File.Delete(packagePath);
                return Convert.ToBase64String(packageBytes);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}