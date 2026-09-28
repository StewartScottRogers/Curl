# ADR-0142 — NTLM, Negotiate and Kerberos answer through SSPI on Windows, and through curl's own NTLM or the system GSS-API elsewhere, with hand-built Kerberos where the system has none

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-525.

## Context

ADR-0028 ranks NTLM and Negotiate as libcurl does but answers neither ("Curl sends nothing
until NTLM is built"). HTTP `--ntlm`, `--negotiate` and `--anyauth` (BL-526, BL-527),
proxy `--proxy-ntlm` and `--proxy-negotiate` (BL-604), SOCKS5 GSS-API (BL-615), SASL
`NTLM` and `GSSAPI` (BL-538) and SMB (BL-596) all need tokens from one of three
mechanisms. The standing rule is that each works on every platform, so this ADR decides
how, never whether.

### Measured on 2026-09-28

With `Record-CurlExchange.ps1` against a loopback server answering every request with
`401` and one `WWW-Authenticate` header (two connections, `-m 5`): curl 8.21.0 (mingw,
Schannel, SSPI, ADR-0018) on Windows 11 and, with `-Curl wsl.exe -ListenAddress
<WSL host address>`, curl 8.18.0 (OpenSSL, mit-krb5 1.22.1, `GSS-API` feature) on Ubuntu.

| Case | Windows (SSPI) | Linux (curl's own NTLM, MIT GSS-API) |
| --- | --- | --- |
| `--ntlm -u u:p`, `401 NTLM` | First request already carries `Authorization: NTLM TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw==` (flags `0xA2088207`, with the OS version block `0A00 F465 0000000F`), resent on each new connection; exit 28 at `-m` | Same shape with `TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=` (flags `0x00088206`, no version block); exit 28 at `-m` |
| `--ntlm -u :` | (not recorded; SSPI takes the logged-on user) | The same Type 1 as with a user; exit 28 at `-m` |
| `--negotiate -u :`, `401 Negotiate`, no ticket | One request, no `Authorization`, no retry, exit 0 | One request, no `Authorization`, no retry, exit 0 |
| `--krb clear ftp://...` | `Warning: --krb is deprecated and has no function anymore`, transfer runs | The same warning, transfer runs |
| `smb://...` | `curl: (1) Protocol "smb" is disabled` | Offered |

The two Type 1 messages differ: the Schannel build's is SSPI's, the OpenSSL build's is
curl's own `Curl_auth_create_ntlm_type1_message`.

A C# probe (`System.Net.Security.NegotiateAuthentication`, `GetOutgoingBlob` with an
empty input, target `HTTP/127.0.0.1`), run with `dotnet run` on Windows and published
self-contained for `linux-x64` into the same Ubuntu (which has `libgssapi_krb5.so.2`
and no `gss-ntlmssp`):

| Package | Credential | Windows (SSPI) | Linux (system GSS-API) |
| --- | --- | --- | --- |
| `NTLM` | `NetworkCredential("u","p")` | `ContinueNeeded`, `TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw==`, byte for byte curl's | `Unsupported` |
| `NTLM` | default | `ContinueNeeded`, a Type 1 naming the workstation and domain | `UnknownCredentials` |
| `Negotiate` | explicit | `ContinueNeeded`, raw NTLM (no domain, so SPNEGO chose NTLM) | `Unsupported` |
| `Negotiate` | default | `ContinueNeeded`, raw NTLM with workstation and domain | `UnknownCredentials` |
| `Kerberos` | explicit | throws `Win32Exception` "The logon attempt failed" | `Unsupported` |
| `Kerberos` | default | `GenericFailure` (no domain, no ticket) | `UnknownCredentials` |

So on Windows the BCL class is SSPI, reaches the logged-on user's credentials, which
nothing hand-built can, and writes SSPI's bytes, which is what the platform's curl sends.
Off Windows it is only as good as the system GSS-API library: no NTLM without
`gss-ntlmssp`, no Kerberos without `libgssapi_krb5.so.2`, and even with `gss-ntlmssp` its
NTLM would not be curl's own. Without the library it answers `Unsupported` (the runtime's
unsupported PAL) rather than throwing. A Linux machine without that library was not
available; that behaviour is the runtime's documented fallback, and the routing below
treats `Unsupported` as "no system mechanism" whichever reason caused it.

## Decision

### Which implementation answers each case

`W` is the BCL's `NegotiateAuthentication` over SSPI, `G` the same class over the system
GSS-API library, `N` hand-built NTLM (`Curl.Ntlm.UnitLibrary`), `K` hand-built Kerberos
V5 and its GSS-API mechanism (`Curl.Kerberos.UnitLibrary`), `S` hand-built SPNEGO
(`Curl.Authentication.UnitLibrary`, BL-692). "G, else S+K" means G when it answers
anything but `Unsupported`, and the hand-built route only when it answers `Unsupported`.

**Windows**

| Use | Default credentials (`-u :`, no user) | `-u user:password` |
| --- | --- | --- |
| HTTP and proxy NTLM | W, package `NTLM`, logged-on user | W, package `NTLM`, `NetworkCredential(user, password, domain)` |
| HTTP and proxy Negotiate | W, package `Negotiate`, logged-on user | W, package `Negotiate`, explicit credential |
| SASL `NTLM` | W, `NTLM` | W, `NTLM`, explicit credential |
| SASL `GSSAPI` | W, package `Kerberos`, with `Wrap`/`Unwrap` for RFC 4752's security layer | W, `Kerberos`, explicit credential |
| SOCKS5 GSS-API | W, `Kerberos`, with `Wrap`/`Unwrap` for RFC 1961's protection | W, `Kerberos`, explicit credential |
| SMB | N (curl's `smb.c` computes its own LM and NT responses in every build) | N |

**Linux and macOS**

| Use | Default credentials | `-u user:password` |
| --- | --- | --- |
| HTTP and proxy NTLM | N (curl's own NTLM, as measured; empty user and password as curl sends them) | N |
| HTTP and proxy Negotiate | G, package `Negotiate`, else S+K | Same as default: curl's GSS-API Negotiate ignores `-u`'s password and uses the credential cache |
| SASL `NTLM` | N | N |
| SASL `GSSAPI` | G, package `Kerberos`, else K | Same as default |
| SOCKS5 GSS-API | G, package `Kerberos`, else K | Same as default |
| SMB | N | N |

The hand-built SPNEGO offers the mechanism list BL-692 measures (Kerberos V5, then the
legacy Microsoft OID, as MIT's library offers without `gss-ntlmssp`). `--service-name`,
`--proxy-service-name` and `--socks5-gssapi-service` set the target name
(`TargetName` for W and G, the service principal for K); `--delegation` maps to
`AllowedImpersonationLevel = Delegation` for W and G and to the forwardable-TGT
`KRB-CRED` for K, with `policy` honouring the ticket's ok-as-delegate flag.

On the hand-built Kerberos route (only when no system library answers), the credential
cache is found as MIT does: `KRB5CCNAME`, else `FILE:/tmp/krb5cc_<uid>`. It reads `FILE:`
(BL-688), `DIR:` and `KCM:` (BL-789, filed by this task). `KEYRING:` is written only by
MIT's library and `API:` only by macOS's GSS framework, and where either exists G
answers, so the hand-built route never meets them.

### `--krb` has no function

Both platform curls print `Warning: --krb is deprecated and has no function anymore` and
run the transfer, so `--krb` belongs to ADR-0137's "deprecated with no function" class,
beside `--krb4`. BL-630 parses it that way; BL-693 (FTP `AUTH GSSAPI`) is deferred,
because a drop-in replacement must not authenticate where both platform curls do not.

### The seam

`Curl.Protocol.Abstractions.UnitLibrary` gets the token seam, beside `ISaslExchange` and
`IHttpAuthenticator`, so protocol libraries, `Curl.Networking.UnitLibrary` (SOCKS5) and
`Curl.Authentication.UnitLibrary` can all take it:

- `ISecurityContextFactory.Create(SecurityContextRequest request)` returns an
  `ISecurityContext`. The request holds the mechanism (`Ntlm`, `Negotiate`,
  `Kerberos`), the target name, the user, password and domain (all null for default
  credentials), the delegation level and whether integrity or confidentiality is wanted.
- `ISecurityContext.NextToken(ReadOnlySpan<byte> incomingToken)` returns the outgoing
  token and a status (`ContinueNeeded`, `Completed`, or a failure naming its kind:
  no credentials, no mechanism, refused by the server, malformed token), so each caller
  maps a failure to curl's exit code and text; `IsCompleted`, `Wrap(data, encrypt)` and
  `Unwrap(data)` follow. It is `IDisposable`.

BL-527 adds the contract. The implementations live in `Curl.Authentication.UnitLibrary`:
one adapter over `NegotiateAuthentication` (W and G, one class, because the BCL class is
the same on every platform), one over the hand-built libraries (N, S+K), and a router
choosing between them by the tables above from an injected `isWindows` flag and the
adapter's status, as ADR-0139 injects `answerDigestMd5AsSspi`. `Curl.Console` composes
the router with `OperatingSystem.IsWindows()`. SMB uses `Curl.Ntlm.UnitLibrary` directly,
as ADR-0120 allows, because its responses are not a GSS-API exchange.

Unit tests fake the seam with a scripted class in the test project (a
`ScriptedSecurityContext` holding the tokens to return, as `ScriptedSaslExchange` does),
never a mocking library. The router is tested with fake adapters; the hand-built adapter
with the in-memory fake KDC of BL-690 through its KDC transport seam and
the SRV-lookup seam of BL-689; the `NegotiateAuthentication` adapter with the measured
NTLM Type 1 above under `[OSCondition(OperatingSystems.Windows)]` and `Unsupported`
elsewhere. The KDC transport (UDP then TCP, RFC 4120 section 7.2.1) and the SRV lookup
over the hand-built DNS client (BL-694) are implemented in `Curl.Networking.UnitLibrary`
and composed in `Curl.Console` by BL-527.

### The library split

Confirmed as BL-667 (ADR-0120) lists it: NTLM messages and responses in
`Curl.Ntlm.UnitLibrary`, Kerberos V5, its caches, `krb5.conf`, KDC exchanges and the
GSS-API Kerberos mechanism in `Curl.Kerberos.UnitLibrary`, SPNEGO and the routing in
`Curl.Authentication.UnitLibrary`. ADR-0120 needs no amendment.

## Consequences

- ADR-0028's consequence "Curl sends nothing until NTLM is built" ends when BL-526 and
  BL-527 land on this ADR: NTLM and Negotiate are then answered wherever ADR-0028's
  ranking picks them, and its no-fallback rule stays.
- Windows output is SSPI's, byte for byte, because the same SSPI writes it; Linux and
  macOS NTLM is curl's own, byte for byte, because `Curl.Ntlm.UnitLibrary` reproduces it.
- Negotiate and Kerberos off Windows behave as the platform's curl wherever the system
  library curl links is installed, and still work from a `FILE:`, `DIR:` or `KCM:` cache
  where it is not, where the platform's curl would not even start.
- Every piece of the hand-built Kerberos stack is still built (BL-685 to BL-691), because
  it is the only route on a machine without a system GSS-API library.

## Alternatives considered

- **Hand-built everywhere.** One code path, but on Windows it cannot reach the logged-on
  user's tickets or NTLM credentials (they live in LSA, reachable only through SSPI), so
  `--negotiate -u :` would fail where curl succeeds, and its NTLM Type 1 would not be
  SSPI's.
- **`NegotiateAuthentication` everywhere.** No hand-built code, but off Windows NTLM is
  `Unsupported` without `gss-ntlmssp` and not curl's bytes with it, and nothing works on
  a machine without `libgssapi_krb5.so.2`.
- **Hand-built first off Windows, system GSS-API as the fallback.** Loses the cache types
  only the system library reads (`KEYRING:`, macOS `API:`) whenever the hand-built
  route finds nothing, and would differ from the platform's curl wherever the two
  libraries disagree; the system library is the one curl links, so it goes first.
- **Build FTP `--krb` (`AUTH GSSAPI`).** Rejected: both platform curls have dropped it and
  say so; building it would make Curl authenticate where curl only warns.
