using System.Net;
using System.Text;
using RikiPath.Application.Services;
using RikiPath.Domain;
using Xunit;

namespace RikiPath.Tests;

public sealed class MentorVerificationDocumentTests
{
    private sealed class SigningHandler : HttpMessageHandler
    {
        public bool PublicBucket { get; set; }
        public int Requests { get; private set; }
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            LastRequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(request.Method == HttpMethod.Get
                    ? $"{{\"id\":\"mentor-private\",\"public\":{(PublicBucket ? "true" : "false")}}}"
                    : "{\"signedURL\":\"/object/sign/mentor-private/mentor/cert.pdf?token=test-only\"}", Encoding.UTF8, "application/json")
            });
        }
    }

    [Fact]
    public async Task Admin_document_link_is_short_lived_and_bound_to_configured_storage()
    {
        var handler = new SigningHandler();
        var service = new SupabaseFileStorageService(new HttpClient(handler), new AppSettings
        {
            Supabase = new SupabaseSettings
            {
                Url = "https://test.supabase.co", Bucket = "public-assets",
                VerificationBucket = "mentor-private", ServiceRoleKey = "test-only"
            }
        });
        var signed = await service.CreateSignedReadUrlAsync(
            "https://test.supabase.co/storage/v1/object/authenticated/mentor-private/mentor/cert.pdf", CancellationToken.None);
        Assert.Contains("/storage/v1/object/sign/mentor-private/mentor/cert.pdf?token=", signed);
        Assert.Equal(2, handler.Requests);
        Assert.Equal("test.supabase.co", handler.LastRequestUri?.Host);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateSignedReadUrlAsync(
            "https://other.example/storage/v1/object/authenticated/mentor-private/mentor/cert.pdf", CancellationToken.None));
        Assert.Equal(2, handler.Requests);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateSignedReadUrlAsync(
            "https://test.supabase.co/storage/v1/object/public/public-assets/mentor/cert.pdf", CancellationToken.None));
    }

    [Fact]
    public async Task Private_upload_uses_owner_path_and_does_not_return_public_url()
    {
        var handler = new SigningHandler();
        var service = new SupabaseFileStorageService(new HttpClient(handler), new AppSettings
        {
            Supabase = new SupabaseSettings
            {
                Url = "https://test.supabase.co", Bucket = "public-assets",
                VerificationBucket = "mentor-private", ServiceRoleKey = "test-only"
            }
        });
        var result = await service.UploadPrivateVerificationAsync(
            new MemoryStream([0x25, 0x50, 0x44, 0x46]), "application/pdf", 42, CancellationToken.None);
        Assert.Contains("/object/authenticated/mentor-private/mentor/42/", result.Url);
        Assert.DoesNotContain("/object/public/", result.Url);
        Assert.Equal(2, handler.Requests);
    }

    [Fact]
    public async Task Upload_fails_closed_when_verification_bucket_is_public()
    {
        var handler = new SigningHandler { PublicBucket = true };
        var service = new SupabaseFileStorageService(new HttpClient(handler), new AppSettings
        {
            Supabase = new SupabaseSettings
            {
                Url = "https://test.supabase.co", Bucket = "public-assets",
                VerificationBucket = "mentor-private", ServiceRoleKey = "test-only"
            }
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadPrivateVerificationAsync(
            new MemoryStream([0x25, 0x50, 0x44, 0x46]), "application/pdf", 42, CancellationToken.None));
        Assert.Equal(1, handler.Requests);
    }
}
