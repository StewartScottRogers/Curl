using System.Globalization;

namespace Curl.Networking;

/// <summary>
/// Follows a chunked reply body to CONNECT one byte at a time, as curl 8.21.0's chunk parser
/// does when it ignores a <c>407</c>'s body (BL-862 Notes): chunk sizes in hex, chunk
/// extensions and any bytes up to the size line's LF skipped, chunk data, then the trailer
/// fields and the empty line that end the body. It tells when the body is complete, so the
/// next byte on the connection is the next reply, or why it is malformed.
/// </summary>
internal sealed class HttpProxyTunnelChunkedBody
{
    /// <summary>
    /// The most hex digits a chunk size may have before curl 8.21.0 gives up with
    /// <c>chunk hex-length longer than 16</c> (measured).
    /// </summary>
    public const int MaximumSizeDigits = 16;

    /// <summary>
    /// The message for a byte where curl expects a CR or LF and finds something else, which
    /// curl reports with the text of <see cref="Protocol.Abstractions.CurlExitCode.RecvError" /> (measured).
    /// </summary>
    public const string ReceiveFailureMessage = "Failure when receiving data from the peer";

    private readonly Func<byte, Position>[] _transitions;
    private string _sizeDigits = string.Empty;
    private long _dataLeft;
    private Position _position = Position.Size;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpProxyTunnelChunkedBody" /> class, at
    /// the first chunk's size.
    /// </summary>
    public HttpProxyTunnelChunkedBody() => _transitions = CreateTransitions();

    private enum Position
    {
        Size,
        SizeLineEnd,
        Data,
        DataEnd,
        TrailerLineStart,
        TrailerLine,
        TrailerLineCr,
        FinalLf,
        Complete,
        Malformed,
    }

    /// <summary>
    /// Gets a value indicating whether the body has ended with the empty line after the last chunk.
    /// </summary>
    public bool IsComplete => _position == Position.Complete;

    /// <summary>
    /// Gets the exit 56 message curl prints for the malformed body, or <see langword="null" />
    /// while it is well formed.
    /// </summary>
    public string? MalformedMessage { get; private set; }

    /// <summary>
    /// Takes the body's next byte.
    /// </summary>
    /// <param name="value">The byte read.</param>
    /// <returns>
    /// <see langword="true" /> while more of the body is to be read; <see langword="false" />
    /// once it is complete or malformed.
    /// </returns>
    public bool Accept(byte value)
    {
        _position = _transitions[(int)_position](value);
        return _position < Position.Complete;
    }

    // Where each byte leads from each position, in Position's order.
    private Func<byte, Position>[] CreateTransitions() =>
    [
        AfterSizeByte,
        AfterSizeLineByte,
        _ => AfterDataByte(),
        AfterDataEndByte,
        AfterTrailerLineStartByte,
        AfterTrailerLineByte,
        value => value == '\n' ? Position.TrailerLineStart : Malformed(ReceiveFailureMessage),
        value => value == '\n' ? Position.Complete : Malformed(ReceiveFailureMessage),
        _ => Position.Complete,
        _ => Position.Malformed,
    ];

    // A hex digit extends the size; the first other byte ends it and, being an LF, the line.
    private Position AfterSizeByte(byte value)
    {
        if (char.IsAsciiHexDigit((char)value))
        {
            return _sizeDigits.Length < MaximumSizeDigits
                ? AppendSizeDigit(value)
                : Malformed(string.Create(CultureInfo.InvariantCulture, $"chunk hex-length longer than {MaximumSizeDigits}"));
        }

        if (_sizeDigits.Length == 0)
        {
            return Malformed(string.Create(CultureInfo.InvariantCulture, $"chunk hex-length char not a hex digit: 0x{value:x}"));
        }

        var size = ulong.Parse(_sizeDigits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        if (size > long.MaxValue)
        {
            return Malformed($"invalid chunk size: '{_sizeDigits}'");
        }

        _dataLeft = (long)size;
        return AfterSizeLineByte(value);
    }

    private Position AppendSizeDigit(byte value)
    {
        _sizeDigits += (char)value;
        return Position.Size;
    }

    // A chunk extension, or anything else before the LF, is skipped; the LF starts the data,
    // or the trailer after the last chunk.
    private Position AfterSizeLineByte(byte value)
    {
        if (value != '\n')
        {
            return Position.SizeLineEnd;
        }

        _sizeDigits = string.Empty;
        return _dataLeft == 0 ? Position.TrailerLineStart : Position.Data;
    }

    private Position AfterDataByte() => --_dataLeft == 0 ? Position.DataEnd : Position.Data;

    // After the data: CRs are passed over, an LF starts the next size, anything else fails.
    private Position AfterDataEndByte(byte value) => value switch
    {
        (byte)'\n' => Position.Size,
        (byte)'\r' => Position.DataEnd,
        _ => Malformed(ReceiveFailureMessage),
    };

    // An empty line ends the body: a CR waits for its LF, a bare LF ends it at once.
    private static Position AfterTrailerLineStartByte(byte value) => value switch
    {
        (byte)'\r' => Position.FinalLf,
        (byte)'\n' => Position.Complete,
        _ => Position.TrailerLine,
    };

    // A trailer field ends at its LF, or at a CR that an LF must follow.
    private static Position AfterTrailerLineByte(byte value) => value switch
    {
        (byte)'\r' => Position.TrailerLineCr,
        (byte)'\n' => Position.TrailerLineStart,
        _ => Position.TrailerLine,
    };

    private Position Malformed(string message)
    {
        MalformedMessage = message;
        return Position.Malformed;
    }
}
