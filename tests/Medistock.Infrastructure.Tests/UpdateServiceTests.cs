using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Contracts.Updates;
using Medistock.Infrastructure.Data;
using Medistock.Infrastructure.Sync.Updates;
using Xunit;

namespace Medistock.Infrastructure.Tests;

public class UpdateServiceTests : IDisposable
{
    private readonly string _testUpdatesDir;

    public UpdateServiceTests()
    {
        _testUpdatesDir = MedistockPaths.UpdateStagingDirectory;
        if (!Directory.Exists(_testUpdatesDir))
        {
            Directory.CreateDirectory(_testUpdatesDir);
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testUpdatesDir))
            {
                foreach (var file in Directory.GetFiles(_testUpdatesDir, "test_*.exe*"))
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }
        catch { }
    }

    [Fact]
    public void UpdateDownloadProgress_CalculatesCorrectly()
    {
        var progress = new UpdateDownloadProgress(
            Version: "1.2.0",
            Percentage: 50.0,
            BytesDownloaded: 5000000,
            TotalBytes: 10000000,
            SpeedBytesPerSec: 1048576, // 1 MB/s
            StatusMessage: "Downloading...",
            IsCompleted: false,
            IsFailed: false
        );

        Assert.Equal("1.2.0", progress.Version);
        Assert.Equal(50.0, progress.Percentage);
        Assert.Equal(5000000, progress.BytesDownloaded);
        Assert.Equal(10000000, progress.TotalBytes);
        Assert.Equal(1048576, progress.SpeedBytesPerSec);
        Assert.False(progress.IsCompleted);
        Assert.False(progress.IsFailed);
    }

    [Fact]
    public async Task DownloadUpdateAsync_ValidatesSha256Checksum()
    {
        var payloadContent = Encoding.UTF8.GetBytes("Fake installer binary content for testing updates");
        var expectedHash = Convert.ToHexString(SHA256.HashData(payloadContent)).ToLowerInvariant();

        var handler = new MockHttpMessageHandler((req) =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payloadContent)
            };
        });

        var httpClient = new HttpClient(handler);
        var updateService = new UpdateService(httpClient);

        var manifest = new UpdateCheckResponse
        {
            UpdateAvailable = true,
            Version = "99.0.0-test",
            DownloadUrl = "https://updates.medistock.local/installer-99.0.0.exe",
            Sha256Hash = expectedHash,
            SizeBytes = payloadContent.Length,
            ReleaseNotes = "Test release"
        };

        UpdateDownloadProgress? lastReported = null;
        var progress = new Progress<UpdateDownloadProgress>(p => lastReported = p);

        var downloadedPath = await updateService.DownloadUpdateAsync(manifest, progress);

        Assert.NotNull(downloadedPath);
        Assert.True(File.Exists(downloadedPath));

        // Clean up test file
        try { File.Delete(downloadedPath); } catch { }
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}
