namespace Curl.Extension.Abstractions.Arguments;

/// <summary>
/// Pins what an <see cref="ITrailingArgumentExpander" /> takes from its <c>--next</c> group (ADR-1051): its companion
/// options and the positional arguments it claims, each in command-line order, and nothing outside its group.
/// </summary>
[TestClass]
public sealed class ArgumentExpansionTrailingArgumentTests
{
    private static readonly ExtensionOption Spec = new("x-spec", ExtensionOptionArgument.Value, "Spec");

    private static readonly ExtensionOption Operation = new("x-op", ExtensionOptionArgument.Value, "Operation");

    private static readonly ExtensionOption Dry = new("x-dry", ExtensionOptionArgument.None, "Dry run");

    private static readonly ExtensionOption Other = new("x-other", ExtensionOptionArgument.Value, "Other spec");

    private static readonly ArgumentExpansionContext Context = new(_ => null, _ => null);

    [TestMethod]
    public void Expand_CompanionsAndPositionals_AreHandedToTheExpanderInCommandLineOrder()
    {
        RecordingExpander expander = new(Spec, Operation, Dry);

        IReadOnlyList<string> expanded = ArgumentExpansion.Expand(["-s", "--x-spec", "s", "--x-op", "o", "a=1", "--x-dry", "b=2", "http://h/", "--x-op=p", "c=3"], [expander], Context);

        CollectionAssert.AreEqual(new[] { "-s", "spec:s", "http://h/" }, expanded.ToArray());
        ClaimedArguments claimed = expander.Claimed.Single();
        Assert.AreEqual("s", claimed.Value);
        CollectionAssert.AreEqual(
            new[] { new CompanionOptionValue(Operation, "o"), new CompanionOptionValue(Dry, string.Empty), new CompanionOptionValue(Operation, "p") },
            claimed.CompanionValues.ToArray());
        CollectionAssert.AreEqual(new[] { "a=1", "b=2", "c=3" }, claimed.PositionalArguments.ToArray());
    }

    [TestMethod]
    public void Expand_ArgumentsInAnotherNextGroup_StayAsTheyAre()
    {
        RecordingExpander expander = new(Spec, Operation);

        IReadOnlyList<string> expanded = ArgumentExpansion.Expand(["--x-spec=s", "a=1", "--next", "--x-op", "o", "b=2", "-:", "c=3"], [expander], Context);

        CollectionAssert.AreEqual(new[] { "spec:s", "--next", "--x-op", "o", "b=2", "-:", "c=3" }, expanded.ToArray());
        CollectionAssert.AreEqual(new[] { "a=1" }, expander.Claimed.Single().PositionalArguments.ToArray());
    }

    [TestMethod]
    public void Expand_WithoutTheExpandersOption_LeavesCompanionsAndPositionalsAsTheyAre()
    {
        string[] arguments = ["--x-op", "o", "a=1", "http://h/"];

        CollectionAssert.AreEqual(arguments, ArgumentExpansion.Expand(arguments, [new RecordingExpander(Spec, Operation)], Context).ToArray());
    }

    [TestMethod]
    public void Expand_PositionalThatMayBeAnOptionsValue_StaysAsItIs()
    {
        RecordingExpander expander = new(Spec);

        IReadOnlyList<string> expanded = ArgumentExpansion.Expand(["--x-spec", "s", "-d", "a=1", "--data", "b=2", "--data=x", "c=3", "-", "d=4", "-e=5"], [expander], Context);

        CollectionAssert.AreEqual(new[] { "spec:s", "-d", "a=1", "--data", "b=2", "--data=x", "-", "-e=5" }, expanded.ToArray());
        CollectionAssert.AreEqual(new[] { "c=3", "d=4" }, expander.Claimed.Single().PositionalArguments.ToArray());
    }

    [TestMethod]
    public void Expand_PositionalTheExpanderDoesNotClaim_StaysAUrl()
    {
        RecordingExpander expander = new(Spec);

        IReadOnlyList<string> expanded = ArgumentExpansion.Expand(["--x-spec", "s", "http://h/", "a=1"], [expander], Context);

        CollectionAssert.AreEqual(new[] { "spec:s", "http://h/" }, expanded.ToArray());
        CollectionAssert.AreEqual(new[] { "a=1" }, expander.Claimed.Single().PositionalArguments.ToArray());
    }

    [TestMethod]
    public void Expand_CompanionWithoutAValue_IsLeftForTheParser()
    {
        RecordingExpander expander = new(Spec, Operation);

        IReadOnlyList<string> expanded = ArgumentExpansion.Expand(["--x-spec", "s", "--x-op"], [expander], Context);

        CollectionAssert.AreEqual(new[] { "spec:s", "--x-op" }, expanded.ToArray());
        Assert.IsEmpty(expander.Claimed.Single().CompanionValues);
    }

    [TestMethod]
    public void Expand_TwoExpandersInOneGroup_EachTakesItsOwnCompanionsAndTheLaterTakesThePositionals()
    {
        RecordingExpander first = new(Spec, Operation);
        RecordingExpander second = new(Other, Dry);

        IReadOnlyList<string> expanded = ArgumentExpansion.Expand(["--x-spec", "s", "--x-other", "t", "--x-op", "o", "--x-dry", "a=1", "http://h/"], [first, second], Context);

        CollectionAssert.AreEqual(new[] { "spec:s", "spec:t", "http://h/" }, expanded.ToArray());
        CollectionAssert.AreEqual(new[] { new CompanionOptionValue(Operation, "o") }, first.Claimed.Single().CompanionValues.ToArray());
        Assert.IsEmpty(first.Claimed.Single().PositionalArguments);
        CollectionAssert.AreEqual(new[] { new CompanionOptionValue(Dry, string.Empty) }, second.Claimed.Single().CompanionValues.ToArray());
        CollectionAssert.AreEqual(new[] { "a=1" }, second.Claimed.Single().PositionalArguments.ToArray());
    }

    [TestMethod]
    public void Expand_AfterABareDoubleDash_NothingIsClaimed()
    {
        RecordingExpander expander = new(Spec, Operation);

        IReadOnlyList<string> expanded = ArgumentExpansion.Expand(["--x-spec", "s", "--", "--x-op", "a=1"], [expander], Context);

        CollectionAssert.AreEqual(new[] { "spec:s", "--", "--x-op", "a=1" }, expanded.ToArray());
        Assert.IsEmpty(expander.Claimed.Single().PositionalArguments);
    }

    [TestMethod]
    public void ArgumentsFor_ValueAlone_IsTheExpansionWithNothingClaimed()
    {
        RecordingExpander expander = new(Spec, Operation);

        IReadOnlyList<string> arguments = ((IArgumentExpander)expander).ArgumentsFor("s", Context);

        CollectionAssert.AreEqual(new[] { "spec:s" }, arguments.ToArray());
        Assert.IsEmpty(expander.Claimed.Single().CompanionValues);
        Assert.IsEmpty(expander.Claimed.Single().PositionalArguments);
    }

    /// <summary>Records what it is given, claims each positional argument holding <c>=</c>, and expands to <c>spec:</c> and its value.</summary>
    private sealed class RecordingExpander(ExtensionOption option, params ExtensionOption[] companions) : ITrailingArgumentExpander
    {
        public List<ClaimedArguments> Claimed { get; } = [];

        public ExtensionOption Option => option;

        public IReadOnlyList<ExtensionOption> CompanionOptions => companions;

        public bool ClaimsPositionalArgument(string argument) => argument.Contains('=', StringComparison.Ordinal);

        public IReadOnlyList<string> ArgumentsFor(ClaimedArguments claimed, ArgumentExpansionContext context)
        {
            Claimed.Add(claimed);
            return ["spec:" + claimed.Value];
        }
    }
}
