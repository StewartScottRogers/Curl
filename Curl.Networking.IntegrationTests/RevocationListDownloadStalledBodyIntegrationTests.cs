using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Curl.Networking;

/// <summary>Pins that a CRL server that sends its headers and then goes silent cannot hang <see cref="RevocationListDownload.Fetch" /> (BL-5429).</summary>
[TestClass]
public sealed class RevocationListDownloadStalledBodyIntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task Fetch_ServerSendsHeadersThenStalls_EndsWithinTheTokensDeadline()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var release = new CancellationTokenSource();
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(release.Token);
            var stream = client.GetStream();
            var buffer = new byte[4096];
            _ = await stream.ReadAsync(buffer, release.Token);
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 1000\r\n\r\nabc"), release.Token);
            await Task.Delay(Timeout.Infinite, release.Token).ContinueWith(static _ => { }, TaskScheduler.Default);
        });

        using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var watch = Stopwatch.StartNew();
        var fetch = Task.Run(() => RevocationListDownload.Fetch(new Uri($"http://127.0.0.1:{port}/list.crl"), deadline.Token));
        var finished = await Task.WhenAny(fetch, Task.Delay(TimeSpan.FromSeconds(15)));
        watch.Stop();
        release.Cancel();
        await server;

        Assert.AreSame(fetch, finished, "Fetch is still blocked in its body read after the token's deadline.");
        Assert.IsTrue(fetch.IsFaulted || fetch.IsCanceled, "Fetch must not return a truncated list.");
        Assert.IsTrue(watch.Elapsed < TimeSpan.FromSeconds(10), $"Fetch took {watch.Elapsed}.");
    }
}
