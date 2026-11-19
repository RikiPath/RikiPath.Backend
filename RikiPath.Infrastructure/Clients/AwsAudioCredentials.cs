using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using RikiPath.Domain;

namespace RikiPath.Infrastructure.Clients;

internal static class AwsAudioCredentials
{
    internal static bool IsConfigured(AwsAudioSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.AccessKeyId) &&
        !string.IsNullOrWhiteSpace(settings.SecretAccessKey) &&
        !string.IsNullOrWhiteSpace(settings.Bucket) &&
        !string.IsNullOrWhiteSpace(settings.Region);

    internal static AWSCredentials Resolve(AwsAudioSettings settings)
    {
        if (!IsConfigured(settings))
            throw new InvalidOperationException("Thiếu cấu hình AwsAudio:AccessKeyId, SecretAccessKey, Region hoặc Bucket.");
        return string.IsNullOrWhiteSpace(settings.SessionToken)
            ? new BasicAWSCredentials(settings.AccessKeyId, settings.SecretAccessKey)
            : new SessionAWSCredentials(settings.AccessKeyId, settings.SecretAccessKey, settings.SessionToken);
    }

    internal static AmazonS3Client S3(AwsAudioSettings settings) =>
        new(Resolve(settings), RegionEndpoint.GetBySystemName(settings.Region));
}
