using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Curl.Output;

/// <summary>Pins that a known variable name longer than curl's name buffer (an extension's) still renders (BL-4919).</summary>
[TestClass]
public sealed class StructuredFieldWriteOutTests
{
    [TestMethod]
    public async Task RenderAsync_KnownVariableLongerThanNameBuffer_RendersItAndTheRestOfTheTemplate()
    {
        using MemoryStream output = new();
        using MemoryStream error = new();
        WriteOutTemplateRenderer renderer = new(new NoFiles(), false, WriteOutTimeDialect.WindowsCRuntime, TimeProvider.System);

        await renderer.RenderAsync("A[%{structured_field.priority}]B", new Source(), output, error);

        Assert.AreEqual("A[Dictionary of 2: u=Integer 1; i=Boolean true]B", Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task RenderAsync_MixedAndUpperCaseNames_RenderLikeTheLowerCaseName()
    {
        string output = await RenderAsync("A[%{structured_field.Priority}]B[%{structured_field.Cache-Status}]C[%{structured_field.PRIORITY}]D");

        Assert.AreEqual("A[Dictionary of 2: u=Integer 1; i=Boolean true]B[List of 1: Token ExampleCache [hit=Boolean true, ttl=Integer 30]]C[Dictionary of 2: u=Integer 1; i=Boolean true]D", output);
    }

    [TestMethod]
    public async Task RenderAsync_UnknownVariableLongerThanNameBuffer_PrintsNothingForItAndTheRestOfTheTemplate()
    {
        string output = await RenderAsync("A[%{structured_field.nosuchfield}]B");

        Assert.AreEqual("A[]B", output);
    }

    private static async Task<string> RenderAsync(string template)
    {
        using MemoryStream output = new();
        using MemoryStream error = new();
        WriteOutTemplateRenderer renderer = new(new NoFiles(), false, WriteOutTimeDialect.WindowsCRuntime, TimeProvider.System);

        await renderer.RenderAsync(template, new Source(), output, error);

        return Encoding.UTF8.GetString(output.ToArray());
    }
    private sealed class NoFiles : IWriteOutFileOpener
    {
        public bool TryOpen(string path, bool append, [NotNullWhen(true)] out Stream? stream)
        {
            stream = null;
            return false;
        }
    }

    private sealed class Source : IWriteOutVariableSource
    {
        public bool TransferFailed => false;

        public bool TryGetVariableText(string name, [NotNullWhen(true)] out string? text)
        {
            text = name.ToLowerInvariant() switch
            {
                "structured_field.priority" => "Dictionary of 2: u=Integer 1; i=Boolean true",
                "structured_field.cache-status" => "List of 1: Token ExampleCache [hit=Boolean true, ttl=Integer 30]",
                _ => null,
            };
            return text is not null;
        }

        public string? FindFirstHeaderValue(string name) => null;
    }
}
