using Curl.Testing;

namespace Curl.Console.UnitTests;

/// <summary>Pins the Zone.Identifier stream <c>--xattr</c> writes on Windows (ADR-1287, BL-5338), through a stand-in stream write.</summary>
[TestClass]
public sealed class WindowsZoneIdentifierWriterTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private static (string? Path, string? Text, bool Ok, string Error) Run(Func<string, string, string?>? write, string? referer)
    {
        string? path = null;
        string? text = null;
        WindowsZoneIdentifierWriter writer = new(write ?? ((p, t) => { path = p; text = t; return null; }));
        IReadOnlyList<KeyValuePair<string, string>> attributes = OutputFileExtendedAttributes.For("http://u:p@host/hello?q=1#f", referer, "text/plain");
        string error = OutputFileExtendedAttributes.Write(writer, "x.txt", attributes) ?? string.Empty;
        return (path, text, error.Length == 0, error);
    }

    [TestMethod]
    public void Write_UrlWithCredentials_WritesHostUrlWithoutThem()
    {
        (string? path, string? text, bool ok, _) = Run(null, null);

        Diagnostics.Assert("stream", "x.txt:Zone.Identifier", path);
        Assert.IsTrue(ok);
        Assert.AreEqual("x.txt:Zone.Identifier", path);
        Assert.AreEqual("[ZoneTransfer]\r\nHostUrl=http://host/hello?q=1#f\r\n", text);
    }

    [TestMethod]
    public void Write_WithReferer_AddsReferrerUrl()
    {
        (_, string? text, _, _) = Run(null, "http://ref.example/");

        Assert.AreEqual("[ZoneTransfer]\r\nHostUrl=http://host/hello?q=1#f\r\nReferrerUrl=http://ref.example/\r\n", text);
    }

    [TestMethod]
    public void Write_StreamWriteFails_ReportsCurlsWarning()
    {
        (_, _, bool ok, string error) = Run((_, _) => "denied", null);

        Assert.IsFalse(ok);
        Assert.AreEqual("Warning: Error setting extended attributes on 'x.txt': denied", error);
    }

    [TestMethod]
    public void TryWrite_SecondFile_DoesNotKeepTheFirstFilesReferrer()
    {
        List<string> texts = [];
        WindowsZoneIdentifierWriter writer = new((_, t) => { texts.Add(t); return null; });
        writer.TryWrite("a", OutputFileExtendedAttributes.ReferrerUrlName, "http://r/", out _);
        writer.TryWrite("a", OutputFileExtendedAttributes.OriginUrlName, "http://h/", out _);
        writer.TryWrite("b", OutputFileExtendedAttributes.OriginUrlName, "http://h/", out _);

        Assert.AreEqual("[ZoneTransfer]\r\nHostUrl=http://h/\r\n", texts[1]);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void RealWrite_OnWindows_CreatesTheStream()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bl5338-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "x.txt");
            File.WriteAllText(file, "body");
            IExtendedAttributeWriter writer = NativeExtendedAttributeWriter.ForCurrentPlatform()!;
            Assert.IsTrue(writer.TryWrite(file, OutputFileExtendedAttributes.OriginUrlName, "http://host/a", out string error), error);
            Assert.AreEqual("[ZoneTransfer]\r\nHostUrl=http://host/a\r\n", File.ReadAllText(file + ":Zone.Identifier"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
