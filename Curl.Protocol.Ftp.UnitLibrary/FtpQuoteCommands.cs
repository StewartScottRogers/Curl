namespace Curl.Protocol.Ftp;

/// <summary>
/// The <c>-Q</c>/<c>--quote</c> values sorted by when curl 8.21.0 sends them.
/// </summary>
/// <param name="AfterLogin">
/// Values with no <c>+</c> or <c>-</c> prefix: sent after <c>PWD</c>, before the first
/// <c>CWD</c>.
/// </param>
/// <param name="BeforeTransfer">
/// Values with a <c>+</c> prefix: sent after <c>TYPE</c>, before <c>SIZE</c>,
/// <c>LIST</c>, <c>STOR</c> or <c>APPE</c>; under <c>-I</c>, after <c>REST 0</c>.
/// </param>
/// <param name="AfterTransfer">
/// Values with a <c>-</c> prefix: sent once the transfer has succeeded, before
/// <c>QUIT</c>; never after a failed one.
/// </param>
internal sealed record FtpQuoteCommands(
    IReadOnlyList<FtpQuoteCommand> AfterLogin,
    IReadOnlyList<FtpQuoteCommand> BeforeTransfer,
    IReadOnlyList<FtpQuoteCommand> AfterTransfer)
{
    private const int AfterLoginStage = 0;

    private const int BeforeTransferStage = 1;

    private const int AfterTransferStage = 2;

    /// <summary>
    /// Sorts <paramref name="values" /> as curl does: a first character of <c>+</c> or
    /// <c>-</c> picks the list and is removed; then a <c>*</c> is removed and marks the
    /// command's failure as ignored. <c>*-X</c> is therefore sent after login as <c>-X</c>.
    /// </summary>
    /// <param name="values">Every <c>-Q</c> value, verbatim and in command-line order.</param>
    /// <returns>The sorted commands, each list in command-line order.</returns>
    public static FtpQuoteCommands Parse(IReadOnlyList<string> values)
    {
        List<FtpQuoteCommand>[] stages = [[], [], []];
        foreach (string value in values)
        {
            int stage = StageOf(value);
            stages[stage].Add(ToCommand(stage == AfterLoginStage ? value : value[1..]));
        }

        return new FtpQuoteCommands(stages[AfterLoginStage], stages[BeforeTransferStage], stages[AfterTransferStage]);
    }

    private static int StageOf(string value) => value.StartsWith('+')
        ? BeforeTransferStage
        : value.StartsWith('-') ? AfterTransferStage : AfterLoginStage;

    private static FtpQuoteCommand ToCommand(string value) =>
        value.StartsWith('*') ? new FtpQuoteCommand(value[1..], true) : new FtpQuoteCommand(value, false);
}
