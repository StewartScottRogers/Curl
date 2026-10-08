using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="StandardOutputVirtualTerminal" />: virtual-terminal processing is turned on
/// for a Windows console on standard output, and its mode put back afterwards, as curl 8.21.0
/// does, so styled header output renders (ADR-0246, BL-736).
/// </summary>
[TestClass]
public sealed class StandardOutputVirtualTerminalTests
{
    private const uint ProcessedOutput = 0x0001;

    private readonly List<uint> writtenModes = [];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, true, false)]
    [DataRow(true, false, true)]
    public void RendersStyles_WithoutAskingWindows_FollowsTheTerminal(bool isTerminal, bool isWindows, bool expected)
    {
        Diagnostics.Arrange("is terminal / is Windows", $"{isTerminal} / {isWindows}");
        bool renders = StandardOutputVirtualTerminal.RendersStyles(isTerminal, isWindows, () => throw new AssertFailedException("asked"));
        Diagnostics.Act("renders styles", renders);

        Diagnostics.Assert("renders styles", expected, renders);
        Assert.AreEqual(expected, renders);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void RendersStyles_OnAWindowsTerminal_IsWhetherVirtualTerminalProcessingIsOn(bool enabled)
    {
        Diagnostics.Arrange("virtual terminal processing on", enabled);
        bool renders = StandardOutputVirtualTerminal.RendersStyles(standardOutputIsTerminal: true, isWindows: true, () => enabled);
        Diagnostics.Act("renders styles", renders);

        Diagnostics.Assert("renders styles", enabled, renders);
        Assert.AreEqual(enabled, renders);
    }

    [TestMethod]
    public void Enable_NoConsole_IsOffAndChangesNothing()
    {
        using StandardOutputVirtualTerminal terminal = Create(mode: null, writeSucceeds: true);

        bool enabled = terminal.Enable();
        ActModes(enabled);

        Diagnostics.Assert("enabled / modes written", "False / 0", $"{enabled} / {writtenModes.Count}");
        Assert.IsFalse(enabled);
        Assert.IsEmpty(writtenModes);
    }

    [TestMethod]
    public void Enable_AlreadyOn_IsOnAndChangesNothing()
    {
        StandardOutputVirtualTerminal terminal = Create(ProcessedOutput | StandardOutputVirtualTerminal.EnableVirtualTerminalProcessing, writeSucceeds: true);

        bool enabled = terminal.Enable();
        terminal.Dispose();
        ActModes(enabled);

        Diagnostics.Assert("enabled / modes written", "True / 0", $"{enabled} / {writtenModes.Count}");
        Assert.IsTrue(enabled);
        Assert.IsEmpty(writtenModes);
    }

    [TestMethod]
    public void Enable_Off_TurnsItOnAndPutsTheModeBackOnceOnDispose()
    {
        StandardOutputVirtualTerminal terminal = Create(ProcessedOutput, writeSucceeds: true);

        bool enabled = terminal.Enable();
        terminal.Dispose();
        terminal.Dispose();
        ActModes(enabled);

        Diagnostics.Assert("enabled / modes written", "True / 0x0005, 0x0001", $"{enabled} / {Modes()}");
        Assert.IsTrue(enabled);
        CollectionAssert.AreEqual(new[] { ProcessedOutput | StandardOutputVirtualTerminal.EnableVirtualTerminalProcessing, ProcessedOutput }, writtenModes);
    }

    [TestMethod]
    public void Enable_ModeCannotBeSet_IsOffAndPutsNothingBack()
    {
        StandardOutputVirtualTerminal terminal = Create(ProcessedOutput, writeSucceeds: false);

        bool enabled = terminal.Enable();
        terminal.Dispose();
        ActModes(enabled);

        Diagnostics.Assert("enabled / modes written", "False / 0x0005", $"{enabled} / {Modes()}");
        Assert.IsFalse(enabled);
        Assert.HasCount(1, writtenModes);
    }

    [TestMethod]
    public void ModeIfRead_EachOutcome_GivesTheModeOnlyWhenRead()
    {
        Diagnostics.Arrange("mode", $"0x{ProcessedOutput:X4}");
        uint? whenRead = StandardOutputVirtualTerminal.ModeIfRead(read: true, ProcessedOutput);
        uint? whenNotRead = StandardOutputVirtualTerminal.ModeIfRead(read: false, ProcessedOutput);
        Diagnostics.Act("when read / when not read", $"{whenRead?.ToString() ?? "null"} / {whenNotRead?.ToString() ?? "null"}");

        Diagnostics.Assert("when read / when not read", "1 / null", $"{whenRead?.ToString() ?? "null"} / {whenNotRead?.ToString() ?? "null"}");
        Assert.AreEqual(ProcessedOutput, whenRead);
        Assert.IsNull(whenNotRead);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ForWindowsConsole_StandardOutputOfTheTestHost_ReadsAndSetsTheRealConsole()
    {
        using StandardOutputVirtualTerminal terminal = StandardOutputVirtualTerminal.ForWindowsConsole();
        Diagnostics.Arrange("console", "the test host's standard output");

        uint? mode = StandardOutputVirtualTerminal.ReadConsoleMode();
        bool enabled = terminal.Enable();
        bool written = StandardOutputVirtualTerminal.WriteConsoleMode(mode ?? 0);
        Diagnostics.Act("mode read / enabled / written", $"{mode is not null} / {enabled} / {written}");

        Diagnostics.Assert("enabled and written follow the mode read", $"{mode is not null} / {mode is not null}", $"{enabled} / {written}");
        Assert.AreEqual(mode is not null, enabled);
        Assert.AreEqual(mode is not null, written);
    }

    private StandardOutputVirtualTerminal Create(uint? mode, bool writeSucceeds)
    {
        Diagnostics.Arrange("console mode", mode is null ? "no console" : $"0x{mode:X4}");
        Diagnostics.Arrange("mode write succeeds", writeSucceeds);
        return new(() => mode, newMode =>
        {
            writtenModes.Add(newMode);
            return writeSucceeds;
        });
    }

    private void ActModes(bool enabled)
    {
        Diagnostics.Act("enabled", enabled);
        Diagnostics.Act("modes written", Modes());
    }

    private string Modes() => string.Join(", ", writtenModes.Select(mode => $"0x{mode:X4}"));
}
