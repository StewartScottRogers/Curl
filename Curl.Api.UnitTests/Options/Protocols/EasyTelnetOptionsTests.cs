using Curl.Api.Options.Protocols;

namespace Curl.Api.UnitTests.Options.Protocols;

[TestClass]
public sealed class EasyTelnetOptionsTests
{
    private static readonly EasyTelnetOptions Defaults = new();

    [TestMethod]
    public void New_TelnetOptions_DefaultToEmpty()
    {
        Assert.IsEmpty(Defaults.TelnetOptions);
    }

    [TestMethod]
    public void With_TelnetOptions_ChangesOnlyThatOption()
    {
        OptionRecordAssert.ChangesOnly(Defaults, Defaults with { TelnetOptions = ["TTYPE=vt100"] }, o => o with { TelnetOptions = Defaults.TelnetOptions });
        Assert.HasCount(1, typeof(EasyTelnetOptions).GetProperties());
    }
}
