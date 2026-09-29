namespace Curl.Kerberos;

/// <summary>Checks that <see cref="KerberosCredential" /> zeroes its session key when disposed.</summary>
[TestClass]
public sealed class KerberosCredentialTests
{
    [TestMethod]
    public void Dispose_Always_ZeroesTheSessionKey()
    {
        KerberosCredential credential = new()
        {
            Client = FakeKdc.Alice,
            Server = FakeKdc.Service,
            Ticket = FakeKdc.TicketGrantingTicket,
            SessionKey = new KerberosKey(18, [.. FakeKdc.ServiceSessionKey]),
            Flags = KerberosTicketFlags.None,
            AuthenticationTime = FakeKdc.Now,
            EndTime = FakeKdc.Now,
        };

        credential.Dispose();

        CollectionAssert.AreEqual(new byte[32], credential.SessionKey.Value.ToArray());
    }
}
