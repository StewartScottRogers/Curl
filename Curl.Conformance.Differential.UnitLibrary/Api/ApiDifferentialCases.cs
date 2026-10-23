namespace Curl.Conformance.Differential.Api;

/// <summary>
/// The <c>Curl.Api</c> differential cases (BL-3242, ADR-0790): the common request shapes (GET,
/// headers, form posts, other methods, redirects, authentication, cookies), each beside the curl
/// command line of the CLI HTTP case it mirrors, on BL-3232's loopback routes.
/// </summary>
public static class ApiDifferentialCases
{
    /// <summary>Gets every case, in report order.</summary>
    public static IReadOnlyList<ApiDifferentialCase> All { get; } =
    [
        new("api-get", "-s {U}/echo", new("GET", "/echo")),
        new("api-headers-custom", "-s -H 'X-A: 1' -H 'X-B: two' {U}/echo", new("GET", "/echo") { Headers = [new("X-A", "1"), new("X-B", "two")] }),
        new("api-headers-empty", "-s -H 'Accept;' {U}/echo", new("GET", "/echo") { Headers = [new("Accept", "")] }),
        new("api-user-agent", "-s -A agent/1 {U}/echo", new("GET", "/echo") { UserAgent = "agent/1" }),
        new("api-post-form", "-s -d a=b {U}/echo", new("POST", "/echo") { Body = "a=b" }),
        new("api-post-form-two", "-s -d a=b -d c=d {U}/echo", new("POST", "/echo") { Body = "a=b&c=d" }),
        new("api-post-content-type", "-s -d a=b -H 'Content-Type: text/plain' {U}/echo", new("POST", "/echo") { Body = "a=b", Headers = [new("Content-Type", "text/plain")] }),
        new("api-post-multipart", "-s -F a=1 -F b=2 {U}/echo", new("POST", "/echo") { FormFields = [new("a", "1"), new("b", "2")] }),
        new("api-put", "-s -X PUT -d x {U}/echo", new("PUT", "/echo") { Body = "x" }),
        new("api-delete", "-s -X DELETE {U}/echo", new("DELETE", "/echo")),
        new("api-redirect-none", "-s {U}/r302", new("GET", "/r302")),
        new("api-redirect-301", "-sL {U}/r301", new("GET", "/r301") { FollowsRedirects = true }),
        new("api-redirect-302", "-sL {U}/r302", new("GET", "/r302") { FollowsRedirects = true }),
        new("api-redirect-303-post", "-sL -d x=1 {U}/r303", new("POST", "/r303") { Body = "x=1", FollowsRedirects = true }),
        new("api-redirect-307-post", "-sL -d x=1 {U}/r307", new("POST", "/r307") { Body = "x=1", FollowsRedirects = true }),
        new("api-auth-basic", "-s -u u:p {U}/needauth", new("GET", "/needauth") { UserName = "u", Password = "p" }),
        new("api-auth-basic-wrong", "-s -u u:x {U}/needauth", new("GET", "/needauth") { UserName = "u", Password = "x" }),
        new("api-auth-digest", "-s --digest -u u:p {U}/digest", new("GET", "/digest") { UserName = "u", Password = "p", UsesDigest = true }),
        new("api-cookie", "-s -b 'a=1; b=2' {U}/echo", new("GET", "/echo") { Cookie = "a=1; b=2" }),
        new("api-cookie-engine-redirect", "-s -b '' -L {U}/redir-cookie", new("GET", "/redir-cookie") { StartsCookieEngine = true, FollowsRedirects = true }),
    ];
}
