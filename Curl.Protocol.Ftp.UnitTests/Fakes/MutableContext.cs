using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp.Fakes;

/// <summary>
/// The <see cref="TransferContext" /> properties the FTP option tests set, settable one at a
/// time so a test can combine options, then built into a <see cref="TransferContext" /> that
/// keeps the URL and output it was given.
/// </summary>
public sealed class MutableContext
{
    public bool FtpDisableEpsv { get; set; }

    public bool FtpSkipPasvIp { get; set; } = true;

    public FtpFileMethod FtpFileMethod { get; set; }

    public bool FtpCreateDirectories { get; set; }

    public bool ListOnly { get; set; }

    public List<string> QuoteCommands { get; } = [];

    public Stream? Upload { get; set; }

    public long? ResumeFrom { get; set; }

    public bool ResumeUploadFromUnknownOffset { get; set; }

    public ByteRange? Range { get; set; }

    public bool NoBody { get; set; }

    public Stream? HeaderOutput { get; set; }

    /// <summary>Gets the output the built context writes to, so a test can send headers there too.</summary>
    public Stream Output { get; private init; } = Stream.Null;

    public static TransferContext Build(TransferContext context, Action<MutableContext> adjust)
    {
        var mutable = new MutableContext { Output = context.Output };
        adjust(mutable);
        return new TransferContext
        {
            Url = context.Url,
            Output = context.Output,
            FtpDisableEpsv = mutable.FtpDisableEpsv,
            FtpSkipPasvIp = mutable.FtpSkipPasvIp,
            FtpFileMethod = mutable.FtpFileMethod,
            FtpCreateDirectories = mutable.FtpCreateDirectories,
            ListOnly = mutable.ListOnly,
            QuoteCommands = mutable.QuoteCommands,
            Upload = mutable.Upload,
            ResumeFrom = mutable.ResumeFrom,
            ResumeUploadFromUnknownOffset = mutable.ResumeUploadFromUnknownOffset,
            Range = mutable.Range,
            NoBody = mutable.NoBody,
            HeaderOutput = mutable.HeaderOutput,
        };
    }
}
