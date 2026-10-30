namespace Curl.Extension.GrpcWeb;

[TestClass]
public sealed class GrpcWebTrailersTests
{
    [TestMethod]
    public void Parse_AnHttp1HeaderBlockWithoutTheEmptyLine_GivesItsFieldsLowerCasedAndTrimmed_MessageFraming()
    {
        IReadOnlyList<KeyValuePair<string, string>> fields = GrpcWebTrailers.Parse("Grpc-Status: 5\r\ngrpc-message:not%20found\nx-own:  a:b \r\n\r\n"u8);

        CollectionAssert.AreEqual(
            new[] { new KeyValuePair<string, string>("grpc-status", "5"), new("grpc-message", "not%20found"), new("x-own", "a:b") },
            fields.ToArray());
    }

    [TestMethod]
    public void Parse_ALineWithNoColon_ThrowsFormatException()
    {
        FormatException thrown = Assert.ThrowsExactly<FormatException>(() => GrpcWebTrailers.Parse("grpc-status 0"u8));
        StringAssert.Contains(thrown.Message, "\"grpc-status 0\" has no colon");
    }
}
