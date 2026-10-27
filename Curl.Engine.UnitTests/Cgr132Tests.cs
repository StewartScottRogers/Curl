using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-132 (BL-3661, ADR-0844): curl 8.22.0 writes an <c>sftp</c> or <c>scp</c> transfer's quote command output
/// (<c>pwd</c>'s <c>257</c> line, <c>statvfs</c>'s lines) only to <c>-D</c>, never to the body output, under
/// <c>-i</c> and <c>-I</c> alike (measured 2026-10-26 against OpenSSH 10.2p1, BL-3661 Notes).
/// </summary>
[TestClass]
public sealed class Cgr132Tests
{
    [TestMethod]
    [DataRow("sftp://h/f.txt", false)]
    [DataRow("sftp://h/f.txt", true)]
    [DataRow("scp://h/f.txt", false)]
    [DataRow("scp://h/f.txt", true)]
    [DataRow("ws://h/f.txt", false)]
    [DataRow("wss://h/f.txt", false)]
    public void WritesHeaderLinesToBody_SshOrWebSocketUrlUnderInclude_IsFalse(string url, bool noBody)
    {
        bool writes = TransferContextFactory.WritesHeaderLinesToBody(
            new EasyBehaviourOptions { IncludeHeader = true },
            new EasyTransferOptions { NoBody = noBody },
            CurlUrl.Parse(url));

        Assert.IsFalse(writes);
    }

    [TestMethod]
    [DataRow("http://h/f.txt")]
    [DataRow("ftp://h/f.txt")]
    public void WritesHeaderLinesToBody_OtherUrlUnderInclude_IsTrue(string url)
    {
        bool writes = TransferContextFactory.WritesHeaderLinesToBody(
            new EasyBehaviourOptions { IncludeHeader = true },
            new EasyTransferOptions(),
            CurlUrl.Parse(url));

        Assert.IsTrue(writes);
    }
}
