using System.Text;
using Curl.Api.Codes;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib1521Tests
{
    private const string OutOfMemory = "returned 27, \"Out of memory\"";

    private const string BadArgument = "returned 43, \"A libcurl function was given a bad argument\"";

    [TestMethod]
    public async Task Lib1521_OverTheFakeEngine_PrintsOnlyOk()
    {
        var run = new PortRun(new FakeCurlEngine(), 1521, "-");

        LibTestResult result = await new Lib1521().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("ok\n", run.OutputText.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.AreEqual("lib1521", new Lib1521().ProgramName);
    }

    [TestMethod]
    public async Task Lib1521_OnACleanedUpLibrary_StopsAtTheFirstEasyInit()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 1521, "-");

        await Lib1521.TestAsync(run.Context, run.Steps, library);

        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task Lib1521_WhenTheLibraryGoesAtTheFirstHandle_StopsAtTheSecondEasyInit()
    {
        var holder = new PortRun.LibraryHolder();
        var run = new PortRun(PortRun.CleaningUpOnEasy(holder, 1), 1521, "-");
        holder.Library = CurlGlobal.Init(run.Context.Engine!);

        await Lib1521.TestAsync(run.Context, run.Steps, holder.Library);

        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task Lib1521_NullArguments_Throw()
    {
        var run = new PortRun(new FakeCurlEngine(), 1521, "-");
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await Lib1521.TestAsync(null!, run.Steps, library));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await Lib1521.TestAsync(run.Context, null!, library));
        Assert.ThrowsExactly<ArgumentNullException>(() => Checks((_, _) => CurlCode.Ok, new StringWriter()).VerifyOption(null!));
    }

    [TestMethod]
    public void VerifyOption_AStringOptionThatRefusesEverything_ReportsTheStringAndTheNull()
    {
        var output = new StringWriter { NewLine = "\n" };
        var values = new List<object?>();

        Checks((_, value) => { values.Add(value); return CurlCode.OutOfMemory; }, output).VerifyOption(Row(CurlOption.Url));

        Assert.AreEqual(
            $"CURLOPT_URL set to a string {OutOfMemory} on line 1\nCURLOPT_URL set to NULL {OutOfMemory} on line 3\n",
            output.ToString());
        CollectionAssert.AreEqual(new object?[] { "string", string.Empty, null }, values);
    }

    [TestMethod]
    public void VerifyOption_AStringRefusalUpstreamAllows_ReportsNothing()
    {
        var output = new StringWriter();

        Checks((_, value) => value is null ? CurlCode.Ok : CurlCode.BadFunctionArgument, output).VerifyOption(Row(CurlOption.DnsServers));

        Assert.AreEqual(string.Empty, output.ToString());
    }

    [TestMethod]
    public void VerifyOption_AStringOptionNotBuiltIn_StopsAfterTheFirstCall()
    {
        var output = new StringWriter();
        int calls = 0;

        Checks((_, _) => { calls++; return CurlCode.NotBuiltIn; }, output).VerifyOption(Row(CurlOption.Url));

        Assert.AreEqual(1, calls);
        Assert.AreEqual(string.Empty, output.ToString());
    }

    [TestMethod]
    public void VerifyOption_ALongThatMustTakeMinusOne_ReportsTheMinusOneAndAllowsTheBadArguments()
    {
        var output = new StringWriter { NewLine = "\n" };
        var values = new List<object?>();

        Checks((_, value) => { values.Add(value); return value is 0L ? CurlCode.Ok : CurlCode.BadFunctionArgument; }, output)
            .VerifyOption(Row(CurlOption.Maxredirs));

        Assert.AreEqual($"CURLOPT_MAXREDIRS set to -1 {BadArgument} on line 4\n", output.ToString());
        CollectionAssert.AreEqual(new object?[] { 0L, 22L, long.MinValue, -1L, long.MaxValue }, values);
    }

    [TestMethod]
    public void VerifyOption_ALongThatRefusesEverything_ReportsTheZeroAndEachNonZero()
    {
        var output = new StringWriter { NewLine = "\n" };

        Checks((_, _) => CurlCode.OutOfMemory, output).VerifyOption(Row(CurlOption.Verbose));

        Assert.AreEqual(
            $"CURLOPT_VERBOSE set to 0 {OutOfMemory} on line 1\n" +
            $"CURLOPT_VERBOSE set to non-zero {OutOfMemory} on line 2\n" +
            $"CURLOPT_VERBOSE set to non-zero {OutOfMemory} on line 3\n" +
            $"CURLOPT_VERBOSE set to non-zero {OutOfMemory} on line 5\n",
            output.ToString());
    }

    [TestMethod]
    public void VerifyOption_AValuesOptionThatAnswersUnsupportedProtocol_IsAllowedOnlyForHttpVersion()
    {
        var allowed = new StringWriter();
        var refused = new StringWriter();

        Checks((_, value) => value is 0L ? CurlCode.BadFunctionArgument : CurlCode.UnsupportedProtocol, allowed)
            .VerifyOption(Row(CurlOption.HttpVersion));
        Checks((_, value) => value is 0L ? CurlCode.Ok : CurlCode.UnsupportedProtocol, refused)
            .VerifyOption(new CurlEasyOption("SSLVERSION", CurlOption.Sslversion, CurlOptionType.Values, CurlEasyOptionFlags.None));

        Assert.AreEqual(string.Empty, allowed.ToString());
        Assert.AreEqual(3, refused.ToString().Split("set to non-zero").Length - 1);
    }

    [TestMethod]
    public void VerifyOption_AnOffTOptionNotBuiltIn_StopsAfterTheZero()
    {
        var values = new List<object?>();
        var output = new StringWriter();

        Checks((_, value) => { values.Add(value); return CurlCode.UnknownOption; }, output).VerifyOption(Row(CurlOption.InfilesizeLarge));
        Checks((_, value) => { values.Add(value); return CurlCode.Ok; }, output).VerifyOption(Row(CurlOption.InfilesizeLarge));

        CollectionAssert.AreEqual(new object?[] { 0L, 0L, 22L, long.MaxValue, long.MinValue }, values);
        Assert.AreEqual(string.Empty, output.ToString());
    }

    [TestMethod]
    public void VerifyOption_ABlob_ReportsTheStaleResultNotItsOwnAnswer()
    {
        var output = new StringWriter { NewLine = "\n" };
        var values = new List<object?>();
        var checks = Checks((_, value) => { values.Add(value); return value is null ? CurlCode.Ok : CurlCode.OutOfMemory; }, output, _ => CurlCode.OutOfMemory);

        checks.VerifyOption(Row(CurlOption.SslcertBlob));
        checks.VerifyInfo(CurlInfo.ResponseCode, "RESPONSE_CODE");
        checks.VerifyOption(Row(CurlOption.SslcertBlob));

        Assert.AreEqual(
            $"CURLINFO_RESPONSE_CODE {OutOfMemory} on line 3\nCURLOPT_SSLCERT_BLOB {OutOfMemory} on line 4\n",
            output.ToString());
        CollectionAssert.AreEqual("silly"u8.ToArray(), (byte[])values[0]!);
        Assert.IsNull(values[1]);
    }

    [TestMethod]
    public void VerifyOption_ABlobNotBuiltIn_SkipsTheNull()
    {
        int calls = 0;

        Checks((_, _) => { calls++; return CurlCode.NotBuiltIn; }, new StringWriter()).VerifyOption(Row(CurlOption.SslcertBlob));

        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public void VerifyOption_PointerOptions_PassTheGeneratorsValues()
    {
        var calls = new List<(CurlOption Option, object? Value)>();
        var share = new object();
        var checks = Checks((option, value) => { calls.Add((option, value)); return CurlCode.Ok; }, new StringWriter(), share: share);

        checks.VerifyOption(Row(CurlOption.Httpheader));
        checks.VerifyOption(Row(CurlOption.Writefunction));
        checks.VerifyOption(Row(CurlOption.Postfields));
        checks.VerifyOption(Row(CurlOption.Share));

        CollectionAssert.AreEqual(
            new (CurlOption, object?)[]
            {
                (CurlOption.Httpheader, null), (CurlOption.Httpheader, null),
                (CurlOption.Writefunction, null), (CurlOption.Writefunction, null),
                (CurlOption.Postfieldsize, 0L), (CurlOption.Postfields, "moooo"), (CurlOption.Postfields, null),
                (CurlOption.Share, share), (CurlOption.Share, null),
            },
            calls);
    }

    [TestMethod]
    public void VerifyOption_APointerThatRefuses_ReportsItAndANotBuiltInOneSkipsTheNull()
    {
        var output = new StringWriter { NewLine = "\n" };
        int calls = 0;

        Checks((_, _) => CurlCode.OutOfMemory, output).VerifyOption(Row(CurlOption.Share));
        Checks((_, _) => { calls++; return CurlCode.NotBuiltIn; }, output).VerifyOption(Row(CurlOption.Share));

        Assert.AreEqual($"CURLOPT_SHARE {OutOfMemory} on line 1\nCURLOPT_SHARE set to NULL {OutOfMemory} on line 2\n", output.ToString());
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public void VerifyAll_EveryOptionAndInfoAnsweringOk_ReportsNothingAndCallsEachInfoOnce()
    {
        var output = new StringWriter();
        var infos = new List<CurlInfo>();

        Checks((_, _) => CurlCode.Ok, output, info => { infos.Add(info); return CurlCode.Ok; }).VerifyAll();

        Assert.AreEqual(string.Empty, output.ToString());
        CollectionAssert.AreEqual(Lib1521Infos.All.Select(static i => i.Info).ToList(), infos);
    }

    [TestMethod]
    public void Options_AreTheNonAliasRowsInCurlHsOrder()
    {
        List<CurlEasyOption> options = Lib1521Checks.Options.ToList();

        Assert.AreEqual(CurlOptions.All.Count(static o => !o.IsAlias), options.Count);
        Assert.AreEqual("WRITEDATA", options[0].Name);
        Assert.IsFalse(options.Any(static o => o.IsAlias));
    }

    [TestMethod]
    public void Infos_AreCurlHs78InOrder()
    {
        Assert.AreEqual(78, Lib1521Infos.All.Count);
        Assert.AreEqual((CurlInfo.EffectiveUrl, "EFFECTIVE_URL"), Lib1521Infos.All[0]);
    }

    [TestMethod]
    [DataRow(CurlOption.DnsLocalIp4, CurlCode.BadFunctionArgument, true)]
    [DataRow(CurlOption.Sslengine, CurlCode.BadFunctionArgument, true)]
    [DataRow(CurlOption.Url, CurlCode.BadFunctionArgument, false)]
    [DataRow(CurlOption.ProtocolsStr, CurlCode.UnsupportedProtocol, true)]
    [DataRow(CurlOption.Url, CurlCode.UnsupportedProtocol, false)]
    [DataRow(CurlOption.Sslengine, CurlCode.SslEngineNotFound, true)]
    [DataRow(CurlOption.Url, CurlCode.SslEngineNotFound, false)]
    [DataRow(CurlOption.Sslengine, CurlCode.OutOfMemory, false)]
    public void IsAllowedStringError_AnswersUpstreamsErrstringList(CurlOption option, CurlCode code, bool allowed) =>
        Assert.AreEqual(allowed, Lib1521Checks.IsAllowedStringError(option, code));

    [TestMethod]
    [DataRow(CurlOption.Verbose, CurlCode.BadFunctionArgument, true)]
    [DataRow(CurlOption.HttpVersion, CurlCode.UnsupportedProtocol, true)]
    [DataRow(CurlOption.Verbose, CurlCode.UnsupportedProtocol, false)]
    [DataRow(CurlOption.Proxyauth, CurlCode.NotBuiltIn, true)]
    [DataRow(CurlOption.Verbose, CurlCode.NotBuiltIn, false)]
    [DataRow(CurlOption.Verbose, CurlCode.OutOfMemory, false)]
    public void IsAllowedNumberError_AnswersUpstreamsErrlongList(CurlOption option, CurlCode code, bool allowed) =>
        Assert.AreEqual(allowed, Lib1521Checks.IsAllowedNumberError(option, code));

    [TestMethod]
    public void IsPresentAndMustTakeMinusOne_AnswerUpstreamsMacros()
    {
        Assert.IsTrue(Lib1521Checks.IsPresent(CurlCode.Ok));
        Assert.IsFalse(Lib1521Checks.IsPresent(CurlCode.NotBuiltIn));
        Assert.IsFalse(Lib1521Checks.IsPresent(CurlCode.UnknownOption));
        Assert.IsTrue(Lib1521Checks.MustTakeMinusOne(CurlOption.ResumeFromLarge));
        Assert.IsFalse(Lib1521Checks.MustTakeMinusOne(CurlOption.Verbose));
    }

    [TestMethod]
    public void ObjectFor_GivesEachOptionTheGeneratorsObject()
    {
        object dependency = new(), share = new(), url = new();
        var values = new Lib1521Values(dependency, share, url);

        Assert.AreSame(dependency, values.ObjectFor(CurlOption.StreamDepends));
        Assert.AreSame(dependency, values.ObjectFor(CurlOption.StreamDependsE));
        Assert.AreSame(share, values.ObjectFor(CurlOption.Share));
        Assert.AreSame(url, values.ObjectFor(CurlOption.Curlu));
        Assert.IsInstanceOfType<StringBuilder>(values.ObjectFor(CurlOption.Errorbuffer));
        Assert.IsNull(values.ObjectFor(CurlOption.Httppost));
        Assert.IsNull(values.ObjectFor(CurlOption.Mimepost));
        Assert.AreSame(Stream.Null, values.ObjectFor(CurlOption.Stderr));
        Assert.AreSame(values.ObjectFor(CurlOption.Writedata), values.ObjectFor(CurlOption.Readdata));
    }

    private static CurlEasyOption Row(CurlOption option) => CurlOptions.ById(option)!;

    private static Lib1521Checks Checks(
        Func<CurlOption, object?, CurlCode> setOption,
        TextWriter output,
        Func<CurlInfo, CurlCode>? getInfo = null,
        object? share = null) =>
        new(setOption, getInfo ?? (_ => CurlCode.Ok), new Lib1521Values(new object(), share ?? new object(), new object()), output);
}
