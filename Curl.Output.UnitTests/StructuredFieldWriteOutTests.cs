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
    public async Task RenderAsync_UnknownVariableLongerThanNameBuffer_StillEndsTheOutput()
    {
        using MemoryStream output = new();
        using MemoryStream error = new();
        WriteOutTemplateRenderer renderer = new(new NoFiles(), false, WriteOutTimeDialect.WindowsCRuntime, TimeProvider.System);

        await renderer.RenderAsync("A[%{structured_field.nosuchfield}]B", new Source(), output, error);

        Assert.AreEqual("A[", Encoding.UTF8.GetString(output.ToArray()));
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
            text = name == "structured_field.priority" ? "Dictionary of 2: u=Integer 1; i=Boolean true" : null;
            return text is not null;
        }

        public string? FindFirstHeaderValue(string name) => null;
    }
}
