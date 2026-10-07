using Curl.Protocol.Abstractions;
using Curl.Testing;

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
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    // ---- Flag ---------------------------------------------------------------------

    [TestMethod]
    public void Flag_WithShortName_KeepsNamesAndTakesNoValue()
    {
        Diagnostics.Arrange("builder", "Flag");
        Diagnostics.Arrange("long name", "silent");
        Diagnostics.Arrange("short name", 's');
        CommandLineOption option = CommandLineOption.Flag("silent", 's', _ => { });

        ActOption(option);
        Diagnostics.Assert("option.LongName", "silent", option.LongName);
        Assert.AreEqual("silent", option.LongName);
        Assert.AreEqual('s', option.ShortName);
        Assert.IsFalse(option.TakesValue);
    }

    [TestMethod]
    public void Flag_WithoutShortName_HasNullShortName()
    {
        Diagnostics.Arrange("builder", "Flag");
        Diagnostics.Arrange("long name", "verbose");
        Diagnostics.Arrange("short name", null);
        CommandLineOption option = CommandLineOption.Flag("verbose", null, _ => { });

        ActOption(option);
        Diagnostics.Assert("option.ShortName", null, option.ShortName);
        Assert.IsNull(option.ShortName);
    }

    [TestMethod]
    public void Flag_NullLongName_ThrowsArgumentNull()
    {
        Diagnostics.Arrange("call", "Flag(null, 's', _ => { })");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.Flag(null!, 's', _ => { }));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception.ParamName", "longName", exception.ParamName);
        Assert.AreEqual("longName", exception.ParamName);
    }

    [TestMethod]
    public void Flag_NullSet_ThrowsArgumentNull()
    {
        Diagnostics.Arrange("call", "Flag(\"silent\", 's', null)");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.Flag("silent", 's', null!));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception.ParamName", "set", exception.ParamName);
        Assert.AreEqual("set", exception.ParamName);
    }

    [TestMethod]
    public void FlagApply_Called_SetsFlagAndReturnsNull()
    {
        CommandLineOptions? setOn = null;
        Diagnostics.Arrange("builder", "Flag");
        Diagnostics.Arrange("long name", "silent");
        Diagnostics.Arrange("short name", 's');
        CommandLineOption option = CommandLineOption.Flag("silent", 's', options => setOn = options);
        CommandLineOptions options = new();

        Diagnostics.Arrange("value", CommandLineParseDiagnostics.QuoteEach([string.Empty]));
        Diagnostics.Arrange("spelled option", CommandLineParseDiagnostics.QuoteEach(["-s"]));
        CommandLineRefusal? refusal = option.Apply(options, string.Empty, "-s", _ => false, new RecordingDataFileReader());
        ActRefusal(refusal);

        Diagnostics.Assert("refusal", null, refusal);
        Assert.IsNull(refusal);
        Assert.AreSame(options, setOn);
    }

    [TestMethod]
    public void Flag_Built_CannotBeNegated()
    {
        Diagnostics.Arrange("builder", "Flag");
        Diagnostics.Arrange("long name", "tlsv1.2");
        Diagnostics.Arrange("short name", null);
        CommandLineOption option = CommandLineOption.Flag("tlsv1.2", null, _ => { });

        ActOption(option);
        Diagnostics.Assert("option.Negate", null, option.Negate);
        Assert.IsNull(option.Negate);
    }

    // ---- NegatableFlag ------------------------------------------------------------

    [TestMethod]
    public void NegatableFlag_WithShortName_KeepsNamesAndTakesNoValue()
    {
        Diagnostics.Arrange("builder", "NegatableFlag");
        Diagnostics.Arrange("long name", "silent");
        Diagnostics.Arrange("short name", 's');
        CommandLineOption option = CommandLineOption.NegatableFlag("silent", 's', (_, _) => { });

        ActOption(option);
        Diagnostics.Assert("option.LongName", "silent", option.LongName);
        Assert.AreEqual("silent", option.LongName);
        Assert.AreEqual('s', option.ShortName);
        Assert.IsFalse(option.TakesValue);
        Assert.IsNotNull(option.Negate);
    }

    [TestMethod]
    public void NegatableFlag_NullLongName_ThrowsArgumentNull()
    {
        Diagnostics.Arrange("call", "NegatableFlag(null, 's', (_, _) => { })");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.NegatableFlag(null!, 's', (_, _) => { }));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception.ParamName", "longName", exception.ParamName);
        Assert.AreEqual("longName", exception.ParamName);
    }

    [TestMethod]
    public void NegatableFlag_NullSet_ThrowsArgumentNull()
    {
        Diagnostics.Arrange("call", "NegatableFlag(\"silent\", 's', null)");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.NegatableFlag("silent", 's', null!));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception.ParamName", "set", exception.ParamName);
        Assert.AreEqual("set", exception.ParamName);
    }

    [TestMethod]
    public void NegatableFlagApply_Called_SetsFlagOnAndReturnsNull()
    {
        bool? setTo = null;
        Diagnostics.Arrange("builder", "NegatableFlag");
        Diagnostics.Arrange("long name", "silent");
        Diagnostics.Arrange("short name", 's');
        CommandLineOption option = CommandLineOption.NegatableFlag("silent", 's', (_, on) => setTo = on);

        Diagnostics.Arrange("value", CommandLineParseDiagnostics.QuoteEach([string.Empty]));
        Diagnostics.Arrange("spelled option", CommandLineParseDiagnostics.QuoteEach(["-s"]));
        CommandLineRefusal? refusal = option.Apply(new CommandLineOptions(), string.Empty, "-s", _ => false, new RecordingDataFileReader());
        ActRefusal(refusal);

        Diagnostics.Assert("refusal", null, refusal);
        Assert.IsNull(refusal);
        Assert.IsTrue(setTo);
    }

    [TestMethod]
    public void NegatableFlagNegate_Called_SetsFlagOff()
    {
        bool? setTo = null;
        Diagnostics.Arrange("builder", "NegatableFlag");
        Diagnostics.Arrange("long name", "silent");
        Diagnostics.Arrange("short name", 's');
        CommandLineOption option = CommandLineOption.NegatableFlag("silent", 's', (_, on) => setTo = on);

        Diagnostics.Arrange("value", CommandLineParseDiagnostics.QuoteEach([string.Empty]));
        Diagnostics.Arrange("spelled option", CommandLineParseDiagnostics.QuoteEach(["--no-silent"]));
        CommandLineRefusal? refusal = option.Negate!(new CommandLineOptions(), string.Empty, "--no-silent", _ => false, new RecordingDataFileReader());
        ActRefusal(refusal);

        Diagnostics.Assert("refusal", null, refusal);
        Assert.IsNull(refusal);
        Assert.IsFalse(setTo);
    }

    // ---- FlagThatCanRefuse ----------------------------------------------------------

    [TestMethod]
    public void FlagThatCanRefuse_WithShortName_KeepsNamesAndTakesNoValueAndCannotBeNegated()
    {
        Diagnostics.Arrange("builder", "FlagThatCanRefuse");
        Diagnostics.Arrange("long name", "tlsv1");
        Diagnostics.Arrange("short name", '1');
        CommandLineOption option = CommandLineOption.FlagThatCanRefuse("tlsv1", '1', (_, _) => null);

        ActOption(option);
        Diagnostics.Assert("option.LongName", "tlsv1", option.LongName);
        Assert.AreEqual("tlsv1", option.LongName);
        Assert.AreEqual('1', option.ShortName);
        Assert.IsFalse(option.TakesValue);
        Assert.IsNull(option.Negate);
    }

    [TestMethod]
    public void FlagThatCanRefuse_NullLongName_ThrowsArgumentNull()
    {
        Diagnostics.Arrange("call", "FlagThatCanRefuse(null, '1', (_, _) => null)");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.FlagThatCanRefuse(null!, '1', (_, _) => null));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception.ParamName", "longName", exception.ParamName);
        Assert.AreEqual("longName", exception.ParamName);
    }

    [TestMethod]
    public void FlagThatCanRefuse_NullSetOrRefuse_ThrowsArgumentNull()
    {
        Diagnostics.Arrange("call", "FlagThatCanRefuse(\"tlsv1\", '1', null)");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.FlagThatCanRefuse("tlsv1", '1', null!));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception.ParamName", "setOrRefuse", exception.ParamName);
        Assert.AreEqual("setOrRefuse", exception.ParamName);
    }

    [TestMethod]
    public void FlagThatCanRefuseApply_Called_PassesSpellingAndReturnsItsRefusal()
    {
        CommandLineRefusal badlyUsed = CommandLineRefusal.BadlyUsedHere("--tlsv1.3");
        string? spelled = null;
        Diagnostics.Arrange("builder", "FlagThatCanRefuse");
        Diagnostics.Arrange("long name", "tlsv1.3");
        Diagnostics.Arrange("short name", null);
        CommandLineOption option = CommandLineOption.FlagThatCanRefuse("tlsv1.3", null, (_, spelling) =>
        {
            spelled = spelling;
            return badlyUsed;
        });

        Diagnostics.Arrange("value", CommandLineParseDiagnostics.QuoteEach([string.Empty]));
        Diagnostics.Arrange("spelled option", CommandLineParseDiagnostics.QuoteEach(["--tlsv1.3"]));
        CommandLineRefusal? refusal = option.Apply(new CommandLineOptions(), string.Empty, "--tlsv1.3", _ => false, new RecordingDataFileReader());
        ActRefusal(refusal);

        Diagnostics.Assert("refusal is badlyUsed", true, ReferenceEquals(badlyUsed, refusal));
        Assert.AreSame(badlyUsed, refusal);
        Assert.AreEqual("--tlsv1.3", spelled);
    }

    // ---- NegatableFlagThatCanRefuse -------------------------------------------------

    [TestMethod]
    public void NegatableFlagThatCanRefuse_WithShortName_KeepsNamesAndTakesNoValue()
    {
        Diagnostics.Arrange("builder", "NegatableFlagThatCanRefuse");
        Diagnostics.Arrange("long name", "head");
        Diagnostics.Arrange("short name", 'I');
        CommandLineOption option = CommandLineOption.NegatableFlagThatCanRefuse("head", 'I', (_, _, _) => null);

        ActOption(option);
        Diagnostics.Assert("option.LongName", "head", option.LongName);
        Assert.AreEqual("head", option.LongName);
        Assert.AreEqual('I', option.ShortName);
        Assert.IsFalse(option.TakesValue);
        Assert.IsNotNull(option.Negate);
    }

    [TestMethod]
    public void NegatableFlagThatCanRefuse_NullLongName_ThrowsArgumentNull()
    {
        Diagnostics.Arrange("call", "NegatableFlagThatCanRefuse(null, 'I', (_, _, _) => null)");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.NegatableFlagThatCanRefuse(null!, 'I', (_, _, _) => null));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception.ParamName", "longName", exception.ParamName);
        Assert.AreEqual("longName", exception.ParamName);
    }

    [TestMethod]
    public void NegatableFlagThatCanRefuse_NullSetOrRefuse_ThrowsArgumentNull()
    {
        Diagnostics.Arrange("call", "NegatableFlagThatCanRefuse(\"head\", 'I', null)");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.NegatableFlagThatCanRefuse("head", 'I', null!));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception.ParamName", "setOrRefuse", exception.ParamName);
        Assert.AreEqual("setOrRefuse", exception.ParamName);
    }

    [TestMethod]
    public void NegatableFlagThatCanRefuseApply_Called_PassesOnAndSpellingAndReturnsItsRefusal()
    {
        CommandLineRefusal badlyUsed = CommandLineRefusal.BadlyUsedHere("-I");
        (bool On, string Spelling)? call = null;
        Diagnostics.Arrange("builder", "NegatableFlagThatCanRefuse");
        Diagnostics.Arrange("long name", "head");
        Diagnostics.Arrange("short name", 'I');
        CommandLineOption option = CommandLineOption.NegatableFlagThatCanRefuse("head", 'I', (_, on, spelling) =>
        {
            call = (on, spelling);
            return badlyUsed;
        });

        Diagnostics.Arrange("value", CommandLineParseDiagnostics.QuoteEach([string.Empty]));
        Diagnostics.Arrange("spelled option", CommandLineParseDiagnostics.QuoteEach(["-I"]));
        CommandLineRefusal? refusal = option.Apply(new CommandLineOptions(), string.Empty, "-I", _ => false, new RecordingDataFileReader());
        ActRefusal(refusal);

        Diagnostics.Assert("refusal is badlyUsed", true, ReferenceEquals(badlyUsed, refusal));
        Assert.AreSame(badlyUsed, refusal);
        Assert.AreEqual((true, "-I"), call);
    }

    [TestMethod]
    public void NegatableFlagThatCanRefuseNegate_Called_PassesOffAndSpelling()
    {
        (bool On, string Spelling)? call = null;
        Diagnostics.Arrange("builder", "NegatableFlagThatCanRefuse");
        Diagnostics.Arrange("long name", "head");
        Diagnostics.Arrange("short name", 'I');
        CommandLineOption option = CommandLineOption.NegatableFlagThatCanRefuse("head", 'I', (_, on, spelling) =>
        {
            call = (on, spelling);
            return null;
        });

        Diagnostics.Arrange("value", CommandLineParseDiagnostics.QuoteEach([string.Empty]));
        Diagnostics.Arrange("spelled option", CommandLineParseDiagnostics.QuoteEach(["--no-head"]));
        CommandLineRefusal? refusal = option.Negate!(new CommandLineOptions(), string.Empty, "--no-head", _ => false, new RecordingDataFileReader());
        ActRefusal(refusal);

        Diagnostics.Assert("refusal", null, refusal);
        Assert.IsNull(refusal);
        Assert.AreEqual((false, "--no-head"), call);
    }

    // ---- Text ---------------------------------------------------------------------

    [TestMethod]
    public void Text_WithShortName_KeepsNamesAndTakesValue()
    {
        Diagnostics.Arrange("builder", "Text");
        Diagnostics.Arrange("long name", "output");
        Diagnostics.Arrange("short name", 'o');
        CommandLineOption option = CommandLineOption.Text("output", 'o', (_, _) => { });

        ActOption(option);
        Diagnostics.Assert("option.LongName", "output", option.LongName);
        Assert.AreEqual("output", option.LongName);
        Assert.AreEqual('o', option.ShortName);
        Assert.IsTrue(option.TakesValue);
    }

    [TestMethod]
    public void Text_WithoutShortName_HasNullShortName()
    {
        Diagnostics.Arrange("builder", "Text");
        Diagnostics.Arrange("long name", "url");
        Diagnostics.Arrange("short name", null);
        CommandLineOption option = CommandLineOption.Text("url", null, (_, _) => { });

        ActOption(option);
        Diagnostics.Assert("option.ShortName", null, option.ShortName);
        Assert.IsNull(option.ShortName);
    }

    [TestMethod]
    public void Text_NullLongName_ThrowsArgumentNull()
    {
        Diagnostics.Arrange("call", "Text(null, 'o', (_, _) => { })");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.Text(null!, 'o', (_, _) => { }));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception.ParamName", "longName", exception.ParamName);
        Assert.AreEqual("longName", exception.ParamName);
    }

    [TestMethod]
    public void Text_NullSet_ThrowsArgumentNull()
    {
        Diagnostics.Arrange("call", "Text(\"output\", 'o', null)");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.Text("output", 'o', null!));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception.ParamName", "set", exception.ParamName);
        Assert.AreEqual("set", exception.ParamName);
    }

    [TestMethod]
    public void TextApply_EmptyValue_RefusesAsBlankWithoutCallingSet()
    {
        bool setCalled = false;
        Diagnostics.Arrange("builder", "Text");
        Diagnostics.Arrange("long name", "output");
        Diagnostics.Arrange("short name", 'o');
        CommandLineOption option = CommandLineOption.Text("output", 'o', (_, _) => setCalled = true);

        Diagnostics.Arrange("value", CommandLineParseDiagnostics.QuoteEach([string.Empty]));
        Diagnostics.Arrange("spelled option", CommandLineParseDiagnostics.QuoteEach(["--output="]));
        CommandLineRefusal? refusal = option.Apply(new CommandLineOptions(), string.Empty, "--output=", _ => false, new RecordingDataFileReader());
        ActRefusal(refusal);

        Diagnostics.Assert("refusal is set", true, refusal is not null);
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
        Diagnostics.Arrange("builder", "Text");
        Diagnostics.Arrange("long name", "output");
        Diagnostics.Arrange("short name", 'o');
        CommandLineOption option = CommandLineOption.Text("output", 'o', (options, text) =>
        {
            setOn = options;
            setValue = text;
        });
        CommandLineOptions options = new();

        Diagnostics.Arrange("value", CommandLineParseDiagnostics.QuoteEach([value]));
        Diagnostics.Arrange("spelled option", CommandLineParseDiagnostics.QuoteEach(["-o"]));
        CommandLineRefusal? refusal = option.Apply(options, value, "-o", _ => false, new RecordingDataFileReader());
        ActRefusal(refusal);

        Diagnostics.Assert("refusal", null, refusal);
        Assert.IsNull(refusal);
        Assert.AreSame(options, setOn);
        Assert.AreEqual(value, setValue);
    }

    // ---- Value --------------------------------------------------------------------

    [TestMethod]
    public void Value_WithShortName_KeepsNamesAndTakesValue()
    {
        Diagnostics.Arrange("builder", "Value");
        Diagnostics.Arrange("long name", "max-time");
        Diagnostics.Arrange("short name", 'm');
        CommandLineOption option = CommandLineOption.Value("max-time", 'm', (_, _, _, _, _) => null);

        ActOption(option);
        Diagnostics.Assert("option.LongName", "max-time", option.LongName);
        Assert.AreEqual("max-time", option.LongName);
        Assert.AreEqual('m', option.ShortName);
        Assert.IsTrue(option.TakesValue);
    }

    [TestMethod]
    public void Value_WithoutShortName_HasNullShortName()
    {
        Diagnostics.Arrange("builder", "Value");
        Diagnostics.Arrange("long name", "tftp-blksize");
        Diagnostics.Arrange("short name", null);
        CommandLineOption option = CommandLineOption.Value("tftp-blksize", null, (_, _, _, _, _) => null);

        ActOption(option);
        Diagnostics.Assert("option.ShortName", null, option.ShortName);
        Assert.IsNull(option.ShortName);
    }

    [TestMethod]
    public void Value_NullLongName_ThrowsArgumentNull()
    {
        Diagnostics.Arrange("call", "Value(null, 'm', (_, _, _, _, _) => null)");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.Value(null!, 'm', (_, _, _, _, _) => null));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception.ParamName", "longName", exception.ParamName);
        Assert.AreEqual("longName", exception.ParamName);
    }

    [TestMethod]
    public void Value_NullApply_ThrowsArgumentNull()
    {
        Diagnostics.Arrange("call", "Value(\"max-time\", 'm', null)");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.Value("max-time", 'm', null!));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception.ParamName", "apply", exception.ParamName);
        Assert.AreEqual("apply", exception.ParamName);
    }

    [TestMethod]
    public void ValueApply_ApplierRefuses_ReturnsApplierRefusal()
    {
        CommandLineRefusal applierRefusal = CommandLineRefusal.ExpectedProperNumericalParameter("--tftp-blksize");
        Diagnostics.Arrange("builder", "Value");
        Diagnostics.Arrange("long name", "tftp-blksize");
        Diagnostics.Arrange("short name", null);
        CommandLineOption option = CommandLineOption.Value("tftp-blksize", null, (_, _, _, _, _) => applierRefusal);

        Diagnostics.Arrange("value", CommandLineParseDiagnostics.QuoteEach(["abc"]));
        Diagnostics.Arrange("spelled option", CommandLineParseDiagnostics.QuoteEach(["--tftp-blksize"]));
        CommandLineRefusal? refusal = option.Apply(new CommandLineOptions(), "abc", "--tftp-blksize", _ => false, new RecordingDataFileReader());
        ActRefusal(refusal);

        Diagnostics.Assert("refusal is applierRefusal", true, ReferenceEquals(applierRefusal, refusal));
        Assert.AreSame(applierRefusal, refusal);
    }

    [TestMethod]
    public void ValueApply_ApplierAccepts_PassesArgumentsUnchangedAndReturnsNull()
    {
        CommandLineOptions? seenOptions = null;
        string? seenValue = null;
        string? seenSpelledOption = null;
        Diagnostics.Arrange("builder", "Value");
        Diagnostics.Arrange("long name", "tftp-blksize");
        Diagnostics.Arrange("short name", null);
        CommandLineOption option = CommandLineOption.Value("tftp-blksize", null, (options, value, spelledOption, _, _) =>
        {
            seenOptions = options;
            seenValue = value;
            seenSpelledOption = spelledOption;
            return null;
        });
        CommandLineOptions options = new();

        Diagnostics.Arrange("value", CommandLineParseDiagnostics.QuoteEach([string.Empty]));
        Diagnostics.Arrange("spelled option", CommandLineParseDiagnostics.QuoteEach(["--tftp-blksize="]));
        CommandLineRefusal? refusal = option.Apply(options, string.Empty, "--tftp-blksize=", _ => false, new RecordingDataFileReader());
        ActRefusal(refusal);

        Diagnostics.Assert("refusal", null, refusal);
        Assert.IsNull(refusal);
        Assert.AreSame(options, seenOptions);
        Assert.AreEqual(string.Empty, seenValue);
        Assert.AreEqual("--tftp-blksize=", seenSpelledOption);
    }

    // ---- FileName -----------------------------------------------------------------

    [TestMethod]
    public void FileName_WithShortName_KeepsNamesAndTakesValue()
    {
        Diagnostics.Arrange("builder", "FileName");
        Diagnostics.Arrange("long name", "output");
        Diagnostics.Arrange("short name", 'o');
        CommandLineOption option = CommandLineOption.FileName("output", 'o', (_, _) => { });

        ActOption(option);
        Diagnostics.Assert("option.LongName", "output", option.LongName);
        Assert.AreEqual("output", option.LongName);
        Assert.AreEqual('o', option.ShortName);
        Assert.IsTrue(option.TakesValue);
    }

    [TestMethod]
    public void FileName_NullLongName_ThrowsArgumentNull()
    {
        Diagnostics.Arrange("call", "FileName(null, 'o', (_, _) => { })");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.FileName(null!, 'o', (_, _) => { }));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception.ParamName", "longName", exception.ParamName);
        Assert.AreEqual("longName", exception.ParamName);
    }

    [TestMethod]
    public void FileName_NullSet_ThrowsArgumentNull()
    {
        Diagnostics.Arrange("call", "FileName(\"output\", 'o', null)");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.FileName("output", 'o', null!));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception.ParamName", "set", exception.ParamName);
        Assert.AreEqual("set", exception.ParamName);
    }

    [TestMethod]
    public void FileNameApply_EmptyValue_RefusesAsBlankWithoutCallingSet()
    {
        bool setCalled = false;
        Diagnostics.Arrange("builder", "FileName");
        Diagnostics.Arrange("long name", "output");
        Diagnostics.Arrange("short name", 'o');
        CommandLineOption option = CommandLineOption.FileName("output", 'o', (_, _) => setCalled = true);

        Diagnostics.Arrange("value", CommandLineParseDiagnostics.QuoteEach([string.Empty]));
        Diagnostics.Arrange("spelled option", CommandLineParseDiagnostics.QuoteEach(["--output="]));
        CommandLineRefusal? refusal = option.Apply(new CommandLineOptions(), string.Empty, "--output=", _ => false, new RecordingDataFileReader());
        ActRefusal(refusal);

        Diagnostics.Assert("refusal is set", true, refusal is not null);
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
        Diagnostics.Arrange("builder", "FileName");
        Diagnostics.Arrange("long name", "output");
        Diagnostics.Arrange("short name", 'o');
        CommandLineOption option = CommandLineOption.FileName("output", 'o', (_, fileName) => setValue = fileName);

        Diagnostics.Arrange("value", CommandLineParseDiagnostics.QuoteEach([value]));
        Diagnostics.Arrange("spelled option", CommandLineParseDiagnostics.QuoteEach(["-o"]));
        CommandLineRefusal? refusal = option.Apply(new CommandLineOptions(), value, "-o", _ => false, new RecordingDataFileReader());
        ActRefusal(refusal);

        Diagnostics.Assert("refusal", null, refusal);
        Assert.IsNull(refusal);
        Assert.AreEqual(value, setValue);
    }

    private void ActOption(CommandLineOption option)
    {
        Diagnostics.Act("long name", option.LongName);
        Diagnostics.Act("short name", option.ShortName);
        Diagnostics.Act("takes value", option.TakesValue);
        Diagnostics.Act("negatable", option.Negate is not null);
    }

    private void ActRefusal(CommandLineRefusal? refusal)
    {
        Diagnostics.Act("refused", refusal is not null);
        foreach (string line in refusal?.StandardErrorLines ?? [])
        {
            Diagnostics.Act("stderr", line);
        }
    }
}
