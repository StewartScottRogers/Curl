namespace Curl.Tls;

/// <summary>A resumable session as <see cref="Tls12TestServer" /> keeps it.</summary>
internal sealed record Tls12TestSession(byte[] MasterSecret, bool ExtendedMasterSecret);

/// <summary>
/// The sessions <see cref="Tls12TestServer" /> can resume, by session ID and by ticket;
/// two servers share one to resume across configurations.
/// </summary>
internal sealed class Tls12TestSessionCache
{
    private readonly Dictionary<string, Tls12TestSession> byId = [];
    private readonly Dictionary<string, Tls12TestSession> byTicket = [];

    public void Add(byte[] sessionId, Tls12TestSession session) => byId[Convert.ToHexString(sessionId)] = session;

    public void AddTicket(byte[] ticket, Tls12TestSession session) => byTicket[Convert.ToHexString(ticket)] = session;

    public Tls12TestSession? FindById(byte[] sessionId) => sessionId.Length == 0 ? null : byId.GetValueOrDefault(Convert.ToHexString(sessionId));

    public Tls12TestSession? FindByTicket(byte[] ticket) => byTicket.GetValueOrDefault(Convert.ToHexString(ticket));
}
