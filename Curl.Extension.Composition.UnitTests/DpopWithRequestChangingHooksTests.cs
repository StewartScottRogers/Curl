using System.Buffers.Text;
using System.Formats.Asn1;
using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Composition;

/// <summary>
/// <c>--dpop</c> beside each hook that sends several requests or changes a request's method or URL (RFC 9449 §4.2,
/// ADR-1439), run through the catalogue's own order against a stand-in for curl's handler.
/// </summary>
[TestClass]
public sealed class DpopWithRequestChangingHooksTests
{
    private string _directory = null!;
    private string _keyFile = null!;

    [TestInitialize]
    public void WriteKey()
    {
        _directory = Directory.CreateTempSubdirectory("curl-dpop-").FullName;
        _keyFile = Path.Combine(_directory, "key.pem");
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        File.WriteAllText(_keyFile, ecdsa.ExportECPrivateKeyPem());
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    public async Task Catalogue_DpopWithWebDavSync_SignsThePropfindAndEachGet()
    {
        const string multistatus =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:multistatus xmlns:D=\"DAV:\">"
            + "<D:response><D:href>/dav/a.txt</D:href><D:propstat><D:prop><D:resourcetype/><D:getcontentlength>1</D:getcontentlength></D:prop><D:status>HTTP/1.1 200 OK</D:status></D:propstat></D:response>"
            + "<D:response><D:href>/dav/b.txt</D:href><D:propstat><D:prop><D:resourcetype/><D:getcontentlength>1</D:getcontentlength></D:prop><D:status>HTTP/1.1 200 OK</D:status></D:propstat></D:response>"
            + "</D:multistatus>";
        var server = new Server(request => request == 0 ? Reply.Of(207, multistatus) : Reply.Of(200, "x"));

        await RunAsync(server, "http://127.0.0.1:27021/dav/", "webdav-sync", Path.Combine(_directory, "copy"));

        AssertEachRequestIsSigned(
            server,
            "PROPFIND http://127.0.0.1:27021/dav/",
            "GET http://127.0.0.1:27021/dav/a.txt",
            "GET http://127.0.0.1:27021/dav/b.txt");
    }

    [TestMethod]
    public async Task Catalogue_DpopWithZsync_SignsTheRangeRequestAndTheWholeResourceRequest()
    {
        var control = Path.Combine(_directory, "file.zsync");
        var header = $"zsync: 0.6.2\nBlocksize: 4\nLength: 6\nHash-Lengths: 1,4,16\nURL: file.bin\nSHA-1: {Convert.ToHexStringLower(SHA1.HashData("abcdef"u8))}\n\n";
        await File.WriteAllBytesAsync(control, [.. Encoding.ASCII.GetBytes(header), .. new byte[2 * (4 + 16)]]);
        var server = new Server(request => request == 0 ? Reply.Of(206, string.Empty) : Reply.Of(200, "abcdef"));
        var values = new Dictionary<string, string> { ["zsync"] = control, ["zsync-seed"] = Path.Combine(_directory, "no-seed") };

        await RunAsync(server, "http://127.0.0.1:27021/file.bin", values);

        AssertEachRequestIsSigned(server, "GET http://127.0.0.1:27021/file.bin", "GET http://127.0.0.1:27021/file.bin");
    }

    [TestMethod]
    public async Task Catalogue_DpopWithGrpcWeb_SignsThePostTheHookMakes()
    {
        var server = new Server(_ => Reply.Of(200, string.Empty));

        await RunAsync(server, "http://127.0.0.1:27021/pkg.Service/Call", "grpc-web", "x");

        AssertEachRequestIsSigned(server, "POST http://127.0.0.1:27021/pkg.Service/Call");
    }

    [TestMethod]
    public async Task Catalogue_DpopWithGrpc_SignsTheHttpPostOfTheCallAndKeepsTheProtoContext()
    {
        var server = new Server(_ => Reply.Of(200, string.Empty));

        await RunAsync(server, "grpc://127.0.0.1:27021/pkg.Service/Call", "grpc-proto", "service.proto");

        AssertEachRequestIsSigned(server, "POST http://127.0.0.1:27021/pkg.Service/Call");
        Assert.AreEqual("GrpcProtoContext", server.Requests[0].GetType().Name);
    }

    [TestMethod]
    public async Task Catalogue_DpopWithPrivacyPass_SignsTheChallengedRequestAndTheRetryWithTheToken()
    {
        var issuer = new BlindRsaIssuer();
        var server = new Server(request => request == 0 ? Reply.Of(401, string.Empty, new KeyValuePair<string, string>("WWW-Authenticate", issuer.Challenge)) : Reply.Of(200, "ok"));
        var values = new Dictionary<string, string> { ["privacy-pass"] = "https://issuer.example/token-request" };

        var result = await RunAsync(server, "https://origin.example/r", values, new TransferHookServices(issuer, issuer));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        AssertEachRequestIsSigned(server, "GET https://origin.example/r", "GET https://origin.example/r");
        Assert.IsTrue(server.Requests[1].Http!.Headers.Any(line => line.StartsWith("Authorization: PrivateToken ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task Catalogue_DpopWithACompressionDictionary_SignsTheRequest()
    {
        var server = new Server(_ => Reply.Of(200, "ok"));

        await RunAsync(server, "https://origin.example/r", "compression-dictionary", Path.Combine(_directory, "dictionaries"));

        AssertEachRequestIsSigned(server, "GET https://origin.example/r");
    }

    [TestMethod]
    public async Task Catalogue_DpopWithObliviousHttp_SendsNoProofOutsideTheEncapsulatedRequest_Adr1431()
    {
        byte[] keyConfigurations = [0x00, 0x29, 0x01, 0x00, 0x20, .. Enumerable.Repeat((byte)9, 32), 0x00, 0x04, 0x00, 0x01, 0x00, 0x01];
        var server = new Server(request => request == 0
            ? new Reply(200, keyConfigurations, [new("Content-Type", "application/ohttp-keys")])
            : Reply.Of(500, string.Empty));

        var result = await RunAsync(server, "https://origin.example/r", "ohttp-relay", "https://relay.example/relay");

        Assert.HasCount(2, server.Requests, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "origin.example", "relay.example" }, server.Requests.Select(request => request.Url.Host).ToArray());
        Assert.IsFalse(server.Requests.SelectMany(request => request.Http!.Headers).Any(line => line.StartsWith("DPoP:", StringComparison.OrdinalIgnoreCase)));
    }

    private static void AssertEachRequestIsSigned(Server server, params string[] expected)
    {
        var claims = server.Requests.Select(ClaimsOf).ToList();
        CollectionAssert.AreEqual(expected, claims.Select(claim => claim.GetProperty("htm").GetString() + " " + claim.GetProperty("htu").GetString()).ToArray());
        Assert.AreEqual(expected.Length, claims.Select(claim => claim.GetProperty("jti").GetString()).Distinct().Count());
    }

    private static JsonElement ClaimsOf(ITransferContext request)
    {
        var proof = request.Http!.Headers.Single(header => header.StartsWith("DPoP: ", StringComparison.Ordinal))["DPoP: ".Length..];
        return JsonElement.Parse(Base64Url.DecodeFromChars(proof.Split('.')[1]));
    }

    private Task<TransferResult> RunAsync(Server server, string url, string option, string value) =>
        RunAsync(server, url, new Dictionary<string, string> { [option] = value });

    private async Task<TransferResult> RunAsync(Server server, string url, Dictionary<string, string> options, TransferHookServices? services = null)
    {
        options["dpop"] = _keyFile;
        var hooks = TransferHooks.ActivatedBy(new ExtensionOptionValues(options), ExtensionTransferHookCatalogue.Entries, services);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            Http = new Curl.Protocol.Abstractions.HttpRequestOptions(),
        };

        return await hooks.Around(server).ExecuteAsync(context);
    }

    private sealed record Reply(int Code, byte[] Body, KeyValuePair<string, string>[] Headers)
    {
        public static Reply Of(int code, string body, params KeyValuePair<string, string>[] headers) => new(code, Encoding.UTF8.GetBytes(body), headers);
    }

    /// <summary>Stands in for curl's handler: records each request and answers it, by its number from 0.</summary>
    private sealed class Server(Func<int, Reply> answer) : IProtocolHandler
    {
        public List<ITransferContext> Requests { get; } = [];

        public IReadOnlyCollection<string> SupportedSchemes => ["http", "https", "grpc"];

        public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            var reply = answer(Requests.Count);
            Requests.Add(context);
            await context.Output.WriteAsync(reply.Body);
            var contentType = reply.Headers.FirstOrDefault(field => field.Key == "Content-Type").Value;
            return TransferResult.Success(reply.Body.Length) with { Report = new TransferReport { ResponseCode = reply.Code, ResponseHeaders = reply.Headers, ContentType = contentType } };
        }
    }

    /// <summary>A Privacy Pass issuer of Blind RSA tokens (RFC 9578 §6), behind a connector that opens a connection to it.</summary>
    private sealed class BlindRsaIssuer : IConnector, IConnectionListener, IConnection
    {
        private const int ModulusLength = 256;

        private readonly RSAParameters _key = CreateKey();
        private readonly MemoryStream _request = new();
        private MemoryStream? _response;

        public string Challenge
        {
            get
            {
                // token_type 2, issuer_name "issuer.example", no redemption context, origin_info "origin.example".
                byte[] challenge = [0x00, 0x02, 0x00, 0x0E, .. "issuer.example"u8, 0x00, 0x00, 0x0E, .. "origin.example"u8];
                return $"PrivateToken challenge=\"{Base64Url.EncodeToString(challenge)}\", token-key=\"{Base64Url.EncodeToString(PublicKey())}\"";
            }
        }

        public bool IsSecure => true;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(this));

        public ValueTask<ListenResult> ListenAsync(ListenTarget target, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            _response ??= new MemoryStream(SignedResponse());
            return ValueTask.FromResult(_response.Read(buffer.Span));
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            _request.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static RSAParameters CreateKey()
        {
            using var rsa = RSA.Create(ModulusLength * 8);
            return rsa.ExportParameters(true);
        }

        private static BigInteger NumberOf(ReadOnlySpan<byte> bytes) => new(bytes, isUnsigned: true, isBigEndian: true);

        // The TokenRequest ends with the blinded message, which the issuer signs with the private exponent.
        private byte[] SignedResponse()
        {
            var signature = BigInteger.ModPow(NumberOf(_request.ToArray().AsSpan()[^ModulusLength..]), NumberOf(_key.D), NumberOf(_key.Modulus))
                .ToByteArray(isUnsigned: true, isBigEndian: true);
            var head = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/private-token-response\r\nContent-Length: {ModulusLength}\r\n\r\n");
            return [.. head, .. new byte[ModulusLength - signature.Length], .. signature];
        }

        // SubjectPublicKeyInfo with the RSASSA-PSS object identifier (RFC 9578 §6.5).
        private byte[] PublicKey()
        {
            var rsaPublicKey = new AsnWriter(AsnEncodingRules.DER);
            using (rsaPublicKey.PushSequence())
            {
                rsaPublicKey.WriteInteger(NumberOf(_key.Modulus));
                rsaPublicKey.WriteInteger(NumberOf(_key.Exponent));
            }

            var info = new AsnWriter(AsnEncodingRules.DER);
            using (info.PushSequence())
            {
                using (info.PushSequence())
                {
                    info.WriteObjectIdentifier("1.2.840.113549.1.1.10");
                }

                info.WriteBitString(rsaPublicKey.Encode());
            }

            return info.Encode();
        }
    }
}
