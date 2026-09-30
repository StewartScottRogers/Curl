using System.Runtime.InteropServices;
using System.Text;

namespace Curl.Protocol.Telnet;

/// <summary>
/// Splits the bytes a telnet server sends into data for the output and the replies its
/// commands call for, byte for byte as curl 8.21.0's <c>lib/telnet.c</c> does.
/// </summary>
/// <remarks>
/// <para>
/// Data is written as received, with two exceptions measured against curl 8.21.0:
/// <c>IAC IAC</c> is written as one <c>0xFF</c>, and a NUL straight after a carriage
/// return is dropped. The byte after a carriage return is otherwise written whatever it
/// is, an <c>IAC</c> included, because curl does so. Every other command sequence is
/// removed.
/// </para>
/// <para>
/// Option negotiation follows RFC 1143. This side performs BINARY and SGA when asked and
/// asks the server to perform BINARY, SGA and ECHO; every other option is refused. A
/// <c>-t BINARY=0</c> takes BINARY out of all of these, so it is refused both ways and
/// left out of the offers. The
/// first <c>WILL</c>, <c>WONT</c>, <c>DO</c> or <c>DONT</c> the server sends makes this
/// side offer its own options once, after the replies to that read: <c>IAC WILL BINARY</c>,
/// <c>IAC DO BINARY</c>, <c>IAC WILL SGA</c>, <c>IAC DO SGA</c>, less any already settled.
/// A server that never negotiates is never sent a command.
/// </para>
/// <para>
/// Each of <c>TTYPE</c>, <c>NAWS</c>, <c>XDISPLOC</c> and <c>NEW-ENVIRON</c> that a
/// <c>-t</c> option (or, for <c>NEW-ENVIRON</c>, the <c>-u</c> user name) gave a value for
/// is one more option this side performs when asked, and offers after SGA, in
/// option-number order, with <c>IAC WILL</c>.
/// </para>
/// <para>
/// NAWS is performed when asked even without <c>-t WS</c>, as curl 8.21.0 does. Each time
/// it becomes enabled, by a <c>DO</c> answering this side's offer or by one answered with
/// <c>WILL</c>, this side sends <c>IAC SB NAWS</c>, the columns and rows as 16-bit
/// big-endian numbers with each <c>0xFF</c> doubled, and <c>IAC SE</c>; the size is 0x0
/// without <c>-t WS</c>.
/// </para>
/// <para>
/// A subnegotiation is removed from the output. A <c>TTYPE</c> or <c>XDISPLOC</c> one is
/// answered with <c>IS</c> and the value <c>-t</c> gave; with none it ends the session,
/// and so does a value over 1000 characters. A <c>NEW-ENVIRON</c> one is answered with an
/// <c>IS</c> list of every variable that fits, <c>USER</c> first when a user name was
/// given, then each <c>NEW_ENV</c>; empty when neither gave one. Any other is ignored.
/// </para>
/// </remarks>
internal sealed class TelnetReceiver
{
    /// <summary>The most characters curl sends as a terminal type or X display location.</summary>
    private const int MaximumSubnegotiationValueLength = 1000;

    /// <summary>
    /// curl 8.21.0 adds a <c>NEW-ENVIRON</c> variable to its reply only while the reply,
    /// less its closing <c>IAC SE</c>, stays shorter than this; a variable that does not
    /// fit is left out and the next one tried.
    /// </summary>
    private const int NewEnvironmentReplyLimit = 2042;

    private readonly TelnetOptionValues optionValues;

    /// <summary>The options this side asks the server to perform, as well as performing them.</summary>
    private readonly byte[] optionsOfferedBothWays;

    private readonly byte[] localOptionsOffered;

    private readonly TelnetOptionSide localOptions;

    private readonly TelnetOptionSide remoteOptions;

    private readonly TelnetDiagnosticLog log;

    private readonly TelnetTraceReporter trace;

    private readonly List<byte> subnegotiation = [];

    /// <summary>
    /// Where in the current read's data the run not yet reported begins: curl passes each
    /// run of data between commands to its writer, and traces it, separately.
    /// </summary>
    private int runStart;

    private TelnetReceiveState state;

    private bool serverNegotiated;

    private bool optionsOffered;

    /// <summary>The <c>WILL</c>, <c>WONT</c>, <c>DO</c> or <c>DONT</c> byte whose option is awaited.</summary>
    private byte negotiationCommand;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnetReceiver" /> class.
    /// </summary>
    /// <param name="optionValues">
    /// What the <c>-t</c> options supplied: each of <c>TTYPE</c>, <c>XDISPLOC</c> and
    /// <c>NEW-ENVIRON</c> that has a value is one more option this side performs and offers,
    /// and a refused <c>BINARY</c> is neither offered nor accepted.
    /// </param>
    /// <param name="log">Where each option negotiation received and sent is logged.</param>
    /// <param name="trace">
    /// Where each negotiation, command and subnegotiation received and sent, and each run of
    /// data, is reported for <c>-v</c> and <c>--trace</c>.
    /// </param>
    public TelnetReceiver(TelnetOptionValues optionValues, TelnetDiagnosticLog log, TelnetTraceReporter trace)
    {
        this.optionValues = optionValues;
        this.log = log;
        this.trace = trace;
        optionsOfferedBothWays = optionValues.BinaryRefused
            ? [TelnetByte.SuppressGoAheadOption]
            : [TelnetByte.BinaryOption, TelnetByte.SuppressGoAheadOption];
        localOptionsOffered = [.. optionsOfferedBothWays, .. OptionsWithValues(optionValues)];
        localOptions = new TelnetOptionSide(
            TelnetByte.Will,
            TelnetByte.Wont,
            [.. localOptionsOffered, TelnetByte.WindowSizeOption],
            log,
            trace);
        remoteOptions = new TelnetOptionSide(
            TelnetByte.Do,
            TelnetByte.Dont,
            [.. optionsOfferedBothWays, TelnetByte.EchoOption],
            log,
            trace);
    }

    /// <summary>
    /// Processes the bytes of one read.
    /// </summary>
    /// <param name="received">The bytes read from the server.</param>
    /// <param name="data">Receives the bytes to write to the output.</param>
    /// <param name="replies">Receives the bytes to send to the server.</param>
    /// <returns>
    /// <see cref="TelnetReceiveError.None" />, or the error that ends the session, in
    /// which case <paramref name="data" /> and <paramref name="replies" /> hold what came
    /// before it and the rest of <paramref name="received" /> is left unprocessed.
    /// </returns>
    public TelnetReceiveError Receive(ReadOnlySpan<byte> received, List<byte> data, List<byte> replies)
    {
        runStart = data.Count;
        foreach (byte value in received)
        {
            TelnetReceiveError error = ReceiveByte(value, data, replies);
            if (error != TelnetReceiveError.None)
            {
                return error;
            }
        }

        ReportRun(data);
        OfferOptionsOnce(replies);
        return TelnetReceiveError.None;
    }

    /// <summary>
    /// Reports the data added since the last run ended, if any, as one run; an error can
    /// only arise inside a subnegotiation, after the run before it was reported.
    /// </summary>
    private void ReportRun(List<byte> data)
    {
        if (data.Count > runStart)
        {
            trace.DataReceived(CollectionsMarshal.AsSpan(data)[runStart..]);
            runStart = data.Count;
        }
    }

    private TelnetReceiveError ReceiveByte(byte value, List<byte> data, List<byte> replies)
    {
        if (state == TelnetReceiveState.SubnegotiationCommand)
        {
            return ReceiveSubnegotiationCommand(value, replies);
        }

        if (state == TelnetReceiveState.Subnegotiation)
        {
            ReceiveSubnegotiation(value);
        }
        else if (state == TelnetReceiveState.Command)
        {
            ReceiveCommand(value, data);
        }
        else if (state is TelnetReceiveState.Data or TelnetReceiveState.AfterCarriageReturn)
        {
            ReceiveData(value, data);
        }
        else
        {
            ReceiveNegotiation(value, replies);
        }

        return TelnetReceiveError.None;
    }

    private void ReceiveData(byte value, List<byte> data)
    {
        if (state == TelnetReceiveState.AfterCarriageReturn)
        {
            state = TelnetReceiveState.Data;
            if (value != TelnetByte.Nul)
            {
                data.Add(value);
                return;
            }

            ReportRun(data);
            return;
        }

        if (value == TelnetByte.InterpretAsCommand)
        {
            ReportRun(data);
            state = TelnetReceiveState.Command;
            return;
        }

        data.Add(value);
        if (value == TelnetByte.CarriageReturn)
        {
            state = TelnetReceiveState.AfterCarriageReturn;
        }
    }

    private void ReceiveCommand(byte value, List<byte> data)
    {
        state = value switch
        {
            TelnetByte.Will => TelnetReceiveState.Will,
            TelnetByte.Wont => TelnetReceiveState.Wont,
            TelnetByte.Do => TelnetReceiveState.Do,
            TelnetByte.Dont => TelnetReceiveState.Dont,
            TelnetByte.SubnegotiationBegin => TelnetReceiveState.Subnegotiation,
            _ => TelnetReceiveState.Data,
        };

        negotiationCommand = value;
        if (value == TelnetByte.InterpretAsCommand)
        {
            data.Add(value);
        }
        else if (state == TelnetReceiveState.Data)
        {
            trace.CommandReceived(value);
        }
    }

    private void ReceiveNegotiation(byte option, List<byte> replies)
    {
        serverNegotiated = true;
        log.OptionReceived(negotiationCommand, option);
        trace.OptionReceived(negotiationCommand, option);
        switch (state)
        {
            case TelnetReceiveState.Will:
                _ = remoteOptions.ReceiveEnable(option, replies);
                break;
            case TelnetReceiveState.Wont:
                remoteOptions.ReceiveDisable(option, replies);
                break;
            case TelnetReceiveState.Do:
                if (localOptions.ReceiveEnable(option, replies) && option == TelnetByte.WindowSizeOption)
                {
                    AppendWindowSize(replies);
                }

                break;
            default:
                localOptions.ReceiveDisable(option, replies);
                break;
        }

        state = TelnetReceiveState.Data;
    }

    private void ReceiveSubnegotiation(byte value)
    {
        if (value == TelnetByte.InterpretAsCommand)
        {
            state = TelnetReceiveState.SubnegotiationCommand;
            return;
        }

        subnegotiation.Add(value);
    }

    private TelnetReceiveError ReceiveSubnegotiationCommand(byte value, List<byte> replies)
    {
        if (value == TelnetByte.InterpretAsCommand)
        {
            subnegotiation.Add(value);
            state = TelnetReceiveState.Subnegotiation;
            return TelnetReceiveError.None;
        }

        if (value != TelnetByte.SubnegotiationEnd)
        {
            return TelnetReceiveError.MalformedSubnegotiation;
        }

        state = TelnetReceiveState.Data;
        TelnetReceiveError error = AnswerSubnegotiation(replies);
        subnegotiation.Clear();
        return error;
    }

    private TelnetReceiveError AnswerSubnegotiation(List<byte> replies)
    {
        if (subnegotiation.Count == 0)
        {
            return TelnetReceiveError.None;
        }

        trace.SubnegotiationReceived(CollectionsMarshal.AsSpan(subnegotiation));
        switch (subnegotiation[0])
        {
            case TelnetByte.TerminalTypeOption:
                return AnswerWithValue(
                    TelnetByte.TerminalTypeOption,
                    optionValues.TerminalType,
                    TelnetReceiveError.TerminalTypeTooLong,
                    replies);
            case TelnetByte.XDisplayLocationOption:
                return AnswerWithValue(
                    TelnetByte.XDisplayLocationOption,
                    optionValues.XDisplayLocation,
                    TelnetReceiveError.XDisplayLocationTooLong,
                    replies);
            case TelnetByte.NewEnvironmentOption:
                AnswerNewEnvironment(replies);
                return TelnetReceiveError.None;
            default:
                return TelnetReceiveError.None;
        }
    }

    private TelnetReceiveError AnswerWithValue(
        byte option,
        string? value,
        TelnetReceiveError tooLong,
        List<byte> replies)
    {
        if (value is null)
        {
            return TelnetReceiveError.SubnegotiationValueMissing;
        }

        if (value.Length > MaximumSubnegotiationValueLength)
        {
            return tooLong;
        }

        int start = replies.Count;
        AppendSubnegotiationStart(option, replies);
        replies.AddRange(Encoding.ASCII.GetBytes(value));
        ReportSubnegotiationSent(replies, start);
        AppendSubnegotiationEnd(replies);
        return TelnetReceiveError.None;
    }

    private void AnswerNewEnvironment(List<byte> replies)
    {
        int start = replies.Count;
        AppendSubnegotiationStart(TelnetByte.NewEnvironmentOption, replies);
        foreach (string variable in optionValues.EnvironmentVariables)
        {
            if (replies.Count - start + variable.Length + 1 < NewEnvironmentReplyLimit)
            {
                AppendEnvironmentVariable(variable, replies);
            }
        }

        ReportSubnegotiationSent(replies, start);
        AppendSubnegotiationEnd(replies);
    }

    /// <summary>
    /// Reports the subnegotiation appended to <paramref name="replies" /> from
    /// <paramref name="start" />, less its opening <c>IAC SB</c>; its <c>IAC SE</c> is not
    /// appended yet.
    /// </summary>
    private void ReportSubnegotiationSent(List<byte> replies, int start) =>
        trace.SubnegotiationSent(CollectionsMarshal.AsSpan(replies)[(start + 2)..]);

    private static void AppendEnvironmentVariable(string variable, List<byte> replies)
    {
        int comma = variable.IndexOf(',', StringComparison.Ordinal);
        replies.Add(TelnetByte.EnvironmentVariable);
        if (comma < 0)
        {
            replies.AddRange(Encoding.ASCII.GetBytes(variable));
            return;
        }

        replies.AddRange(Encoding.ASCII.GetBytes(variable[..comma]));
        replies.Add(TelnetByte.EnvironmentValue);
        replies.AddRange(Encoding.ASCII.GetBytes(variable[(comma + 1)..]));
    }

    private void AppendWindowSize(List<byte> replies)
    {
        TelnetWindowSize size = optionValues.WindowSize ?? new TelnetWindowSize(0, 0);
        trace.SubnegotiationSent(
        [
            TelnetByte.WindowSizeOption,
            (byte)(size.Columns >> 8),
            (byte)size.Columns,
            (byte)(size.Rows >> 8),
            (byte)size.Rows,
        ]);
        replies.Add(TelnetByte.InterpretAsCommand);
        replies.Add(TelnetByte.SubnegotiationBegin);
        replies.Add(TelnetByte.WindowSizeOption);
        AppendDoublingInterpretAsCommand((byte)(size.Columns >> 8), replies);
        AppendDoublingInterpretAsCommand((byte)size.Columns, replies);
        AppendDoublingInterpretAsCommand((byte)(size.Rows >> 8), replies);
        AppendDoublingInterpretAsCommand((byte)size.Rows, replies);
        AppendSubnegotiationEnd(replies);
    }

    private static void AppendDoublingInterpretAsCommand(byte value, List<byte> replies)
    {
        replies.Add(value);
        if (value == TelnetByte.InterpretAsCommand)
        {
            replies.Add(value);
        }
    }

    private static void AppendSubnegotiationStart(byte option, List<byte> replies)
    {
        replies.Add(TelnetByte.InterpretAsCommand);
        replies.Add(TelnetByte.SubnegotiationBegin);
        replies.Add(option);
        replies.Add(TelnetByte.IsQualifier);
    }

    private static void AppendSubnegotiationEnd(List<byte> replies)
    {
        replies.Add(TelnetByte.InterpretAsCommand);
        replies.Add(TelnetByte.SubnegotiationEnd);
    }

    private static IEnumerable<byte> OptionsWithValues(TelnetOptionValues optionValues)
    {
        if (optionValues.TerminalType is not null)
        {
            yield return TelnetByte.TerminalTypeOption;
        }

        if (optionValues.WindowSize is not null)
        {
            yield return TelnetByte.WindowSizeOption;
        }

        if (optionValues.XDisplayLocation is not null)
        {
            yield return TelnetByte.XDisplayLocationOption;
        }

        if (optionValues.EnvironmentVariables.Count > 0)
        {
            yield return TelnetByte.NewEnvironmentOption;
        }
    }

    private void OfferOptionsOnce(List<byte> replies)
    {
        if (!serverNegotiated || optionsOffered)
        {
            return;
        }

        optionsOffered = true;
        foreach (byte option in localOptionsOffered)
        {
            localOptions.RequestEnable(option, replies);
            if (Array.IndexOf(optionsOfferedBothWays, option) >= 0)
            {
                remoteOptions.RequestEnable(option, replies);
            }
        }
    }
}
