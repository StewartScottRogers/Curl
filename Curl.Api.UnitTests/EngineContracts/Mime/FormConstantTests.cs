using Curl.Api.EngineContracts.Mime;

namespace Curl.Api.UnitTests.EngineContracts.Mime;

[TestClass]
public sealed class FormConstantTests
{
    [TestMethod]
    [DataRow(CurlFormOption.Nothing, 0, DisplayName = "CURLFORM_NOTHING")]
    [DataRow(CurlFormOption.CopyName, 1, DisplayName = "CURLFORM_COPYNAME")]
    [DataRow(CurlFormOption.PointerName, 2, DisplayName = "CURLFORM_PTRNAME")]
    [DataRow(CurlFormOption.NameLength, 3, DisplayName = "CURLFORM_NAMELENGTH")]
    [DataRow(CurlFormOption.CopyContents, 4, DisplayName = "CURLFORM_COPYCONTENTS")]
    [DataRow(CurlFormOption.PointerContents, 5, DisplayName = "CURLFORM_PTRCONTENTS")]
    [DataRow(CurlFormOption.ContentsLength, 6, DisplayName = "CURLFORM_CONTENTSLENGTH")]
    [DataRow(CurlFormOption.FileContent, 7, DisplayName = "CURLFORM_FILECONTENT")]
    [DataRow(CurlFormOption.Array, 8, DisplayName = "CURLFORM_ARRAY")]
    [DataRow(CurlFormOption.Obsolete, 9, DisplayName = "CURLFORM_OBSOLETE")]
    [DataRow(CurlFormOption.File, 10, DisplayName = "CURLFORM_FILE")]
    [DataRow(CurlFormOption.Buffer, 11, DisplayName = "CURLFORM_BUFFER")]
    [DataRow(CurlFormOption.BufferPointer, 12, DisplayName = "CURLFORM_BUFFERPTR")]
    [DataRow(CurlFormOption.BufferLength, 13, DisplayName = "CURLFORM_BUFFERLENGTH")]
    [DataRow(CurlFormOption.ContentType, 14, DisplayName = "CURLFORM_CONTENTTYPE")]
    [DataRow(CurlFormOption.ContentHeader, 15, DisplayName = "CURLFORM_CONTENTHEADER")]
    [DataRow(CurlFormOption.FileName, 16, DisplayName = "CURLFORM_FILENAME")]
    [DataRow(CurlFormOption.End, 17, DisplayName = "CURLFORM_END")]
    [DataRow(CurlFormOption.Obsolete2, 18, DisplayName = "CURLFORM_OBSOLETE2")]
    [DataRow(CurlFormOption.Stream, 19, DisplayName = "CURLFORM_STREAM")]
    [DataRow(CurlFormOption.ContentLength, 20, DisplayName = "CURLFORM_CONTENTLEN")]
    public void CurlFormOption_Member_HasUpstreamNumber(CurlFormOption option, int expected)
    {
        Assert.AreEqual(expected, (int)option);
    }

    [TestMethod]
    public void CurlFormOption_HasOneMemberPerUpstreamOptionBeforeLastEntry()
    {
        Assert.HasCount(21, Enum.GetValues<CurlFormOption>());
    }

    [TestMethod]
    public void CurlFormArgument_KeepsOptionAndValue()
    {
        var argument = new CurlFormArgument(CurlFormOption.CopyName, "field");

        Assert.AreEqual(CurlFormOption.CopyName, argument.Option);
        Assert.AreEqual("field", argument.Value);
    }

    [TestMethod]
    public void CurlFormArgument_EqualPairs_AreEqual()
    {
        Assert.AreEqual(new CurlFormArgument(CurlFormOption.ContentsLength, 4L), new CurlFormArgument(CurlFormOption.ContentsLength, 4L));
        Assert.AreNotEqual(new CurlFormArgument(CurlFormOption.ContentsLength, 4L), new CurlFormArgument(CurlFormOption.ContentLength, 4L));
    }
}
