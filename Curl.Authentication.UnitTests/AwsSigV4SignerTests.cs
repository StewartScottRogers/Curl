using System.Globalization;
using System.Text;
using Curl.Kerberos;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="AwsSigV4Signer" /> to the headers curl 8.21.0 (Schannel, Windows) sent,
/// recorded with <c>Record-CurlExchange.ps1</c> (BL-628), and to AWS's published Signature
/// Version 4 examples.
/// </summary>
[TestClass]
public sealed class AwsSigV4SignerTests
{
    private const string LoopbackHost = "127.0.0.1:18628";

    private const string ExampleAccessKey = "AKIDEXAMPLE";

    private const string ExampleSecretKey = "wJalrXUtnFEMI/K7MDENG+bPxRfiCYEXAMPLEKEY";

    private const string EmptyHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Sign_S3GetWithAQuery_SendsTheMeasuredHeaders()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", "aws:amz:us-east-1:s3");
        diagnostics.Arrange("request", "GET /bucket/key%20a?b=2&a=1&c at 20260929T060111Z, user AKID, password SECRET");

        AwsSigV4SigningResult result = SignAt("20260929T060111Z", Loopback("aws:amz:us-east-1:s3") with
        {
            Path = "/bucket/key%20a",
            Query = "b=2&a=1&c",
            IsGetOrHead = true,
        });

        diagnostics.Act("header lines", string.Join(" | ", result.HeaderLines));
        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("canonical request", result.CanonicalRequest);
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        diagnostics.Assert("error message", null, result.ErrorMessage);
        diagnostics.Assert("header line count", 3, result.HeaderLines.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                "Authorization: AWS4-HMAC-SHA256 Credential=AKID/20260929/us-east-1/s3/aws4_request, SignedHeaders=host;x-amz-content-sha256;x-amz-date, Signature=6f96d5ca68a6971972090dfc3d89b3f9674419c900e06d56ef1c7b28373389d7",
                "X-Amz-Date: 20260929T060111Z",
                "x-amz-content-sha256: " + EmptyHash,
            },
            result.HeaderLines.ToArray());
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.ErrorMessage);
        Assert.AreEqual(
            "GET\n/bucket/key%20a\na=1&b=2&c=\nhost:127.0.0.1:18628\nx-amz-content-sha256:" + EmptyHash + "\nx-amz-date:20260929T060111Z\n\nhost;x-amz-content-sha256;x-amz-date\n" + EmptyHash,
            result.CanonicalRequest);
        StringAssert.StartsWith(result.StringToSign, "AWS4-HMAC-SHA256\n20260929T060111Z\n20260929/us-east-1/s3/aws4_request\n", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Sign_S3PostWithFields_HashesTheFields()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", "aws:amz:us-east-1:s3");
        diagnostics.Arrange("request", "POST /upload with fields hello=world at 20260929T060111Z");

        AwsSigV4SigningResult result = SignAt("20260929T060111Z", Loopback("aws:amz:us-east-1:s3") with
        {
            Method = "POST",
            Path = "/upload",
            PostFields = Encoding.ASCII.GetBytes("hello=world"),
        });

        diagnostics.Act("header lines", string.Join(" | ", result.HeaderLines));
        diagnostics.Assert("header line count", 3, result.HeaderLines.Count);
        diagnostics.Diff(
            "content hash line",
            "x-amz-content-sha256: 3d011e09502a84552a0f8ae112d024cc2c115597e3a577d5f49007902c221dc5",
            result.HeaderLines[2]);
        CollectionAssert.AreEqual(
            new[]
            {
                "Authorization: AWS4-HMAC-SHA256 Credential=AKID/20260929/us-east-1/s3/aws4_request, SignedHeaders=host;x-amz-content-sha256;x-amz-date, Signature=445ec533470e073a3ca813c59a7f730706cf61288fda6024ac5ff39fd37d85e7",
                "X-Amz-Date: 20260929T060111Z",
                "x-amz-content-sha256: 3d011e09502a84552a0f8ae112d024cc2c115597e3a577d5f49007902c221dc5",
            },
            result.HeaderLines.ToArray());
    }

    [TestMethod]
    public void Sign_OscWithServiceAndRegionInTheHost_TakesThemFromTheHost()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", "osc");
        diagnostics.Arrange("host", "fcu.eu-west-2.outscale.com:18628");
        diagnostics.Arrange("request", "GET /path at 20260929T060112Z");

        AwsSigV4SigningResult result = SignAt("20260929T060112Z", Loopback("osc") with
        {
            HostName = "fcu.eu-west-2.outscale.com",
            HostHeaderValue = "fcu.eu-west-2.outscale.com:18628",
            Path = "/path",
            IsGetOrHead = true,
        });

        diagnostics.Act("header lines", string.Join(" | ", result.HeaderLines));
        diagnostics.Assert("header line count", 2, result.HeaderLines.Count);
        diagnostics.Diff("date line", "X-Osc-Date: 20260929T060112Z", result.HeaderLines[1]);
        CollectionAssert.AreEqual(
            new[]
            {
                "Authorization: OSC4-HMAC-SHA256 Credential=AKID/20260929/eu-west-2/fcu/osc4_request, SignedHeaders=host;x-osc-date, Signature=235bcff21c7d26e2cff783594087d84a221296654fe89689144b13fc6690bee5",
                "X-Osc-Date: 20260929T060112Z",
            },
            result.HeaderLines.ToArray());
    }

    [TestMethod]
    public void Sign_S3UploadWithCustomHeaders_SignsThemAndLeavesThePayloadUnsigned()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", "AWS:Amz:us-east-1:s3");
        diagnostics.Arrange("request", "PUT /a%2Fb?x=%2b&y=a+b&=z&&m, upload size 3, at 20260929T060349Z");
        diagnostics.Arrange("custom headers", "X-Custom:  a   b  | x-custom: c | Empty; | Gone:");

        AwsSigV4SigningResult result = SignAt("20260929T060349Z", Loopback("AWS:Amz:us-east-1:s3") with
        {
            Method = "PUT",
            Path = "/a%2Fb",
            Query = "x=%2b&y=a+b&=z&&m",
            CustomHeaders = ["X-Custom:  a   b ", "x-custom: c", "Empty;", "Gone:"],
            UploadFileSize = 3,
        });

        diagnostics.Act("header lines", string.Join(" | ", result.HeaderLines));
        diagnostics.Act("canonical request", result.CanonicalRequest);
        diagnostics.Assert("header line count", 3, result.HeaderLines.Count);
        diagnostics.Diff("payload hash line", "x-Amz-content-sha256: UNSIGNED-PAYLOAD", result.HeaderLines[2]);
        CollectionAssert.AreEqual(
            new[]
            {
                "Authorization: AWS4-HMAC-SHA256 Credential=AKID/20260929/us-east-1/s3/aws4_request, SignedHeaders=empty;host;x-amz-content-sha256;x-amz-date;x-custom, Signature=9996afa707ad405ea9dab3f80bb1effd66daa8eeafd074851850b20154af86e5",
                "X-Amz-Date: 20260929T060349Z",
                "x-Amz-content-sha256: UNSIGNED-PAYLOAD",
            },
            result.HeaderLines.ToArray());
        StringAssert.Contains(result.CanonicalRequest, "\n(nil)=z&m=&x=%2B&y=a%20b\n", StringComparison.Ordinal);
        StringAssert.Contains(result.CanonicalRequest, "\nx-custom:a b,c\n", StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("20260929T061026Z", "/a%2Fb", null, new string[0], "d7c0534b5b2c357abfa727717cd1f5905002657579700549d0bade7ea2dab2d2", DisplayName = "Encoded slash in the path")]
    [DataRow("20260929T061027Z", "/a", "x=%2b&y=a+b&=z&&m", new string[0], "1c227a0ed931430f5211609a2cde467c1cd6d0d017c16924d88fc3d8e6082362", DisplayName = "Query to normalize")]
    [DataRow("20260929T061053Z", "/a", "x=%2b", new string[0], "d9ae6de365ab55c550628604f79e36f6710c0c815ccc96986d0ba810e37e5be2", DisplayName = "Encoded plus")]
    [DataRow("20260929T061054Z", "/a", "y=a+b", new string[0], "395610468313bf42702ab19be2d03d0ecd125391b2a2656b52dd92ea3e6bd214", DisplayName = "Literal plus")]
    [DataRow("20260929T061055Z", "/a", "=z&m", new string[0], "d4c62545fdd3f746d528a7ce23f7c23709ae84b3b7f51b9aaca5da806c802cd8", DisplayName = "Empty key and no value")]
    [DataRow("20260929T061056Z", "/a", "b&&a", new string[0], "174971772033ae0eae9841258e3ecff391e6dbfbf80da81c2a05bc95dbf95b39", DisplayName = "Empty component")]
    [DataRow("20260929T061028Z", "/a", null, new[] { "X-Custom:  a   b ", "x-custom: c" }, "0d376d065e49ac3345f887b17c2b91ce8ab5a8371563d870dd9356a0a68cf7fe", DisplayName = "Repeated header to trim")]
    [DataRow("20260929T061028Z", "/a", null, new[] { "Empty;", "Gone:" }, "8d8e7b25f4800da561637d53e319a5fc2a837b3521cc9085860d8c0cb4adf115", DisplayName = "Empty and removed headers")]
    public void Sign_S3UploadPart_GivesTheMeasuredSignature(string timestamp, string path, string? query, string[] headers, string signature)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("timestamp", timestamp);
        diagnostics.Arrange("request", "PUT " + path + " query " + query + " upload size 3");
        diagnostics.Arrange("custom headers", string.Join(" | ", headers));

        AwsSigV4SigningResult result = SignAt(timestamp, Loopback("AWS:Amz:us-east-1:s3") with
        {
            Method = "PUT",
            Path = path,
            Query = query,
            CustomHeaders = headers,
            UploadFileSize = 3,
        });

        diagnostics.Act("authorization", result.HeaderLines[0]);
        diagnostics.Assert("signature suffix", ", Signature=" + signature, result.HeaderLines[0]);
        StringAssert.EndsWith(result.HeaderLines[0], ", Signature=" + signature, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Sign_CustomAmzDateAndAnEscapedUser_UsesTheCustomDateAndEncodesTheNonS3Path()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", "aws:amz:eu-west-1:execute-api");
        diagnostics.Arrange("user", "AK ID");
        diagnostics.Arrange("request", "GET /p%20q/~x at 20990101T000000Z with X-Amz-Date: 20200102T030405Z");

        AwsSigV4SigningResult result = SignAt("20990101T000000Z", Loopback("aws:amz:eu-west-1:execute-api") with
        {
            UserName = "AK ID",
            Path = "/p%20q/~x",
            CustomHeaders = ["X-Amz-Date: 20200102T030405Z"],
            IsGetOrHead = true,
        });

        diagnostics.Act("header lines", string.Join(" | ", result.HeaderLines));
        diagnostics.Act("canonical request", result.CanonicalRequest);
        diagnostics.Assert("header line count", 1, result.HeaderLines.Count);
        diagnostics.Diff(
            "authorization",
            "Authorization: AWS4-HMAC-SHA256 Credential=AK%20ID/20200102/eu-west-1/execute-api/aws4_request, SignedHeaders=host;x-amz-date, Signature=efe7ddea6a755246b7d5c9e76acb3a9d9aa49ead89c8868903e8319743245724",
            result.HeaderLines[0]);
        CollectionAssert.AreEqual(
            new[]
            {
                "Authorization: AWS4-HMAC-SHA256 Credential=AK%20ID/20200102/eu-west-1/execute-api/aws4_request, SignedHeaders=host;x-amz-date, Signature=efe7ddea6a755246b7d5c9e76acb3a9d9aa49ead89c8868903e8319743245724",
            },
            result.HeaderLines.ToArray());
        StringAssert.StartsWith(result.CanonicalRequest, "GET\n/p%2520q/~x\n\n", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Sign_NoRegionOrServiceAndAnIpv4Host_TakesThemFromTheAddress()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", "aws:amz");
        diagnostics.Arrange("host", "127.0.0.1");
        diagnostics.Arrange("request", "GET / at 20260929T060352Z");

        AwsSigV4SigningResult result = SignAt("20260929T060352Z", Loopback("aws:amz") with { IsGetOrHead = true });

        diagnostics.Act("authorization", result.HeaderLines[0]);
        diagnostics.Diff(
            "authorization",
            "Authorization: AWS4-HMAC-SHA256 Credential=AKID/20260929/0/127/aws4_request, SignedHeaders=host;x-amz-date, Signature=1be685a1c134867c1550e80a9b0a11d03e2f673ddd21eef3c82a81d2597ffb35",
            result.HeaderLines[0]);
        Assert.AreEqual(
            "Authorization: AWS4-HMAC-SHA256 Credential=AKID/20260929/0/127/aws4_request, SignedHeaders=host;x-amz-date, Signature=1be685a1c134867c1550e80a9b0a11d03e2f673ddd21eef3c82a81d2597ffb35",
            result.HeaderLines[0]);
    }

    [TestMethod]
    public void Sign_RegionAndServiceGiven_IgnoresTheHost()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", "aws:amz:eu:svc");
        diagnostics.Arrange("host", "x:18628");
        diagnostics.Arrange("request", "GET / at 20260929T060411Z");

        AwsSigV4SigningResult result = SignAt("20260929T060411Z", Loopback("aws:amz:eu:svc") with
        {
            HostName = "x",
            HostHeaderValue = "x:18628",
            IsGetOrHead = true,
        });

        diagnostics.Act("authorization", result.HeaderLines[0]);
        diagnostics.Diff(
            "authorization",
            "Authorization: AWS4-HMAC-SHA256 Credential=AKID/20260929/eu/svc/aws4_request, SignedHeaders=host;x-amz-date, Signature=1b004d41baf846bc2c03f4924afe6a6299337ad85e5f6ee02da0614b01290ab6",
            result.HeaderLines[0]);
        Assert.AreEqual(
            "Authorization: AWS4-HMAC-SHA256 Credential=AKID/20260929/eu/svc/aws4_request, SignedHeaders=host;x-amz-date, Signature=1b004d41baf846bc2c03f4924afe6a6299337ad85e5f6ee02da0614b01290ab6",
            result.HeaderLines[0]);
    }

    [TestMethod]
    public void Sign_ACustomDateHeaderThatIsNoTimestamp_SignsAnEmptyDate()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", "aws:amz:r:s");
        diagnostics.Arrange("custom headers", "Date: Mon, 01 Jan 2024 | Authorization2: x");
        diagnostics.Arrange("request", "GET / at 20260929T060411Z");

        AwsSigV4SigningResult result = SignAt("20260929T060411Z", Loopback("aws:amz:r:s") with
        {
            CustomHeaders = ["Date: Mon, 01 Jan 2024", "Authorization2: x"],
            IsGetOrHead = true,
        });

        diagnostics.Act("header lines", string.Join(" | ", result.HeaderLines));
        diagnostics.Assert("header line count", 1, result.HeaderLines.Count);
        diagnostics.Diff(
            "authorization",
            "Authorization: AWS4-HMAC-SHA256 Credential=AKID//r/s/aws4_request, SignedHeaders=authorization2;date;host, Signature=d97f0e32ec62a93e5ccd1a376b0b8ed9853c1aea17d7efccdbed7541627d9691",
            result.HeaderLines[0]);
        CollectionAssert.AreEqual(
            new[]
            {
                "Authorization: AWS4-HMAC-SHA256 Credential=AKID//r/s/aws4_request, SignedHeaders=authorization2;date;host, Signature=d97f0e32ec62a93e5ccd1a376b0b8ed9853c1aea17d7efccdbed7541627d9691",
            },
            result.HeaderLines.ToArray());
    }

    [TestMethod]
    [DataRow("localhost", "aws:amz", CurlExitCode.UrlMalformat, "aws-sigv4: service missing in parameters and hostname", DisplayName = "No dot in the host")]
    [DataRow("svc.localhost", "aws:amz", CurlExitCode.UrlMalformat, "aws-sigv4: region missing in parameters and hostname", DisplayName = "One dot in the host")]
    [DataRow(".b.c", "aws:amz", CurlExitCode.UrlMalformat, "aws-sigv4: service missing in parameters and hostname", DisplayName = "Empty first label")]
    [DataRow("a..c", "aws:amz", CurlExitCode.UrlMalformat, "aws-sigv4: region missing in parameters and hostname", DisplayName = "Empty second label")]
    [DataRow("a.b.c", ":amz", CurlExitCode.BadFunctionArgument, "first aws-sigv4 provider cannot be empty", DisplayName = "Empty first provider")]
    public void Sign_ParameterOrHostCurlRejects_FailsAsCurlDoes(string hostName, string parameter, CurlExitCode exitCode, string message)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("host", hostName);
        diagnostics.Arrange("provider", parameter);

        AwsSigV4SigningResult result = SignAt("20260929T060411Z", Loopback(parameter) with { HostName = hostName });

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Assert("exit code", exitCode, result.ExitCode);
        diagnostics.Assert("error message", message, result.ErrorMessage);
        diagnostics.Assert("header line count", 0, result.HeaderLines.Count);
        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        Assert.IsEmpty(result.HeaderLines);
        Assert.IsNull(result.CanonicalRequest);
        Assert.IsNull(result.StringToSign);
    }

    [TestMethod]
    public void Sign_FirstProviderLongerThan64_FailsAsEmpty()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider length", 65);
        diagnostics.Arrange("request", "GET / at 20260929T060411Z");

        AwsSigV4SigningResult result = SignAt("20260929T060411Z", Loopback(new string('a', 65)));

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Assert("exit code", CurlExitCode.BadFunctionArgument, result.ExitCode);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, result.ExitCode);
    }

    [TestMethod]
    [DataRow("aws", "AWS4-HMAC-SHA256 Credential=AKID/20260929/r/s/aws4_request, SignedHeaders=host;x-aws-date,", DisplayName = "One provider names both")]
    [DataRow("aws::r:s", "AWS4-HMAC-SHA256 Credential=AKID/20260929/r/s/aws4_request, SignedHeaders=host;x-aws-date,", DisplayName = "Empty second provider: the host names the rest")]
    [DataRow("", "AWS4-HMAC-SHA256 Credential=AKID/20260929/r/s/aws4_request, SignedHeaders=host;x-amz-date,", DisplayName = "Empty means aws:amz")]
    [DataRow("aws:amz:eu", "AWS4-HMAC-SHA256 Credential=AKID/20260929/eu/s/aws4_request, SignedHeaders=host;x-amz-date,", DisplayName = "Region given, service from the host")]
    [DataRow("aws:amz::x", "AWS4-HMAC-SHA256 Credential=AKID/20260929/r/s/aws4_request, SignedHeaders=host;x-amz-date,", DisplayName = "Empty region: the service is not read")]
    [DataRow("aws:amz:eu:", "AWS4-HMAC-SHA256 Credential=AKID/20260929/eu/s/aws4_request, SignedHeaders=host;x-amz-date,", DisplayName = "Empty service")]
    public void Sign_PartialParameter_CompletesItAsCurlDoes(string parameter, string expectedStart)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", parameter);
        diagnostics.Arrange("host", "s.r.example");

        AwsSigV4SigningResult result = SignAt("20260929T060411Z", Loopback(parameter) with { HostName = "s.r.example" });

        diagnostics.Act("authorization", result.HeaderLines[0]);
        diagnostics.Assert("authorization start", "Authorization: " + expectedStart, result.HeaderLines[0]);
        StringAssert.StartsWith(result.HeaderLines[0], "Authorization: " + expectedStart, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Sign_SecondProviderLongerThan64_FallsBackToTheFirst()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", "aws:<65 b>:r:s");
        diagnostics.Arrange("host", "s.r.example");

        AwsSigV4SigningResult result = SignAt("20260929T060411Z", Loopback("aws:" + new string('b', 65) + ":r:s") with { HostName = "s.r.example" });

        diagnostics.Act("header lines", string.Join(" | ", result.HeaderLines));
        diagnostics.Diff("date line", "X-Aws-Date: 20260929T060411Z", result.HeaderLines[1]);
        Assert.AreEqual("X-Aws-Date: 20260929T060411Z", result.HeaderLines[1]);
    }

    [TestMethod]
    public void Sign_PathAsIs_FailsAsCurlDoes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", "aws:amz:r:s");
        diagnostics.Arrange("path as is", true);

        AwsSigV4SigningResult result = SignAt("20260929T060411Z", Loopback("aws:amz:r:s") with { PathAsIs = true });

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Assert("exit code", CurlExitCode.BadFunctionArgument, result.ExitCode);
        diagnostics.Diff("error message", "Cannot use sigv4 authentication with path-as-is flag", string.Concat(result.ErrorMessage));
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, result.ExitCode);
        Assert.AreEqual("Cannot use sigv4 authentication with path-as-is flag", result.ErrorMessage);
    }

    [TestMethod]
    public void Sign_CustomAuthorizationHeader_SignsNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", ":bad");
        diagnostics.Arrange("custom headers", "authorization: Bearer x");

        AwsSigV4SigningResult result = SignAt("20260929T060411Z", Loopback(":bad") with { CustomHeaders = ["authorization: Bearer x"] });

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("header lines", string.Join(" | ", result.HeaderLines));
        diagnostics.Assert("is the not-signed result", true, ReferenceEquals(AwsSigV4SigningResult.NotSigned, result));
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(AwsSigV4SigningResult.NotSigned, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsEmpty(result.HeaderLines);
    }

    [TestMethod]
    public void Sign_DateHeaderWrittenWithASemicolon_FailsOutOfMemoryAsCurlDoes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", "aws:amz:r:s");
        diagnostics.Arrange("custom headers", "X-Amz-Date;");

        AwsSigV4SigningResult result = SignAt("20260929T060411Z", Loopback("aws:amz:r:s") with { CustomHeaders = ["X-Amz-Date;"] });

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Assert("exit code", CurlExitCode.OutOfMemory, result.ExitCode);
        diagnostics.Diff("error message", "Out of memory", string.Concat(result.ErrorMessage));
        Assert.AreEqual(CurlExitCode.OutOfMemory, result.ExitCode);
        Assert.AreEqual("Out of memory", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(127, CurlExitCode.Ok, DisplayName = "127 components sign")]
    [DataRow(128, CurlExitCode.TooLarge, DisplayName = "128 components are too many")]
    public void Sign_ManyQueryComponents_FailsAt128AsCurlDoes(int count, CurlExitCode exitCode)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("query component count", count);
        diagnostics.Arrange("expected exit code", exitCode);
        string query = string.Concat(Enumerable.Range(1, count).Select(i => "a=" + i.ToString(CultureInfo.InvariantCulture) + "&"));

        AwsSigV4SigningResult result = SignAt("20260929T060411Z", Loopback("aws:amz:r:s") with { Query = query });

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Assert("exit code", exitCode, result.ExitCode);
        Assert.AreEqual(exitCode, result.ExitCode);
    }

    [TestMethod]
    public void Sign_TooManyQueryComponents_PrintsCurlsMessage()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("query component count", 128);
        string query = string.Concat(Enumerable.Repeat("a&", 128));

        AwsSigV4SigningResult result = SignAt("20260929T060411Z", Loopback("aws:amz:r:s") with { Query = query });

        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Diff("error message", "HTTP request too large", string.Concat(result.ErrorMessage));
        Assert.AreEqual("HTTP request too large", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("x-amz-content-sha256: abc ", "abc", DisplayName = "Value trimmed")]
    [DataRow("X-AMZ-CONTENT-SHA256:UNSIGNED-PAYLOAD", "UNSIGNED-PAYLOAD", DisplayName = "Name case-insensitive")]
    public void Sign_CustomContentHashHeader_SignsItsValueAndAddsNoHeader(string header, string payloadHash)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", "aws:amz:r:s3");
        diagnostics.Arrange("custom header", header);

        AwsSigV4SigningResult result = SignAt("20260929T060411Z", Loopback("aws:amz:r:s3") with { CustomHeaders = [header] });

        diagnostics.Act("canonical request", result.CanonicalRequest);
        diagnostics.Act("header lines", string.Join(" | ", result.HeaderLines));
        diagnostics.Assert("header line count", 2, result.HeaderLines.Count);
        diagnostics.Assert("payload hash", payloadHash, result.CanonicalRequest);
        StringAssert.EndsWith(result.CanonicalRequest, "\n" + payloadHash, StringComparison.Ordinal);
        Assert.HasCount(2, result.HeaderLines);
    }

    [TestMethod]
    public void Sign_CustomContentHashHeaderWithASemicolon_ComputesTheHashAndMergesBoth()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", "aws:amz:r:s3");
        diagnostics.Arrange("custom header", "x-amz-content-sha256;");

        AwsSigV4SigningResult result = SignAt("20260929T060411Z", Loopback("aws:amz:r:s3") with
        {
            CustomHeaders = ["x-amz-content-sha256;"],
            IsGetOrHead = true,
        });

        diagnostics.Act("canonical request", result.CanonicalRequest);
        diagnostics.Act("header lines", string.Join(" | ", result.HeaderLines));
        diagnostics.Diff("content hash line", "x-amz-content-sha256: " + EmptyHash, result.HeaderLines[2]);
        StringAssert.Contains(result.CanonicalRequest, "\nx-amz-content-sha256:" + EmptyHash + ",\n", StringComparison.Ordinal);
        Assert.AreEqual("x-amz-content-sha256: " + EmptyHash, result.HeaderLines[2]);
    }

    [TestMethod]
    [DataRow(0L, null, EmptyHash, DisplayName = "Empty upload")]
    [DataRow(-1L, null, "UNSIGNED-PAYLOAD", DisplayName = "Form or unknown size")]
    [DataRow(-1L, "abc", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", DisplayName = "In-memory fields")]
    public void Sign_S3NonGetPayload_HashesWhatIsKnown(long uploadFileSize, string? postFields, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("upload file size", uploadFileSize);
        diagnostics.Arrange("post fields", postFields);

        AwsSigV4SigningResult result = SignAt("20260929T060411Z", Loopback("aws:amz:r:s3") with
        {
            Method = "POST",
            UploadFileSize = uploadFileSize,
            PostFields = postFields is null ? null : Encoding.ASCII.GetBytes(postFields),
        });

        diagnostics.Act("header lines", string.Join(" | ", result.HeaderLines));
        diagnostics.Diff("content hash line", "x-amz-content-sha256: " + expected, result.HeaderLines[2]);
        Assert.AreEqual("x-amz-content-sha256: " + expected, result.HeaderLines[2]);
    }

    [TestMethod]
    public void Sign_S3ForAnotherProvider_AddsNoContentHashHeader()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", "osc:osc:r:s3");
        diagnostics.Arrange("upload file size", 5);

        AwsSigV4SigningResult result = SignAt("20260929T060411Z", Loopback("osc:osc:r:s3") with { UploadFileSize = 5 });

        diagnostics.Act("header lines", string.Join(" | ", result.HeaderLines));
        diagnostics.Act("canonical request", result.CanonicalRequest);
        diagnostics.Assert("header line count", 2, result.HeaderLines.Count);
        Assert.HasCount(2, result.HeaderLines);
        StringAssert.EndsWith(result.CanonicalRequest, "\n" + EmptyHash, StringComparison.Ordinal);
        StringAssert.StartsWith(result.CanonicalRequest, "GET\n/\n", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Sign_CustomHostHeader_SignsItInsteadOfCurls()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", "aws:amz:r:s");
        diagnostics.Arrange("custom headers", "Host: other.example | Blank:    | NoSeparator | Tab;\\t");

        AwsSigV4SigningResult result = SignAt("20260929T060411Z", Loopback("aws:amz:r:s") with
        {
            CustomHeaders = ["Host: other.example", "Blank:   ", "NoSeparator", "Tab;\t"],
        });

        diagnostics.Act("canonical request", result.CanonicalRequest);
        diagnostics.Assert("canonical request", "contains host:other.example", result.CanonicalRequest);
        StringAssert.Contains(result.CanonicalRequest, "\nhost:other.example\nx-amz-date:20260929T060411Z\n\nhost;x-amz-date\n", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Sign_QueryNeedingNormalization_EncodesAsCurlDoes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", "aws:amz:r:s");
        diagnostics.Arrange("query", "b=%41%7e%zz%4&a=x=y=&a=&b&%");

        AwsSigV4SigningResult result = SignAt("20260929T060411Z", Loopback("aws:amz:r:s") with
        {
            Query = "b=%41%7e%zz%4&a=x=y=&a=&b&%",
        });

        diagnostics.Act("canonical request", result.CanonicalRequest);
        diagnostics.Assert("canonical request", "contains \\n%25=&a=&a=x%3Dy%3D&b=&b=A~%25zz%254\\n", result.CanonicalRequest);
        StringAssert.Contains(result.CanonicalRequest, "\n%25=&a=&a=x%3Dy%3D&b=&b=A~%25zz%254\n", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Sign_QueryWithEmptyKeys_SignsThemFirstAsNilInTheirOrder()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", "aws:amz:r:s");
        diagnostics.Arrange("query", "z=1&=b&=a");

        AwsSigV4SigningResult result = SignAt("20260929T061201Z", Loopback("aws:amz:r:s") with { Query = "z=1&=b&=a", IsGetOrHead = true });

        diagnostics.Act("canonical request", result.CanonicalRequest);
        diagnostics.Act("authorization", result.HeaderLines[0]);
        diagnostics.Assert("signature suffix", ", Signature=0f2038639d875719aeabded493e9af7723a9449ed50cae92767e9f58997067ad", result.HeaderLines[0]);
        StringAssert.Contains(result.CanonicalRequest, "\n(nil)=b&(nil)=a&z=1\n", StringComparison.Ordinal);
        StringAssert.EndsWith(result.HeaderLines[0], ", Signature=0f2038639d875719aeabded493e9af7723a9449ed50cae92767e9f58997067ad", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Sign_EmptyPath_SignsASlash()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", "aws:amz:r:s3");
        diagnostics.Arrange("path", string.Empty);

        AwsSigV4SigningResult result = SignAt("20260929T060411Z", Loopback("aws:amz:r:s3") with { Path = string.Empty });

        diagnostics.Act("canonical request", result.CanonicalRequest);
        diagnostics.Assert("canonical request start", "GET\n/\n", result.CanonicalRequest);
        StringAssert.StartsWith(result.CanonicalRequest, "GET\n/\n", StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("s3", "/a%20b", DisplayName = "s3")]
    [DataRow("s3-express", "/a%20b", DisplayName = "s3-express")]
    [DataRow("s3-outposts", "/a%20b", DisplayName = "s3-outposts")]
    [DataRow("S3", "/a%2520b", DisplayName = "S3 compares case-sensitively")]
    public void Sign_S3Services_SignThePathAsGiven(string service, string expectedPath)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("service", service);
        diagnostics.Arrange("path", "/a%20b");

        AwsSigV4SigningResult result = SignAt("20260929T060411Z", Loopback("aws:amz:r:" + service) with { Path = "/a%20b" });

        diagnostics.Act("canonical request", result.CanonicalRequest);
        diagnostics.Assert("canonical request start", "GET\n" + expectedPath + "\n", result.CanonicalRequest);
        StringAssert.StartsWith(result.CanonicalRequest, "GET\n" + expectedPath + "\n", StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("GET", "/", null, new string[0], "5fa00fa31553b73ebf1942676e86291e8372ff2a2260956d9b8aae1d763fbf31", DisplayName = "get-vanilla")]
    [DataRow("GET", "/", "Param2=value2&Param1=value1", new string[0], "b97d918cfa904a5beff61c982a1b6f458b799221646efd99d3219ec94cdf2500", DisplayName = "get-vanilla-query-order-key-case")]
    [DataRow("POST", "/", null, new string[0], "5da7c1a2acd57cee7505fc6676e4e544621c30862966e37dddb68e92efbe5d6b", DisplayName = "post-vanilla")]
    public void Sign_AwsTestSuiteRequest_GivesThePublishedSignature(string method, string path, string? query, string[] headers, string signature)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("request", method + " " + path + " query " + query + " at 20150830T123600Z");
        diagnostics.Arrange("custom headers", string.Join(" | ", headers));

        AwsSigV4SigningResult result = SignExample("aws:amz:us-east-1:service", "example.amazonaws.com", method, path, query, headers);

        diagnostics.Act("authorization", result.HeaderLines[0]);
        diagnostics.Assert("signature suffix", ", Signature=" + signature, result.HeaderLines[0]);
        StringAssert.EndsWith(result.HeaderLines[0], ", Signature=" + signature, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Sign_AwsIamListUsersExample_GivesThePublishedAuthorization()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", "aws:amz:us-east-1:iam");
        diagnostics.Arrange("request", "GET iam.amazonaws.com/?Action=ListUsers&Version=2010-05-08 at 20150830T123600Z");
        diagnostics.Arrange("access key", ExampleAccessKey);

        AwsSigV4SigningResult result = SignExample(
            "aws:amz:us-east-1:iam",
            "iam.amazonaws.com",
            "GET",
            "/",
            "Action=ListUsers&Version=2010-05-08",
            ["Content-Type: application/x-www-form-urlencoded; charset=utf-8"]);

        diagnostics.Act("authorization", result.HeaderLines[0]);
        diagnostics.Diff(
            "authorization",
            "Authorization: AWS4-HMAC-SHA256 Credential=AKIDEXAMPLE/20150830/us-east-1/iam/aws4_request, SignedHeaders=content-type;host;x-amz-date, Signature=5d672d79c15b13162d9279b0855cfba6789a8edb4c82c400e06b5924a6f2b5d7",
            result.HeaderLines[0]);
        Assert.AreEqual(
            "Authorization: AWS4-HMAC-SHA256 Credential=AKIDEXAMPLE/20150830/us-east-1/iam/aws4_request, SignedHeaders=content-type;host;x-amz-date, Signature=5d672d79c15b13162d9279b0855cfba6789a8edb4c82c400e06b5924a6f2b5d7",
            result.HeaderLines[0]);
    }

    [TestMethod]
    public void Sign_ServiceAndRegionFromHost_ListsBothPickedLinesInCurlsOrder()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", "aws:amz");
        diagnostics.Arrange("host", "s3.eu-west-1.localhost");

        AwsSigV4SigningResult result = SignAt("20260929T060411Z", Loopback("aws:amz") with { HostName = "s3.eu-west-1.localhost" });

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("picked lines", string.Join(" | ", result.PickedFromHostLines));
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        diagnostics.Diff(
            "picked lines",
            "aws_sigv4: picked service s3 from host | aws_sigv4: picked region eu-west-1 from host",
            string.Join(" | ", result.PickedFromHostLines));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "aws_sigv4: picked service s3 from host", "aws_sigv4: picked region eu-west-1 from host" },
            result.PickedFromHostLines.ToArray());
    }

    [TestMethod]
    public void Sign_RegionGivenServiceFromHost_ListsOnlyThePickedServiceLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", "aws:amz:us-east-1");
        diagnostics.Arrange("host", "s3.eu-west-1.localhost");

        AwsSigV4SigningResult result = SignAt("20260929T060411Z", Loopback("aws:amz:us-east-1") with { HostName = "s3.eu-west-1.localhost" });

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("picked lines", string.Join(" | ", result.PickedFromHostLines));
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        diagnostics.Diff("picked lines", "aws_sigv4: picked service s3 from host", string.Join(" | ", result.PickedFromHostLines));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "aws_sigv4: picked service s3 from host" }, result.PickedFromHostLines.ToArray());
    }

    [TestMethod]
    public void Sign_RegionAndServiceGiven_ListsNoPickedLines()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", "aws:amz:us-east-1:s3");
        diagnostics.Arrange("host", "s3.eu-west-1.localhost");

        AwsSigV4SigningResult result = SignAt("20260929T060411Z", Loopback("aws:amz:us-east-1:s3") with { HostName = "s3.eu-west-1.localhost" });

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("picked lines", string.Join(" | ", result.PickedFromHostLines));
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        diagnostics.Assert("picked line count", 0, result.PickedFromHostLines.Count);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsEmpty(result.PickedFromHostLines);
    }

    [TestMethod]
    [DataRow("localhost", "aws-sigv4: service missing in parameters and hostname", DisplayName = "Service missing")]
    [DataRow("s3.localhost", "aws-sigv4: region missing in parameters and hostname", DisplayName = "Region missing")]
    public void Sign_HostLacksServiceOrRegion_FailsWithNoPickedLines(string hostName, string errorMessage)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", "aws:amz");
        diagnostics.Arrange("host", hostName);

        AwsSigV4SigningResult result = SignAt("20260929T060411Z", Loopback("aws:amz") with { HostName = hostName });

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Assert("exit code", CurlExitCode.UrlMalformat, result.ExitCode);
        diagnostics.Assert("error message", errorMessage, result.ErrorMessage);
        diagnostics.Assert("picked line count", 0, result.PickedFromHostLines.Count);
        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual(errorMessage, result.ErrorMessage);
        Assert.IsEmpty(result.PickedFromHostLines);
    }

    [TestMethod]
    public void Sign_CustomAuthorizationHeader_ListsNoPickedLines()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("provider", "aws:amz");
        diagnostics.Arrange("custom headers", "Authorization: Bearer x");

        AwsSigV4SigningResult result = SignAt("20260929T060411Z", Loopback("aws:amz") with { CustomHeaders = ["Authorization: Bearer x"] });

        diagnostics.Act("picked lines", string.Join(" | ", result.PickedFromHostLines));
        diagnostics.Assert("picked line count", 0, result.PickedFromHostLines.Count);
        Assert.IsEmpty(result.PickedFromHostLines);
    }

    [TestMethod]
    public void Sign_NullRequest_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("request", "null");
        AwsSigV4Signer signer = new(TimeProvider.System, Encoding.UTF8);

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => signer.Sign(null!));

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Act("parameter name", exception.ParamName);
        diagnostics.Assert("exception type", typeof(ArgumentNullException), exception.GetType());
    }

    private static AwsSigV4Request Loopback(string parameter) => new()
    {
        SigV4Parameter = parameter,
        UserName = "AKID",
        Password = "SECRET",
        Method = "GET",
        HostName = "127.0.0.1",
        HostHeaderValue = LoopbackHost,
        Path = "/",
    };

    private static AwsSigV4SigningResult SignAt(string timestamp, AwsSigV4Request request)
    {
        DateTimeOffset now = DateTimeOffset.ParseExact(timestamp, "yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        return new AwsSigV4Signer(new FixedTimeProvider(now), Encoding.UTF8).Sign(request);
    }

    private static AwsSigV4SigningResult SignExample(string parameter, string host, string method, string path, string? query, string[] headers) =>
        SignAt("20150830T123600Z", new AwsSigV4Request
        {
            SigV4Parameter = parameter,
            UserName = ExampleAccessKey,
            Password = ExampleSecretKey,
            Method = method,
            HostName = host,
            HostHeaderValue = host,
            Path = path,
            Query = query,
            CustomHeaders = headers,
            IsGetOrHead = method == "GET",
        });
}
