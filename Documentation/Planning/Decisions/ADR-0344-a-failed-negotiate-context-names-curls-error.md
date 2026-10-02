# ADR-0344 — A failed Negotiate context's line is the error of a failed HTTP transfer

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-981.

## Context

curl keeps the first `failf` message of a transfer in its error buffer, and the tool prints
that buffer, not the exit code's generic text, as `curl: (N) ...`. A Negotiate context that
fails while curl makes the first request's `Authorization` value calls `failf`, so
`curl --negotiate -u : -sSf` against a `401 Negotiate` with no ticket ends
`curl: (22) InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package`
on curl 8.21.0 Schannel (measured, BL-955 Notes), not
`The requested URL returned error: 401`. Curl's WebSocket handler already did this for
`ws://` (`WsProtocolHandler.WithFirstAuthFailure`, BL-955); the HTTP handler did not.

## Decision

`HttpProtocolHandler.ExecuteAsync` gives a transfer that ends with any exit other than 0 the
first line the authenticator reported while making the first request's `Authorization`
value (ADR-0231's `HttpInfoLineRecorder`) as its `ErrorMessage`. Only Negotiate reports a
line there, so the rule names no scheme. It applies to every failure after it, exit 22 and
exit 52 alike, because curl's buffer keeps the first message whatever fails later. The
wording is the authenticator's per platform (SSPI on Windows, GSS-API elsewhere).

## Consequences

- `HttpProtocolHandlerTests.NegotiateFail.cs` pins the message for `-f` on each platform and
  for an empty reply.
- A context failure reported later, while answering a 401 (`--anyauth`), does not yet name
  the error; curl's `failf` there would. Measure it before changing it.

## Alternatives considered

- Only for exit 22: curl's buffer does not care which failure follows, and the WebSocket
  handler already applies it to every failure.
- Throw the context failure as the error at once: curl sends the request anyway, as
  the `-v` lines measured in BL-843 show, so the transfer must go on.
