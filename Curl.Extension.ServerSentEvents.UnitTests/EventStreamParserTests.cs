using System.Text;

namespace Curl.Extension.ServerSentEvents;

/// <summary>
/// Each test pins one rule of WHATWG HTML §9.2.6 "Interpreting an event stream", or one of that
/// section's own examples; streams given as text are fed both whole and one byte at a time.
/// </summary>
[TestClass]
public sealed class EventStreamParserTests
{
    [TestMethod]
    public void Parse_SpecificationExampleOfThreeDataLines_JoinsThemWithLineFeeds()
    {
        // §9.2.6: "data: YHOO", "data: +2", "data: 10" fire one message event with data "YHOO\n+2\n10".
        AssertEvents("data: YHOO\ndata: +2\ndata: 10\n\n", new ServerSentEvent("message", "YHOO\n+2\n10", string.Empty));
    }

    [TestMethod]
    public void Parse_SpecificationExampleOfFourBlocks_FiresThreeEventsAndResetsTheLastEventId()
    {
        // §9.2.6: a comment block fires nothing; "id: 1" sets the last event ID; "id" with no value resets it;
        // "data:  third event" keeps one of its two leading spaces.
        AssertEvents(
            ": test stream\n\ndata: first event\nid: 1\n\ndata:second event\nid\n\ndata:  third event\n\n",
            new ServerSentEvent("message", "first event", "1"),
            new ServerSentEvent("message", "second event", string.Empty),
            new ServerSentEvent("message", " third event", string.Empty));
    }

    [TestMethod]
    public void Parse_SpecificationExampleOfEmptyData_FiresTwoEventsAndDiscardsTheUnfinishedBlock()
    {
        // §9.2.6: "data" alone fires empty data; two "data" lines fire a single LF; the last block lacks its blank line.
        AssertEvents(
            "data\n\ndata\ndata\n\ndata:",
            new ServerSentEvent("message", string.Empty, string.Empty),
            new ServerSentEvent("message", "\n", string.Empty));
    }

    [TestMethod]
    public void Parse_SpecificationExampleOfOptionalSpace_FiresTwoIdenticalEvents()
    {
        // §9.2.6: "data:test" and "data: test" are the same, because one space after the colon is dropped.
        AssertEvents(
            "data:test\n\ndata: test\n\n",
            new ServerSentEvent("message", "test", string.Empty),
            new ServerSentEvent("message", "test", string.Empty));
    }

    [TestMethod]
    public void Parse_StreamStartingWithByteOrderMark_StripsItOnce()
    {
        // §9.2.6: the stream is decoded with "UTF-8 decode", which strips one leading byte order mark; a later
        // one is part of a field name, so "﻿data" is an unknown field.
        AssertEvents("﻿data: first\n\n﻿data: second\n\n", new ServerSentEvent("message", "first", string.Empty));
        AssertEvents("﻿﻿data: x\n\n");
    }

    [TestMethod]
    public void Parse_InvalidUtf8_DecodesItAsReplacementCharacters()
    {
        // §9.2.6: "UTF-8 decode" replaces an invalid byte with U+FFFD.
        var events = new EventStreamParser().Parse([.. "data: a"u8, 0xFF, .. "b\n\n"u8]);

        Assert.AreEqual("a�b", events.Single().Data);
    }

    [TestMethod]
    public void Parse_CarriageReturnLineFeedAndCarriageReturnAlone_EachEndALine()
    {
        // §9.2.6: end-of-line = ( cr lf / cr / lf ).
        AssertEvents("data: a\r\ndata: b\rdata: c\n\r\n", new ServerSentEvent("message", "a\nb\nc", string.Empty));
    }

    [TestMethod]
    public void Parse_CarriageReturnAndLineFeedInSeparateChunks_EndOneLine()
    {
        // §9.2.6: a CRLF is one end-of-line even when the CR ends one chunk and the LF starts the next.
        var parser = new EventStreamParser();

        Assert.IsEmpty(parser.Parse("data: a\r"u8));
        Assert.IsEmpty(parser.Parse("\ndata: b\r"u8));
        var events = parser.Parse("\n\r\n"u8);

        Assert.AreEqual("a\nb", events.Single().Data);
    }

    [TestMethod]
    public void Parse_MultibyteCharacterSplitAcrossChunks_DecodesIt()
    {
        // §9.2.6: the stream is one UTF-8 byte sequence, however it arrives.
        var bytes = Encoding.UTF8.GetBytes("data: é€\n\n");
        var parser = new EventStreamParser();

        Assert.IsEmpty(parser.Parse(bytes.AsSpan(0, 7)));
        var events = parser.Parse(bytes.AsSpan(7));

        Assert.AreEqual("é€", events.Single().Data);
    }

    [TestMethod]
    public void Parse_CommentLine_IsIgnored()
    {
        // §9.2.6: "If the line starts with a U+003A COLON character (:) - Ignore the line."
        AssertEvents(": comment\ndata: x\n:another\n\n", new ServerSentEvent("message", "x", string.Empty));
    }

    [TestMethod]
    public void Parse_ValueAfterColon_LosesOnlyOneLeadingSpaceAndKeepsLaterColons()
    {
        // §9.2.6: the field name ends at the first colon; one U+0020 SPACE after it is removed from the value.
        AssertEvents("data:   a: b\n\n", new ServerSentEvent("message", "  a: b", string.Empty));
    }

    [TestMethod]
    public void Parse_EventField_SetsTheTypeOfTheNextEventOnly()
    {
        // §9.2.6: "event" sets the event type buffer, which each dispatch empties; an empty type is "message".
        AssertEvents(
            "event: add\ndata: 1\n\ndata: 2\n\nevent\ndata: 3\n\n",
            new ServerSentEvent("add", "1", string.Empty),
            new ServerSentEvent("message", "2", string.Empty),
            new ServerSentEvent("message", "3", string.Empty));
    }

    [TestMethod]
    public void Parse_EventTypeWithoutData_IsNotDispatchedAndDoesNotCarryOver()
    {
        // §9.2.6 "dispatch the event", step 2: with an empty data buffer, the data and event type buffers are
        // emptied and nothing is dispatched.
        AssertEvents("event: lost\n\ndata: x\n\n", new ServerSentEvent("message", "x", string.Empty));
    }

    [TestMethod]
    public void Parse_IdWithoutData_SetsTheLastEventIdThatLaterEventsCarry()
    {
        // §9.2.6 "dispatch the event", step 1: the last event ID is set even when no event fires, and the last
        // event ID buffer is never emptied, so later events carry it.
        var parser = new EventStreamParser();

        Assert.IsEmpty(parser.Parse("id: 7\n\n"u8));
        Assert.AreEqual("7", parser.LastEventId);
        var events = parser.Parse("data: a\n\ndata: b\n\n"u8);

        CollectionAssert.AreEqual(
            new[] { new ServerSentEvent("message", "a", "7"), new ServerSentEvent("message", "b", "7") },
            events.ToArray());
    }

    [TestMethod]
    public void Parse_IdBeforeTheBlankLine_IsNotTheLastEventIdUntilDispatch()
    {
        // §9.2.6: "id" sets the last event ID buffer; only a dispatch copies it to the last event ID string, and
        // a stream ending before the blank line discards the pending event.
        var parser = new EventStreamParser();

        var events = parser.Parse("id: 9\ndata: never\n"u8);

        Assert.IsEmpty(events);
        Assert.AreEqual(string.Empty, parser.LastEventId);
    }

    [TestMethod]
    public void Parse_IdContainingNull_IsIgnored()
    {
        // §9.2.6: "If the field value does not contain U+0000 NULL, then set the last event ID buffer".
        AssertEvents(
            "id: 1\ndata: a\n\nid: 2\0x\ndata: b\n\n",
            new ServerSentEvent("message", "a", "1"),
            new ServerSentEvent("message", "b", "1"));
    }

    [TestMethod]
    public void Parse_RetryOfAsciiDigits_SetsTheReconnectionTimeWithoutDispatch()
    {
        // §9.2.6: "retry" made only of ASCII digits is the reconnection time in milliseconds.
        var parser = new EventStreamParser();

        Assert.IsNull(parser.ReconnectionTimeMilliseconds);
        Assert.IsEmpty(parser.Parse("retry: 03000\n"u8));

        Assert.AreEqual(3000L, parser.ReconnectionTimeMilliseconds);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("1a")]
    [DataRow("-1")]
    [DataRow("+1")]
    [DataRow(" 1")]
    [DataRow("1 ")]
    [DataRow("1.5")]
    [DataRow("١")]
    [DataRow("99999999999999999999")]
    public void Parse_RetryNotOfAsciiDigitsOnly_IsIgnored(string value)
    {
        // §9.2.6: "If the field value consists of only ASCII digits ... Otherwise, ignore the field." A value too
        // large for a 64-bit count of milliseconds is ignored too (ADR-1011).
        var parser = new EventStreamParser();
        parser.Parse("retry: 5\n"u8);

        parser.Parse(Encoding.UTF8.GetBytes("retry: " + value + "\n"));

        Assert.AreEqual(5L, parser.ReconnectionTimeMilliseconds);
    }

    [TestMethod]
    [DataRow("Data: x")]
    [DataRow("data : x")]
    [DataRow("unknown: x")]
    public void Parse_FieldOfAnotherName_IsIgnored(string line)
    {
        // §9.2.6: field names are compared exactly; any other field is ignored.
        AssertEvents(line + "\n\n");
    }

    [TestMethod]
    public void EndStream_DiscardsThePendingEventAndKeepsTheEventSourceState()
    {
        // §9.2.6: at the end of the file pending data is discarded; the last event ID and reconnection time persist.
        var parser = new EventStreamParser();
        Assert.HasCount(1, parser.Parse("retry: 9\nid: 3\ndata: a\n\nevent: x\ndata: cut\r"u8));

        parser.EndStream();
        var events = parser.Parse([0xEF, 0xBB, 0xBF, .. "data: b\n\n"u8]);

        Assert.HasCount(1, events);
        Assert.AreEqual(new ServerSentEvent("message", "b", "3"), events[0]);
        Assert.AreEqual(9L, parser.ReconnectionTimeMilliseconds);
    }

    private static void AssertEvents(string stream, params ServerSentEvent[] expected)
    {
        var bytes = Encoding.UTF8.GetBytes(stream);

        var whole = new EventStreamParser().Parse(bytes);
        var byteByByteParser = new EventStreamParser();
        var byteByByte = new List<ServerSentEvent>();
        foreach (var value in bytes)
        {
            byteByByte.AddRange(byteByByteParser.Parse([value]));
        }

        CollectionAssert.AreEqual(expected, whole.ToArray(), "fed whole");
        CollectionAssert.AreEqual(expected, byteByByte, "fed one byte at a time");
    }
}
