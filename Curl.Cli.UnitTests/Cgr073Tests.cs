using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins CGR-073 (BL-3602): curl 8.21.0's tool reads <c>-z "=date"</c> (and <c>"==date"</c>) as
/// <c>CURL_TIMECOND_LASTMOD</c>, so an HTTP request carries <c>Last-Modified: date</c> rather than
/// <c>If-Modified-Since</c>, and <c>--libcurl</c> writes <c>CURL_TIMECOND_LASTMOD</c>. Measured on
/// Windows on 2026-10-26.
/// </summary>
[TestClass]
public sealed class Cgr073Tests
{
    private const string Url = "http://127.0.0.1:1/f";

    private const long ConditionSeconds = 1445412480;

    [TestMethod]
    [DataRow("=Wed, 21 Oct 2015 07:28:00 GMT")]
    [DataRow("==Wed, 21 Oct 2015 07:28:00 GMT")]
    public void Parse_TimeCondEqualsDate_IsSentAsLastModified(string value)
    {
        TimeCondition? condition = CommandLineParser.Parse(["-z", value, Url]).Options!.TimeCondition;

        Assert.AreEqual(TimeCondition.FromUnixSeconds(ConditionSeconds, TimeConditionKind.IfModifiedSince) with { SentAsLastModified = true }, condition);
    }

    [TestMethod]
    [DataRow("Wed, 21 Oct 2015 07:28:00 GMT")]
    [DataRow("+Wed, 21 Oct 2015 07:28:00 GMT")]
    [DataRow("-Wed, 21 Oct 2015 07:28:00 GMT")]
    public void Parse_TimeCondWithoutEquals_IsNotSentAsLastModified(string value)
    {
        TimeCondition? condition = CommandLineParser.Parse(["-z", value, Url]).Options!.TimeCondition;

        Assert.IsFalse(condition!.SentAsLastModified);
        Assert.AreEqual(ConditionSeconds, condition.ValueUnixSeconds);
    }

    [TestMethod]
    public void Parse_TimeCondEqualsFileName_IsThatFilesTimeSentAsLastModified()
    {
        DateTimeOffset modified = new(2026, 9, 26, 21, 5, 24, TimeSpan.Zero);
        RecordingDataFileReader reader = new() { ModificationTimes = { ["CLAUDE.md"] = modified } };

        TimeCondition? condition = CommandLineParser.Parse(["-z", "=CLAUDE.md", Url], _ => true, new UnexpectedPasswordPrompt(), reader).Options!.TimeCondition;

        Assert.AreEqual(new TimeCondition(modified, TimeConditionKind.IfModifiedSince) { SentAsLastModified = true }, condition);
    }

    [TestMethod]
    [DataRow("=Wed, 21 Oct 2015 07:28:00 GMT", "CURL_TIMECOND_LASTMOD")]
    [DataRow("Wed, 21 Oct 2015 07:28:00 GMT", "CURL_TIMECOND_IFMODSINCE")]
    [DataRow("-Wed, 21 Oct 2015 07:28:00 GMT", "CURL_TIMECOND_IFUNMODSINCE")]
    public void Generate_TimeCond_WritesCurlsTimeConditionName(string value, string expectedName)
    {
        CommandLineOptions options = CommandLineParser.Parse(["-z", value, Url]).Options!;

        string source = LibcurlSourceCode.Generate([(options, Url)]);

        StringAssert.Contains(source, $"  curl_easy_setopt(curl, CURLOPT_TIMECONDITION, (long){expectedName});\n");
        StringAssert.Contains(source, $"  curl_easy_setopt(curl, CURLOPT_TIMEVALUE_LARGE, (curl_off_t){ConditionSeconds});\n");
    }

    private sealed class UnexpectedPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => throw new AssertFailedException("No password prompt was expected.");
    }
}
