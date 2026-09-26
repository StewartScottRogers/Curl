using Curl.Protocol.Abstractions;
using Curl.Protocol.File;

namespace Curl.Console;

/// <summary>
/// Pins the composition root: which handlers the executable registers, and that the
/// production composition performs a <c>file://</c> transfer end to end.
/// </summary>
[TestClass]
public sealed class CurlCompositionTests
{
    [TestMethod]
    public void CreateProtocolHandlers_ServesFileThroughFileProtocolHandler()
    {
        IReadOnlyList<IProtocolHandler> handlers = CurlComposition.CreateProtocolHandlers();

        IProtocolHandler handler = handlers.Single();
        Assert.IsInstanceOfType<FileProtocolHandler>(handler);
        CollectionAssert.Contains(handler.SupportedSchemes.ToArray(), "file");
    }

    [TestMethod]
    public async Task CreateRunner_FileUrlOfTemporaryFile_WritesItsBytesToStandardOutput()
    {
        string path = Path.Combine(Path.GetTempPath(), $"curl-bl068-{Guid.NewGuid():N}.bin");
        byte[] content = [0, 1, 2, 13, 10, 255, (byte)'x'];
        await System.IO.File.WriteAllBytesAsync(path, content);

        try
        {
            using MemoryStream standardOutput = new();
            using MemoryStream standardError = new();

            int exitCode = await CurlComposition.CreateRunner(standardOutput, standardError)
                .RunAsync([new Uri(path).AbsoluteUri]);

            Assert.AreEqual(0, exitCode);
            CollectionAssert.AreEqual(content, standardOutput.ToArray());
            Assert.AreEqual(0, standardError.Length);
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }
}
