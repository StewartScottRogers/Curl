namespace Curl.Extension.CodeGeneration.UnitTests;

// Checked against the HttpClient documentation: HttpClientHandler.AllowAutoRedirect, MaxAutomaticRedirections and
// AutomaticDecompression; HttpRequestMessage(HttpMethod, string); HttpHeaders.TryAddWithoutValidation; the content
// fields HttpContentHeaders holds; ByteArrayContent; HttpClient.SendAsync; HttpContent.CopyToAsync.
[TestClass]
public sealed class CSharpProgramTests
{
    [TestMethod]
    public void For_AGet_SendsItWithoutFollowingRedirectsOrDecompressing()
    {
        var program = CSharpProgram.For([CodeGenerationExtensionTests.Request()]);

        StringAssert.Contains(program, "AllowAutoRedirect = false,\n");
        StringAssert.Contains(program, "AutomaticDecompression = DecompressionMethods.None,\n");
        Assert.DoesNotContain("MaxAutomaticRedirections", program);
        StringAssert.Contains(program, "using (var request = new HttpRequestMessage(new HttpMethod(\"GET\"), \"https://example.com/\"))\n");
        StringAssert.Contains(program, "    using var response = await client.SendAsync(request);\n    await response.Content.CopyToAsync(output);\n");
        Assert.DoesNotContain("request.Content =", program);
    }

    [TestMethod]
    public void For_FollowingRedirectsAndDecompressing_SetsTheHandler()
    {
        var program = CSharpProgram.For([new GeneratedRequest("GET", "https://example.com/", [], null, true, 5, true)]);

        StringAssert.Contains(program, "AllowAutoRedirect = true,\n    MaxAutomaticRedirections = 5,\n    AutomaticDecompression = DecompressionMethods.All,\n");
    }

    [TestMethod]
    public void For_FollowingRedirectsWithNoLimitOrNone_SetsOnlyAllowAutoRedirect()
    {
        var unlimited = CSharpProgram.For([new GeneratedRequest("GET", "u", [], null, true, -1, false)]);
        var none = CSharpProgram.For([new GeneratedRequest("GET", "u", [], null, true, 0, false)]);

        StringAssert.Contains(unlimited, "AllowAutoRedirect = true,\n    AutomaticDecompression");
        StringAssert.Contains(none, "AllowAutoRedirect = false,\n    AutomaticDecompression");
    }

    [TestMethod]
    public void For_FieldsAndABody_PutsContentFieldsOnTheContent()
    {
        var program = CSharpProgram.For([CodeGenerationExtensionTests.Request("POST", "a=\"1\""u8.ToArray(), new("Accept", "*/*"), new("content-type", "text/x"))]);

        StringAssert.Contains(program, "    request.Headers.TryAddWithoutValidation(\"Accept\", \"*/*\");\n");
        StringAssert.Contains(program, "    request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(\"a=\\\"1\\\"\"));\n");
        StringAssert.Contains(program, "    request.Content!.Headers.TryAddWithoutValidation(\"content-type\", \"text/x\");\n");
    }

    [TestMethod]
    public void For_AContentFieldWithoutABody_SendsAnEmptyContent()
    {
        var program = CSharpProgram.For([CodeGenerationExtensionTests.Request("GET", null, new KeyValuePair<string, string>("Content-Language", "en"))]);

        StringAssert.Contains(program, "    request.Content = new ByteArrayContent([]);\n");
    }

    [TestMethod]
    public void For_ABodyThatIsNotUtf8_SendsItsBytesFromBase64()
    {
        var program = CSharpProgram.For([CodeGenerationExtensionTests.Request("POST", [0xff, 0x00])]);

        StringAssert.Contains(program, "new ByteArrayContent(Convert.FromBase64String(\"/wA=\"));\n");
    }

    [TestMethod]
    public void For_SeveralRequests_SendsEachInOrder()
    {
        var program = CSharpProgram.For([CodeGenerationExtensionTests.Request("GET"), CodeGenerationExtensionTests.Request("DELETE")]);

        Assert.IsLessThan(program.IndexOf("\"DELETE\"", StringComparison.Ordinal), program.IndexOf("\"GET\"", StringComparison.Ordinal));
    }

    // C# language specification, "String literals": simple escape sequences and \u escapes; a newline character,
    // U+2028 and U+2029 among them, may not appear in a regular string literal.
    [TestMethod]
    public void Literal_EscapesWhatARegularStringLiteralCannotHold()
    {
        Assert.AreEqual("\"a\\\\b\\\"c\\n\\r\\t\\u0001\\u007f\\u2028\\u2029é\"", CSharpProgram.Literal("a\\b\"c\n\r\t\u0001\u007f" + (char)0x2028 + (char)0x2029 + "é"));
    }
}
