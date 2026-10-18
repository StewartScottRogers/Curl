using System.Globalization;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Info;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib2032.c</c>'s per-handle state and its write callback <c>cb2032</c>: the bytes each of <see cref="Handles"/> handles
/// received, the socket each was first seen on, and <c>ntlmcb_res</c>, which becomes
/// <see cref="LibTestErrorCodes.MajorBad"/> when a handle moved to another socket (known bug #37). The
/// <c>curl_easy_getinfo</c> failure check is left out: Libcurl answers <c>CURLINFO_LASTSOCKET</c> on a live handle.
/// </summary>
/// <param name="error">The program's stderr.</param>
public sealed class Lib2032Connections(TextWriter error)
{
    /// <summary><c>MAX_EASY_HANDLES</c>.</summary>
    public const int Handles = 3;

    /// <summary><c>CURL_SOCKET_BAD</c> as <c>CURLINFO_LASTSOCKET</c> gives it.</summary>
    public const long NoSocket = -1;

    private readonly TextWriter error = error ?? throw new ArgumentNullException(nameof(error));
    private readonly long[] sockets = [NoSocket, NoSocket, NoSocket];

    /// <summary>Gets the bytes each handle received, <c>ntlm_counter</c>.</summary>
    public int[] Received { get; } = new int[Handles];

    /// <summary>Gets <c>ntlmcb_res</c>: <see cref="CurlCode.Ok"/>, or <see cref="LibTestErrorCodes.MajorBad"/> once a write failed.</summary>
    public CurlCode CallbackResult { get; private set; }

    /// <summary><c>cb2032</c> for the handle at <paramref name="index"/>.</summary>
    /// <param name="index">The handle's index.</param>
    /// <param name="easy">The handle.</param>
    /// <param name="data">The chunk.</param>
    /// <returns>The chunk's size, or a size other than it (0, or 1 for an empty chunk) to fail the transfer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="easy"/> is <see langword="null"/>.</exception>
    public long Write(int index, CurlEasy easy, ReadOnlySpan<byte> data)
    {
        ArgumentNullException.ThrowIfNull(easy);
        Received[index] += data.Length;
        _ = easy.GetInfo(CurlInfo.Lastsocket, out object? value);
        if (KeptItsSocket(index, (long)value!))
        {
            return data.Length;
        }

        CallbackResult = LibTestErrorCodes.MajorBad;
        return data.Length > 0 ? 0 : 1;
    }

    private bool KeptItsSocket(int index, long socket)
    {
        if (socket == NoSocket || sockets[index] == socket)
        {
            return true;
        }

        if (sockets[index] == NoSocket)
        {
            sockets[index] = socket;
            return true;
        }

        error.Write(string.Create(CultureInfo.InvariantCulture, $"Handle {index} started on socket {sockets[index]} and moved to {socket}\n"));
        return false;
    }
}
