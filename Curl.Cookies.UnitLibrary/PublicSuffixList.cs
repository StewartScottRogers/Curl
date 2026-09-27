using System.Globalization;

namespace Curl.Cookies;

/// <summary>
/// The embedded Public Suffix List snapshot (ADR-0049), asked the one question curl asks it: may a host set a
/// cookie for a domain? The answer is libpsl 0.21.5's <c>psl_is_cookie_domain_acceptable</c>, which curl
/// 8.21.0 calls, with rules from both the ICANN and the private sections, as BL-223 measured.
/// </summary>
internal sealed class PublicSuffixList
{
    /// <summary>The manifest name <c>Curl.Cookies.UnitLibrary.csproj</c> gives the snapshot.</summary>
    private const string ResourceName = "Curl.Cookies.PublicSuffixList.public_suffix_list.dat";

    /// <summary>curl refuses every cookie from a host this long or longer, before asking libpsl.</summary>
    private const int LongestHost = 255;

    private static readonly Lazy<PublicSuffixList> EmbeddedList = new(ReadEmbedded);

    private static readonly IdnMapping Idn = new();

    /// <summary>Each rule's domain, without <c>*.</c> or <c>!</c>, and the kinds of rule written for it.</summary>
    private readonly Dictionary<string, RuleKinds> rules = new(StringComparer.Ordinal);

    [Flags]
    private enum RuleKinds
    {
        None = 0,
        Plain = 1,
        Wildcard = 2,
        Exception = 4,
    }

    /// <summary>The snapshot embedded in this assembly, read on first use.</summary>
    public static PublicSuffixList Embedded => EmbeddedList.Value;

    /// <summary>
    /// libpsl's <c>psl_is_cookie_domain_acceptable</c>, behind curl's length limit: a host of more than
    /// <see cref="LongestHost"/> characters sets no cookie; otherwise a host may set a cookie for itself, or
    /// for a parent domain longer than the public suffix the host ends in.
    /// </summary>
    /// <param name="host">The request host.</param>
    /// <param name="cookieDomain">The cookie's domain: the host itself, or a parent of it in any case.</param>
    public bool IsCookieDomainAcceptable(string host, string cookieDomain)
    {
        string lowerHost = host.ToLowerInvariant();
        string lowerDomain = cookieDomain.ToLowerInvariant();
        return lowerHost.Length <= LongestHost
            && (lowerHost == lowerDomain || lowerDomain.Length > UnregistrableSuffix(lowerHost).Length);
    }

    /// <summary>
    /// libpsl's <c>psl_unregistrable_domain</c>: the longest public suffix <paramref name="host"/> ends in,
    /// found by dropping labels from the left until what is left is a public suffix.
    /// </summary>
    private string UnregistrableSuffix(string host)
    {
        string suffix = host;
        while (!IsPublicSuffix(suffix))
        {
            suffix = suffix[(suffix.IndexOf('.', StringComparison.Ordinal) + 1)..];
        }

        return suffix;
    }

    /// <summary>
    /// libpsl's <c>psl_is_public_suffix</c>: a single label (the implicit <c>*</c> rule), a domain a rule
    /// names that is not an exception, or a child of a wildcard rule's domain.
    /// </summary>
    private bool IsPublicSuffix(string domain)
    {
        int firstDot = domain.IndexOf('.', StringComparison.Ordinal);
        if (firstDot < 0)
        {
            return true;
        }

        if (rules.TryGetValue(domain, out RuleKinds kinds))
        {
            return !kinds.HasFlag(RuleKinds.Exception);
        }

        return rules.GetValueOrDefault(domain[(firstDot + 1)..]).HasFlag(RuleKinds.Wildcard);
    }

    private static PublicSuffixList ReadEmbedded()
    {
        using Stream stream = typeof(PublicSuffixList).Assembly.GetManifestResourceStream(ResourceName)!;
        using StreamReader reader = new(stream);
        return Read(reader);
    }

    /// <summary>
    /// Reads the list's format: one rule per line, up to the first whitespace; blank lines and <c>//</c>
    /// comments are skipped. A rule with a non-ASCII label is also kept in its ASCII (punycode) form.
    /// </summary>
    private static PublicSuffixList Read(TextReader reader)
    {
        PublicSuffixList list = new();
        for (string? line = reader.ReadLine(); line is not null; line = reader.ReadLine())
        {
            string rule = line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            if (rule.Length > 0 && !rule.StartsWith("//", StringComparison.Ordinal))
            {
                list.Add(rule);
            }
        }

        return list;
    }

    private void Add(string rule)
    {
        (string domain, RuleKinds kind) = rule switch
        {
            _ when rule.StartsWith("*.", StringComparison.Ordinal) => (rule[2..], RuleKinds.Wildcard),
            _ when rule.StartsWith('!') => (rule[1..], RuleKinds.Exception),
            _ => (rule, RuleKinds.Plain),
        };

        AddKind(domain, kind);
        if (!domain.All(char.IsAscii))
        {
            AddKind(Idn.GetAscii(domain), kind);
        }
    }

    private void AddKind(string domain, RuleKinds kind) => rules[domain] = rules.GetValueOrDefault(domain) | kind;
}
