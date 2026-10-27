using System.Globalization;
using System.Text;
using Curl.Kerberos;

namespace Curl.Authentication;

/// <summary>
/// CGR-119: on Linux, where the argument encoding is UTF-8, a raw non-ASCII path such as
/// <c>/472/a=あ</c> goes on the request line as <c>/472/a=%E3%81%82</c>, and curl signs that
/// path percent-encoded again (<c>/472/a%3D%25E3%2581%2582</c>), giving upstream test472's
/// signature (BL-3648).
/// </summary>
[TestClass]
public sealed class Cgr119Tests
{
    private const string Test472Signature = "b8783c8387a5249b084642126fe1f8e07e12a2847820fd5b6cd64b2047149da4";

    [TestMethod]
    public void Sign_RawNonAsciiPathForEsInUtf8_SignsThePathEncodedTwice()
    {
        AwsSigV4SigningResult result = Sign("/472/a=あ", InvalidUtf8ByteEscapingEncoding.Instance);

        StringAssert.StartsWith(result.CanonicalRequest, "GET\n/472/a%3D%25E3%2581%2582\n\n", StringComparison.Ordinal);
        StringAssert.EndsWith(result.HeaderLines[0], "Signature=" + Test472Signature, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Sign_PreEncodedPathForEs_StillGivesTest472Signature()
    {
        AwsSigV4SigningResult result = Sign("/472/a=%E3%81%82", InvalidUtf8ByteEscapingEncoding.Instance);

        StringAssert.EndsWith(result.HeaderLines[0], "Signature=" + Test472Signature, StringComparison.Ordinal);
    }

    private static AwsSigV4SigningResult Sign(string path, Encoding encoding)
    {
        AwsSigV4Request request = new()
        {
            SigV4Parameter = "aws:amz:us-east-2:es",
            UserName = "user",
            Password = "secret",
            Method = "GET",
            HostName = "fake.fake.fake",
            HostHeaderValue = "fake.fake.fake:8000",
            Path = path,
            CustomHeaders = ["X-Amz-Date: 19700101T000000Z"],
            IsGetOrHead = true,
        };
        DateTimeOffset now = DateTimeOffset.Parse("1970-01-01T00:00:00Z", CultureInfo.InvariantCulture);

        return new AwsSigV4Signer(new FixedTimeProvider(now), encoding).Sign(request);
    }
}
