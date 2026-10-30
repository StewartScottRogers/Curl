using System.Runtime.InteropServices;

namespace Curl.Extension.ClientHints.UnitTests;

[TestClass]
public sealed class ClientHintsPlatformTests
{
    [TestMethod]
    [DataRow("WINDOWS", "Windows")]
    [DataRow("OSX", "macOS")]
    [DataRow("LINUX", "Linux")]
    [DataRow("FREEBSD", "Unknown")]
    public void NameFor_ThePlatform_GivesTheWicgUaChPlatformBrand_Section3Point1(string platform, string expected)
    {
        Assert.AreEqual(expected, ClientHintsPlatform.NameFor(candidate => candidate == OSPlatform.Create(platform)));
    }
}
