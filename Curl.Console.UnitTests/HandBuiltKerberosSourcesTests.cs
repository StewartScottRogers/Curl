using System.Text;
using Curl.Kerberos;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="HandBuiltKerberosSources" />: <c>krb5.conf</c> and the default credential
/// cache found where MIT finds them, through an in-memory file reader, and the ticket source
/// and KDC client made over them.
/// </summary>
[TestClass]
public sealed class HandBuiltKerberosSourcesTests
{
    [TestMethod]
    public void ReadConfiguration_Krb5ConfigSet_ReadsThatFile()
    {
        MemoryFiles files = new() { ["/etc/other.conf"] = "[libdefaults]\n default_realm = EXAMPLE.TEST\n" };

        KerberosConfiguration configuration = Sources(files, name => name == "KRB5_CONFIG" ? "/etc/other.conf" : null).ReadConfiguration();

        Assert.AreEqual("EXAMPLE.TEST", configuration.DefaultRealm);
    }

    [TestMethod]
    public void ReadCredentialCache_NoKrb5ccname_ReadsTmpKrb5ccOfTheUserId()
    {
        MemoryFiles files = new();

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(() => Sources(files, _ => null).ReadCredentialCache());

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

    private static HandBuiltKerberosSources Sources(MemoryFiles files, Func<string, string?> environment) =>
        new(files, environment, () => 1000, new NoSrvRecords(), new UnreachableKdc(), TimeProvider.System);

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
}
