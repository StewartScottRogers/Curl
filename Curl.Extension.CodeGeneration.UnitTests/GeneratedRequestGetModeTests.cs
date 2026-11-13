namespace Curl.Extension.CodeGeneration.UnitTests;

/// <summary>
/// <c>-G</c> moves the data into the query and the transfer carries no body (ADR-1138 "What is sent", BL-5125):
/// the generated code is the GET or HEAD curl sends, never a POST with a body.
/// </summary>
[TestClass]
public sealed class GeneratedRequestGetModeTests
{
    [TestMethod]
    public void Generate_GetWithDataInTheQuery_IsAGetWithNoBody()
    {
        var request = GeneratedRequest.From(Context("http://127.0.0.1:28000/echo?a=1"));

        var csharp = CSharpProgram.For([request]);
        var powershell = PowerShellScript.For([request]);

        StringAssert.Contains(csharp, "new HttpMethod(\"GET\"), \"http://127.0.0.1:28000/echo?a=1\"");
        Assert.DoesNotContain("request.Content", csharp);
        Assert.DoesNotContain("Content-Type", csharp);
        StringAssert.Contains(powershell, "-Method Get");
        Assert.DoesNotContain("-Body", powershell);
    }

    [TestMethod]
    public void Generate_GetWithEncodedDataInTheQuery_KeepsTheEncodedQueryAndNoBody()
    {
        var request = GeneratedRequest.From(Context("http://127.0.0.1:28000/echo?q=a%20b"));

        var csharp = CSharpProgram.For([request]);
        var powershell = PowerShellScript.For([request]);

        StringAssert.Contains(csharp, "\"http://127.0.0.1:28000/echo?q=a%20b\"");
        Assert.DoesNotContain("request.Content", csharp);
        StringAssert.Contains(powershell, "echo?q=a%20b' -Method Get");
        Assert.DoesNotContain("-Body", powershell);
    }

    [TestMethod]
    public void Generate_HeadWithDataInTheQuery_IsAHeadWithNoBody()
    {
        var request = GeneratedRequest.From(new TransferContext { Url = CurlUrl.Parse("http://127.0.0.1:28000/echo?a=1"), Output = Stream.Null, NoBody = true });

        var csharp = CSharpProgram.For([request]);
        var powershell = PowerShellScript.For([request]);

        Assert.AreEqual("HEAD", request.Method);
        Assert.IsTrue(csharp.Contains("HttpMethod.Head", StringComparison.Ordinal) || csharp.Contains("new HttpMethod(\"HEAD\")", StringComparison.Ordinal));
        Assert.DoesNotContain("request.Content", csharp);
        StringAssert.Contains(powershell, "-Method Head");
        Assert.DoesNotContain("-Body", powershell);
    }

    [TestMethod]
    public void Generate_DataWithoutGet_IsAPostWithTheBody()
    {
        var request = GeneratedRequest.From(new TransferContext { Url = CurlUrl.Parse("http://127.0.0.1:28000/echo"), Output = Stream.Null, PostData = "a=1"u8.ToArray() });

        var csharp = CSharpProgram.For([request]);
        var powershell = PowerShellScript.For([request]);

        StringAssert.Contains(csharp, "new HttpMethod(\"POST\")");
        StringAssert.Contains(csharp, "request.Content");
        StringAssert.Contains(powershell, "-Method Post");
        StringAssert.Contains(powershell, "-Body");
    }

    private static TransferContext Context(string url) => new() { Url = CurlUrl.Parse(url), Output = Stream.Null };
}
