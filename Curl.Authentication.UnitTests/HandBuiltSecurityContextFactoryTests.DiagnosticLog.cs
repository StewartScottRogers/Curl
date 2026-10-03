using Curl.Authentication.Fakes;
using Curl.Kerberos;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Pins the line <see cref="HandBuiltSecurityContextFactory" /> writes at <c>verbose</c> for each
/// context it makes (BL-1151): NTLM, Kerberos inside SPNEGO for Negotiate, or Kerberos alone.
/// </summary>
public sealed partial class HandBuiltSecurityContextFactoryTests
{
    [TestMethod]
    [DataRow(SecurityMechanism.Ntlm, "Ntlm context for server.example.test: hand-built NTLM (NTLM asked for)", DisplayName = "NTLM")]
    [DataRow(SecurityMechanism.Negotiate, "Negotiate context for server.example.test: hand-built Kerberos (SPNEGO offering Kerberos V5 alone, not NTLM)", DisplayName = "Negotiate")]
    [DataRow(SecurityMechanism.Kerberos, "Kerberos context for server.example.test: hand-built Kerberos (Kerberos asked for)", DisplayName = "Kerberos")]
    public void Create_WithALog_LogsTheContextChosenAtVerbose(SecurityMechanism mechanism, string expected)
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        HandBuiltSecurityContextFactory factory = new(
            Tickets(new FakeKdc(), () => Cache(TicketGrantingTicket())),
            new FixedTimeProvider(FakeKdc.Now),
            new FixedKerberosRandomSource(RandomBytes),
            new FixedNtlmRandomSource(RandomBytes),
            log);

        using ISecurityContext context = factory.Create(Request(mechanism));

        CollectionAssert.AreEqual(new[] { expected }, log.At(DiagnosticLogLevel.Verbose));
        Assert.AreEqual(DiagnosticLogComponents.Auth, log.Lines[0].Component);
    }
}
