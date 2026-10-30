using System.Globalization;
using System.Text;

namespace Curl.Extension.ServerSentEvents;

/// <summary>
/// Interprets <c>text/event-stream</c> bytes as WHATWG HTML §9.2.6 "Interpreting an event stream"
/// says, fed in chunks of any size. Bytes are decoded as UTF-8 with one leading byte order mark
/// stripped, lines end at CRLF, LF or CR, and an empty line dispatches the event built so far.
/// A line or event the stream ends in the middle of is never dispatched.
/// </summary>
public sealed class EventStreamParser
{
    private const string DefaultEventType = "message";
    private const char ByteOrderMark = '﻿';

    private readonly Decoder _decoder = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false).GetDecoder();
    private readonly StringBuilder _line = new();
    private readonly StringBuilder _data = new();
    private string _eventType = string.Empty;
    private string _lastEventIdBuffer = string.Empty;
    private bool _isAtStreamStart = true;
    private bool _isAfterCarriageReturn;

    /// <summary>The last event ID string: the last <c>id</c> field's value as of the last dispatch, empty until one is set.</summary>
    public string LastEventId { get; private set; } = string.Empty;

    /// <summary>The reconnection time the last valid <c>retry</c> field set, in milliseconds; <see langword="null"/> until one does.</summary>
    public long? ReconnectionTimeMilliseconds { get; private set; }

    /// <summary>Reads the next chunk of the stream and returns the events it completes, in order.</summary>
    /// <param name="bytes">The next bytes of the stream; a UTF-8 sequence or a CRLF may be split across chunks.</param>
    /// <returns>The events whose final empty line this chunk holds; empty when it holds none.</returns>
    public IReadOnlyList<ServerSentEvent> Parse(ReadOnlySpan<byte> bytes)
    {
        var characters = new char[_decoder.GetCharCount(bytes, flush: false)];
        _decoder.GetChars(bytes, characters, flush: false);
        var events = new List<ServerSentEvent>();
        foreach (var character in characters)
        {
            ReadCharacter(character, events);
        }

        return events;
    }

    /// <summary>
    /// Ends the stream the parser has been reading, as a closed connection does: the pending line and event are
    /// discarded (§9.2.6, "Once the end of the file is reached, any pending data must be discarded") and the next
    /// chunk starts a new stream, its leading byte order mark dropped. <see cref="LastEventId"/>, the last event ID
    /// buffer and <see cref="ReconnectionTimeMilliseconds"/> belong to the event source and are kept for the reconnection.
    /// </summary>
    public void EndStream()
    {
        _decoder.Reset();
        _line.Clear();
        _data.Clear();
        _eventType = string.Empty;
        _isAtStreamStart = true;
        _isAfterCarriageReturn = false;
    }

    private void ReadCharacter(char character, List<ServerSentEvent> events)
    {
        if (IsSkipped(character))
        {
            return;
        }

        if (character is '\r' or '\n')
        {
            ReadLine(_line.ToString(), events);
            _line.Clear();
            return;
        }

        _line.Append(character);
    }

    /// <summary>Whether the character is the stream's leading byte order mark or the LF of a CRLF, which read as nothing.</summary>
    private bool IsSkipped(char character)
    {
        var isLeadingByteOrderMark = _isAtStreamStart && character == ByteOrderMark;
        _isAtStreamStart = false;
        var isLineFeedOfCrLf = _isAfterCarriageReturn && character == '\n';
        _isAfterCarriageReturn = character == '\r';
        return isLeadingByteOrderMark || isLineFeedOfCrLf;
    }

    private void ReadLine(string line, List<ServerSentEvent> events)
    {
        if (line.Length == 0)
        {
            Dispatch(events);
            return;
        }

        var colon = line.IndexOf(':');
        if (colon == 0)
        {
            return;
        }

        if (colon < 0)
        {
            ReadField(line, string.Empty);
            return;
        }

        var value = line[(colon + 1)..];
        ReadField(line[..colon], value.StartsWith(' ') ? value[1..] : value);
    }

    private void ReadField(string field, string value)
    {
        switch (field)
        {
            case "event":
                _eventType = value;
                break;
            case "data":
                _data.Append(value).Append('\n');
                break;
            case "id" when !value.Contains('\0'):
                _lastEventIdBuffer = value;
                break;
            case "retry" when long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds):
                ReconnectionTimeMilliseconds = milliseconds;
                break;
        }
    }

    private void Dispatch(List<ServerSentEvent> events)
    {
        LastEventId = _lastEventIdBuffer;
        if (_data.Length > 0)
        {
            // Every data field appends its value and an LF, so the buffer ends in the LF the specification drops.
            _data.Length--;
            events.Add(new ServerSentEvent(_eventType.Length == 0 ? DefaultEventType : _eventType, _data.ToString(), LastEventId));
            _data.Clear();
        }

        _eventType = string.Empty;
    }
}
