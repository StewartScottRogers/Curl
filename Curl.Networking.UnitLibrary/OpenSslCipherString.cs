using System.Collections.Frozen;
using System.Net.Security;

namespace Curl.Networking;

/// <summary>
/// Expands a <c>--ciphers</c> value as OpenSSL 3's <c>SSL_CTX_set_cipher_list</c> does
/// (ADR-0817): a leading <c>DEFAULT</c>, aliases such as <c>HIGH</c>, <c>aRSA</c> or
/// <c>ECDHE</c> joined by <c>+</c> into their intersection, the <c>!</c>, <c>-</c> and
/// <c>+</c> rule prefixes, and the <c>@STRENGTH</c> and <c>@SECLEVEL=n</c> commands, over
/// the TLS 1.2-and-below suites of <see cref="OpenSslCipherSuites" />' table in OpenSSL's
/// <c>ALL</c> order. An entry starting <c>TLS_</c> that names a suite is that suite
/// (ADR-0011); an unknown alias is skipped; an invalid command fails the whole value.
/// </summary>
internal sealed class OpenSslCipherString
{
    private const string DefaultKeyword = "DEFAULT";
    private const string SecurityLevelCommand = "SECLEVEL=";
    private const string StrengthCommand = "STRENGTH";
    private const string IanaNamePrefix = "TLS_";
    private const string RulePrefixes = "-+!@";

    private static readonly Rule[] PrefixedRules = [Rule.Delete, Rule.MoveToEnd, Rule.Kill, Rule.Command];

    // Each alias and the suites (by OpenSSL name) it selects, as OpenSSL 3.5.5's
    // `openssl ciphers` lists them for the suites the table has.
    private static readonly (string Alias, Func<string, bool> Selects)[] AliasRules =
    [
        ("ALL", _ => true),
        ("HIGH", _ => true),
        ("COMPLEMENTOFDEFAULT", IsDss),
        ("kECDHE", IsEcdhe), ("kEECDH", IsEcdhe), ("ECDHE", IsEcdhe), ("EECDH", IsEcdhe),
        ("kDHE", IsDhe), ("kEDH", IsDhe), ("DHE", IsDhe), ("EDH", IsDhe),
        ("kRSA", IsRsaKeyExchange), ("RSA", IsRsaKeyExchange),
        ("aRSA", name => !IsEcdsa(name) && !IsDss(name)),
        ("aECDSA", IsEcdsa), ("ECDSA", IsEcdsa),
        ("aDSS", IsDss), ("DSS", IsDss),
        ("AES", name => name.Contains("AES", StringComparison.Ordinal)),
        ("AES128", name => name.Contains("AES128", StringComparison.Ordinal)),
        ("AES256", name => name.Contains("AES256", StringComparison.Ordinal)),
        ("AESGCM", name => name.Contains("AES", StringComparison.Ordinal) && IsGcm(name)),
        ("CHACHA20", name => name.Contains("CHACHA20", StringComparison.Ordinal)),
        ("ARIA", name => name.Contains("ARIA", StringComparison.Ordinal)),
        ("ARIA128", name => name.Contains("ARIA128", StringComparison.Ordinal)),
        ("ARIA256", name => name.Contains("ARIA256", StringComparison.Ordinal)),
        ("ARIAGCM", name => name.Contains("ARIA", StringComparison.Ordinal)),
        ("CAMELLIA", name => name.Contains("CAMELLIA", StringComparison.Ordinal)),
        ("CAMELLIA128", name => name.Contains("CAMELLIA128", StringComparison.Ordinal)),
        ("CAMELLIA256", name => name.Contains("CAMELLIA256", StringComparison.Ordinal)),
        ("SHA1", HasSha1Mac), ("SHA", HasSha1Mac),
        ("SHA256", name => !IsGcm(name) && name.EndsWith("SHA256", StringComparison.Ordinal)),
        ("SHA384", name => !IsGcm(name) && name.EndsWith("SHA384", StringComparison.Ordinal)),
        ("SSLv3", HasSha1Mac), ("TLSv1", HasSha1Mac), ("TLSv1.0", HasSha1Mac),
        ("TLSv1.2", name => !HasSha1Mac(name)),
    ];

    private static readonly FrozenDictionary<string, TlsCipherSuite[]> SuitesByAlias = BuildSuitesByAlias();

    private readonly string _value;
    private readonly List<TlsCipherSuite> _active = [];
    private readonly HashSet<TlsCipherSuite> _killed = [];
    private int _position;

    private OpenSslCipherString(string value) => _value = value;

    private enum Rule
    {
        Add,
        Delete,
        Kill,
        MoveToEnd,
        Command,
    }

    /// <summary>
    /// Expands one <c>--ciphers</c> value into the suites it selects, in order.
    /// </summary>
    /// <param name="value">The option value.</param>
    /// <returns>
    /// The selected suites, possibly none; <see langword="null" /> when the value holds an
    /// invalid command (an empty alias, or <c>@</c> with neither <c>STRENGTH</c> nor
    /// <c>SECLEVEL=0</c> to <c>5</c>), which OpenSSL refuses outright.
    /// </returns>
    public static IReadOnlyList<TlsCipherSuite>? Expand(string value)
    {
        var expansion = new OpenSslCipherString(value);
        return expansion.ApplyAllRules() ? expansion._active : null;
    }

    private static bool IsEcdhe(string name) => name.StartsWith("ECDHE-", StringComparison.Ordinal);

    private static bool IsDhe(string name) => name.StartsWith("DHE-", StringComparison.Ordinal);

    private static bool IsRsaKeyExchange(string name) => !name.Contains("DHE-", StringComparison.Ordinal);

    private static bool IsEcdsa(string name) => name.Contains("ECDSA", StringComparison.Ordinal);

    private static bool IsDss(string name) => name.Contains("-DSS-", StringComparison.Ordinal);

    private static bool IsGcm(string name) => name.Contains("GCM", StringComparison.Ordinal);

    private static bool HasSha1Mac(string name) => name.EndsWith("-SHA", StringComparison.Ordinal);

    private static FrozenDictionary<string, TlsCipherSuite[]> BuildSuitesByAlias()
    {
        var names = OpenSslCipherSuites.AliasedTls12Names;
        var byAlias = AliasRules.ToDictionary(
            rule => rule.Alias,
            rule => names.Where(entry => rule.Selects(entry.OpenSslName)).Select(entry => entry.Suite).ToArray(),
            StringComparer.Ordinal);
        foreach (var (openSslName, suite) in OpenSslCipherSuites.NamedTls12Suites)
        {
            byAlias[openSslName] = [suite];
        }

        return byAlias.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static bool IsSeparator(char ch) => ch is ':' or ' ' or ';' or ',';

    private static bool IsAliasCharacter(char ch) => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '.' or '=';

    // The rule a prefix character starts; any other character starts an Add rule.
    private static Rule RuleOf(char ch)
    {
        var index = RulePrefixes.IndexOf(ch, StringComparison.Ordinal);
        return index < 0 ? Rule.Add : PrefixedRules[index];
    }

    // The strength bits OpenSSL sorts @STRENGTH by.
    private static int StrengthBitsOf(TlsCipherSuite suite)
    {
        var name = suite.ToString();
        if (name.Contains("_256_", StringComparison.Ordinal) || name.Contains("CHACHA20", StringComparison.Ordinal))
        {
            return 256;
        }

        return name.Contains("3DES", StringComparison.Ordinal) ? 112 : 128;
    }

    private bool ApplyAllRules()
    {
        if (_value.StartsWith(DefaultKeyword, StringComparison.Ordinal))
        {
            ApplyRule(Rule.Add, OpenSslCipherSuites.DefaultTls12Suites);
            _position = DefaultKeyword.Length;
        }

        var valid = true;
        while (_position < _value.Length)
        {
            valid &= ApplyNextRule();
        }

        return valid;
    }

    private bool ApplyNextRule()
    {
        var ch = _value[_position];
        if (IsSeparator(ch))
        {
            _position++;
            return true;
        }

        var rule = RuleOf(ch);
        _position += rule == Rule.Add ? 0 : 1;
        if (rule == Rule.Command)
        {
            return ApplyCommand();
        }

        var (valid, suites) = ReadSelection();
        if (suites is null)
        {
            // OpenSSL skips the rest of an element it found nothing for; after a found
            // selection an '@' command may follow in the same element.
            SkipToSeparator();
        }
        else
        {
            ApplyRule(rule, suites);
        }

        return valid;
    }

    // An IANA name up to the next separator, else aliases joined by '+'; null suites for
    // a selection naming an unknown alias.
    private (bool Valid, IReadOnlyList<TlsCipherSuite>? Suites) ReadSelection()
    {
        var end = _value.IndexOfAny([':', ' ', ';', ','], _position);
        var element = end < 0 ? _value[_position..] : _value[_position..end];
        if (element.StartsWith(IanaNamePrefix, StringComparison.Ordinal) && OpenSslCipherSuites.Find(element) is { } named && !OpenSslCipherSuites.IsTls13(named))
        {
            _position += element.Length;
            return (true, [named]);
        }

        return ReadAliases();
    }

    private (bool Valid, IReadOnlyList<TlsCipherSuite>? Suites) ReadAliases()
    {
        IEnumerable<TlsCipherSuite>? selected = null;
        while (true)
        {
            var word = ReadWord();
            if (word.Length == 0)
            {
                _position++;
                return (false, null);
            }

            if (!SuitesByAlias.TryGetValue(word, out var suites))
            {
                return (true, null);
            }

            selected = selected is null ? suites : selected.Intersect(suites);
            if (_position >= _value.Length || _value[_position] != '+')
            {
                return (true, selected.ToArray());
            }

            _position++;
        }
    }

    private string ReadWord()
    {
        var start = _position;
        while (_position < _value.Length && IsAliasCharacter(_value[_position]))
        {
            _position++;
        }

        return _value[start.._position];
    }

    private void SkipToSeparator()
    {
        while (_position < _value.Length && !IsSeparator(_value[_position]))
        {
            _position++;
        }
    }

    private bool ApplyCommand()
    {
        var word = ReadWord();
        var valid = word == StrengthCommand || IsSecurityLevel(word);
        if (word == StrengthCommand)
        {
            var sorted = _active.OrderByDescending(StrengthBitsOf).ToArray();
            _active.Clear();
            _active.AddRange(sorted);
        }

        _position += word.Length == 0 ? 1 : 0;
        SkipToSeparator();
        return valid;
    }

    // @SECLEVEL=0 to 5; every suite in the table passes levels 0 to 2, so the level is not kept.
    private static bool IsSecurityLevel(string word) =>
        word.Length == SecurityLevelCommand.Length + 1
        && word.StartsWith(SecurityLevelCommand, StringComparison.Ordinal)
        && word[^1] is >= '0' and <= '5';

    private void ApplyRule(Rule rule, IReadOnlyList<TlsCipherSuite> suites)
    {
        switch (rule)
        {
            case Rule.Add:
                _active.AddRange(suites.Where(suite => !_killed.Contains(suite) && !_active.Contains(suite)).ToArray());
                break;
            case Rule.Kill:
                _killed.UnionWith(suites);
                _active.RemoveAll(suites.Contains);
                break;
            case Rule.Delete:
                _active.RemoveAll(suites.Contains);
                break;
            default:
                var moved = _active.Where(suites.Contains).ToArray();
                _active.RemoveAll(suites.Contains);
                _active.AddRange(moved);
                break;
        }
    }
}
