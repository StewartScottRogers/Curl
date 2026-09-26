# ADR-0014 — HTTP request options and the auth, cookie and proxy seams

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation.

## Context

The Phase 1 HTTP plan (protocol-architect, 2026-09-26, item X1; filed as BL-157) needs
an HTTP handler that reproduces curl 8.21.0's request bytes for `-X`, `-H`, `-A`,
`-e`, `-d`, `-F`, `-f`, `-L`, `-u`, `--oauth2-bearer` and `-x`
(<https://curl.se/docs/manpage.html>, behaviour measured on curl 8.21.0 by the tasks
that pin it). None of these reach a protocol handler today:

- `ITransferContext` (`Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs`)
  carries the options ADR-0003, ADR-0006 and ADR-0008 added: `Url`, `Output`, `Upload`,
  `ResumeFrom`, `Range`, `MaxFileSize`, `NoBody`, `TimeCondition`, `HeaderOutput`,
  `PostData`, `Credentials`, `TelnetOptions`, `TftpBlockSize`, `TftpNoOptions`,
  `ConvertLineEndings`, `CreateFileMode`, `ConnectTimeout`, `MaxTime`, `TimeProvider`
  and `CancellationToken`. No member is HTTP-specific.
- `ConnectTarget(string Host, int Port, bool UseTls)` (ADR-0005) can name only a direct
  connection. It validates `Host` and `Port` in property initialisers and gives them
  no `init` accessor, so a `with` expression cannot bypass the checks.
- `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` recognises `-d`/`--data` and
  `-u`/`--user` but none of the other options above; parsing them is BL-188, BL-189,
  BL-192 and their siblings.

Three dependency facts constrain where the new types can live:

- `Curl.Protocol.Http.UnitLibrary` may reference only
  `Curl.Protocol.Abstractions.UnitLibrary`. Its `CLAUDE.md` says so, and
  `Curl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs`
  (`ProtocolLibrary_References_OnlyAbstractions`) fails the fast suite if a protocol
  library references anything else.
- `Curl.Authentication.UnitLibrary` and `Curl.Cookies.UnitLibrary` each reference only
  `Curl.Protocol.Abstractions.UnitLibrary` and contain no code yet.
- `Curl.Console` references every library, `Curl.Authentication.UnitLibrary`,
  `Curl.Cookies.UnitLibrary` and `Curl.Core.UnitLibrary` included, so it is the one
  place that can hand an implementation from one to the HTTP handler.

So the HTTP handler cannot call Authentication or Cookies code directly, and it cannot
call a multipart builder in `Curl.Core.UnitLibrary` either. It needs contracts it can
see, and the command-line layer needs somewhere typed to put the HTTP options.

`TransferReport`, `TransferTimings` and `ConnectTimings` belong to the same plan but
are recorded separately by BL-158's ADR and are not specified here.

## Decision

Every type below is added to `Curl.Protocol.Abstractions.UnitLibrary`, in the
`Curl.Protocol.Abstractions` namespace, one type per file. BL-159, BL-161 and BL-162
will add them; none exists in the code yet.

### `ITransferContext.Http` and `TransferContext.Http`

```csharp
// ITransferContext
HttpRequestOptions? Http { get; }

// TransferContext
public HttpRequestOptions? Http { get; init; }   // defaults to null
```

- `null` means no HTTP option was given, and an HTTP handler treats it exactly as
  `new HttpRequestOptions()`, every member at its default.
- Every non-HTTP handler ignores `Http`. The object is a single member, so the context
  grows by one, not by sixteen.
- Existing handlers compile unchanged: none reads the new member, and `TransferContext`
  defaults it. The one other implementer of `ITransferContext`,
  `Curl.Protocol.File.UnitTests/Fakes/FakeTransferContext.cs`, is replaced by
  `TransferContext` in BL-159, as ADR-0006 expects.
- `PostData` and `Credentials` stay where ADR-0006 put them. MQTT keeps reading
  `PostData`; the HTTP handler never reads it and sends `Http.Body` instead.
  `Credentials` (`-u`, else the URL's user information) is shared by every scheme and
  is what the HTTP handler passes to `IHttpAuthenticator`; it is not repeated inside
  `HttpRequestOptions`.
- `-T`/`--upload-file` stays on `ITransferContext.Upload` (ADR-0003), and the HTTP
  handler sends it as a PUT body. `Body` carries only the `-d` and `-F` families, which
  curl sends as POST. Keeping the two apart lets the handler choose the method from
  which member is set, without a flag on the body saying where it came from.

### `HttpRequestOptions`

```csharp
public sealed record HttpRequestOptions
{
    public string? CustomMethod { get; init; }
    public IReadOnlyList<string> Headers { get; init; } = [];
    public string? UserAgent { get; init; }
    public string? Referer { get; init; }
    public HttpRequestBody? Body { get; init; }
    public bool FollowRedirects { get; init; }
    public HttpFailMode Fail { get; init; }                  // HttpFailMode.None
    public HttpVersionPreference Version { get; init; }      // HttpVersionPreference.Http11
    public bool Compressed { get; init; }
    public bool Raw { get; init; }
    public bool IgnoreContentLength { get; init; }
    public string? RequestTarget { get; init; }
    public HttpAuthSchemes AuthSchemes { get; init; } = HttpAuthSchemes.Basic;
    public string? BearerToken { get; init; }
    public ProxyEndpoint? ForwardProxy { get; init; }
    public bool ProxyTunnel { get; init; }
}
```

| Member | Type | Default | Filled from | Meaning |
| --- | --- | --- | --- | --- |
| `CustomMethod` | `string?` | `null` | `-X`/`--request` | The method, verbatim; `null` lets the handler choose GET, HEAD (`NoBody`), POST (`Body`) or PUT (`Upload`). |
| `Headers` | `IReadOnlyList<string>` | empty | `-H`/`--header` | Each value verbatim, in command-line order. The handler applies curl's replace (`Name: v`), remove (`Name:`) and empty (`Name;`) rules (BL-172). |
| `UserAgent` | `string?` | `null` | `-A`/`--user-agent` | `null` sends `User-Agent: curl/8.21.0`; the empty string sends no `User-Agent` header, as `-A ""` does. |
| `Referer` | `string?` | `null` | `-e`/`--referer` | The referring URL; `null` sends no `Referer` header. The `;auto` suffix is not carried here. |
| `Body` | `HttpRequestBody?` | `null` | `-d`/`--data`, `--data-binary`, `--data-raw`, `--data-ascii`, `--data-urlencode`, `--json`, `-F`/`--form`, `--form-string` | The request body, already encoded by the command-line layer; `null` sends none. |
| `FollowRedirects` | `bool` | `false` | `-L`/`--location` | The handler drains a 3xx body instead of writing it (BL-179). Following the redirect is `Curl.Core`'s redirect follower (BL-203), not the handler. |
| `Fail` | `HttpFailMode` | `None` | `-f`/`--fail` gives `Fail`; `--fail-with-body` gives `FailWithBody` | How a status of 400 or above ends the transfer (BL-176). |
| `Version` | `HttpVersionPreference` | `Http11` | `-0`/`--http1.0` gives `Http10`; `--http1.1` gives `Http11` | The HTTP version the request line asks for. |
| `Compressed` | `bool` | `false` | `--compressed` | Ask for and decode a compressed body (BL-154, BL-177). |
| `Raw` | `bool` | `false` | `--raw` | Pass content and transfer encodings through undecoded. |
| `IgnoreContentLength` | `bool` | `false` | `--ignore-content-length` | Read the body to close regardless of `Content-Length`. |
| `RequestTarget` | `string?` | `null` | `--request-target` | The request line's target, verbatim; `null` derives it from `Url`. |
| `AuthSchemes` | `HttpAuthSchemes` | `Basic` | `--basic`, `--digest`, `--ntlm`, `--negotiate`, `--anyauth`, `--oauth2-bearer` | The schemes the authenticator may answer with, for the origin. curl's default is Basic. |
| `BearerToken` | `string?` | `null` | `--oauth2-bearer` | The token for Bearer authentication; `null` when not given. |
| `ForwardProxy` | `ProxyEndpoint?` | `null` | `-x`/`--proxy`, `-U`/`--proxy-user`, `--proxy1.0`, `--socks4`, `--socks4a`, `--socks5`, `--socks5-hostname`, and the proxy environment variables | The proxy already chosen for this URL; `null` connects directly. `--noproxy` and the environment are resolved before the context is built (BL-206). |
| `ProxyTunnel` | `bool` | `false` | `-p`/`--proxytunnel` | Tunnel through an HTTP-kind proxy with CONNECT even for an `http://` URL. |

Spellings chosen here, beyond the plan's list:

- `Fail` and `Version` are the property names, and their types are `HttpFailMode` and
  `HttpVersionPreference`, so a call site reads `Fail = HttpFailMode.FailWithBody`.
- `ProxyTunnel` is added. The plan's list had no member for `-p`, which BL-192 parses
  and BL-212 measures, and without it `-p` could not reach the handler.
- `AuthSchemes` defaults to `Basic`, not to `default(HttpAuthSchemes)` (`None`), so
  `new HttpRequestOptions()` means what curl does when only `-u` is given.

Which of several `-f`/`--fail-with-body`, `--http1.0`/`--http1.1` or auth-scheme options
wins, or whether scheme options combine, is for the parsing tasks to measure; the types
express any answer.

### `HttpFailMode`

```csharp
public enum HttpFailMode
{
    None,          // default: a 4xx or 5xx body is written and the exit code is 0
    Fail,          // -f/--fail: exit 22, no body written
    FailWithBody,  // --fail-with-body: the body is written, then exit 22
}
```

Exit 22 is `CurlExitCode.HttpReturnedError`
(<https://curl.se/libcurl/c/libcurl-errors.html>).

### `HttpVersionPreference`

```csharp
public enum HttpVersionPreference
{
    Http11,  // default, and --http1.1
    Http10,  // -0/--http1.0
}
```

`Http11` is first so that `default(HttpVersionPreference)` is curl's default. Values for
HTTP/2 and HTTP/3 (`--http2`, `--http3` and their variants) will be appended by a later
ADR when a task implements them; appending breaks no caller.

### `HttpAuthSchemes`

```csharp
[Flags]
public enum HttpAuthSchemes
{
    None = 0,
    Basic = 1,       // --basic, and the default
    Digest = 2,      // --digest
    Ntlm = 4,        // --ntlm
    Negotiate = 8,   // --negotiate
    Bearer = 16,     // --oauth2-bearer
    Any = Basic | Digest | Ntlm | Negotiate,  // --anyauth
}
```

- `--anyauth` maps to `Any`.
- `Bearer` is present. `--oauth2-bearer` adds it and sets `BearerToken`, so the
  authenticator decides from one set of flags which schemes it may use, rather than
  from the flags plus a separate "is there a token" test.
- `Bearer` is not in `Any`. `Any` is the set of schemes a user name and password can
  answer; Bearer needs a token that only `--oauth2-bearer` supplies, so it is enabled
  by that option and not by `--anyauth`.
- The numeric values are this solution's own. They are not libcurl's `CURLAUTH_*` bits
  and are never cast to or from them.
- AWS SigV4 (`--aws-sigv4`) has no flag; it will arrive by a later ADR with the task
  that implements it.

### `HttpRequestBody`, `BytesBody` and `StreamBody`

```csharp
public abstract record HttpRequestBody
{
    public string ContentType { get; }
}

public sealed record BytesBody(ReadOnlyMemory<byte> Content, string ContentType)
    : HttpRequestBody;

public sealed record StreamBody(Stream Content, long? Length, string ContentType)
    : HttpRequestBody;
```

- `ContentType` is the value of the `Content-Type` header the body is sent with, for
  example `application/x-www-form-urlencoded` for `-d` (measured in BL-175) or
  `multipart/form-data; boundary=...` for `-F` (measured in BL-205). It is never null.
  A `-H "Content-Type: ..."` or `-H "Content-Type:"` still overrides or removes it
  through `Headers`, by the same rules as any other internal header.
- `BytesBody` is for a body already in memory: every `-d` form, which the command-line
  layer reads in full at parse time, as `CommandLineOptionTable.AppendPostData` does
  today. It is sent with `Content-Length: Content.Length`.
- `StreamBody` is for a body produced while sending, such as a multipart body with a
  file part. A non-null `Length` is sent as `Content-Length`; a `null` `Length` means
  the size is unknown and the body is sent with `Transfer-Encoding: chunked`, as curl
  does for a body of unknown size. The handler reads `Content` but does not own it;
  whoever built the body disposes it.
- The constructor of the derived records passes `ContentType` to the base, which
  exposes it read-only; BL-159 chooses the exact constructor shape, provided the
  positional signatures above hold.

### `ProxyEndpoint`, `ProxyKind` and `ConnectTarget.Proxy`

```csharp
public sealed record ProxyEndpoint(
    ProxyKind Kind,
    string Host,
    int Port,
    System.Net.NetworkCredential? Credential);

public enum ProxyKind
{
    Http,            // http:// or no scheme on -x: HTTP/1.1 proxy
    Http10,          // --proxy1.0: HTTP/1.0 proxy
    Https,           // https:// on -x: TLS to the proxy itself
    Socks4,          // socks4:// or --socks4
    Socks4a,         // socks4a:// or --socks4a
    Socks5,          // socks5:// or --socks5: local name resolution
    Socks5Hostname,  // socks5h:// or --socks5-hostname: the proxy resolves the name
}

// ConnectTarget gains, keeping its positional constructor unchanged:
public ProxyEndpoint? Proxy { get; init; }   // defaults to null
```

- `ProxyEndpoint` validates `Host` and `Port` exactly as `ConnectTarget` does, in
  property initialisers with no `init` accessor, throwing the same
  `ArgumentNullException`, `ArgumentException` and `ArgumentOutOfRangeException`.
- `Credential` comes from `-U`/`--proxy-user`, else from the proxy URL's user
  information; `null` when neither is present.
- The kinds are the ones Phase 1 tasks use: BL-192 parses all seven proxy schemes and
  options, BL-212 tunnels through the HTTP kinds and BL-213 through the SOCKS kinds.
  Any other proxy type will arrive by a later ADR.
- `ConnectTarget.Proxy` set means: connect to the proxy, open a tunnel to `Host` and
  `Port` through it (CONNECT for the HTTP kinds, the SOCKS handshake for the SOCKS
  kinds), then apply TLS over the tunnel when `UseTls` is `true`. `null`, the default,
  is a direct connection, exactly as today. `new ConnectTarget(host, port, useTls)`
  keeps compiling and behaving as it does now.
- A plain `http://` URL through an HTTP-kind proxy without `ProxyTunnel` is not a
  tunnel. The handler connects to the proxy as an ordinary target, with `Proxy` left
  `null`, and sends an absolute-form request line (BL-183). An `https://` URL, a SOCKS
  proxy, or `ProxyTunnel` passes the proxy through `ConnectTarget.Proxy`, and the
  connector does the rest (BL-212, BL-213).
- A tunnel failure is a `ConnectResult.Failed` like any other connect failure
  (ADR-0005); the exit codes are for BL-212 and BL-213 to measure.

### `IHttpAuthenticator` and `HttpAuthRequest`

```csharp
public interface IHttpAuthenticator
{
    string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges);
}

public sealed record HttpAuthRequest(
    string Method,
    Uri Url,
    string RequestTarget,
    System.Net.NetworkCredential? Credential,
    string? BearerToken,
    HttpAuthSchemes AllowedSchemes,
    bool IsProxy);
```

`HttpAuthRequest` members:

| Member | Type | Meaning |
| --- | --- | --- |
| `Method` | `string` | The method of the request being authorised, as sent; Digest hashes it. |
| `Url` | `Uri` | The URL of the request being authorised. |
| `RequestTarget` | `string` | The request line's target as sent (origin form, absolute form or `RequestTarget`); Digest's `uri=` field is this. |
| `Credential` | `NetworkCredential?` | For the origin, `ITransferContext.Credentials`; for a proxy, `ProxyEndpoint.Credential`. `null` when none. |
| `BearerToken` | `string?` | `HttpRequestOptions.BearerToken` for the origin; always `null` for a proxy. |
| `AllowedSchemes` | `HttpAuthSchemes` | For the origin, `HttpRequestOptions.AuthSchemes`; for a proxy, `Basic`, curl's default, until a later ADR adds `--proxy-digest` and its siblings. |
| `IsProxy` | `bool` | `true` when answering a proxy: the challenges are `Proxy-Authenticate` values and the result is sent as `Proxy-Authorization`. `false` for the origin: `WWW-Authenticate` and `Authorization`. |

`CreateAuthorization` semantics:

- `challenges` holds the values of every `WWW-Authenticate` header (or
  `Proxy-Authenticate` when `IsProxy`) from the response being answered, verbatim and in
  the order received. It is empty before the first response, which is how Basic and
  Bearer are sent pre-emptively.
- The return value is the header value only, for example `Basic dTpw` (measured for
  `-u u:p` in BL-216), without the header name or line ending. The handler writes it as
  `Authorization` or `Proxy-Authorization`.
- `null` means send no `Authorization` (or `Proxy-Authorization`) header: no credential
  or token, no allowed scheme, or no challenge the authenticator can answer.
- It is synchronous and does no I/O. It keeps no state between calls, so NTLM and
  Negotiate, which need state across a connection's handshake legs, will need a later
  ADR before they are implemented.

### `ICookieStore`

```csharp
public interface ICookieStore
{
    string? GetCookieHeader(Uri uri, bool secure, DateTimeOffset now);

    void StoreFromResponse(Uri uri, IReadOnlyList<string> setCookieHeaders, DateTimeOffset now);
}
```

- `GetCookieHeader` returns the value of the `Cookie` header to send to `uri`, without
  the header name, or `null` when no stored cookie matches. `secure` is `true` when the
  request travels over TLS, so the store can withhold `Secure` cookies otherwise; the
  caller decides it rather than the store inferring it from the scheme. `now` decides
  expiry.
- `StoreFromResponse` stores the cookies from a response to a request for `uri`.
  `setCookieHeaders` holds the value of each `Set-Cookie` header, verbatim and in the
  order received. `now` is the receive time that relative expiry (`Max-Age`) counts
  from.
- The store never reads a clock. The caller passes `now` from
  `ITransferContext.TimeProvider.GetUtcNow()`, so expiry is testable with a fake time
  provider.
- Loading `-b` and writing `-c` are not on this contract; `Curl.Console` does both
  around the transfers (BL-237).

### Where the implementations live

- `Curl.Authentication.UnitLibrary` will implement `IHttpAuthenticator` (BL-216 to
  BL-218), and `Curl.Cookies.UnitLibrary` will implement `ICookieStore` (BL-220).
- `Curl.Console` will compose them and register them with dependency injection for the
  HTTP handler (BL-237). BL-173 plans the handler's constructor as
  `HttpProtocolHandler(IConnector, IHttpAuthenticator, ICookieStore?)`.
- The multipart body builder will live in `Curl.Core.UnitLibrary` and produce a
  `StreamBody` (BL-205). curl's manual gives `-F` a meaning for SMTP and IMAP as well as
  HTTP: it composes a multipart mail message
  (<https://curl.se/docs/manpage.html#-F>, as referenced by the Phase 1 HTTP plan for
  curl 8.21.0). Those handlers will need the same MIME, and a protocol library may not
  reference another, so the builder cannot live in `Curl.Protocol.Http.UnitLibrary`.

## Consequences

Good:

- The HTTP handler reaches authentication, cookies and multipart bodies through types
  it can see, and `ProtocolIsolationTests` keeps passing with no new project reference.
- Every HTTP option is typed and nullable-checked, one place to look, and a handler
  test builds a context with `Http = new HttpRequestOptions { ... }`.
- Non-HTTP handlers, and every existing `new ConnectTarget(...)` call site, compile
  unchanged.
- Authenticators and cookie stores are unit-tested on strings and a passed-in time,
  with no connection and no clock.
- The implementing tasks can proceed in parallel once this is Accepted: BL-159 adds
  `HttpRequestOptions`, `HttpRequestBody` and `ITransferContext.Http`; BL-161 adds
  `IHttpAuthenticator`, `HttpAuthRequest`, `HttpAuthSchemes` and `ICookieStore`; BL-162
  adds `ProxyEndpoint`, `ProxyKind` and `ConnectTarget.Proxy`; and BL-164 records the
  connection-reuse ADR, whose pool key includes the proxy this ADR defines.

Costs and caveats:

- `Curl.Protocol.Abstractions.UnitLibrary` now holds HTTP-specific types that no other
  protocol reads.
- `ForwardProxy` sits under `Http`, so only the HTTP handler can use a proxy. FTP or
  another scheme through a proxy will need a later ADR.
- `-d` reaches two members: `PostData` for MQTT and `Http.Body` for HTTP.
  `Curl.Console` must fill both from the same data.
- `IHttpAuthenticator` is stateless and synchronous; multi-leg schemes (NTLM,
  Negotiate) will need it extended or superseded.
- Proxy authentication is limited to Basic until a later ADR adds the `--proxy-*`
  scheme options.

## Alternatives considered

- **Put the types in `Curl.Protocol.Http.UnitLibrary` and have it reference
  `Curl.Authentication` and `Curl.Cookies`.** Rejected: a protocol library may reference
  only `Curl.Protocol.Abstractions.UnitLibrary`, and `ProtocolIsolationTests` fails the
  fast test suite otherwise. Reversing that rule for one protocol would undo the
  isolation every protocol test relies on.
- **Put the contracts in `Curl.Authentication` and `Curl.Cookies` and have Http
  reference them.** Rejected for the same reason.
- **A dictionary of options** (`IReadOnlyDictionary<string, object>` or of strings).
  Rejected, as ADR-0003 and ADR-0006 rejected it: it defeats nullable checking and turns
  a misspelt key or a wrong type into a run-time failure.
- **Flatten the HTTP options onto `ITransferContext`.** Rejected: sixteen more members
  that every other handler must ignore, each one a change to a shared interface. A
  single nested `Http` adds one member now, and later HTTP options change only
  `HttpRequestOptions`.
- **Put `-T` into `Body` as a `StreamBody`.** Rejected: `Upload` already carries it for
  every scheme (ADR-0003), and a `StreamBody` from `-T` would be indistinguishable from
  one from `-F`, yet curl sends the first as PUT and the second as POST.
- **Build multipart bodies in `Curl.Protocol.Http.UnitLibrary`.** Rejected: SMTP and
  IMAP `-F` need the same MIME, and they cannot reference the HTTP library.
- **Let the authenticator write the header itself, or return a name and value.**
  Rejected: the handler already knows which header, from `IsProxy`, and owns header
  ordering (`Authorization` is sent before `User-Agent`, measured in BL-168). A plain
  value keeps the authenticator free of request-writing concerns.
- **Let the cookie store read a `TimeProvider` of its own.** Rejected: the transfer
  already carries one, and passing `now` keeps the store a pure function of its inputs.
