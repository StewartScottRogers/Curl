namespace Curl.Engine;

/// <summary>
/// The conformance harness's stand-in for upstream's debug-build <c>CURL_FTP_PWD_STOP</c>: <c>ftp_state_pwd</c> sends
/// no <c>PWD</c> for the process's first transfer (<c>data->id</c> 0) and leaves its state machine waiting, so only the
/// first perform of an engine that has one claims it (upstream test753, BL-2636, ADR-0687).
/// </summary>
internal sealed class FirstTransferFtpPwdStop
{
    private int claimed;

    /// <summary>Gives a new stop when <paramref name="asked" /> is set, else none.</summary>
    /// <param name="asked">Whether the engine stands in for <c>CURL_FTP_PWD_STOP</c>.</param>
    /// <returns>The stop, or <see langword="null" />.</returns>
    internal static FirstTransferFtpPwdStop? When(bool asked) => asked ? new FirstTransferFtpPwdStop() : null;

    /// <summary>Claims the stop for the engine's first perform: <see langword="true" /> once, then never again.</summary>
    /// <returns>Whether this perform's FTP connect phase stops before <c>PWD</c>.</returns>
    internal bool TryClaim() => Interlocked.Exchange(ref claimed, 1) == 0;
}
