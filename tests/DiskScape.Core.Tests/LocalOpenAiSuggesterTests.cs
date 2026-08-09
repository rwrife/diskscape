using System.Net;
using System.Text;
using System.Text.Json;
using DiskScape.Core;

namespace DiskScape.Core.Tests;

public sealed class LocalOpenAiSuggesterTests
{
    [Fact]
    public async Task BuildSnapshot_ContainsOnlyAggregateMetadata_NotFullPathsOrFileNames()
    {
        using var fixture = await ScanFixture.CreateAsync();

        var snapshot = AiSuggestionRequestBuilder.BuildSnapshot(fixture.RootNode, maxFolders: 10, maxExtensionsPerFolder: 10);
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.Contains("\"folders\"", json, StringComparison.Ordinal);
        Assert.Contains("cache", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".log", json, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(fixture.RootPath, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("taxes-2025.pdf", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SuggestCleanupAsync_ReturnsFallback_WhenEndpointIsUnreachable()
    {
        using var fixture = await ScanFixture.CreateAsync();

        using var httpClient = new HttpClient(new ThrowingHandler())
        {
            BaseAddress = new Uri("http://127.0.0.1:11434")
        };

        var suggester = new LocalOpenAiSuggester(
            httpClient,
            new LocalAiSettings
            {
                Enabled = true,
                EndpointUrl = "http://127.0.0.1:11434",
                Model = "llama3.2:3b"
            });

        var result = await suggester.SuggestCleanupAsync(fixture.RootNode);

        Assert.Equal(AiSuggestionMode.Fallback, result.Mode);
        Assert.Contains("non-AI mode", result.Notice, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Suggestions);
    }

    [Fact]
    public async Task SuggestCleanupAsync_ParsesModelResponse_AndKeepsPayloadPrivacy()
    {
        using var fixture = await ScanFixture.CreateAsync();

        string? capturedBody = null;
        var handler = new StubHttpHandler(async request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri is not null && request.RequestUri.AbsoluteUri.EndsWith("/v1/models", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"data\":[]}", Encoding.UTF8, "application/json")
                };
            }

            if (request.Method == HttpMethod.Post && request.RequestUri is not null && request.RequestUri.AbsoluteUri.EndsWith("/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                capturedBody = await request.Content!.ReadAsStringAsync();

                var completion = new
                {
                    choices = new[]
                    {
                        new
                        {
                            message = new
                            {
                                content = "{\"summary\":\"Most usage is from caches and logs.\",\"suggestions\":[{\"folderName\":\"cache\",\"risk\":\"safe_to_clean\",\"rationale\":\"Mostly temporary cache files.\"},{\"folderName\":\"docs\",\"risk\":\"be_careful\",\"rationale\":\"Likely user documents.\"}]}"
                            }
                        }
                    }
                };

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(completion), Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:11434")
        };

        var suggester = new LocalOpenAiSuggester(
            httpClient,
            new LocalAiSettings
            {
                Enabled = true,
                EndpointUrl = "http://127.0.0.1:11434",
                Model = "llama3.2:3b"
            });

        var result = await suggester.SuggestCleanupAsync(fixture.RootNode);

        Assert.Equal(AiSuggestionMode.Generated, result.Mode);
        Assert.Equal("Most usage is from caches and logs.", result.Summary);
        Assert.Equal(2, result.Suggestions.Count);
        Assert.Equal(AiSuggestionRisk.LikelySafeToClean, result.Suggestions[0].Risk);
        Assert.Equal(AiSuggestionRisk.BeCareful, result.Suggestions[1].Risk);

        Assert.NotNull(capturedBody);
        Assert.Contains("extensions", capturedBody!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fixture.RootPath, capturedBody!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("taxes-2025.pdf", capturedBody!, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("connection refused");
        }
    }

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder;

        public StubHttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _responder(request);
        }
    }

    private sealed class ScanFixture : IDisposable
    {
        private ScanFixture(string rootPath, ScanNode rootNode)
        {
            RootPath = rootPath;
            RootNode = rootNode;
        }

        public string RootPath { get; }
        public ScanNode RootNode { get; }

        public static async Task<ScanFixture> CreateAsync()
        {
            var root = Path.Combine(Path.GetTempPath(), "diskscape-ai-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            var cache = Path.Combine(root, "cache");
            var docs = Path.Combine(root, "docs");
            var logs = Path.Combine(root, "logs");

            Directory.CreateDirectory(cache);
            Directory.CreateDirectory(docs);
            Directory.CreateDirectory(logs);

            await File.WriteAllTextAsync(Path.Combine(cache, "npm-cache.bin"), new string('a', 500));
            await File.WriteAllTextAsync(Path.Combine(logs, "app.log"), new string('b', 300));
            await File.WriteAllTextAsync(Path.Combine(docs, "taxes-2025.pdf"), new string('c', 120));

            var scanner = new Scanner();
            var scannedRoot = await scanner.ScanAsync(root);

            return new ScanFixture(root, scannedRoot);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
