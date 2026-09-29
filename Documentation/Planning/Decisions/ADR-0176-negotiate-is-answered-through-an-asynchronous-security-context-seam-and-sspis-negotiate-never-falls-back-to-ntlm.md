# ADR-0176 — Negotiate is answered through an asynchronous security-context seam, and SSPI's Negotiate never falls back to NTLM

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-527.

## Context

ADR-0142 decided which implementation answers NTLM, Negotiate and Kerberos on each
platform and named the seam, `ISecurityContextFactory` and `ISecurityContext`, with a
synchronous `NextToken`. BL-527 builds the seam and answers HTTP `--negotiate` (and
`--anyauth` when Negotiate ranks first) through it. Three things came up that ADR-0142 did
not settle.

### Measured on 2026-09-28

With `Record-CurlExchange.ps1`, a loopback server answering every request with
`401 Unauthorized`, `WWW-Authenticate: Negotiate` and the body `deny`, on a Windows 11
machine that is in no domain, curl 8.21.0 (mingw, Schannel, SSPI) and, through WSL, curl
8.18.0 (OpenSSL, MIT krb5 1.22.1) with no ticket:

| Command | Windows | Linux |
| --- | --- | --- |
| `--negotiate -u : -v` | One `GET` with no `Authorization`, the body `deny`, exit 0. `-v` prints `InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package` and `Server auth using Negotiate with user ''` before the request, and the first line again after the `401` | The same request bytes, body and exit code; the lines are `gss_init_sec_context() failed: No credentials were supplied, or the credentials were unavailable or inaccessible. SPNEGO cannot find mechanisms to negotiate.` and `Server auth using Negotiate with user ''` |
| `--negotiate -v` (no `-u`) | The same: one request, both lines, exit 0 | (not recorded) |
| `--negotiate -u u:p -v` | The same, `with user 'u'`, `SEC_E_NO_CREDENTIALS` | (not recorded) |
| `--negotiate -u : http://localhost:...` | The same, `SEC_E_NO_CREDENTIALS` | (not recorded) |
| `--anyauth -u : -v` | Two requests, neither with `Authorization`: the first a probe, the second after `Issue another request`, then exit 0 | (not recorded) |

The BCL's `NegotiateAuthentication` on the same Windows machine, same user, package
`Negotiate`, target `HTTP/127.0.0.1`, answers `ContinueNeeded` with a bare NTLM Type 1
(`TlRMTVNTUAABAAAAl7II4g...`) for the default credentials, for an explicit empty
credential, and at every `RequiredProtectionLevel`; for `HTTP/localhost` it answers a
SPNEGO NegTokenInit offering NEGOEX, MS-KRB5, KRB5 and NTLM with that NTLM Type 1 as its
optimistic token. So where curl's SSPI Negotiate finds no Kerberos credential and stops,
the BCL's falls back to NTLM.

## Decision

### The step is asynchronous

`ISecurityContext.NextTokenAsync(ReadOnlyMemory<byte>, CancellationToken)` returns a
`ValueTask<SecurityContextStep>` (a `SecurityContextStatus` and the token), not ADR-0142's
synchronous `NextToken`, because the hand-built route's first step asks a KDC for a
service ticket and the solution is async all the way. A failure is a status, never an
exception, except cancellation. `ISecurityContextFactory.Create` does no I/O; SSPI's
credential is acquired in the first step, where SSPI can refuse it. `SecurityContextRequest`
carries the mechanism, the service name and the host separately (each implementation forms
its own target: `HTTP/host` for SSPI and GSS-API, `HTTP/host@REALM` for the hand-built
Kerberos), the credential, and the `--delegation` level, which BL-631 fills in with
`--service-name`. `Wrap` and `Unwrap` are left to the first callers that need them, SASL
`GSSAPI` (BL-538) and SOCKS5 GSS-API (BL-615).

`IHttpAuthenticator` gains `CreateAuthorizationAsync`, a default interface member that
returns `CreateAuthorization`'s answer, and the HTTP handler calls it for the first request
and for the answer to a 401. Every existing authenticator and fake is unchanged;
`RankedHttpAuthenticator` overrides it to answer Negotiate, and its synchronous
`CreateAuthorization` still sends nothing for a Negotiate pick. The WebSocket handler keeps
the synchronous call, so `ws://` sends no Negotiate yet.

### When Negotiate is tried

As libcurl does: with `--negotiate` the one scheme allowed, it is tried before the first
request, whether or not `-u` was given; after a 401 it answers only when Negotiate is the
pick and `-u` was given, even as `-u :`. A context that makes no token sends no header, so
the transfer ends on the 401 with exit 0, as measured. Proxy Negotiate is BL-604's.

### SSPI's Negotiate never falls back to NTLM

On Windows, a first Negotiate token that carries NTLM (a bare NTLMSSP message, or a
NegTokenInit whose `mechToken` is one) is answered as `NoCredentials` and nothing is sent,
because curl 8.21.0 measured on the same SSPI gets `SEC_E_NO_CREDENTIALS` there, with
`-u :`, with `-u u:p`, and with no `-u`. A Kerberos token passes through untouched, so on a
domain the logged-on user's ticket is sent as curl sends it. `--ntlm` is not affected.

### The production adapters

`Curl.Networking.UnitLibrary` provides `KerberosKdcSocketTransport` (a UDP datagram through
`IDatagramConnector`, answered within one second, MIT's first per-KDC wait, and a direct TCP
connection through `IConnector`, whose stream disposes the connection) and
`KerberosDnsSrvLookup` (SRV records through `DnsServerResolver.ResolveServiceAsync`, asking
the system's DNS servers). Networking references `Curl.Kerberos.UnitLibrary` for their
interfaces. `Curl.Console` composes the router with `OperatingSystem.IsWindows()`, reads
`krb5.conf` and the credential cache from disk only when a hand-built context first asks,
and takes the `<uid>` of `FILE:/tmp/krb5cc_<uid>` from `/proc/self/status`, since the BCL
has no `getuid`; off Linux that file is absent and the id is 0, which no route reaches.

### `-V`

`Features:` lists `GSS-API`, `Kerberos` and `SPNEGO` on every platform, since Negotiate is
answered everywhere (ADR-0021's rule that the list names what Curl does).

## Consequences

- `--negotiate -u :` behaves as both platform curls where no ticket exists, and sends the
  ticket where one does: through SSPI on Windows, the system GSS-API elsewhere, and the
  hand-built SPNEGO and Kerberos where no system library answers.
- The `-v` lines curl prints for a failed context and `Server auth using Negotiate with
  user '...'` are not written yet (BL-843).
- Under `--anyauth` with no ticket curl sends a second request without `Authorization`;
  Curl sends one (BL-844).
- A 401 that carries a Negotiate continuation token, a multi-leg exchange, is not answered
  (BL-842); nor is Negotiate over `ws://`.
- ADR-0142's table is refined on one cell: Windows Negotiate uses SSPI but never its NTLM
  fallback.

## Alternatives considered

- **Keep `NextToken` synchronous and block on the KDC exchange.** Rejected: `.Result` and
  `.Wait()` are forbidden, and a synchronous KDC exchange would hold a thread for seconds.
- **Make `IHttpAuthenticator.CreateAuthorization` itself asynchronous.** Every
  authenticator and test fake in the HTTP and WebSocket libraries would change for one
  scheme that needs I/O; the default interface member changes none of them.
- **SSPI's `Kerberos` package wrapped in hand-built SPNEGO on Windows.** It would also never
  fall back to NTLM, but on a domain it would send a mechanism list and token of Curl's
  making rather than SSPI's Negotiate token, which is what curl sends there.
- **Let the BCL fall back to NTLM.** Rejected: measured curl does not, and the server would
  see a second request curl never sends.
