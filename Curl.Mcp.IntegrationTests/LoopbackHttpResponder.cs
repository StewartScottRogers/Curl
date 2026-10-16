using System.Net.Sockets;
using System.Text;

namespace Curl.Mcp;

/// <summary>
/// Answers one HTTP request on a loopback listener the test opened, with a fixed response, and gives
/// back the request head it read.
/// </summary>
internal static class LoopbackHttpResponder
{
    /// <summary>Accepts one connection, reads the request head, writes <paramref name="response" /> and closes.</summary>
    /// <param name="listener">The started loopback listener.</param>
    /// <param name="response">The whole response, head and body.</param>
    /// <returns>The request head as read, up to and including the blank line.</returns>
    internal static async Task<string> ServeOneResponseAsync(TcpListener listener, string response)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync();
        NetworkStream stream = client.GetStream();
        StringBuilder request = new();
        byte[] buffer = new byte[4096];
        while (!request.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
        {
            int read = await stream.ReadAsync(buffer);
            if (read == 0)
            {
                break;
            }

            request.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }

        await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
        await stream.FlushAsync();
        return request.ToString();
    }
}
