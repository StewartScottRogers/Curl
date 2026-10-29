using Curl.Engine;

namespace Curl.Console;

/// <summary>
/// CGR-182 (BL-3711): <c>--skip-existing -o NUL</c> skips the transfer on Windows, because curl's
/// <c>stat</c> finds the NUL device.
/// </summary>
[TestClass]
public sealed class Cgr182Tests
{
    private static PhysicalOutputPaths PathsFindingDosDevices(bool findsDosDevices) =>
        new(isRegularFile: null, CRuntimeErrorNumbers.Windows, findsDosDevices: findsDosDevices);

    [TestMethod]
    [DataRow("NUL")]
    [DataRow("nul")]
    [DataRow("COM1")]
    [DataRow("lpt9")]
    public void Exists_DosDeviceNameWhereDevicesAreFound_IsTrue(string name)
    {
        bool exists = PathsFindingDosDevices(findsDosDevices: true).Exists(name);

        Assert.IsTrue(exists);
    }

    [TestMethod]
    public void Exists_DosDeviceNameWhereDevicesAreNotFound_IsFalse()
    {
        bool exists = PathsFindingDosDevices(findsDosDevices: false).Exists("NUL");

        Assert.IsFalse(exists);
    }

    [TestMethod]
    public void Exists_NameOnlyStartingLikeADosDevice_IsFalse()
    {
        bool exists = PathsFindingDosDevices(findsDosDevices: true).Exists("curl-cgr182-nul-missing");

        Assert.IsFalse(exists);
    }
}
