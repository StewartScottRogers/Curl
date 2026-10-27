using Curl.Cli;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="RawCommandLineArguments"/> (CGR-016, BL-3849): argv's raw bytes replace the
/// arguments .NET decoded, keeping each invalid UTF-8 byte, and the arguments stay as given when
/// argv cannot be read or does not line up with them.
/// </summary>
[TestClass]
public sealed class RawCommandLineArgumentsTests
{
    [TestMethod]
    public void SplitProcCommandLine_NulTerminatedArguments_GivesEachOne()
    {
        IReadOnlyList<byte[]> split = RawCommandLineArguments.SplitProcCommandLine([0x2F, 0x63, 0, 0x2D, 0x64, 0, 0, 0xFF, 0]);

        Assert.HasCount(4, split);
        CollectionAssert.AreEqual(new byte[] { 0x2F, 0x63 }, split[0]);
        CollectionAssert.AreEqual(new byte[] { 0x2D, 0x64 }, split[1]);
        Assert.IsEmpty(split[2]);
        CollectionAssert.AreEqual(new byte[] { 0xFF }, split[3]);
    }

    [TestMethod]
    public void SplitProcCommandLine_LastArgumentWithoutNul_GivesItToo()
    {
        IReadOnlyList<byte[]> split = RawCommandLineArguments.SplitProcCommandLine([0x61, 0, 0x62]);

        Assert.HasCount(2, split);
        CollectionAssert.AreEqual(new byte[] { 0x62 }, split[1]);
    }

    [TestMethod]
    public void Select_InvalidByteArgument_KeepsTheByte()
    {
        IReadOnlyList<string> selected = RawCommandLineArguments.Select(["-d", "�"], [[0x63], [0x2D, 0x64], [0xFF]]);

        CollectionAssert.AreEqual(new[] { "-d", "\uDCFF" }, selected.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0xFF }, InvalidUtf8ByteEscape.Encoding.GetBytes(selected[1]));
    }

    [TestMethod]
    public void Select_ValidUtf8Arguments_AreUnchanged()
    {
        IReadOnlyList<string> selected = RawCommandLineArguments.Select(["é"], [[0x63], [0xC3, 0xA9]]);

        CollectionAssert.AreEqual(new[] { "é" }, selected.ToArray());
    }

    [TestMethod]
    public void Select_ArgvUnreadable_GivesTheArguments()
    {
        string[] arguments = ["-d", "x"];

        Assert.AreSame(arguments, RawCommandLineArguments.Select(arguments, null));
    }

    [TestMethod]
    public void Select_ArgvTooShort_GivesTheArguments()
    {
        string[] arguments = ["-d", "x"];

        Assert.AreSame(arguments, RawCommandLineArguments.Select(arguments, [[0x78]]));
    }

    [TestMethod]
    public void Select_ArgvNotLiningUp_GivesTheArguments()
    {
        string[] arguments = ["-d", "x"];

        Assert.AreSame(arguments, RawCommandLineArguments.Select(arguments, [[0x63], [0x2D, 0x64], [0x79]]));
    }

    [TestMethod]
    public void ForCurrentPlatform_ArgumentsNotInThisProcesssArgv_GivesTheArguments()
    {
        string[] arguments = ["-d", "x"];

        IReadOnlyList<string> selected = RawCommandLineArguments.ForCurrentPlatform(arguments);

        CollectionAssert.AreEqual(arguments, selected.ToArray());
    }
}
