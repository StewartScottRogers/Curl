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
/// asks the server to perform BINARY, SGA and ECHO; every other option is refused. The
/// first <c>WILL</c>, <c>WONT</c>, <c>DO</c> or <c>DONT</c> the server sends makes this
/// side offer its own options once, after the replies to that read: <c>IAC WILL BINARY</c>,
/// <c>IAC DO BINARY</c>, <c>IAC WILL SGA</c>, <c>IAC DO SGA</c>, less any already settled.
/// A server that never negotiates is never sent a command.
/// </para>
/// <para>
/// A subnegotiation is removed from the output. With no <c>-t</c> options, a
/// <c>NEW-ENVIRON</c> subnegotiation is answered with an empty <c>IS</c> list, a
/// <c>TTYPE</c> or <c>XDISPLOC</c> one ends the session, and any other is ignored.
/// </para>
/// </remarks>
internal sealed class TelnetReceiver
{
    private static readonly byte[] OptionsOffered =
        [TelnetByte.BinaryOption, TelnetByte.SuppressGoAheadOption];

    private static readonly byte[] EmptyNewEnvironmentReply =
    [
        TelnetByte.InterpretAsCommand, TelnetByte.SubnegotiationBegin, TelnetByte.NewEnvironmentOption,
        TelnetByte.IsQualifier, TelnetByte.InterpretAsCommand, TelnetByte.SubnegotiationEnd,
    ];

    private readonly TelnetOptionSide localOptions =
        new(TelnetByte.Will, TelnetByte.Wont, OptionsOffered);

    private readonly TelnetOptionSide remoteOptions =
        new(TelnetByte.Do, TelnetByte.Dont, [.. OptionsOffered, TelnetByte.EchoOption]);

    private readonly List<byte> subnegotiation = [];

    private TelnetReceiveState state;

    private bool serverNegotiated;

    private bool optionsOffered;

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
        foreach (byte value in received)
        {
            TelnetReceiveError error = ReceiveByte(value, data, replies);
            if (error != TelnetReceiveError.None)
            {
                return error;
            }
        }

        OfferOptionsOnce(replies);
        return TelnetReceiveError.None;
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
            }

            return;
        }

        if (value == TelnetByte.InterpretAsCommand)
        {
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

        if (value == TelnetByte.InterpretAsCommand)
        {
            data.Add(value);
        }
    }

    private void ReceiveNegotiation(byte option, List<byte> replies)
    {
        serverNegotiated = true;
        switch (state)
        {
            case TelnetReceiveState.Will:
                remoteOptions.ReceiveEnable(option, replies);
                break;
            case TelnetReceiveState.Wont:
                remoteOptions.ReceiveDisable(option, replies);
                break;
            case TelnetReceiveState.Do:
                localOptions.ReceiveEnable(option, replies);
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

        switch (subnegotiation[0])
        {
            case TelnetByte.TerminalTypeOption:
            case TelnetByte.XDisplayLocationOption:
                return TelnetReceiveError.SubnegotiationValueMissing;
            case TelnetByte.NewEnvironmentOption:
                replies.AddRange(EmptyNewEnvironmentReply);
                return TelnetReceiveError.None;
            default:
                return TelnetReceiveError.None;
        }
    }

    private void OfferOptionsOnce(List<byte> replies)
    {
        if (!serverNegotiated || optionsOffered)
        {
            return;
        }

        optionsOffered = true;
        foreach (byte option in OptionsOffered)
        {
            localOptions.RequestEnable(option, replies);
            remoteOptions.RequestEnable(option, replies);
        }
    }
}
