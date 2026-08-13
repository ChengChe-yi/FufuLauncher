/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Net;

namespace FufuLauncher.Services.GameServer;

public sealed class GameServerHttpClientProvider
{
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36";

    private HttpClient? _apiClient;
    private HttpClient? _chunkClient;
    
    public HttpClient ApiClient => _apiClient ??= CreateApiClientCore();
    
    public HttpClient ChunkClient => _chunkClient ??= CreateChunkClientCore();

    private static HttpClient CreateApiClientCore()
    {
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(30),
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(2),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        return client;
    }

    private static HttpClient CreateChunkClientCore()
    {
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(30),
            MaxConnectionsPerServer = 8,
        };

        var client = new HttpClient(handler)
        {
            DefaultRequestVersion = HttpVersion.Version20,
            Timeout = Timeout.InfiniteTimeSpan,
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        return client;
    }
}
