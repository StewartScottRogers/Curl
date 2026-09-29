# ADR-0181 — HTTP NTLM answers in three legs, each from a fresh context, through a continuation on `IHttpAuthenticator`

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-526.

## Context

ADR-0142 decides which implementation makes NTLM's tokens on each platform: SSPI through the
BCL's `NegotiateAuthentication` on Windows, curl's own NTLM (`Curl.Ntlm.UnitLibrary`)
elsewhere. What it left open is how HTTP carries a handshake of three legs.
`IHttpAuthenticator` said "neither call keeps state between calls" and "a handshake of more
than one leg (NTLM) needs a later ADR", and the HTTP handler retried a 401 only when the
request that drew it had sent no `Authorization`, so a Type 1 message sent up front could
never be followed by Type 3.

### Measured on 2026-09-28

With `Record-CurlExchange.ps1 -Script` (one connection, steps `read`/`send`), curl 8.21.0
(mingw, Schannel, SSPI) on Windows 11 and curl 8.18.0 (OpenSSL, curl's own NTLM) on Ubuntu
through WSL, `-u u:p`, the Type 2 message being MS-NLMP 4.2.4.3's CHALLENGE:

| Server's answers | Both platforms |
| --- | --- |
| `--ntlm`: 401 Type 2, then 200 | Type 1 on the first request, Type 3 on the second, same connection, body of the 200, exit 0 |
| `--anyauth`: 401 `Basic` + `NTLM`, 401 Type 2, 200 | No `Authorization`, then Type 1, then Type 3, same connection, exit 0 |
| `--ntlm`: 401 bare `NTLM`, 401 bare `NTLM` | Type 1, Type 1 again, then stops ("NTLM handshake failure"), 401 body, exit 0 |
| `--ntlm`: 401 Type 2, 401 bare `NTLM` | Type 1, Type 3, then stops ("NTLM handshake rejected"), 401 body, exit 0 |
| `--ntlm`: 401 `NTLM @@@notbase64` | Type 1, then stops, 401 body, exit 0 |

| Server's answer | Windows (SSPI) | Linux (curl's own NTLM) |
| --- | --- | --- |
| `--ntlm`: 401 `NTLM TlRMTVNTUAACAAAA` (a Type 2 cut to 12 bytes) | `curl: (94) An authentication function returned an error`, no body | Stops ("bad type-2 message"), 401 body, exit 0 |

Linux's Type 3 carried the client challenge `1A7D34CAD3333A85` and time
`0x01DD4FDB71672480`; `Curl.Ntlm`'s `NtlmChallengeAnswerer` given those two reproduces the
whole message byte for byte. SSPI's Type 3 carries the workstation's own name, a MIC and a
fresh nonce, so only its header, flags (`0xA2888205`) and user are fixed. A C# probe showed
that a fresh `NegotiateAuthentication` for `NTLM` with the same credential writes the same
Type 1 every time and answers the Type 2 with `Completed`.

## Decision

### The contract

`IHttpAuthenticator` gains `ContinueAuthorizationAsync(request, sentAuthorization,
sentBeforeAnyChallenge, challenges, cancellationToken)`, a default member answering
`null`. The HTTP handler calls it for a 401 with challenges to a request that sent an
`Authorization` value, and `CreateAuthorizationAsync` as before when it sent none. Every
authenticator but NTLM keeps the default, so a credential sent and refused still ends the
transfer on the 401, as curl does for Basic, Digest and Negotiate; the RTSP and WebSocket
handlers are untouched. The handler tells it the value the request sent and whether that
value was made before any challenge (the transfer's first request) or answered one; that is
all of curl's per-transfer NTLM state (`NTLMSTATE_NONE`, `TYPE1`, `TYPE2`, `TYPE3`) that
the next answer depends on.

An authenticator that must fail the transfer, not take the 401, throws
`HttpAuthenticationFailedException` (in `Curl.Protocol.Abstractions`) with curl's exit code
and message; the handler ends the transfer with them and writes none of the body.

### No context is kept between legs

`NtlmHttpAuthenticator` makes Type 3 from a fresh context stepped through its Type 1 and
then the server's Type 2. Both routes write the same Type 1 for the same credential every
time, so SSPI's MIC over the three messages still covers the one that was sent, and the
authenticator stays stateless: no context to hold per transfer, per connection or per
parallel transfer (`-Z`), and nothing to dispose when a transfer ends between legs.

### The legs

`RankedHttpAuthenticator` hands NTLM to `NtlmHttpAuthenticator` for the origin (proxy NTLM
is BL-604's) when `-u` was given (even `-u :`, which means SSPI's logged-on user on Windows
and an empty user off it): before any challenge when NTLM is the one scheme allowed
(`--ntlm`), and after one when NTLM is the pick. It answers:

| Sent | Challenge | Answer |
| --- | --- | --- |
| Type 3 | anything | nothing |
| nothing | bare `NTLM`, or none yet | Type 1 |
| Type 1 made before any challenge | bare `NTLM` | Type 1 again |
| Type 1 made for a challenge | bare `NTLM` | nothing |
| anything else | `NTLM <Type 2>` | Type 3 |
| anything | `NTLM <not base64>` | nothing |

Where the context cannot answer the Type 2, the answer is nothing where curl's own NTLM is
matched and exit 94 with curl's message where the SSPI build is; `NtlmHttpAuthenticator`
takes that as a constructor flag, and `Curl.Console` passes `OperatingSystem.IsWindows()`,
as ADR-0139 injects `answerDigestMd5AsSspi`.

`HandBuiltSecurityContextFactory` now makes `HandBuiltNtlmSecurityContext` for NTLM (it
answered `NoMechanism` before), over `NtlmChallengeAnswerer` with the injected
`TimeProvider` and an `INtlmRandomSource`. `curl -V` lists `NTLM` on every platform
(ADR-0021).

## Consequences

- ADR-0028's "Curl sends nothing until NTLM is built" and ADR-0142's matching consequence
  end for HTTP: NTLM is answered wherever ADR-0028's ranking picks it for the origin.
- A server that keeps sending Type 2 after Type 3 gets nothing more, where curl's state
  machine would answer again; curl's loop there is unbounded, this one ends on the 401.
- A Type 3 that would pass curl's 1024-byte `NTLM_BUFSIZE` (a user or domain of hundreds
  of characters) ends on the 401 off Windows, where curl fails with `CURLE_TOO_LARGE`.
- Proxy NTLM (BL-604) and SASL `NTLM` (BL-538) reuse `NtlmHttpAuthenticator`'s approach
  or the same contexts; `Proxy-Authenticate` needs the same continuation for proxies.
- The `-v` lines curl writes for NTLM (`Server auth using NTLM with user 'u'`, `NTLM
  handshake rejected`) are not written.

## Alternatives considered

- **Keep the context between legs** in the authenticator, keyed by transfer. Needs a key the
  contract does not have, breaks under parallel transfers, and leaks a context when a
  transfer ends mid-handshake; the fresh context gives the same bytes.
- **Let the handler retry any refused credential and have every authenticator decide.**
  Changes Basic, Digest and Negotiate behaviour through every existing fake and caller;
  a default member that answers nothing changes none of them.
- **Teach the handler NTLM** (retry when the value sent starts `NTLM `). Puts a scheme in
  the protocol handler, which ADR-0014 keeps behind `IHttpAuthenticator`.
