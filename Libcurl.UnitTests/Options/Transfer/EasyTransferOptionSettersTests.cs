using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Libcurl.Options;

namespace Libcurl.UnitTests.Options.Transfer;

[TestClass]
public sealed class EasyTransferOptionSettersTests
{
    private static readonly (CurlOption Option, Func<EasyTransferOptions, string?> Field)[] Strings =
    [
        (CurlOption.Range, static t => t.Range),
        (CurlOption.Customrequest, static t => t.CustomRequest),
    ];

    private static readonly (CurlOption Option, Func<EasyTransferOptions, bool> Field)[] Flags =
    [
        (CurlOption.Filetime, static t => t.FileTime),
        (CurlOption.Dirlistonly, static t => t.DirListOnly),
        (CurlOption.Nobody, static t => t.NoBody),
        (CurlOption.Upload, static t => t.Upload),
        (CurlOption.Transfertext, static t => t.TransferText),
        (CurlOption.Crlf, static t => t.Crlf),
    ];

    private static readonly (CurlOption Option, Func<EasyTransferOptions, long> Field, long Least)[] Numbers =
    [
        (CurlOption.ResumeFrom, static t => t.ResumeFrom, -1),
        (CurlOption.ResumeFromLarge, static t => t.ResumeFrom, -1),
        (CurlOption.Infilesize, static t => t.InFileSize, -1),
        (CurlOption.InfilesizeLarge, static t => t.InFileSize, -1),
        (CurlOption.Maxfilesize, static t => t.MaxFileSize, 0),
        (CurlOption.MaxfilesizeLarge, static t => t.MaxFileSize, 0),
        (CurlOption.LowSpeedLimit, static t => t.LowSpeedLimit, 0),
        (CurlOption.MaxSendSpeedLarge, static t => t.MaxSendSpeed, 0),
        (CurlOption.MaxRecvSpeedLarge, static t => t.MaxReceiveSpeed, 0),
    ];

    private static readonly (CurlOption Option, Func<EasyTransferOptions, TimeSpan> Field, TimeSpan Unit)[] Times =
    [
        (CurlOption.LowSpeedTime, static t => t.LowSpeedTime, TimeSpan.FromSeconds(1)),
        (CurlOption.Timeout, static t => t.Timeout, TimeSpan.FromSeconds(1)),
        (CurlOption.TimeoutMs, static t => t.Timeout, TimeSpan.FromMilliseconds(1)),
    ];

    [TestMethod]
    public void SetOption_EachStringOption_SetsItsFieldAndNullClearsIt()
    {
        foreach (var (option, field) in Strings)
        {
            var options = new EasyOptions();

            Assert.AreEqual(CurlCode.Ok, Set(ref options, option, "0-99"), option.ToString());
            Assert.AreEqual("0-99", field(options.Transfer), option.ToString());
            Assert.AreEqual(CurlCode.Ok, Set(ref options, option, null), option.ToString());
            Assert.IsNull(field(options.Transfer), option.ToString());
        }
    }

    [TestMethod]
    public void SetOption_Range_LeavesTheOtherTransferFieldsAlone()
    {
        var options = new EasyOptions();

        Set(ref options, CurlOption.Range, "0-99");

        Assert.AreEqual(new EasyTransferOptions { Range = "0-99" }, options.Transfer);
    }

    [TestMethod]
    public void SetOption_EachOnOffOption_SetsItsFieldOnForNonZeroAndOffForZero()
    {
        foreach (var (option, field) in Flags)
        {
            var options = new EasyOptions();

            Assert.AreEqual(CurlCode.Ok, Set(ref options, option, 2L), option.ToString());
            Assert.IsTrue(field(options.Transfer), option.ToString());
            Assert.AreEqual(CurlCode.Ok, Set(ref options, option, 0), option.ToString());
            Assert.IsFalse(field(options.Transfer), option.ToString());
        }
    }

    [TestMethod]
    public void SetOption_EachNumberOptionAndItsLargeAlias_SetsItsFieldFromItsLeastValueUp()
    {
        foreach (var (option, field, least) in Numbers)
        {
            var options = new EasyOptions();

            Assert.AreEqual(CurlCode.Ok, Set(ref options, option, 5_000_000_000L), option.ToString());
            Assert.AreEqual(5_000_000_000L, field(options.Transfer), option.ToString());
            Assert.AreEqual(CurlCode.Ok, Set(ref options, option, (int)least), option.ToString());
            Assert.AreEqual(least, field(options.Transfer), option.ToString());
        }
    }

    [TestMethod]
    public void SetOption_EachNumberOptionBelowItsLeastValue_ReturnsBadFunctionArgument()
    {
        foreach (var (option, _, least) in Numbers)
        {
            AssertBadFunctionArgument(option, least - 1);
        }
    }

    [TestMethod]
    public void SetOption_EachTimeOptionAndItsMsAlias_SetsItsFieldInItsUnitAndCutsPastIntMax()
    {
        foreach (var (option, field, unit) in Times)
        {
            var options = new EasyOptions();

            Assert.AreEqual(CurlCode.Ok, Set(ref options, option, 7L), option.ToString());
            Assert.AreEqual(unit * 7, field(options.Transfer), option.ToString());
            Assert.AreEqual(CurlCode.Ok, Set(ref options, option, 0), option.ToString());
            Assert.AreEqual(TimeSpan.Zero, field(options.Transfer), option.ToString());
            Assert.AreEqual(CurlCode.Ok, Set(ref options, option, long.MaxValue), option.ToString());
            Assert.AreEqual(unit * int.MaxValue, field(options.Transfer), option.ToString());
        }
    }

    [TestMethod]
    public void SetOption_EachTimeOptionGivenANegativeValue_ReturnsBadFunctionArgument()
    {
        foreach (var (option, _, _) in Times)
        {
            AssertBadFunctionArgument(option, -1L);
        }
    }

    [TestMethod]
    [DataRow(CurlOption.Timevalue, DisplayName = "TIMEVALUE")]
    [DataRow(CurlOption.TimevalueLarge, DisplayName = "TIMEVALUE_LARGE")]
    public void SetOption_TimeValueOrItsLargeAlias_SetsTheTimeValueBefore1970AndPast9999(CurlOption option)
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.Ok, Set(ref options, option, -86400L));
        Assert.AreEqual(-86400L, options.Transfer.TimeValue);
        Assert.AreEqual(CurlCode.Ok, Set(ref options, option, 253402300800L));
        Assert.AreEqual(253402300800L, options.Transfer.TimeValue);
    }

    [TestMethod]
    [DataRow(0L, TimeCondition.None, DisplayName = "CURL_TIMECOND_NONE")]
    [DataRow(1L, TimeCondition.IfModifiedSince, DisplayName = "CURL_TIMECOND_IFMODSINCE")]
    [DataRow(2L, TimeCondition.IfUnmodifiedSince, DisplayName = "CURL_TIMECOND_IFUNMODSINCE")]
    [DataRow(3L, TimeCondition.LastModified, DisplayName = "CURL_TIMECOND_LASTMOD")]
    public void SetOption_TimeConditionInRange_SetsTheCondition(long value, TimeCondition condition)
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Timecondition, value));
        Assert.AreEqual(condition, options.Transfer.TimeCondition);
    }

    [TestMethod]
    public void SetOption_TimeConditionGivenTheEnum_SetsTheCondition()
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Timecondition, TimeCondition.IfUnmodifiedSince));
        Assert.AreEqual(TimeCondition.IfUnmodifiedSince, options.Transfer.TimeCondition);
    }

    [TestMethod]
    [DataRow(-1L, DisplayName = "TIMECONDITION -1")]
    [DataRow(4L, DisplayName = "TIMECONDITION CURL_TIMECOND_LAST")]
    public void SetOption_TimeConditionOutOfRange_ReturnsBadFunctionArgument(long value)
    {
        AssertBadFunctionArgument(CurlOption.Timecondition, value);
    }

    [TestMethod]
    [DataRow(0L, false, DisplayName = "PROXY_TRANSFER_MODE 0")]
    [DataRow(1L, true, DisplayName = "PROXY_TRANSFER_MODE 1")]
    public void SetOption_ProxyTransferModeZeroOrOne_SetsTheMode(long value, bool on)
    {
        var options = new EasyOptions { Transfer = new EasyTransferOptions { ProxyTransferMode = !on } };

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.ProxyTransferMode, value));
        Assert.AreEqual(on, options.Transfer.ProxyTransferMode);
    }

    [TestMethod]
    [DataRow(-1L, DisplayName = "PROXY_TRANSFER_MODE -1")]
    [DataRow(2L, DisplayName = "PROXY_TRANSFER_MODE 2")]
    public void SetOption_ProxyTransferModeOtherThanZeroOrOne_ReturnsBadFunctionArgument(long value)
    {
        AssertBadFunctionArgument(CurlOption.ProxyTransferMode, value);
    }

    [TestMethod]
    [DataRow(16384L, 16384L, DisplayName = "BUFFERSIZE 16384 kept")]
    [DataRow(0L, 16384L, DisplayName = "BUFFERSIZE 0 is the default")]
    [DataRow(1L, 1024L, DisplayName = "BUFFERSIZE 1 raised to 1024")]
    [DataRow(1024L, 1024L, DisplayName = "BUFFERSIZE 1024 kept")]
    [DataRow(10485760L, 10485760L, DisplayName = "BUFFERSIZE 10 MB kept")]
    [DataRow(10485761L, 10485760L, DisplayName = "BUFFERSIZE past 10 MB cut")]
    public void SetOption_BufferSize_KeepsItBetween1024BytesAnd10MegabytesAndZeroIsTheDefault(long value, long kept)
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Buffersize, value));
        Assert.AreEqual(kept, options.Transfer.BufferSize);
    }

    [TestMethod]
    [DataRow(65536L, 65536L, DisplayName = "UPLOAD_BUFFERSIZE 65536 kept")]
    [DataRow(0L, 16384L, DisplayName = "UPLOAD_BUFFERSIZE 0 raised to 16384")]
    [DataRow(16383L, 16384L, DisplayName = "UPLOAD_BUFFERSIZE 16383 raised to 16384")]
    [DataRow(2097152L, 2097152L, DisplayName = "UPLOAD_BUFFERSIZE 2 MB kept")]
    [DataRow(2097153L, 2097152L, DisplayName = "UPLOAD_BUFFERSIZE past 2 MB cut")]
    public void SetOption_UploadBufferSize_KeepsItBetween16KilobytesAnd2Megabytes(long value, long kept)
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.UploadBuffersize, value));
        Assert.AreEqual(kept, options.Transfer.UploadBufferSize);
    }

    [TestMethod]
    public void SetOption_EveryTransferOption_IsInTheOptionsTableAndNoLongerNotBuiltIn()
    {
        CurlOption[] all =
        [
            CurlOption.Range, CurlOption.ResumeFrom, CurlOption.ResumeFromLarge, CurlOption.Customrequest,
            CurlOption.Filetime, CurlOption.Dirlistonly, CurlOption.Nobody, CurlOption.Upload,
            CurlOption.Infilesize, CurlOption.InfilesizeLarge, CurlOption.Buffersize, CurlOption.UploadBuffersize,
            CurlOption.Maxfilesize, CurlOption.MaxfilesizeLarge, CurlOption.Timecondition, CurlOption.Timevalue,
            CurlOption.TimevalueLarge, CurlOption.Transfertext, CurlOption.Crlf, CurlOption.ProxyTransferMode,
            CurlOption.LowSpeedLimit, CurlOption.LowSpeedTime, CurlOption.MaxSendSpeedLarge,
            CurlOption.MaxRecvSpeedLarge, CurlOption.Timeout, CurlOption.TimeoutMs,
        ];

        foreach (var option in all)
        {
            var options = new EasyOptions();

            Assert.IsNotNull(CurlOptions.ById(option), option.ToString());
            Assert.AreNotEqual(CurlCode.NotBuiltIn, Set(ref options, option, CurlOptions.ById(option)!.Type == CurlOptionType.String ? null : 0L), option.ToString());
        }
    }

    private static CurlCode Set(ref EasyOptions options, CurlOption option, object? value) =>
        EasyOptionDispatch.Default.SetOption(ref options, option, value);

    private static void AssertBadFunctionArgument(CurlOption option, object? value)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.BadFunctionArgument, Set(ref options, option, value), option.ToString());
        Assert.AreSame(before, options, option.ToString());
    }
}
