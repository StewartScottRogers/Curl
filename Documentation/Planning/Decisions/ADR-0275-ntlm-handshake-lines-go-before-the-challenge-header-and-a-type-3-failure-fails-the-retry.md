# ADR-0275 — NTLM's handshake lines go before the challenge header, and a Type 3 failure fails the retry

- **Status:** Accepted
- **Date:** 2026-09-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-848.

## Context

ADR-0181 made `--ntlm` answer in three legs but wrote no `-v` line for a handshake that goes
wrong, and failed the transfer with exit 94 (SSPI) or 100 (curl's own NTLM, BL-849) as soon
as the 401 carrying the Type 2 message was read.

`Record-CurlExchange.ps1 -Script` was run on 2026-09-30 with `--ntlm -u u:p -v` against
curl 8.21.0 (Windows, Schannel, SSPI) and curl 8.18.0 (Ubuntu, OpenSSL, its own NTLM):

| Responses | Lines (both builds unless named) | Where |
| --- | --- | --- |
| 401 Type 2, 401 bare `NTLM` | `NTLM handshake rejected`, `NTLM authentication problem, ignoring.` | between the second 401's status line and its `WWW-Authenticate` |
| 401 bare `NTLM` twice | `NTLM handshake failure (internal error)`, `NTLM authentication problem, ignoring.` | the same, on the second 401 |
| 401 `NTLM @@@notbase64` | `NTLM authentication problem, ignoring.` | the same |
| 401 `NTLM TlRMTVNTUAACAAAA`, Ubuntu | `NTLM handshake failure (bad type-2 message)`, `NTLM authentication problem, ignoring.`; exit 0 | the same |
| 401 `NTLM TlRMTVNTUAACAAAA`, Windows | `NTLM handshake failure (type-3 message): Status=0x80090308` and an empty line; exit 94 | after the 401's body is ignored, `Issue another request` and `Reusing existing http: connection`; no request is sent |

The `Server auth using ...` lines for every scheme were measured and written by BL-954.

## Decision

1. `NtlmHttpAuthenticator` reports these lines to `HttpAuthRequest.Events`, the seam
   Negotiate's failure lines use (ADR-0231); `NtlmHandshakeLines` holds the texts.
2. The HTTP handler defers a final head's headers from the first `WWW-Authenticate` of a
   401 offering NTLM when NTLM is allowed (`HttpNtlmInfoLines`), as it does for Negotiate,
   so the lines land before that header. The same applies to a 407's `Proxy-Authenticate`
   for the proxy's request: not measured, but curl reads a proxy's NTLM challenge in the
   same `Curl_input_ntlm` call.
3. When the authenticator refuses to answer a challenge and Negotiate was not picked
   (NTLM's exit 94 and exit 100), the handler no longer fails at the 401: it makes the retry
   (`HttpRequestPlan.WithAuthorizationFailure`), which reads and ignores the 401's body,
   takes the connection again, writes the recorded lines and fails before it sends, as curl
   fails making Type 3 on the way out. A Negotiate refusal still fails at the 401, as before.
4. SSPI's Type 3 line names `SEC_E_INVALID_TOKEN` (measured) for a token SSPI cannot read;
   the other statuses use the SSPI codes `NegotiateFailureLines` already maps them to.

## Consequences

- `-v` output for these cases matches the measured bytes; without `-v` nothing changes,
  since the lines go only through `ITransferEvents`.
- A 401 carrying an unreadable Type 2 on a connection it closes now opens a new connection
  before failing, as curl's retry would.
