using System.Net;

namespace Curl.Extension.Abstractions.Resolution;

[TestClass]
public sealed class ResolverHooksTests
{
    private static readonly ExtensionOption Dot = new("example-dot", ExtensionOptionArgument.Value, "Resolve over DNS-over-TLS");

    private static readonly ExtensionOption Odoh = new("example-odoh", ExtensionOptionArgument.Value, "Resolve over Oblivious DoH");

    [TestMethod]
    public void ActivatedBy_WithNoExtensionOptionGiven_GivesCurlsResolverBack()
    {
        NamedResolver curls = new();

        ResolverHooks hooks = ResolverHooks.ActivatedBy(ExtensionOptionValues.None, [ExampleHook.ActivatedBy(Dot, new ExampleHook(Dot))], dohUrlGiven: false);

        Assert.IsNull(hooks.Hook);
        Assert.AreSame(curls, hooks.ResolverOver(curls));
        Assert.IsNull(ResolverHooks.None.Hook);
        Assert.AreSame(curls, ResolverHooks.None.ResolverOver(curls));
    }

    [TestMethod]
    public void ActivatedBy_WithOnlyDohUrlGiven_GivesCurlsResolverBack()
    {
        NamedResolver curls = new();

        ResolverHooks hooks = ResolverHooks.ActivatedBy(ExtensionOptionValues.None, [ExampleHook.ActivatedBy(Dot, new ExampleHook(Dot))], dohUrlGiven: true);

        Assert.IsNull(hooks.Hook);
        Assert.AreSame(curls, hooks.ResolverOver(curls));
    }

    [TestMethod]
    public void ActivatedBy_WithItsOptionGiven_GivesTheHooksResolverOverCurls()
    {
        NamedResolver curls = new();
        ExampleHook hook = new(Dot);

        ResolverHooks hooks = ResolverHooks.ActivatedBy(ValuesOf(Dot), [ExampleHook.ActivatedBy(Dot, hook), ExampleHook.ActivatedBy(Odoh, new ExampleHook(Odoh))], dohUrlGiven: false);

        Assert.AreSame(hook, hooks.Hook);
        Assert.AreSame(hook.Resolver, hooks.ResolverOver(curls));
        Assert.AreSame(curls, hook.GivenResolver);
    }

    [TestMethod]
    public void ActivatedBy_WithAnExtensionResolverAndDohUrl_FailsNamingBoth()
    {
        ExtensionHookConflictException failure = Assert.ThrowsExactly<ExtensionHookConflictException>(() => ResolverHooks.ActivatedBy(
            ValuesOf(Dot),
            [ExampleHook.ActivatedBy(Dot, new ExampleHook(Dot))],
            dohUrlGiven: true));

        StringAssert.StartsWith(failure.Message, "the options --example-dot and --doh-url each supply the resolver a transfer looks host names up with; give only one of them");
    }

    [TestMethod]
    public void ActivatedBy_WithTwoExtensionsClaimingTheResolver_FailsNamingBothOptions()
    {
        ExtensionHookConflictException failure = Assert.ThrowsExactly<ExtensionHookConflictException>(() => ResolverHooks.ActivatedBy(
            ValuesOf(Dot, Odoh),
            [ExampleHook.ActivatedBy(Dot, new ExampleHook(Dot)), ExampleHook.ActivatedBy(Odoh, new ExampleHook(Odoh))],
            dohUrlGiven: false));

        StringAssert.StartsWith(failure.Message, "the options --example-dot and --example-odoh each supply the resolver a transfer looks host names up with; give only one of them");
    }

    private static ExtensionOptionValues ValuesOf(params ExtensionOption[] options) =>
        new(options.ToDictionary(option => option.Name, _ => ""));

    private sealed class ExampleHook(ExtensionOption option) : IResolverHook
    {
        public ExtensionOption Option => option;

        public NamedResolver Resolver { get; } = new();

        public IDnsResolver? GivenResolver { get; private set; }

        public static Func<ExtensionOptionValues, IResolverHook?> ActivatedBy(ExtensionOption option, ExampleHook hook) =>
            values => values.IsGiven(option) ? hook : null;

        public IDnsResolver ResolverOver(IDnsResolver curlResolver)
        {
            GivenResolver = curlResolver;
            return Resolver;
        }
    }

    private sealed class NamedResolver : IDnsResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            throw new NotSupportedException("never resolves");
    }
}
