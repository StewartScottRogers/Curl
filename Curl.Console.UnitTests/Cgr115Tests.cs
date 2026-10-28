using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins CGR-115: off Windows nothing reads <see cref="System.Console.WindowWidth" /> or writes
/// through .NET's console streams, either of which writes terminfo's <c>keypad_xmit</c>
/// (<c>ESC[?1h ESC=</c>) to the terminal, which curl 8.18.0 on Linux never writes (ADR-0866).
/// </summary>
[TestClass]
public sealed class Cgr115Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ReadConsoleColumns_OffWindows_ReadsTheStandardInputTerminalNotTheConsoleWindow()
    {
        Diagnostics.Arrange("runs on Windows / stdin terminal width", "False / 132");
        int? columns = TerminalColumns.ReadConsoleColumns(
            runsOnWindows: false,
            () => throw new AssertFailedException("The console window's width was read."),
            () => 132);
        Diagnostics.Act("columns", columns?.ToString() ?? "null");

        Diagnostics.Assert("columns", "132", columns?.ToString() ?? "null");
        Assert.AreEqual(132, columns);
    }

    [TestMethod]
    public void ReadConsoleColumns_OnWindows_ReadsTheStandardErrorConsole()
    {
        Diagnostics.Arrange("runs on Windows / stderr console width", "True / 79");
        int? columns = TerminalColumns.ReadConsoleColumns(
            runsOnWindows: true,
            () => 79,
            () => throw new AssertFailedException("The standard-input terminal was read."));
        Diagnostics.Act("columns", columns?.ToString() ?? "null");

        Diagnostics.Assert("columns", "79", columns?.ToString() ?? "null");
        Assert.AreEqual(79, columns);
    }

    [TestMethod]
    public void ReadConsoleColumns_ThisProcess_IsNullOrAWidth()
    {
        Diagnostics.Arrange("console", "this test host's");
        int? columns = TerminalColumns.ReadConsoleColumns();
        bool nullOrWidth = columns is null or >= -1;
        Diagnostics.Act("is null or a width", nullOrWidth);

        Diagnostics.Assert("is null or a width", true, nullOrWidth);
        Assert.IsTrue(nullOrWidth, $"Unexpected width {columns}.");
    }

    [TestMethod]
    [DataRow(false, false, true)]
    [DataRow(true, false, true)]
    [DataRow(true, true, true)]
    [DataRow(false, true, false)]
    public void OpensOverHandle_OnlyAWindowsConsoleIsOpenedAsAConsoleStream(bool isOutputRedirected, bool runsOnWindows, bool expected)
    {
        Diagnostics.Arrange("output redirected / runs on Windows", $"{isOutputRedirected} / {runsOnWindows}");
        bool opensOverHandle = StandardOutputOpener.OpensOverHandle(isOutputRedirected, runsOnWindows);
        Diagnostics.Act("opens over handle", opensOverHandle);

        Diagnostics.Assert("opens over handle", expected, opensOverHandle);
        Assert.AreEqual(expected, opensOverHandle);
    }

    [TestMethod]
    public void StandardErrorOpen_OffWindows_OpensDescriptor2NotTheConsoleStream()
    {
        using MemoryStream handleStream = new();
        nint openedHandle = -1;
        Diagnostics.Arrange("runs on Windows", false);

        Stream opened = StandardErrorOpener.Open(
            runsOnWindows: false,
            () => throw new AssertFailedException("The console stream was opened."),
            handle =>
            {
                openedHandle = handle;
                return handleStream;
            },
            _ => true);
        Diagnostics.Act("descriptor", openedHandle);

        Diagnostics.Assert("descriptor", (nint)StandardErrorOpener.UnixStandardErrorFileDescriptor, openedHandle);
        Assert.AreEqual((nint)2, openedHandle);
        Assert.AreSame(handleStream, opened);
    }

    [TestMethod]
    public void StandardErrorOpen_OnWindows_OpensTheConsoleStream()
    {
        using MemoryStream console = new();
        Diagnostics.Arrange("runs on Windows", true);

        Stream opened = StandardErrorOpener.Open(
            runsOnWindows: true,
            () => console,
            _ => throw new AssertFailedException("A descriptor was opened."),
            _ => throw new AssertFailedException("A descriptor was checked."));
        Diagnostics.Act("opened the console stream", ReferenceEquals(console, opened));

        Diagnostics.Assert("opened the console stream", true, ReferenceEquals(console, opened));
        Assert.AreSame(console, opened);
    }

    [TestMethod]
    public void StandardErrorOpen_ThisProcess_OpensAWritableStream()
    {
        Diagnostics.Arrange("standard error", "this test host's");
        using Stream opened = StandardErrorOpener.Open();
        Diagnostics.Act("can write", opened.CanWrite);

        Diagnostics.Assert("can write", true, opened.CanWrite);
        Assert.IsTrue(opened.CanWrite);
    }
}
