# ADR-0203 — A SASL security context that cannot make its first token fails with exit 94

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-856.

## Context

ADR-0184 runs SASL GSSAPI and NTLM on one `ISecurityContext` per exchange. When the
context could not make its first token - no credentials, no KDC -
`SecurityContextSaslExchange.GetInitialResponseAsync` answered `null`, which the SMTP,
IMAP and POP3 handlers read as "this mechanism has no initial response" and then as a
refused exchange: exit 67.

curl 8.21.0 (Schannel), measured on 2026-09-29 with
`Record-CurlExchange.ps1 -Smtp -SmtpReply 'EHLO=250-localhost\r\n250 AUTH GSSAPI','AUTH=334 '`
and `-u 'DOMAIN\u:p'` (and `-u :`), no KDC:

- without `--sasl-ir` it sends `AUTH GSSAPI`, reads `334 `, closes, and exits 94
  `An authentication function returned an error`;
- with `--sasl-ir` it sends no `AUTH` at all, closes, and exits 94.

This is `Curl_sasl_start` in `lib/sasl.c`: the initial response is built before the
command only when `force_ir || data->set.sasl_ir` (IMAP passes its `SASL-IR` capability
as `force_ir`; SMTP and POP3 pass false), and otherwise at the first continuation. A
failure to build it returns `CURLE_AUTH_ERROR`, and nothing more is sent.

## Decision

- **The exchange throws.** `SecurityContextSaslExchange.GetInitialResponseAsync` throws
  `SaslAuthenticationFailedException(CurlExitCode.AuthError, "An authentication function
  returned an error")` when the first step fails. The exception already existed for
  BL-781's DIGEST-MD5 and every mail session already ends the transfer with its exit
  code and message, sending nothing more, so no new contract was needed. Later steps
  that fail still answer `null` (unchanged, not measured).
- **The handlers ask when curl builds it.** `ISaslExchange.GetInitialResponseAsync` is
  asked before the command only under `--sasl-ir` (and, for IMAP, when the server
  advertised `SASL-IR`); otherwise at the first continuation. For mechanisms whose
  initial response cannot fail (PLAIN, LOGIN, EXTERNAL, the bearer ones) nothing on the
  wire changes; only the moment it is computed moves.
- **POP3 `+OK` before the initial response was made is `Login denied`,** as it already
  was when an unsent one was pending. curl's state machine denies any final code before
  its final state, so this now holds for a mechanism with no initial response too.

## Consequences

- `ISaslExchange`'s documentation states when the handler asks and that
  `GetInitialResponseAsync` may throw `SaslAuthenticationFailedException`.
- The SMTP, IMAP and POP3 tests pin both measured wire shapes; the authentication tests
  pin exit 94 for GSSAPI and NTLM.
