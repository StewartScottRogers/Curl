namespace Curl.Protocol.Ftp;

/// <summary>
/// When a <c>-Q</c>/<c>--quote</c> command is sent, which names the state curl 8.21.0's
/// <c>--trace-config ftp</c> lines show while its reply is awaited (BL-1197).
/// </summary>
internal enum FtpQuoteStage
{
    /// <summary>After <c>PWD</c>, before the first <c>CWD</c>: state <c>QUOTE</c>.</summary>
    AfterLogin,

    /// <summary>After <c>TYPE</c>: state <c>RETR_PREQUOTE</c>, <c>STOR_PREQUOTE</c> or <c>LIST_PREQUOTE</c>.</summary>
    BeforeTransfer,

    /// <summary>After the transfer: no state change, its reply read as <c>getftpresponse</c>.</summary>
    AfterTransfer,
}
