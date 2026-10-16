using Curl.Api.Codes;
using Libcurl.Misc.ErrorText;

namespace Libcurl.UnitTests.Misc.ErrorText;

[TestClass]
public sealed class CurlErrorTextTests
{
    [TestMethod]
    [DataRow(CurlMultiCode.CallMultiPerform, "Please call curl_multi_perform() soon")]
    [DataRow(CurlMultiCode.Ok, "No error")]
    [DataRow(CurlMultiCode.BadHandle, "Invalid multi handle")]
    [DataRow(CurlMultiCode.BadEasyHandle, "Invalid easy handle")]
    [DataRow(CurlMultiCode.OutOfMemory, "Out of memory")]
    [DataRow(CurlMultiCode.InternalError, "Internal error")]
    [DataRow(CurlMultiCode.BadSocket, "Invalid socket argument")]
    [DataRow(CurlMultiCode.UnknownOption, "Unknown option")]
    [DataRow(CurlMultiCode.AddedAlready, "The easy handle is already added to a multi handle")]
    [DataRow(CurlMultiCode.RecursiveApiCall, "API function called from within callback")]
    [DataRow(CurlMultiCode.WakeupFailure, "Wakeup is unavailable or failed")]
    [DataRow(CurlMultiCode.BadFunctionArgument, "A libcurl function was given a bad argument")]
    [DataRow(CurlMultiCode.AbortedByCallback, "Operation was aborted by an application callback")]
    [DataRow(CurlMultiCode.UnrecoverablePoll, "Unrecoverable error in select/poll")]
    [DataRow((CurlMultiCode)13, "Unknown error")]
    [DataRow((CurlMultiCode)(-2), "Unknown error")]
    public void Multi_DefinedOrUnknownCode_AnswersUpstreamsTextOrFallback(CurlMultiCode code, string expected)
    {
        Assert.AreEqual(expected, CurlErrorText.Multi(code));
    }

    [TestMethod]
    [DataRow(CurlShareCode.Ok, "No error")]
    [DataRow(CurlShareCode.BadOption, "Unknown share option")]
    [DataRow(CurlShareCode.InUse, "Share currently in use")]
    [DataRow(CurlShareCode.Invalid, "Invalid share handle")]
    [DataRow(CurlShareCode.NoMem, "Out of memory")]
    [DataRow(CurlShareCode.NotBuiltIn, "Feature not enabled in this library")]
    [DataRow((CurlShareCode)6, "CURLSHcode unknown")]
    [DataRow((CurlShareCode)(-1), "CURLSHcode unknown")]
    public void Share_DefinedOrUnknownCode_AnswersUpstreamsTextOrFallback(CurlShareCode code, string expected)
    {
        Assert.AreEqual(expected, CurlErrorText.Share(code));
    }

    [TestMethod]
    [DataRow(CurlUrlCode.Ok, "No error")]
    [DataRow(CurlUrlCode.BadHandle, "An invalid CURLU pointer was passed as argument")]
    [DataRow(CurlUrlCode.BadPartPointer, "An invalid 'part' argument was passed as argument")]
    [DataRow(CurlUrlCode.MalformedInput, "Malformed input to a URL function")]
    [DataRow(CurlUrlCode.BadPortNumber, "Port number was not a decimal number between 0 and 65535")]
    [DataRow(CurlUrlCode.UnsupportedScheme, "Unsupported URL scheme")]
    [DataRow(CurlUrlCode.UrlDecode, "URL decode error, most likely because of rubbish in the input")]
    [DataRow(CurlUrlCode.OutOfMemory, "A memory function failed")]
    [DataRow(CurlUrlCode.UserNotAllowed, "Credentials was passed in the URL when prohibited")]
    [DataRow(CurlUrlCode.UnknownPart, "An unknown part ID was passed to a URL API function")]
    [DataRow(CurlUrlCode.NoScheme, "No scheme present")]
    [DataRow(CurlUrlCode.NoUser, "No user present")]
    [DataRow(CurlUrlCode.NoPassword, "No password present")]
    [DataRow(CurlUrlCode.NoOptions, "No options present")]
    [DataRow(CurlUrlCode.NoHost, "No host present")]
    [DataRow(CurlUrlCode.NoPort, "No port number present")]
    [DataRow(CurlUrlCode.NoQuery, "No query present")]
    [DataRow(CurlUrlCode.NoFragment, "No fragment present")]
    [DataRow(CurlUrlCode.NoZoneId, "No zoneid present")]
    [DataRow(CurlUrlCode.BadFileUrl, "Bad file:// URL")]
    [DataRow(CurlUrlCode.BadFragment, "Bad fragment")]
    [DataRow(CurlUrlCode.BadHostname, "Bad hostname")]
    [DataRow(CurlUrlCode.BadIPv6, "Bad IPv6 address")]
    [DataRow(CurlUrlCode.BadLogin, "Bad login")]
    [DataRow(CurlUrlCode.BadPassword, "Bad password")]
    [DataRow(CurlUrlCode.BadPath, "Bad path")]
    [DataRow(CurlUrlCode.BadQuery, "Bad query")]
    [DataRow(CurlUrlCode.BadScheme, "Bad scheme")]
    [DataRow(CurlUrlCode.BadSlashes, "Unsupported number of slashes following scheme")]
    [DataRow(CurlUrlCode.BadUser, "Bad user")]
    [DataRow(CurlUrlCode.LacksIdn, "libcurl lacks IDN support")]
    [DataRow(CurlUrlCode.TooLarge, "A value or data field is larger than allowed")]
    [DataRow(CurlUrlCode.Backslash, "Found a backslash where a forward slash was expected")]
    [DataRow((CurlUrlCode)33, "CURLUcode unknown")]
    [DataRow((CurlUrlCode)(-1), "CURLUcode unknown")]
    public void Url_DefinedOrUnknownCode_AnswersUpstreamsTextOrFallback(CurlUrlCode code, string expected)
    {
        Assert.AreEqual(expected, CurlErrorText.Url(code));
    }

    [TestMethod]
    [DataRow(CurlHeaderCode.Ok, "No error")]
    [DataRow(CurlHeaderCode.BadIndex, "Header exists but not with this index")]
    [DataRow(CurlHeaderCode.Missing, "No such header exists")]
    [DataRow(CurlHeaderCode.NoHeaders, "No headers at all exist (yet)")]
    [DataRow(CurlHeaderCode.NoRequest, "No request with this number was used")]
    [DataRow(CurlHeaderCode.OutOfMemory, "Out of memory while processing")]
    [DataRow(CurlHeaderCode.BadArgument, "A requested argument was bad")]
    [DataRow(CurlHeaderCode.NotBuiltIn, "The header API was disabled in the build")]
    [DataRow((CurlHeaderCode)8, "CURLHcode unknown")]
    [DataRow((CurlHeaderCode)(-1), "CURLHcode unknown")]
    public void Header_DefinedOrUnknownCode_AnswersTheCurlheCommentOrFallback(CurlHeaderCode code, string expected)
    {
        Assert.AreEqual(expected, CurlErrorText.Header(code));
    }

    [TestMethod]
    public void AllFour_EveryDefinedCode_AnswersTextOtherThanTheFallback()
    {
        foreach (var code in Enum.GetValues<CurlMultiCode>())
        {
            Assert.AreNotEqual("Unknown error", CurlErrorText.Multi(code), code.ToString());
        }

        foreach (var code in Enum.GetValues<CurlShareCode>())
        {
            Assert.AreNotEqual("CURLSHcode unknown", CurlErrorText.Share(code), code.ToString());
        }

        foreach (var code in Enum.GetValues<CurlUrlCode>())
        {
            Assert.AreNotEqual("CURLUcode unknown", CurlErrorText.Url(code), code.ToString());
        }

        foreach (var code in Enum.GetValues<CurlHeaderCode>())
        {
            Assert.AreNotEqual("CURLHcode unknown", CurlErrorText.Header(code), code.ToString());
        }
    }
}
