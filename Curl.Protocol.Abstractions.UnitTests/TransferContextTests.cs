using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="TransferContext" />: an option left out of the initializer reports its
/// "not given" value, and every option set in the initializer reads back unchanged.
/// </summary>
[TestClass]
public sealed class TransferContextTests
{
    private static readonly Uri AnyUrl = new("tftp://example.com/file");

    [TestMethod]
    public void TransferContext_OnlyRequiredMembersSet_ReportsNotGivenForEveryOption()
    {
        using var output = new MemoryStream();

        var context = new TransferContext { Url = AnyUrl, Output = output };

        Assert.IsNull(context.Upload);
        Assert.IsNull(context.ResumeFrom);
        Assert.IsNull(context.Range);
        Assert.IsFalse(context.NoBody);
        Assert.IsNull(context.TimeCondition);
        Assert.IsNull(context.HeaderOutput);
        Assert.IsNull(context.PostData);
        Assert.IsNull(context.Credentials);
        Assert.IsEmpty(context.TelnetOptions);
        Assert.IsNull(context.TftpBlockSize);
        Assert.IsFalse(context.TftpNoOptions);
        Assert.IsFalse(context.ConvertLineEndings);
        Assert.AreEqual((UnixFileMode)0b110_100_100, context.CreateFileMode);
        Assert.AreSame(TimeProvider.System, context.TimeProvider);
        Assert.AreEqual(CancellationToken.None, context.CancellationToken);
    }

    [TestMethod]
    public void TransferContext_EveryMemberSet_RoundTripsEveryValue()
    {
        using var output = new MemoryStream();
        using var upload = new MemoryStream();
        using var headerOutput = new MemoryStream();
        using var cancellation = new CancellationTokenSource();
        var range = ByteRange.Bounded(1, 9);
        var timeCondition = new TimeCondition(
            new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.Zero),
            TimeConditionKind.IfModifiedSince);
        ReadOnlyMemory<byte> postData = new byte[] { 0x78 };
        var credentials = new NetworkCredential("bob", "secret");
        string[] telnetOptions = ["TTYPE=vt100", "XDISPLOC=host:0"];
        var timeProvider = new StubTimeProvider();

        var context = new TransferContext
        {
            Url = AnyUrl,
            Output = output,
            Upload = upload,
            ResumeFrom = 42,
            Range = range,
            NoBody = true,
            TimeCondition = timeCondition,
            HeaderOutput = headerOutput,
            PostData = postData,
            Credentials = credentials,
            TelnetOptions = telnetOptions,
            TftpBlockSize = 70000,
            TftpNoOptions = true,
            ConvertLineEndings = true,
            CreateFileMode = UnixFileMode.UserRead,
            TimeProvider = timeProvider,
            CancellationToken = cancellation.Token,
        };

        Assert.AreSame(AnyUrl, context.Url);
        Assert.AreSame(output, context.Output);
        Assert.AreSame(upload, context.Upload);
        Assert.AreEqual(42L, context.ResumeFrom);
        Assert.AreEqual(range, context.Range);
        Assert.IsTrue(context.NoBody);
        Assert.AreEqual(timeCondition, context.TimeCondition);
        Assert.AreSame(headerOutput, context.HeaderOutput);
        Assert.IsTrue(context.PostData.HasValue);
        Assert.IsTrue(postData.Span.SequenceEqual(context.PostData.Value.Span));
        Assert.AreSame(credentials, context.Credentials);
        Assert.AreSame(telnetOptions, context.TelnetOptions);
        Assert.AreEqual(70000, context.TftpBlockSize);
        Assert.IsTrue(context.TftpNoOptions);
        Assert.IsTrue(context.ConvertLineEndings);
        Assert.AreEqual(UnixFileMode.UserRead, context.CreateFileMode);
        Assert.AreSame(timeProvider, context.TimeProvider);
        Assert.AreEqual(cancellation.Token, context.CancellationToken);
    }

    private sealed class StubTimeProvider : TimeProvider;
}
