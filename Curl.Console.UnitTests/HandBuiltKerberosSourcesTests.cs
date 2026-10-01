using System.Text;
using Curl.Kerberos;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="HandBuiltKerberosSources" />: <c>krb5.conf</c> and the default credential
/// cache found where MIT finds them, through an in-memory file reader, and the ticket source
/// and KDC client made over them, whose <c>https://</c> KDCs go through the proxy transport.
/// </summary>
[TestClass]
public sealed class HandBuiltKerberosSourcesTests
{
    private const string Realm = "EXAMPLE.TEST";

    [TestMethod]
    public void ReadConfiguration_Krb5ConfigSet_ReadsThatFile()
    {
        MemoryFiles files = new() { ["/etc/other.conf"] = "[libdefaults]\n default_realm = EXAMPLE.TEST\n" };

        KerberosConfiguration configuration = Sources(files, name => name == "KRB5_CONFIG" ? "/etc/other.conf" : null).ReadConfiguration();

        Assert.AreEqual("EXAMPLE.TEST", configuration.DefaultRealm);
    }

    [TestMethod]
    public void CreateCredentialCacheStore_NoKrb5ccname_ReadsTmpKrb5ccOfTheUserId()
    {
        MemoryFiles files = new();

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(() => Sources(files, _ => null).CreateCredentialCacheStore().ReadDefault());

        Assert.AreEqual(KerberosFileError.NotFound, failure.Error);
        CollectionAssert.AreEqual(new[] { "/tmp/krb5cc_1000" }, files.PathsRead);
    }

    [TestMethod]
    public async Task CreateTicketSource_NoCache_FailsBeforeAnyKdcExchange()
    {
        MemoryFiles files = new();
        Authentication.KerberosServiceTicketSource source = Sources(files, _ => null).CreateTicketSource();

        await Assert.ThrowsExactlyAsync<KerberosFileException>(() => source.GetAsync("HTTP", "server.example.test", CancellationToken.None));

        CollectionAssert.AreEqual(new[] { "/etc/krb5.conf", "/tmp/krb5cc_1000" }, files.PathsRead);
    }

    [TestMethod]
    public void ReadProcessEnvironmentVariable_Name_ReadsTheProcessEnvironment()
    {
        Assert.AreEqual(Environment.GetEnvironmentVariable("PATH"), HandBuiltKerberosSources.ReadProcessEnvironmentVariable("PATH"));
    }

    [TestMethod]
    public void CreateKdcClient_Configuration_MakesAClient()
    {
        Assert.IsNotNull(Sources(new MemoryFiles(), _ => null).CreateKdcClient(KerberosConfiguration.Empty));
    }

    [TestMethod]
    public async Task CreateKdcClient_HttpsKdc_PostsThroughTheProxyTransport()
    {
        MemoryFiles files = new() { ["/etc/krb5.conf"] = "[realms]\n EXAMPLE.TEST = {\n kdc = https://kdcproxy.example.test/KdcProxy\n }\n" };
        RecordingProxy proxy = new();
        HandBuiltKerberosSources sources = new(files, new NoFileWriter(), _ => null, () => 1000, new NoSrvRecords(), new UnreachableKdc(), proxy, TimeProvider.System);
        KerberosKdcClient client = sources.CreateKdcClient(sources.ReadConfiguration());
        using CredentialCache cache = new(null, new KerberosPrincipal(1, Realm, ["alice"]), [TicketGrantingTicket()]);

        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(
            () => client.GetServiceTicketAsync(new KerberosPrincipal(3, Realm, ["HTTP", "server.example.test"]), cache, CancellationToken.None));

        Assert.AreEqual(KerberosKdcError.KdcUnreachable, failure.Error);
        Assert.AreEqual("kdcproxy.example.test:443/KdcProxy", proxy.Posted.Single());
    }

    private static HandBuiltKerberosSources Sources(MemoryFiles files, Func<string, string?> environment) =>
        new(files, new NoFileWriter(), environment, () => 1000, new NoSrvRecords(), new UnreachableKdc(), new RecordingProxy(), TimeProvider.System);

    private sealed class MemoryFiles : Dictionary<string, string>, IKerberosFileReader
    {
        public List<string> PathsRead { get; } = [];

        public byte[]? ReadAllBytes(string path)
        {
            PathsRead.Add(path);
            return TryGetValue(path, out string? text) ? Encoding.UTF8.GetBytes(text) : null;
        }

        public IReadOnlyList<string>? ListFileNames(string path) => null;
    }

    private sealed class NoFileWriter : IKerberosFileWriter
    {
        public bool AppendAllBytes(string path, ReadOnlySpan<byte> bytes) => false;
    }

    private sealed class NoSrvRecords : IKerberosSrvLookup
    {
        public Task<IReadOnlyList<KerberosSrvRecord>> LookUpAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<KerberosSrvRecord>>([]);
    }

    private sealed class UnreachableKdc : IKerberosKdcTransport
    {
        public Task<byte[]> ExchangeDatagramAsync(string host, int port, ReadOnlyMemory<byte> request, CancellationToken cancellationToken) =>
            throw new IOException("unreachable");

        public Task<Stream> ConnectStreamAsync(string host, int port, CancellationToken cancellationToken) =>
            throw new IOException("unreachable");
    }

    private static CachedCredential TicketGrantingTicket() => new()
    {
        Client = new KerberosPrincipal(1, Realm, ["alice"]),
        Server = KerberosKdcClient.TicketGrantingServer(Realm),
        SessionKey = new KerberosKey(18, new byte[32]),
        AuthenticationTime = DateTimeOffset.UtcNow.AddHours(-1),
        StartTime = DateTimeOffset.UnixEpoch,
        EndTime = DateTimeOffset.UtcNow.AddHours(1),
        RenewUntil = DateTimeOffset.UnixEpoch,
        IsEncryptedInSessionKey = false,
        Flags = KerberosTicketFlags.Forwardable,
        Addresses = [],
        AuthorizationData = [],
        Ticket = new KerberosTicket(Realm, new KerberosPrincipalName(2, ["krbtgt", Realm]), new KerberosEncryptedData(18, 1, [0xDE, 0xAD])).Encode(),
        SecondTicket = [],
    };

    /// <summary>An MS-KKDCP proxy that records each post as <c>host:port/path</c> and never answers.</summary>
    private sealed class RecordingProxy : IKerberosKdcProxyTransport
    {
        public List<string> Posted { get; } = [];

        public Task<byte[]> PostAsync(string host, int port, string path, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
        {
            Posted.Add($"{host}:{port}/{path}");
            throw new IOException("unreachable");
        }
    }
}
