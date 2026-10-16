using Curl.Api.Options.Behaviour;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="EasyOptionsFromCommandLine.BehaviourOf" />, <see cref="EasyOptionsFromCommandLine.TransferOf" />
/// and <see cref="EasyOptionsFromCommandLine.UploadTransferOf" />: each behaviour and transfer command-line option
/// given sets its member, and a command line without it keeps libcurl's default, except where curl's tool always
/// sets one (ADR-0528).
/// </summary>
public sealed partial class EasyOptionsFromCommandLineTests
{
    private static readonly EasyBehaviourOptions LibcurlBehaviourDefaults = new();

    private static readonly EasyTransferOptions LibcurlTransferDefaults = new();

    private static readonly DateTimeOffset ConditionDate = new(1994, 11, 6, 8, 49, 37, TimeSpan.Zero);

    public static IEnumerable<object?[]> GivenBehaviourOptions =>
    [
        GivenBehaviour("-v", o => o.Verbose, true, "-v"),
        GivenBehaviour("--trace", o => o.Verbose, true, "--trace", "trace.txt"),
        GivenBehaviour("--trace-ascii", o => o.Verbose, true, "--trace-ascii", "trace.txt"),
        GivenBehaviour("--no-verbose", o => o.Verbose, false, "-v", "--no-verbose"),
        GivenBehaviour("-i", o => o.IncludeHeader, true, "-i"),
        GivenBehaviour("--include", o => o.IncludeHeader, true, "--include"),
        GivenBehaviour("-I", o => o.IncludeHeader, true, "-I"),
        GivenBehaviour("-s", o => o.NoProgress, true, "-s"),
        GivenBehaviour("--no-progress-meter", o => o.NoProgress, true, "--no-progress-meter"),
        GivenBehaviour("-#", o => o.NoProgress, false, "-#"),
        GivenBehaviour("-f", o => o.FailOnError, true, "-f"),
        GivenBehaviour("--fail-with-body", o => o.FailOnError, false, "--fail-with-body"),
        GivenBehaviour("--no-fail", o => o.FailOnError, false, "-f", "--no-fail"),
        GivenBehaviour("--create-file-mode", o => o.NewFilePermissions, UnixFileMode.UserRead | UnixFileMode.UserWrite, "--create-file-mode", "0600"),
        GivenBehaviour("--create-file-mode 0", o => o.NewFilePermissions, LibcurlBehaviourDefaults.NewFilePermissions, "--create-file-mode", "0"),
        GivenBehaviour("-N", o => o.Verbose, false, "-N"),
    ];

    public static IEnumerable<object?[]> AbsentBehaviourOptions =>
    [
        AbsentBehaviour("Verbose", o => o.Verbose),
        AbsentBehaviour("IncludeHeader", o => o.IncludeHeader),
        AbsentBehaviour("NoSignal", o => o.NoSignal),
        AbsentBehaviour("WildcardMatch", o => o.WildcardMatch),
        AbsentBehaviour("ErrorBuffer", o => o.ErrorBuffer),
        AbsentBehaviour("StandardError", o => o.StandardError),
        AbsentBehaviour("FailOnError", o => o.FailOnError),
        AbsentBehaviour("KeepSendingOnError", o => o.KeepSendingOnError),
        AbsentBehaviour("Private", o => o.Private),
        AbsentBehaviour("Share", o => o.Share),
        AbsentBehaviour("QuickExit", o => o.QuickExit),
        AbsentBehaviour("NewFilePermissions", o => o.NewFilePermissions),
        AbsentBehaviour("NewDirectoryPermissions", o => o.NewDirectoryPermissions),
    ];

    public static IEnumerable<object?[]> GivenTransferOptions =>
    [
        GivenTransfer("-r", o => o.Range, "0-99", "-r", "0-99"),
        GivenTransfer("-C", o => o.ResumeFrom, 100L, "-C", "100"),
        GivenTransfer("-C -", o => o.ResumeFrom, 0L, "-C", "-"),
        GivenTransfer("-X", o => o.CustomRequest, "PATCH", "-X", "PATCH"),
        GivenTransfer("-R", o => o.FileTime, true, "-R"),
        GivenTransfer("-I file time", o => o.FileTime, true, "-I"),
        GivenTransfer("-I", o => o.NoBody, true, "-I"),
        GivenTransfer("-l", o => o.DirListOnly, true, "-l"),
        GivenTransfer("--max-filesize", o => o.MaxFileSize, 2048L, "--max-filesize", "2k"),
        GivenTransfer("-z", o => o.TimeCondition, TimeCondition.IfModifiedSince, "-z", "Sun, 06 Nov 1994 08:49:37 GMT"),
        GivenTransfer("-z time", o => o.TimeValue, ConditionDate.ToUnixTimeSeconds(), "-z", "Sun, 06 Nov 1994 08:49:37 GMT"),
        GivenTransfer("-z -", o => o.TimeCondition, TimeCondition.IfUnmodifiedSince, "-z", "-Sun, 06 Nov 1994 08:49:37 GMT"),
        GivenTransfer("-B", o => o.TransferText, true, "-B"),
        GivenTransfer("--crlf", o => o.Crlf, true, "--crlf"),
        GivenTransfer("-Y", o => o.LowSpeedLimit, 500L, "-Y", "500"),
        GivenTransfer("-Y alone time", o => o.LowSpeedTime, TimeSpan.FromSeconds(30), "-Y", "500"),
        GivenTransfer("-y", o => o.LowSpeedTime, TimeSpan.FromSeconds(7), "-y", "7"),
        GivenTransfer("-y alone limit", o => o.LowSpeedLimit, 1L, "-y", "7"),
        GivenTransfer("-Y -y", o => o.LowSpeedLimit, 500L, "-Y", "500", "-y", "7"),
        GivenTransfer("--limit-rate send", o => o.MaxSendSpeed, 1024L, "--limit-rate", "1k"),
        GivenTransfer("--limit-rate receive", o => o.MaxReceiveSpeed, 1024L, "--limit-rate", "1k"),
        GivenTransfer("--limit-rate buffer", o => o.BufferSize, 1024L, "--limit-rate", "1k"),
        GivenTransfer("--limit-rate fast buffer", o => o.BufferSize, 102400L, "--limit-rate", "1m"),
        GivenTransfer("--limit-rate 0 buffer", o => o.BufferSize, 102400L, "--limit-rate", "0"),
        GivenTransfer("-m", o => o.Timeout, TimeSpan.FromMilliseconds(2500), "-m", "2.5"),
        GivenTransfer("-m 0", o => o.Timeout, TimeSpan.Zero, "-m", "0"),
    ];

    public static IEnumerable<object?[]> AbsentTransferOptions =>
    [
        AbsentTransfer("Range", o => o.Range),
        AbsentTransfer("ResumeFrom", o => o.ResumeFrom),
        AbsentTransfer("CustomRequest", o => o.CustomRequest),
        AbsentTransfer("FileTime", o => o.FileTime),
        AbsentTransfer("DirListOnly", o => o.DirListOnly),
        AbsentTransfer("NoBody", o => o.NoBody),
        AbsentTransfer("Upload", o => o.Upload),
        AbsentTransfer("InFileSize", o => o.InFileSize),
        AbsentTransfer("UploadBufferSize", o => o.UploadBufferSize),
        AbsentTransfer("MaxFileSize", o => o.MaxFileSize),
        AbsentTransfer("TimeCondition", o => o.TimeCondition),
        AbsentTransfer("TimeValue", o => o.TimeValue),
        AbsentTransfer("TransferText", o => o.TransferText),
        AbsentTransfer("Crlf", o => o.Crlf),
        AbsentTransfer("ProxyTransferMode", o => o.ProxyTransferMode),
        AbsentTransfer("LowSpeedLimit", o => o.LowSpeedLimit),
        AbsentTransfer("LowSpeedTime", o => o.LowSpeedTime),
        AbsentTransfer("MaxSendSpeed", o => o.MaxSendSpeed),
        AbsentTransfer("MaxReceiveSpeed", o => o.MaxReceiveSpeed),
        AbsentTransfer("Timeout", o => o.Timeout),
    ];

    [TestMethod]
    [DynamicData(nameof(GivenBehaviourOptions))]
    public void BehaviourOf_OptionGiven_SetsItsMember(string option, Func<EasyBehaviourOptions, object?> read, object? expected, string[] arguments)
    {
        EasyBehaviourOptions behaviour = EasyOptionsFromCommandLine.BehaviourOf(Parse(arguments));

        Assert.AreEqual(expected, read(behaviour), option);
    }

    [TestMethod]
    [DynamicData(nameof(AbsentBehaviourOptions))]
    public void BehaviourOf_OptionAbsent_KeepsLibcurlDefault(string member, Func<EasyBehaviourOptions, object?> read)
    {
        EasyBehaviourOptions behaviour = EasyOptionsFromCommandLine.BehaviourOf(Parse());

        Assert.AreEqual(read(LibcurlBehaviourDefaults), read(behaviour), member);
    }

    [TestMethod]
    public void BehaviourOf_NoSilentOption_TurnsTheProgressCallbacksOn()
    {
        EasyBehaviourOptions behaviour = EasyOptionsFromCommandLine.BehaviourOf(Parse());

        Assert.IsFalse(behaviour.NoProgress);
    }

    [TestMethod]
    public void BehaviourOf_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => EasyOptionsFromCommandLine.BehaviourOf(null!));
    }

    [TestMethod]
    [DynamicData(nameof(GivenTransferOptions))]
    public void TransferOf_OptionGiven_SetsItsMember(string option, Func<EasyTransferOptions, object?> read, object? expected, string[] arguments)
    {
        EasyTransferOptions transfer = EasyOptionsFromCommandLine.TransferOf(Parse(arguments));

        Assert.AreEqual(expected, read(transfer), option);
    }

    [TestMethod]
    [DynamicData(nameof(AbsentTransferOptions))]
    public void TransferOf_OptionAbsent_KeepsLibcurlDefault(string member, Func<EasyTransferOptions, object?> read)
    {
        EasyTransferOptions transfer = EasyOptionsFromCommandLine.TransferOf(Parse());

        Assert.AreEqual(read(LibcurlTransferDefaults), read(transfer), member);
    }

    [TestMethod]
    public void TransferOf_NoLimitRate_AsksForTheToolsReceiveBuffer()
    {
        EasyTransferOptions transfer = EasyOptionsFromCommandLine.TransferOf(Parse());

        Assert.AreEqual(102400L, transfer.BufferSize);
    }

    [TestMethod]
    public void TransferOf_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => EasyOptionsFromCommandLine.TransferOf(null!));
    }

    [TestMethod]
    public void UploadTransferOf_KnownSize_SetsUploadAndItsSize()
    {
        EasyTransferOptions transfer = EasyOptionsFromCommandLine.UploadTransferOf(EasyOptionsFromCommandLine.TransferOf(Parse("-T", "file.txt")), 4096);

        Assert.AreEqual((true, 4096L), (transfer.Upload, transfer.InFileSize));
    }

    [TestMethod]
    public void UploadTransferOf_EmptyFile_SetsSizeZero()
    {
        EasyTransferOptions transfer = EasyOptionsFromCommandLine.UploadTransferOf(new EasyTransferOptions(), 0);

        Assert.AreEqual((true, 0L), (transfer.Upload, transfer.InFileSize));
    }

    [TestMethod]
    public void UploadTransferOf_UnknownSize_KeepsLibcurlsUnknown()
    {
        EasyTransferOptions transfer = EasyOptionsFromCommandLine.UploadTransferOf(new EasyTransferOptions(), null);

        Assert.AreEqual((true, -1L), (transfer.Upload, transfer.InFileSize));
    }

    [TestMethod]
    public void UploadTransferOf_KeepsTheGroupsOptions()
    {
        EasyTransferOptions transfer = EasyOptionsFromCommandLine.UploadTransferOf(EasyOptionsFromCommandLine.TransferOf(Parse("--crlf")), 1);

        Assert.IsTrue(transfer.Crlf);
    }

    [TestMethod]
    public void UploadTransferOf_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => EasyOptionsFromCommandLine.UploadTransferOf(null!, 1));
    }

    private static object?[] GivenBehaviour(string option, Func<EasyBehaviourOptions, object?> read, object? expected, params string[] arguments) =>
        [option, read, expected, arguments];

    private static object?[] AbsentBehaviour(string member, Func<EasyBehaviourOptions, object?> read) => [member, read];

    private static object?[] GivenTransfer(string option, Func<EasyTransferOptions, object?> read, object? expected, params string[] arguments) =>
        [option, read, expected, arguments];

    private static object?[] AbsentTransfer(string member, Func<EasyTransferOptions, object?> read) => [member, read];
}
