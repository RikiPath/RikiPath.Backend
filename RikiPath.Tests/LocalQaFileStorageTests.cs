using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Moq;
using RikiPath.WebApi.Services;

namespace RikiPath.Tests;

public sealed class LocalQaFileStorageTests
{
    private static LocalQaFileStorageService Create(string storageRoot, string environmentName = "MentorQA")
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "rikipath-qa-content-root");
        var host = new Mock<IWebHostEnvironment>();
        host.SetupGet(h => h.EnvironmentName).Returns(environmentName);
        host.SetupGet(h => h.ContentRootPath).Returns(contentRoot);
        host.SetupGet(h => h.WebRootPath).Returns(Path.Combine(contentRoot, "wwwroot"));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["LocalQaVerificationStorage:Enabled"] = "true",
            ["LocalQaVerificationStorage:RootPath"] = storageRoot
        }).Build();
        return new LocalQaFileStorageService(config, host.Object);
    }

    [Fact]
    public async Task Real_QA_file_is_written_outside_web_root_and_read_by_opaque_reference()
    {
        var root = Path.Combine(Path.GetTempPath(), "rikipath-qa-storage-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var storage = Create(root);
            byte[] bytes = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31];
            var uploaded = await storage.UploadPrivateVerificationAsync(
                new MemoryStream(bytes), "application/pdf", 42, CancellationToken.None);
            Assert.StartsWith("localqa://mentor-verification/42/", uploaded.Url);
            Assert.DoesNotContain(root, uploaded.Url, StringComparison.OrdinalIgnoreCase);
            await storage.VerifyPrivateVerificationAsync(uploaded.Url, CancellationToken.None);
            var document = await storage.OpenPrivateVerificationAsync(uploaded.Url, CancellationToken.None);
            await using (document.Content)
            {
                using var content = new MemoryStream();
                await document.Content.CopyToAsync(content);
                Assert.Equal(bytes, content.ToArray());
            }
            Assert.Equal("application/pdf", document.ContentType);
            await Assert.ThrowsAsync<ArgumentException>(() => storage.OpenPrivateVerificationAsync(
                "localqa://mentor-verification/42/../99/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.pdf", CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => storage.VerifyPrivateVerificationAsync(
                "https://public.example/cert.pdf", CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(root) && root.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task QA_storage_rejects_unsafe_content_and_production_environment()
    {
        var root = Path.Combine(Path.GetTempPath(), "rikipath-qa-storage-test-" + Guid.NewGuid().ToString("N"));
        Assert.Throws<InvalidOperationException>(() => Create(root, "Production"));
        var storage = Create(root);
        await Assert.ThrowsAsync<ArgumentException>(() => storage.UploadPrivateVerificationAsync(
            new MemoryStream([0x25, 0x50, 0x44, 0x46]), "text/plain", 1, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => storage.UploadPrivateVerificationAsync(
            new MemoryStream([0x00, 0x00, 0x00, 0x00]), "application/pdf", 1, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => storage.UploadPrivateVerificationAsync(
            new MemoryStream(new byte[5 * 1024 * 1024 + 1]), "application/pdf", 1, CancellationToken.None));
        Assert.False(Directory.Exists(root));
    }
}
