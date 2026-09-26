using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins the five kinds of option-table row: <see cref="CommandLineOption.Flag"/> takes no
/// value, always applies and cannot be negated; <see cref="CommandLineOption.NegatableFlag"/> is a
/// flag that <c>--no-</c> turns off; <see cref="CommandLineOption.Text"/> takes a value, refuses an
/// empty one as blank without storing it and stores any other; <see cref="CommandLineOption.Value"/>
/// takes a value and returns whatever its own applier returns; <see cref="CommandLineOption.FileName"/>
/// refuses and stores as <see cref="CommandLineOption.Text"/> does.
/// </summary>
[TestClass]
public sealed class CommandLineOptionTests
{
    // ---- Flag ---------------------------------------------------------------------

    [TestMethod]
    public void Flag_WithShortName_KeepsNamesAndTakesNoValue()
    {
        CommandLineOption option = CommandLineOption.Flag("silent", 's', _ => { });

        Assert.AreEqual("silent", option.LongName);
        Assert.AreEqual('s', option.ShortName);
        Assert.IsFalse(option.TakesValue);
    }

    [TestMethod]
    public void Flag_WithoutShortName_HasNullShortName()
    {
        CommandLineOption option = CommandLineOption.Flag("verbose", null, _ => { });

        Assert.IsNull(option.ShortName);
    }

    [TestMethod]
    public void Flag_NullLongName_ThrowsArgumentNull()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.Flag(null!, 's', _ => { }));

        Assert.AreEqual("longName", exception.ParamName);
    }

    [TestMethod]
    public void Flag_NullSet_ThrowsArgumentNull()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.Flag("silent", 's', null!));

        Assert.AreEqual("set", exception.ParamName);
    }

    [TestMethod]
    public void FlagApply_Called_SetsFlagAndReturnsNull()
    {
        CommandLineOptions? setOn = null;
        CommandLineOption option = CommandLineOption.Flag("silent", 's', options => setOn = options);
        CommandLineOptions options = new();

        CommandLineRefusal? refusal = option.Apply(options, string.Empty, "-s", _ => false, new RecordingDataFileReader());

        Assert.IsNull(refusal);
        Assert.AreSame(options, setOn);
    }

    [TestMethod]
    public void Flag_Built_CannotBeNegated()
    {
        CommandLineOption option = CommandLineOption.Flag("tlsv1.2", null, _ => { });

        Assert.IsNull(option.Negate);
    }

    // ---- NegatableFlag ------------------------------------------------------------

    [TestMethod]
    public void NegatableFlag_WithShortName_KeepsNamesAndTakesNoValue()
    {
        CommandLineOption option = CommandLineOption.NegatableFlag("silent", 's', (_, _) => { });

        Assert.AreEqual("silent", option.LongName);
        Assert.AreEqual('s', option.ShortName);
        Assert.IsFalse(option.TakesValue);
        Assert.IsNotNull(option.Negate);
    }

    [TestMethod]
    public void NegatableFlag_NullLongName_ThrowsArgumentNull()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.NegatableFlag(null!, 's', (_, _) => { }));

        Assert.AreEqual("longName", exception.ParamName);
    }

    [TestMethod]
    public void NegatableFlag_NullSet_ThrowsArgumentNull()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.NegatableFlag("silent", 's', null!));

        Assert.AreEqual("set", exception.ParamName);
    }

    [TestMethod]
    public void NegatableFlagApply_Called_SetsFlagOnAndReturnsNull()
    {
        bool? setTo = null;
        CommandLineOption option = CommandLineOption.NegatableFlag("silent", 's', (_, on) => setTo = on);

        CommandLineRefusal? refusal = option.Apply(new CommandLineOptions(), string.Empty, "-s", _ => false, new RecordingDataFileReader());

        Assert.IsNull(refusal);
        Assert.IsTrue(setTo);
    }

    [TestMethod]
    public void NegatableFlagNegate_Called_SetsFlagOff()
    {
        bool? setTo = null;
        CommandLineOption option = CommandLineOption.NegatableFlag("silent", 's', (_, on) => setTo = on);

        option.Negate!(new CommandLineOptions());

        Assert.IsFalse(setTo);
    }

    // ---- Text ---------------------------------------------------------------------

    [TestMethod]
    public void Text_WithShortName_KeepsNamesAndTakesValue()
    {
        CommandLineOption option = CommandLineOption.Text("output", 'o', (_, _) => { });

        Assert.AreEqual("output", option.LongName);
        Assert.AreEqual('o', option.ShortName);
        Assert.IsTrue(option.TakesValue);
    }

    [TestMethod]
    public void Text_WithoutShortName_HasNullShortName()
    {
        CommandLineOption option = CommandLineOption.Text("url", null, (_, _) => { });

        Assert.IsNull(option.ShortName);
    }

    [TestMethod]
    public void Text_NullLongName_ThrowsArgumentNull()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.Text(null!, 'o', (_, _) => { }));

        Assert.AreEqual("longName", exception.ParamName);
    }

    [TestMethod]
    public void Text_NullSet_ThrowsArgumentNull()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.Text("output", 'o', null!));

        Assert.AreEqual("set", exception.ParamName);
    }

    [TestMethod]
    public void TextApply_EmptyValue_RefusesAsBlankWithoutCallingSet()
    {
        bool setCalled = false;
        CommandLineOption option = CommandLineOption.Text("output", 'o', (_, _) => setCalled = true);

        CommandLineRefusal? refusal = option.Apply(new CommandLineOptions(), string.Empty, "--output=", _ => false, new RecordingDataFileReader());

        Assert.IsNotNull(refusal);
        Assert.AreEqual(CurlExitCode.FailedInit, refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --output=: blank argument where content is expected", CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
        Assert.IsFalse(setCalled);
    }

    [TestMethod]
    [DataRow("page.html")]
    [DataRow(" ")]
    [DataRow("-")]
    public void TextApply_NonEmptyValue_PassesValueToSetAndReturnsNull(string value)
    {
        CommandLineOptions? setOn = null;
        string? setValue = null;
        CommandLineOption option = CommandLineOption.Text("output", 'o', (options, text) =>
        {
            setOn = options;
            setValue = text;
        });
        CommandLineOptions options = new();

        CommandLineRefusal? refusal = option.Apply(options, value, "-o", _ => false, new RecordingDataFileReader());

        Assert.IsNull(refusal);
        Assert.AreSame(options, setOn);
        Assert.AreEqual(value, setValue);
    }

    // ---- Value --------------------------------------------------------------------

    [TestMethod]
    public void Value_WithShortName_KeepsNamesAndTakesValue()
    {
        CommandLineOption option = CommandLineOption.Value("max-time", 'm', (_, _, _, _, _) => null);

        Assert.AreEqual("max-time", option.LongName);
        Assert.AreEqual('m', option.ShortName);
        Assert.IsTrue(option.TakesValue);
    }

    [TestMethod]
    public void Value_WithoutShortName_HasNullShortName()
    {
        CommandLineOption option = CommandLineOption.Value("tftp-blksize", null, (_, _, _, _, _) => null);

        Assert.IsNull(option.ShortName);
    }

    [TestMethod]
    public void Value_NullLongName_ThrowsArgumentNull()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.Value(null!, 'm', (_, _, _, _, _) => null));

        Assert.AreEqual("longName", exception.ParamName);
    }

    [TestMethod]
    public void Value_NullApply_ThrowsArgumentNull()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.Value("max-time", 'm', null!));

        Assert.AreEqual("apply", exception.ParamName);
    }

    [TestMethod]
    public void ValueApply_ApplierRefuses_ReturnsApplierRefusal()
    {
        CommandLineRefusal applierRefusal = CommandLineRefusal.ExpectedProperNumericalParameter("--tftp-blksize");
        CommandLineOption option = CommandLineOption.Value("tftp-blksize", null, (_, _, _, _, _) => applierRefusal);

        CommandLineRefusal? refusal = option.Apply(new CommandLineOptions(), "abc", "--tftp-blksize", _ => false, new RecordingDataFileReader());

        Assert.AreSame(applierRefusal, refusal);
    }

    [TestMethod]
    public void ValueApply_ApplierAccepts_PassesArgumentsUnchangedAndReturnsNull()
    {
        CommandLineOptions? seenOptions = null;
        string? seenValue = null;
        string? seenSpelledOption = null;
        CommandLineOption option = CommandLineOption.Value("tftp-blksize", null, (options, value, spelledOption, _, _) =>
        {
            seenOptions = options;
            seenValue = value;
            seenSpelledOption = spelledOption;
            return null;
        });
        CommandLineOptions options = new();

        CommandLineRefusal? refusal = option.Apply(options, string.Empty, "--tftp-blksize=", _ => false, new RecordingDataFileReader());

        Assert.IsNull(refusal);
        Assert.AreSame(options, seenOptions);
        Assert.AreEqual(string.Empty, seenValue);
        Assert.AreEqual("--tftp-blksize=", seenSpelledOption);
    }

    // ---- FileName -----------------------------------------------------------------

    [TestMethod]
    public void FileName_WithShortName_KeepsNamesAndTakesValue()
    {
        CommandLineOption option = CommandLineOption.FileName("output", 'o', (_, _) => { });

        Assert.AreEqual("output", option.LongName);
        Assert.AreEqual('o', option.ShortName);
        Assert.IsTrue(option.TakesValue);
    }

    [TestMethod]
    public void FileName_NullLongName_ThrowsArgumentNull()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.FileName(null!, 'o', (_, _) => { }));

        Assert.AreEqual("longName", exception.ParamName);
    }

    [TestMethod]
    public void FileName_NullSet_ThrowsArgumentNull()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.FileName("output", 'o', null!));

        Assert.AreEqual("set", exception.ParamName);
    }

    [TestMethod]
    public void FileNameApply_EmptyValue_RefusesAsBlankWithoutCallingSet()
    {
        bool setCalled = false;
        CommandLineOption option = CommandLineOption.FileName("output", 'o', (_, _) => setCalled = true);

        CommandLineRefusal? refusal = option.Apply(new CommandLineOptions(), string.Empty, "--output=", _ => false, new RecordingDataFileReader());

        Assert.IsNotNull(refusal);
        Assert.AreEqual("curl: option --output=: blank argument where content is expected", refusal.StandardErrorLines[0]);
        Assert.IsFalse(setCalled);
    }

    [TestMethod]
    [DataRow("-s")]
    [DataRow("file")]
    public void FileNameApply_NonEmptyValue_PassesValueToSetAndReturnsNull(string value)
    {
        string? setValue = null;
        CommandLineOption option = CommandLineOption.FileName("output", 'o', (_, fileName) => setValue = fileName);

        CommandLineRefusal? refusal = option.Apply(new CommandLineOptions(), value, "-o", _ => false, new RecordingDataFileReader());

        Assert.IsNull(refusal);
        Assert.AreEqual(value, setValue);
    }
}
