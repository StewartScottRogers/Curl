namespace Curl.Http3;

/// <summary>
/// Opens the client's three unidirectional streams (RFC 9114 section 6.2, RFC 9204 section
/// 4.2) by writing what each starts with: the control stream's type and <c>SETTINGS</c>,
/// and the QPACK encoder and decoder streams' types.
/// </summary>
public static class Http3LocalUnidirectionalStreams
{
    /// <summary>
    /// Gets the <c>SETTINGS</c> curl's ngtcp2 build sends: nghttp3's defaults, which curl
    /// leaves as they are (ADR-0144), in the order nghttp3 writes them. The largest field
    /// section is 2^62 - 1 (unlimited), and QPACK's dynamic table capacity and blocked
    /// streams are 0.
    /// </summary>
    public static IReadOnlyList<Http3Setting> CurlSettings { get; } =
    [
        new(Http3SettingIdentifier.MaximumFieldSectionSize, Http3VariableLengthInteger.LargestValue),
        new(Http3SettingIdentifier.QpackMaximumTableCapacity, 0),
        new(Http3SettingIdentifier.QpackBlockedStreams, 0),
    ];

    /// <summary>
    /// Writes the control stream's type and a <c>SETTINGS</c> frame carrying
    /// <paramref name="settings" />.
    /// </summary>
    /// <param name="stream">The new control stream.</param>
    /// <param name="settings">The settings; <see cref="CurlSettings" /> for curl's.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the bytes are written and flushed.</returns>
    public static async ValueTask OpenControlStreamAsync(Stream stream, IReadOnlyList<Http3Setting> settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        List<byte> opening = [];
        Http3VariableLengthInteger.Write(opening, (long)Http3UnidirectionalStreamType.Control);
        opening.AddRange(new Http3SettingsFrame(settings).ToBytes());
        await WriteAndFlushAsync(stream, [.. opening], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the QPACK encoder stream's type.
    /// </summary>
    /// <param name="stream">The new encoder stream.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the byte is written and flushed.</returns>
    public static ValueTask OpenQpackEncoderStreamAsync(Stream stream, CancellationToken cancellationToken) =>
        WriteAndFlushAsync(stream, [(byte)Http3UnidirectionalStreamType.QpackEncoder], cancellationToken);

    /// <summary>
    /// Writes the QPACK decoder stream's type.
    /// </summary>
    /// <param name="stream">The new decoder stream.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the byte is written and flushed.</returns>
    public static ValueTask OpenQpackDecoderStreamAsync(Stream stream, CancellationToken cancellationToken) =>
        WriteAndFlushAsync(stream, [(byte)Http3UnidirectionalStreamType.QpackDecoder], cancellationToken);

    private static async ValueTask WriteAndFlushAsync(Stream stream, byte[] bytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
