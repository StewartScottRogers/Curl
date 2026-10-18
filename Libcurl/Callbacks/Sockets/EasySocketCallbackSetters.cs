using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;

namespace Libcurl.Callbacks.Sockets;

/// <summary>
/// The setter rows of <c>CURLOPT_SOCKOPTFUNCTION</c>, <c>CURLOPT_OPENSOCKETFUNCTION</c> and
/// <c>CURLOPT_CLOSESOCKETFUNCTION</c> and their <c>*DATA</c> options (ADR-0573). Each function row takes its delegate
/// (<see cref="SocketOptionCallback"/>, <see cref="OpenSocketCallback"/>, <see cref="CloseSocketCallback"/>) or
/// <see langword="null"/> (libcurl's default) and answers <see cref="CurlCode.BadFunctionArgument"/> for any other value.
/// The engine calls them around each TCP connection it opens, with an opaque <see cref="CurlSocket"/> over the
/// connection: the open-socket callback once connected (<see cref="CurlSocket.Bad"/> fails with
/// <see cref="CurlCode.CouldntConnect"/>), the socket-option callback next (<see cref="SocketOptionResult.Error"/> fails
/// with <see cref="CurlCode.AbortedByCallback"/>), and the close-socket callback once when the connection closes
/// (ADR-0610). The managed callbacks carry their user data in their closures, so the <c>*DATA</c> options take any
/// value and change nothing.
/// </summary>
internal static class EasySocketCallbackSetters
{
    /// <summary>Adds the socket rows to <paramref name="setters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    internal static void AddTo(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.Sockoptfunction] = new(SetSocketOptionFunction);
        setters[CurlOption.Opensocketfunction] = new(SetOpenSocketFunction);
        setters[CurlOption.Closesocketfunction] = new(SetCloseSocketFunction);
        setters[CurlOption.Sockoptdata] = new(TakeUserData);
        setters[CurlOption.Opensocketdata] = new(TakeUserData);
        setters[CurlOption.Closesocketdata] = new(TakeUserData);
    }

    private static CurlCode SetSocketOptionFunction(ref EasyOptions options, object? value)
    {
        if (value is not (null or SocketOptionCallback))
        {
            return CurlCode.BadFunctionArgument;
        }

        options = options with { Callbacks = options.Callbacks with { SocketOption = (SocketOptionCallback?)value } };
        return CurlCode.Ok;
    }

    private static CurlCode SetOpenSocketFunction(ref EasyOptions options, object? value)
    {
        if (value is not (null or OpenSocketCallback))
        {
            return CurlCode.BadFunctionArgument;
        }

        options = options with { Callbacks = options.Callbacks with { OpenSocket = (OpenSocketCallback?)value } };
        return CurlCode.Ok;
    }

    private static CurlCode SetCloseSocketFunction(ref EasyOptions options, object? value)
    {
        if (value is not (null or CloseSocketCallback))
        {
            return CurlCode.BadFunctionArgument;
        }

        options = options with { Callbacks = options.Callbacks with { CloseSocket = (CloseSocketCallback?)value } };
        return CurlCode.Ok;
    }

    private static CurlCode TakeUserData(ref EasyOptions options, object? value) => CurlCode.Ok;
}
