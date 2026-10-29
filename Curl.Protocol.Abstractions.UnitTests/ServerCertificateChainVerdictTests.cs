using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="ServerCertificateChainVerdict" />: the accepted verdict and a rejection's
/// exit code, message and guards.
/// </summary>
[TestClass]
public sealed class ServerCertificateChainVerdictTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Accepted_IsAcceptedWithExitZeroAndNoMessage()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("verdict", "Accepted");

        var verdict = ServerCertificateChainVerdict.Accepted;

        diagnostics.Act("exit code", verdict.ExitCode);
        diagnostics.Assert("exit code", CurlExitCode.Ok, verdict.ExitCode);
        Assert.IsTrue(verdict.IsAccepted);
        Assert.AreEqual(CurlExitCode.Ok, verdict.ExitCode);
        Assert.IsNull(verdict.ErrorMessage);
    }

    [TestMethod]
    public void Rejected_WithExitCodeAndMessage_CarriesThem()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("rejection", "60, bad chain");

        var verdict = ServerCertificateChainVerdict.Rejected(CurlExitCode.PeerFailedVerification, "bad chain");

        diagnostics.Act("exit code", verdict.ExitCode);
        diagnostics.Assert("exit code", CurlExitCode.PeerFailedVerification, verdict.ExitCode);
        Assert.IsFalse(verdict.IsAccepted);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, verdict.ExitCode);
        Assert.AreEqual("bad chain", verdict.ErrorMessage);
    }

    [TestMethod]
    public void Rejected_WithExitZero_ThrowsArgumentOutOfRangeException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exit code", CurlExitCode.Ok);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ServerCertificateChainVerdict.Rejected(CurlExitCode.Ok, "message"));

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("exception", nameof(ArgumentOutOfRangeException), exception.GetType().Name);
    }

    [TestMethod]
    public void Rejected_WithNullMessage_ThrowsArgumentNullException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("message", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => ServerCertificateChainVerdict.Rejected(CurlExitCode.PeerFailedVerification, null!));

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("exception", nameof(ArgumentNullException), exception.GetType().Name);
    }
}
