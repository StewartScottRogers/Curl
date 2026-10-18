using Curl.Api.Codes;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class LibTestErrorCodesTests
{
    [TestMethod]
    public void Codes_AreFirstHsObsoleteCurlCodeNumbers()
    {
        CurlCode[] codes =
        [
            LibTestErrorCodes.MajorBad, LibTestErrorCodes.RunsForever, LibTestErrorCodes.EasyInit,
            LibTestErrorCodes.Multi, LibTestErrorCodes.NumHandles, LibTestErrorCodes.Select,
            LibTestErrorCodes.Success, LibTestErrorCodes.Failure, LibTestErrorCodes.Usage,
            LibTestErrorCodes.FileOpen, LibTestErrorCodes.FileStat, LibTestErrorCodes.BadTimeout,
        ];

        CollectionAssert.AreEqual(
            new[] { 20, 24, 29, 32, 34, 40, 41, 44, 46, 50, 51, 57 },
            codes.Select(code => (int)code).ToArray());
    }
}
