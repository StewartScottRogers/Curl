namespace Curl.Tls;

/// <summary>The tickets <see cref="Tls13TestServer" /> issued and the resumption PSK each stands for, shared by the servers of one test so a later connection can resume.</summary>
internal sealed class Tls13TestTicketCache
{
    private readonly Dictionary<string, byte[]> preSharedKeys = [];

    public void Add(byte[] ticket, byte[] preSharedKey) => preSharedKeys[Convert.ToHexString(ticket)] = preSharedKey;

    public byte[]? Find(byte[] ticket) => preSharedKeys.GetValueOrDefault(Convert.ToHexString(ticket));
}
