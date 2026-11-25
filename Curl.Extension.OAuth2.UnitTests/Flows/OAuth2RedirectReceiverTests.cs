using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OAuth2.Flows;

/// <summary>Pins that the redirect receiver stops reading at the blank line that ends the request head (RFC 9112 §2.1).</summary>
[TestClass]
public sealed class OAuth2RedirectReceiverTests
{
    [TestMethod]
    public async Task ReceiveAsync_HeadEndedAndBrowserWaitsForAnswer_AnswersWithoutReadingAgain()
    {
        var browser = new WaitingBrowserConnection("GET /cb?code=abc&state=xyz HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n");
        var pending = new AcceptingPendingConnection(ConnectResult.Connected(browser));

        var (redirect, failure) = await OAuth2RedirectReceiver.ReceiveAsync(pending, CancellationToken.None);

        Assert.IsNull(failure);
        Assert.IsNotNull(redirect);
        Assert.AreEqual(1, browser.ReadCount);
        StringAssert.StartsWith(browser.Written, "HTTP/1.1 200 OK");
    }

    /// <summary>A browser's connection that stays open after its request: a read past the data throws instead of returning 0.</summary>
    private sealed class WaitingBrowserConnection(string request) : IConnection
    {
        private readonly byte[] requestBytes = Encoding.ASCII.GetBytes(request);
        private readonly MemoryStream written = new();

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public int ReadCount { get; private set; }

        public string Written => Encoding.ASCII.GetString(written.ToArray());

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (ReadCount++ > 0)
            {
                throw new InvalidOperationException("The receiver read again after the request head ended.");
            }

            requestBytes.CopyTo(buffer);
            return ValueTask.FromResult(requestBytes.Length);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            written.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
