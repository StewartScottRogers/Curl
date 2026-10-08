using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="AltSvcHeaderOutcome" /> and <see cref="AltSvcSkipReason" />: an outcome holds
/// the alternative added or the reason it was skipped, never both (ADR-0409).
/// </summary>
[TestClass]
public sealed class AltSvcHeaderOutcomeTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Adding_AnAlternative_HoldsItAndNoSkipReason()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        AltSvcAlternative alternative = new("h2", "alt.example", 8443);
        diagnostics.Arrange("alternative", alternative);

        AltSvcHeaderOutcome outcome = AltSvcHeaderOutcome.Adding(alternative);

        diagnostics.Act("added", outcome.Added);
        diagnostics.Act("skip reason", outcome.SkipReason);
        diagnostics.Assert("added is the alternative", alternative, outcome.Added);
        diagnostics.Assert("skip reason", null, outcome.SkipReason);
        Assert.AreSame(alternative, outcome.Added);
        Assert.IsNull(outcome.SkipReason);
    }

    [TestMethod]
    public void Adding_NullAlternative_Throws()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("alternative", null);

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => AltSvcHeaderOutcome.Adding(null!));

        diagnostics.Act("exception message", exception.Message);
        diagnostics.Assert("exception type", typeof(ArgumentNullException), exception.GetType());
    }

    [TestMethod]
    [DataRow(AltSvcSkipReason.BadHostname)]
    [DataRow(AltSvcSkipReason.BadIpv6Hostname)]
    [DataRow(AltSvcSkipReason.UnknownPortNumber)]
    public void Skipping_AReason_HoldsItAndNoAlternative(AltSvcSkipReason reason)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("reason", reason);

        AltSvcHeaderOutcome outcome = AltSvcHeaderOutcome.Skipping(reason);

        diagnostics.Act("skip reason", outcome.SkipReason);
        diagnostics.Act("added", outcome.Added);
        diagnostics.Assert("skip reason", reason, outcome.SkipReason);
        Assert.AreEqual(reason, outcome.SkipReason);
        Assert.IsNull(outcome.Added);
    }

    [TestMethod]
    public void Outcomes_WithTheSameContent_AreEqual()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        AltSvcHeaderOutcome addedFirst = AltSvcHeaderOutcome.Adding(new("h3", "a.example", 443));
        AltSvcHeaderOutcome addedSecond = AltSvcHeaderOutcome.Adding(new("h3", "a.example", 443));
        AltSvcHeaderOutcome badHostFirst = AltSvcHeaderOutcome.Skipping(AltSvcSkipReason.BadHostname);
        AltSvcHeaderOutcome badHostSecond = AltSvcHeaderOutcome.Skipping(AltSvcSkipReason.BadHostname);
        AltSvcHeaderOutcome badPort = AltSvcHeaderOutcome.Skipping(AltSvcSkipReason.UnknownPortNumber);
        diagnostics.Arrange("added first", addedFirst);
        diagnostics.Arrange("skipped bad hostname", badHostFirst);
        diagnostics.Arrange("skipped unknown port", badPort);

        diagnostics.Act("added equal", addedFirst.Equals(addedSecond));
        diagnostics.Act("skipped equal", badHostFirst.Equals(badHostSecond));
        diagnostics.Act("different reasons equal", badHostFirst.Equals(badPort));
        diagnostics.Assert("added equal", true, addedFirst.Equals(addedSecond));
        Assert.AreEqual(AltSvcHeaderOutcome.Adding(new("h3", "a.example", 443)), AltSvcHeaderOutcome.Adding(new("h3", "a.example", 443)));
        Assert.AreEqual(AltSvcHeaderOutcome.Skipping(AltSvcSkipReason.BadHostname), AltSvcHeaderOutcome.Skipping(AltSvcSkipReason.BadHostname));
        Assert.AreNotEqual(AltSvcHeaderOutcome.Skipping(AltSvcSkipReason.BadHostname), AltSvcHeaderOutcome.Skipping(AltSvcSkipReason.UnknownPortNumber));
    }

    [TestMethod]
    public void AltSvcSkipReason_HasExactlyCurlsThreeReasons()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        AltSvcSkipReason[] expected = [AltSvcSkipReason.BadHostname, AltSvcSkipReason.BadIpv6Hostname, AltSvcSkipReason.UnknownPortNumber];
        diagnostics.Arrange("expected reasons", string.Join(",", expected));

        AltSvcSkipReason[] actual = Enum.GetValues<AltSvcSkipReason>();

        diagnostics.Act("defined reasons", string.Join(",", actual));
        diagnostics.Assert("defined reasons", string.Join(",", expected), string.Join(",", actual));
        CollectionAssert.AreEqual(expected, actual);
    }
}
