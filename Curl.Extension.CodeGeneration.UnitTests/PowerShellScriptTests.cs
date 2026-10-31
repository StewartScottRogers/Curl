namespace Curl.Extension.CodeGeneration.UnitTests;

// Checked against the Invoke-RestMethod documentation (PowerShell 7): -Uri, -Method (WebRequestMethod), -CustomMethod,
// -Headers, -ContentType, -Body, -MaximumRedirection, -SkipHeaderValidation and -SkipHttpErrorCheck; and
// about_Quoting_Rules for single-quoted strings.
[TestClass]
public sealed class PowerShellScriptTests
{
    [TestMethod]
    public void For_AGet_SendsItWithoutFollowingRedirects()
    {
        var script = PowerShellScript.For([CodeGenerationExtensionTests.Request()]);

        Assert.AreEqual(
            "# Written by curl --powershell: the requests this command line sends, with Invoke-RestMethod.\n\n"
            + "Invoke-RestMethod -Uri 'https://example.com/' -Method Get -MaximumRedirection 0 -SkipHeaderValidation -SkipHttpErrorCheck\n",
            script);
    }

    [TestMethod]
    public void For_AMethodWebRequestMethodDoesNotName_UsesCustomMethod()
    {
        StringAssert.Contains(PowerShellScript.For([CodeGenerationExtensionTests.Request("PROPFIND")]), " -CustomMethod 'PROPFIND' ");
        StringAssert.Contains(PowerShellScript.For([CodeGenerationExtensionTests.Request("get")]), " -CustomMethod 'get' ");
        StringAssert.Contains(PowerShellScript.For([CodeGenerationExtensionTests.Request("PATCH")]), " -Method Patch ");
    }

    [TestMethod]
    public void For_FieldsAndABody_GivesHeadersContentTypeAndBytes()
    {
        var script = PowerShellScript.For([CodeGenerationExtensionTests.Request(
            "POST",
            "it's"u8.ToArray(),
            new("Accept", "a"), new("X-Many", "1"), new("x-many", "2"), new("Content-Type", "text/x"))]);

        StringAssert.Contains(script, " -Headers @{ 'Accept' = 'a'; 'X-Many' = '1, 2' }");
        StringAssert.Contains(script, " -ContentType 'text/x' -Body ([System.Text.Encoding]::UTF8.GetBytes('it''s'))");
    }

    [TestMethod]
    public void For_ABodyThatIsNotUtf8_SendsItsBytesFromBase64()
    {
        StringAssert.Contains(PowerShellScript.For([CodeGenerationExtensionTests.Request("POST", [0xff, 0x00])]), " -Body ([System.Convert]::FromBase64String('/wA='))");
    }

    [TestMethod]
    public void For_FollowingRedirects_SetsMaximumRedirection()
    {
        Assert.Contains(" -MaximumRedirection 7 ", PowerShellScript.For([new GeneratedRequest("GET", "u", [], null, true, 7, false)]));
        Assert.Contains(" -MaximumRedirection 2147483647 ", PowerShellScript.For([new GeneratedRequest("GET", "u", [], null, true, -1, false)]));
    }

    [TestMethod]
    public void Literal_DoublesEveryQuoteCharacterPowerShellReads()
    {
        Assert.AreEqual("'a''b‘‘’’‚‚‛‛$x'", PowerShellScript.Literal("a'b‘’‚‛$x"));
    }
}
