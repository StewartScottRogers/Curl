using Curl.Api.Options.Protocols;

namespace Curl.Api.UnitTests.Options.Protocols;

[TestClass]
public sealed class EasyTftpOptionsTests
{
    private static readonly EasyTftpOptions Defaults = new();

    [TestMethod]
    public void New_EveryOption_DefaultsToLibcurlDefault()
    {
        Assert.AreEqual(512, Defaults.BlockSize);
        Assert.IsFalse(Defaults.NoOptions);
    }

    [TestMethod]
    public void With_OneOption_ChangesOnlyThatOption()
    {
        OptionRecordAssert.ChangesOnly(Defaults, Defaults with { BlockSize = 1024 }, o => o with { BlockSize = Defaults.BlockSize });
        OptionRecordAssert.ChangesOnly(Defaults, Defaults with { NoOptions = true }, o => o with { NoOptions = Defaults.NoOptions });
        Assert.HasCount(2, typeof(EasyTftpOptions).GetProperties());
    }
}
