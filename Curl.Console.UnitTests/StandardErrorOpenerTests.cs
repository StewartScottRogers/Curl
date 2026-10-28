using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Checks that <see cref="StandardErrorOpener" /> writes nothing to a descriptor 2 the shell did not
/// give, as curl writes nothing to a closed standard error (<c>2&gt;&amp;-</c>, BL-3824, ADR-0868).
/// </summary>
[TestClass]
public sealed class StandardErrorOpenerTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Open_OffWindowsDescriptor2NotInherited_OpensTheNullStreamNotTheDescriptor()
    {
        int askedDescriptor = -1;
        Diagnostics.Arrange("descriptor 2 inherited", false);

        Stream opened = StandardErrorOpener.Open(
            runsOnWindows: false,
            () => throw new AssertFailedException("The console stream was opened."),
            _ => throw new AssertFailedException("Descriptor 2 was opened."),
            descriptor =>
            {
                askedDescriptor = descriptor;
                return false;
            });
        Diagnostics.Act("opened the null stream", ReferenceEquals(Stream.Null, opened));

        Diagnostics.Assert("opened the null stream", true, ReferenceEquals(Stream.Null, opened));
        Assert.AreSame(Stream.Null, opened);
        Assert.AreEqual(StandardErrorOpener.UnixStandardErrorFileDescriptor, askedDescriptor);
    }
}
