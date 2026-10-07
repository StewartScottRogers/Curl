using Curl.Testing;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Pins which failure messages curl 8.21.0's <c>-v</c> writes as a <c>*</c> line (BL-552 Notes).
/// </summary>
[TestClass]
public sealed class Pop3SessionMessagesTests
{
    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(Pop3SessionMessages.ResponseReadingFailed, true)]
    [DataRow(Pop3SessionMessages.UnexpectedResponse, true)]
    [DataRow(Pop3SessionMessages.StlsNotSupported, true)]
    [DataRow(Pop3SessionMessages.StartTlsDenied, true)]
    [DataRow(Pop3SessionMessages.NulByteInLine, true)]
    [DataRow("Access denied. -", true)]
    [DataRow("Authentication failed: 45", true)]
    [DataRow(Pop3SessionMessages.WeirdServerReply, false)]
    [DataRow(Pop3SessionMessages.UrlMalformed, false)]
    [DataRow(Pop3SessionMessages.LoginDenied, false)]
    [DataRow(Pop3SessionMessages.ResponseLineTooLarge, false)]
    public void IsWrittenByVerbose_EachFailureMessage_IsWrittenOnlyWhenCurlFormatsIt(string message, bool written)
    {
        Diagnostics.Arrange("message", message);
        bool actual = Pop3SessionMessages.IsWrittenByVerbose(message);
        Diagnostics.Act("written by verbose", actual);

        Diagnostics.Assert("written by verbose", written, actual);
        Assert.AreEqual(written, Pop3SessionMessages.IsWrittenByVerbose(message));
    }
}
