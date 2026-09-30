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

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, true, false)]
    [DataRow(true, false, true)]
    public void RendersStyles_WithoutAskingWindows_FollowsTheTerminal(bool isTerminal, bool isWindows, bool expected)
    {
        Assert.AreEqual(expected, StandardOutputVirtualTerminal.RendersStyles(isTerminal, isWindows, () => throw new AssertFailedException("asked")));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void RendersStyles_OnAWindowsTerminal_IsWhetherVirtualTerminalProcessingIsOn(bool enabled)
    {
        Assert.AreEqual(enabled, StandardOutputVirtualTerminal.RendersStyles(standardOutputIsTerminal: true, isWindows: true, () => enabled));
    }

    [TestMethod]
    public void Enable_NoConsole_IsOffAndChangesNothing()
    {
        using StandardOutputVirtualTerminal terminal = Create(mode: null, writeSucceeds: true);

        Assert.IsFalse(terminal.Enable());
        Assert.IsEmpty(writtenModes);
    }

    [TestMethod]
    public void Enable_AlreadyOn_IsOnAndChangesNothing()
    {
        StandardOutputVirtualTerminal terminal = Create(ProcessedOutput | StandardOutputVirtualTerminal.EnableVirtualTerminalProcessing, writeSucceeds: true);

        Assert.IsTrue(terminal.Enable());
        terminal.Dispose();
        Assert.IsEmpty(writtenModes);
    }

    [TestMethod]
    public void Enable_Off_TurnsItOnAndPutsTheModeBackOnceOnDispose()
    {
        StandardOutputVirtualTerminal terminal = Create(ProcessedOutput, writeSucceeds: true);

        Assert.IsTrue(terminal.Enable());
        terminal.Dispose();
        terminal.Dispose();
        CollectionAssert.AreEqual(new[] { ProcessedOutput | StandardOutputVirtualTerminal.EnableVirtualTerminalProcessing, ProcessedOutput }, writtenModes);
    }

    [TestMethod]
    public void Enable_ModeCannotBeSet_IsOffAndPutsNothingBack()
    {
        StandardOutputVirtualTerminal terminal = Create(ProcessedOutput, writeSucceeds: false);

        Assert.IsFalse(terminal.Enable());
        terminal.Dispose();
        Assert.HasCount(1, writtenModes);
    }

    [TestMethod]
    public void ModeIfRead_EachOutcome_GivesTheModeOnlyWhenRead()
    {
        Assert.AreEqual(ProcessedOutput, StandardOutputVirtualTerminal.ModeIfRead(read: true, ProcessedOutput));
        Assert.IsNull(StandardOutputVirtualTerminal.ModeIfRead(read: false, ProcessedOutput));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ForWindowsConsole_StandardOutputOfTheTestHost_ReadsAndSetsTheRealConsole()
    {
        using StandardOutputVirtualTerminal terminal = StandardOutputVirtualTerminal.ForWindowsConsole();

        uint? mode = StandardOutputVirtualTerminal.ReadConsoleMode();

        Assert.AreEqual(mode is not null, terminal.Enable());
        Assert.AreEqual(mode is not null, StandardOutputVirtualTerminal.WriteConsoleMode(mode ?? 0));
    }

    private StandardOutputVirtualTerminal Create(uint? mode, bool writeSucceeds) =>
        new(() => mode, newMode =>
        {
            writtenModes.Add(newMode);
            return writeSucceeds;
        });
}
