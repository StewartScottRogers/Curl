// <sample usings>
using System.Net;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Http;
// </sample>

namespace Curl.Api.Readme;

/// <summary>
/// The code blocks of <c>Curl.Api/README.md</c>, each between its <c>sample</c> markers; a test keeps the
/// README's block and the marked source identical, and runs each sample against a fake engine.
/// </summary>
internal static class ReadmeSamples
{
    public static async Task CreateClientAsync()
    {
        // <sample create>
        await using CurlClient client = CurlClient.Create(new CurlClientOptions
        {
            UserAgent = "my-app/1.0",
            Timeout = TimeSpan.FromSeconds(30),
            ConnectTimeout = TimeSpan.FromSeconds(5),
        });
        // </sample>
        await Task.CompletedTask;
    }

    public static async Task SendAndStreamAsync(CurlClient client, Stream destination, CancellationToken cancellationToken)
    {
        // <sample send>
        CurlRequest request = new(HttpMethod.Get, "https://example.com/data.bin");
        CurlResponse response = await client.SendAsync(request, cancellationToken);

        Console.WriteLine($"{response.StatusCode} {response.ReasonPhrase} over HTTP {response.Version}");
        foreach (KeyValuePair<string, string> header in response.Headers)
        {
            Console.WriteLine($"{header.Key}: {header.Value}");
        }

        await using Stream body = response.Body;
        await body.CopyToAsync(destination, cancellationToken);
        // </sample>
    }

    public static async Task SetOptionsAsync(CurlClient client, CancellationToken cancellationToken)
    {
        // <sample options>
        CurlRequest request = new(HttpMethod.Get, "https://example.com/redirected")
        {
            Headers = [new("Accept", "application/json"), new("X-Trace", "42")],
            Http = new CurlHttpOptions { FollowLocation = FollowLocation.All, MaxRedirects = 5 },
            Network = new CurlNetworkOptions { TcpKeepAlive = true },
        };
        CurlResponse response = await client.SendAsync(request, cancellationToken);
        await response.Body.DisposeAsync();
        // </sample>
    }

    public static async Task UploadBytesAndStreamAsync(CurlClient client, Stream file, CancellationToken cancellationToken)
    {
        // <sample upload>
        CurlRequest json = new(HttpMethod.Post, "https://example.com/items")
        {
            Headers = [new("Content-Type", "application/json")],
            Body = "{\"name\":\"widget\"}"u8.ToArray(),
        };
        CurlRequest large = new(HttpMethod.Put, "https://example.com/upload")
        {
            Body = CurlContent.FromStream(file, length: file.Length),
        };
        await (await client.SendAsync(json, cancellationToken)).Body.DisposeAsync();
        await (await client.SendAsync(large, cancellationToken)).Body.DisposeAsync();
        // </sample>
    }

    public static async Task CancelAndHandleErrorsAsync(CurlClient client, CancellationToken cancellationToken)
    {
        // <sample cancel>
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, "https://example.com/slow"), timeout.Token);
            await using Stream body = response.Body;
            await body.CopyToAsync(Stream.Null, timeout.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Cancelled: the transfer was aborted.");
        }
        catch (CurlException failure)
        {
            Console.WriteLine($"curl error {(int)failure.Code}: {failure.Message}");
        }
        // </sample>
    }

    public static async Task ReportProgressAsync(CurlClient client, CancellationToken cancellationToken)
    {
        // <sample progress>
        Progress<CurlTransferProgress> progress = new(report =>
            Console.WriteLine($"{report.Phase}: {report.DownloadNow} of {report.DownloadTotal} bytes"));
        CurlRequest request = new(HttpMethod.Get, "https://example.com/big.iso") { Progress = progress };
        CurlResponse response = await client.SendAsync(request, cancellationToken);
        await response.Body.CopyToAsync(Stream.Null, cancellationToken);
        // </sample>
    }

    public static async Task UploadMultipartAsync(CurlClient client, CancellationToken cancellationToken)
    {
        // <sample multipart>
        CurlMultipartContent form = new(
            CurlMimePart.Text("title", "Holiday"),
            CurlMimePart.Bytes("photo", new byte[] { 0xFF, 0xD8, 0xFF }) with
            {
                FileName = "photo.jpg",
                ContentType = "image/jpeg",
            });
        CurlRequest request = new(HttpMethod.Post, "https://example.com/albums") { Body = form };
        CurlResponse response = await client.SendAsync(request, cancellationToken);
        await response.Body.DisposeAsync();
        // </sample>
    }

    public static async Task UseWebSocketAsync(CurlClient client, CancellationToken cancellationToken)
    {
        // <sample websocket>
        await using CurlWebSocket socket = await client.ConnectWebSocketAsync("wss://example.com/chat", cancellationToken);
        await socket.SendTextAsync("hello", cancellationToken);
        await foreach (CurlWebSocketMessage message in socket.ReadMessagesAsync(cancellationToken))
        {
            if (message.Type == CurlWebSocketMessageType.Text)
            {
                Console.WriteLine(message.Text);
            }
        }

        await socket.CloseAsync(cancellationToken: cancellationToken);
        // </sample>
    }

    public static async Task RunConcurrentTransfersAsync(CurlClient client, CancellationToken cancellationToken)
    {
        // <sample concurrent>
        string[] urls = ["https://example.com/a", "https://example.com/b", "https://example.com/c"];
        Task<CurlResponse>[] transfers = [.. urls.Select(url => client.SendAsync(new CurlRequest(HttpMethod.Get, url), cancellationToken))];
        foreach (CurlResponse response in await Task.WhenAll(transfers))
        {
            await using Stream body = response.Body;
            await body.CopyToAsync(Stream.Null, cancellationToken);
        }
        // </sample>
    }

    public static async Task UseWithHttpClientAsync(CurlClient client, CancellationToken cancellationToken)
    {
        // <sample httpclient>
        using CurlHttpMessageHandler handler = new(client)
        {
            AutomaticDecompression = DecompressionMethods.All,
            MaxAutomaticRedirections = 10,
        };
        using HttpClient http = new(handler);
        string text = await http.GetStringAsync("https://example.com/", cancellationToken);
        // </sample>
        Console.WriteLine(text);
    }
}
