namespace Curl.Protocol.Telnet;

/// <summary>
/// The RFC 1143 state of every option on one side of the connection: either the options
/// this side performs, which the peer asks for with <c>DO</c> and <c>DONT</c> and this side
/// answers with <c>WILL</c> and <c>WONT</c>, or the options the peer performs, the other
/// way round.
/// </summary>
/// <param name="enableCommand">The command this side sends to enable an option.</param>
/// <param name="disableCommand">The command this side sends to disable an option.</param>
/// <param name="preferredOptions">The options this side agrees to enable.</param>
/// <param name="log">Where each command sent, and each request or offer refused, is logged.</param>
internal sealed class TelnetOptionSide(
    byte enableCommand,
    byte disableCommand,
    byte[] preferredOptions,
    TelnetDiagnosticLog log)
{
    private readonly TelnetOptionState[] states = new TelnetOptionState[256];

    /// <summary>
    /// Answers the peer's request or offer to enable <paramref name="option" />.
    /// </summary>
    /// <param name="option">The option named.</param>
    /// <param name="replies">Receives any reply to send.</param>
    /// <returns>
    /// <see langword="true" /> when this enabled the option, whether answering an offer or
    /// confirming this side's own request; <see langword="false" /> when it was already
    /// enabled or is refused.
    /// </returns>
    public bool ReceiveEnable(byte option, List<byte> replies)
    {
        TelnetOptionState previous = states[option];
        if (previous != TelnetOptionState.No)
        {
            states[option] = TelnetOptionState.Yes;
            return previous == TelnetOptionState.WantYes;
        }

        if (Array.IndexOf(preferredOptions, option) < 0)
        {
            log.OptionRefused(disableCommand, option);
            AppendCommand(replies, disableCommand, option);
            return false;
        }

        states[option] = TelnetOptionState.Yes;
        AppendCommand(replies, enableCommand, option);
        return true;
    }

    /// <summary>
    /// Answers the peer's request or refusal to disable <paramref name="option" />.
    /// </summary>
    /// <param name="option">The option named.</param>
    /// <param name="replies">Receives any reply to send.</param>
    public void ReceiveDisable(byte option, List<byte> replies)
    {
        if (states[option] == TelnetOptionState.Yes)
        {
            AppendCommand(replies, disableCommand, option);
        }

        states[option] = TelnetOptionState.No;
    }

    /// <summary>
    /// Asks the peer to enable <paramref name="option" /> unless it is already enabled or
    /// asked for.
    /// </summary>
    /// <param name="option">The option to ask for.</param>
    /// <param name="replies">Receives the request.</param>
    public void RequestEnable(byte option, List<byte> replies)
    {
        if (states[option] == TelnetOptionState.No)
        {
            states[option] = TelnetOptionState.WantYes;
            AppendCommand(replies, enableCommand, option);
        }
    }

    private void AppendCommand(List<byte> replies, byte command, byte option)
    {
        log.OptionSent(command, option);
        replies.Add(TelnetByte.InterpretAsCommand);
        replies.Add(command);
        replies.Add(option);
    }
}
