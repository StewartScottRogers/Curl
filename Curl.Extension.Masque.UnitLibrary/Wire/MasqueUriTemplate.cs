using System.Globalization;
using System.Text;

namespace Curl.Extension.Masque.Wire;

/// <summary>
/// Expands a MASQUE proxy's URI template (the RFC 6570 expressions <c>{a,b}</c>, <c>{?a,b}</c>
/// and <c>{&amp;a,b}</c>) with the variables of RFC 9298 section 2 (<c>target_host</c>,
/// <c>target_port</c>) or RFC 9484 section 3 (<c>target</c>, <c>ipproto</c>). Values are
/// percent-encoded as RFC 6570 simple expansion does: every character but the unreserved ones.
/// </summary>
internal static class MasqueUriTemplate
{
    /// <summary>
    /// Expands a CONNECT-UDP template, which must name both <c>target_host</c> and <c>target_port</c>.
    /// </summary>
    /// <param name="template">The proxy's URI template.</param>
    /// <param name="targetHost">The UDP target's host name or IP address; an IPv6 address may be bracketed.</param>
    /// <param name="targetPort">The UDP target's port.</param>
    /// <returns>The URI to send the upgrade request to.</returns>
    /// <exception cref="MasqueException">The template is malformed, names another variable or misses one of the pair.</exception>
    public static Uri ExpandConnectUdp(string template, string targetHost, int targetPort)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal)
        {
            ["target_host"] = targetHost.Trim('[', ']'),
            ["target_port"] = targetPort.ToString(CultureInfo.InvariantCulture),
        };
        return Expand(template, values, values.Count, "target_host and target_port");
    }

    /// <summary>
    /// Expands a CONNECT-IP template, which must name <c>target</c>, <c>ipproto</c> or both.
    /// </summary>
    /// <param name="template">The proxy's URI template.</param>
    /// <param name="target">The IP prefix or host name to reach, or <c>*</c> for any.</param>
    /// <param name="ipProtocol">The IP protocol number to carry, or <c>*</c> for any.</param>
    /// <returns>The URI to send the upgrade request to.</returns>
    /// <exception cref="MasqueException">The template is malformed, names another variable or names neither of the pair.</exception>
    public static Uri ExpandConnectIp(string template, string target, string ipProtocol)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal)
        {
            ["target"] = target,
            ["ipproto"] = ipProtocol,
        };
        return Expand(template, values, 1, "target or ipproto");
    }

    private static Uri Expand(string template, Dictionary<string, string> values, int minimumNamed, string requiredNames)
    {
        StringBuilder expanded = new();
        HashSet<string> named = new(StringComparer.Ordinal);
        var position = 0;
        var open = template.IndexOf('{', StringComparison.Ordinal);
        while (open >= 0)
        {
            var close = template.IndexOf('}', open);
            if (close < 0)
            {
                throw new MasqueException($"MASQUE URI template has an unclosed expression: {template}");
            }

            expanded.Append(template, position, open - position);
            AppendExpression(expanded, template[(open + 1)..close], values, named);
            position = close + 1;
            open = template.IndexOf('{', position);
        }

        expanded.Append(template, position, template.Length - position);
        if (named.Count < minimumNamed)
        {
            throw new MasqueException($"MASQUE URI template must name {requiredNames}: {template}");
        }

        return ToHttpUri(expanded.ToString());
    }

    // One expression: a simple list joined by commas, or a form query (? or &) of name=value pairs.
    private static void AppendExpression(StringBuilder expanded, string expression, Dictionary<string, string> values, HashSet<string> named)
    {
        var isForm = expression.StartsWith('?') || expression.StartsWith('&');
        var names = (isForm ? expression[1..] : expression).Split(',');
        var parts = names.Select(name => ExpandVariable(name, values, named, isForm)).ToArray();
        if (isForm)
        {
            expanded.Append(expression[0]);
        }

        expanded.AppendJoin(isForm ? '&' : ',', parts);
    }

    private static string ExpandVariable(string name, Dictionary<string, string> values, HashSet<string> named, bool isForm)
    {
        if (!values.TryGetValue(name, out var value))
        {
            throw new MasqueException($"MASQUE URI template names the unknown variable '{name}'");
        }

        named.Add(name);
        var encoded = Uri.EscapeDataString(value);
        return isForm ? $"{name}={encoded}" : encoded;
    }

    private static Uri ToHttpUri(string expanded)
    {
        if (!Uri.TryCreate(expanded, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new MasqueException($"MASQUE URI template does not expand to an http or https URI: {expanded}");
        }

        return uri;
    }
}
