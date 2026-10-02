# ADR-0348 — A refused CONNECT tunnel fails with the first SSPI Negotiate failure

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1033.

## Context

ADR-0270 left a gap: a CONNECT tunnel refused with `407` after a `--proxy-negotiate` context
failed ended `curl: (7) CONNECT tunnel failed, response 407`, and the context's failure line
never reached `-v`, because `TcpConnector` asked the proxy authenticator with no events.
Measured with `Record-CurlExchange.ps1 -Script` (BL-1033 Notes),
`curl -s -S -v -p -x http://<proxy> --proxy-negotiate -U : http://example.test/` against a
`407` with a bare `Proxy-Authenticate: Negotiate` exits 7 on both builds, but:

- curl 8.21.0 (mingw, SSPI) ends `curl: (7) InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package`:
  the SSPI code writes the line with `failf`, and libcurl's error buffer keeps a transfer's first `failf`.
- curl 8.18.0 (Linux, OpenSSL, MIT GSS-API) ends `curl: (7) CONNECT tunnel failed, response 407`:
  `Curl_gss_log_error` writes `gss_init_sec_context() failed: ...` with `infof`, which leaves
  the error buffer alone.

Both write the failure line under `-v` before `Proxy auth using Negotiate with user ''` and
again after the `407`'s `Proxy-Authenticate` header.

## Decision

1. `TcpConnector` hands the proxy authenticator a `SspiFailureRecordingTransferEvents` around
   the target's events for a CONNECT tunnel, so the authenticator's `-v` lines reach the
   transfer's output, and the first `InitializeSecurityContext failed: ...` line is kept.
2. A tunnel the proxy refuses fails with that line as its message when there is one, keeping
   its exit code (7 for a refusal, 56 for a reply it could not read); otherwise with its own
   message as before.
3. Only the SSPI wording counts, since only curl's SSPI build writes it with `failf`; which
   wording the authenticator uses is `NegotiateHttpAuthenticator`'s platform choice
   (ADR-0231), so Windows gets the failure line and other platforms keep the `407` message.
4. The CONNECT-UDP tunnel keeps asking with no events; it has no measured Negotiate case.

## Consequences

- `TcpConnectorTests` pins the Windows and the non-Windows message, each under `OSCondition`.
- The authenticator's lines land after the reply's whole head under `-v`, not between its
  header lines as curl writes the second one; the error message and exit code match.

## Alternatives considered

- A failure-reporting member on `ITransferEvents` or `HttpAuthRequest`: a change to the shared
  contract for one line curl already words, and it would make every protocol task wait.
- Keeping the failure line in the authenticator and throwing `HttpAuthenticationFailedException`:
  the transfer would end before the CONNECT is sent, which curl does not do.
