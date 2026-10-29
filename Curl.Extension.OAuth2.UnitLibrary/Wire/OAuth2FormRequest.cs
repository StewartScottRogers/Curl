using System.Text;

namespace Curl.Extension.OAuth2.Wire;

/// <summary>
/// A request to a token or device authorization endpoint: a body sent by POST with
/// <c>Content-Type: application/x-www-form-urlencoded</c>, and the client's <c>Authorization</c> header.
/// </summary>
/// <param name="Body">The <c>application/x-www-form-urlencoded</c> body.</param>
/// <param name="Authorization">The <c>Authorization</c> header's value, or <see langword="null"/> to send none.</param>
public sealed record OAuth2FormRequest(string Body, string? Authorization)
{
    /// <summary>The <c>Content-Type</c> the body is sent with.</summary>
    public const string ContentType = "application/x-www-form-urlencoded";

    /// <summary>
    /// Writes the request for <paramref name="client"/> (RFC 6749 §2.3.1, §3.2.1): a public client adds
    /// its <c>client_id</c> to the fields, a confidential one sends HTTP Basic or adds <c>client_id</c>
    /// and <c>client_secret</c>.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="fields">The request's own fields, which the client's fields follow.</param>
    /// <returns>The request.</returns>
    internal static OAuth2FormRequest ForClient(OAuth2Client client, List<KeyValuePair<string, string>> fields)
    {
        if (client.ClientSecret is null)
        {
            fields.Add(new("client_id", client.ClientId));
            return new(OAuth2FormEncoder.EncodeFields(fields), null);
        }

        if (client.Authentication == OAuth2ClientAuthentication.Basic)
        {
            return new(OAuth2FormEncoder.EncodeFields(fields), BasicAuthorization(client.ClientId, client.ClientSecret));
        }

        fields.Add(new("client_id", client.ClientId));
        fields.Add(new("client_secret", client.ClientSecret));
        return new(OAuth2FormEncoder.EncodeFields(fields), null);
    }

    // §2.3.1: the identifier and password are form-encoded first, then used as Basic's user and password.
    private static string BasicAuthorization(string clientId, string clientSecret) =>
        "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(
            OAuth2FormEncoder.EncodeComponent(clientId) + ":" + OAuth2FormEncoder.EncodeComponent(clientSecret)));
}
