using System.Text;
using Curl.Api.EngineContracts;
using Libcurl.Global;

namespace Curl.Conformance.LibTest.UnitTests;

/// <summary>One run of a port in a test: its context over <see cref="Output"/> and <see cref="Error"/>.</summary>
internal sealed class PortRun
{
    public PortRun(ICurlEngine? engine, int testNumber, params string[] arguments)
    {
        Context = new LibTestContext(testNumber, arguments) { Engine = engine, StandardOutput = Output, StandardError = Error };
        Steps = new LibTestSteps(Error);
    }

    public MemoryStream Output { get; } = new();

    public StringWriter Error { get; } = new();

    public LibTestContext Context { get; }

    public LibTestSteps Steps { get; }

    public string OutputText => Encoding.UTF8.GetString(Output.ToArray());

    /// <summary>A library already cleaned up, so every handle init on it fails.</summary>
    public static async Task<CurlLibrary> CleanedUpLibraryAsync()
    {
        CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await library.DisposeAsync();
        return library;
    }

    /// <summary>An engine that cleans up the library <paramref name="holder"/> names on the <paramref name="count"/>th easy handle it creates.</summary>
    public static FakeCurlEngine CleaningUpOnEasy(LibraryHolder holder, int count)
    {
        int created = 0;
        return new FakeCurlEngine { CreatingEasy = () => CleanUpWhen(holder, ++created == count) };
    }

    public static void CleanUpWhen(LibraryHolder holder, bool now)
    {
        if (now)
        {
            holder.Library!.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    internal sealed class LibraryHolder
    {
        public CurlLibrary? Library { get; set; }
    }
}
