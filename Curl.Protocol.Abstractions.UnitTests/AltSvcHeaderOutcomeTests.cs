namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="AltSvcHeaderOutcome" /> and <see cref="AltSvcSkipReason" />: an outcome holds
/// the alternative added or the reason it was skipped, never both (ADR-0409).
/// </summary>
[TestClass]
public sealed class AltSvcHeaderOutcomeTests
{
    [TestMethod]
    public void Adding_AnAlternative_HoldsItAndNoSkipReason()
    {
        AltSvcAlternative alternative = new("h2", "alt.example", 8443);

        AltSvcHeaderOutcome outcome = AltSvcHeaderOutcome.Adding(alternative);

        Assert.AreSame(alternative, outcome.Added);
        Assert.IsNull(outcome.SkipReason);
    }

    [TestMethod]
    public void Adding_NullAlternative_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => AltSvcHeaderOutcome.Adding(null!));

    [TestMethod]
    [DataRow(AltSvcSkipReason.BadHostname)]
    [DataRow(AltSvcSkipReason.BadIpv6Hostname)]
    [DataRow(AltSvcSkipReason.UnknownPortNumber)]
    public void Skipping_AReason_HoldsItAndNoAlternative(AltSvcSkipReason reason)
    {
        AltSvcHeaderOutcome outcome = AltSvcHeaderOutcome.Skipping(reason);

        Assert.AreEqual(reason, outcome.SkipReason);
        Assert.IsNull(outcome.Added);
    }

    [TestMethod]
    public void Outcomes_WithTheSameContent_AreEqual()
    {
        Assert.AreEqual(AltSvcHeaderOutcome.Adding(new("h3", "a.example", 443)), AltSvcHeaderOutcome.Adding(new("h3", "a.example", 443)));
        Assert.AreEqual(AltSvcHeaderOutcome.Skipping(AltSvcSkipReason.BadHostname), AltSvcHeaderOutcome.Skipping(AltSvcSkipReason.BadHostname));
        Assert.AreNotEqual(AltSvcHeaderOutcome.Skipping(AltSvcSkipReason.BadHostname), AltSvcHeaderOutcome.Skipping(AltSvcSkipReason.UnknownPortNumber));
    }

    [TestMethod]
    public void AltSvcSkipReason_HasExactlyCurlsThreeReasons() =>
        CollectionAssert.AreEqual(
            new[] { AltSvcSkipReason.BadHostname, AltSvcSkipReason.BadIpv6Hostname, AltSvcSkipReason.UnknownPortNumber },
            Enum.GetValues<AltSvcSkipReason>());
}
