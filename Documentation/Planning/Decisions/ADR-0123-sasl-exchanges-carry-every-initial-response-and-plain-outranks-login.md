# ADR-0123 — SASL exchanges carry every initial response, and PLAIN outranks LOGIN

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-536.

## Context

BL-536 builds PLAIN, LOGIN, EXTERNAL, XOAUTH2 and OAUTHBEARER behind ADR-0121's
`ISaslAuthenticator` in `Curl.Authentication.UnitLibrary`. Measuring curl 8.21.0 (mingw,
Schannel) with `Record-CurlExchange.ps1 -Smtp` on 2026-09-28 found four points where
ADR-0121 and the contract's XML documentation do not match curl:

| Command line (`-u u:p` unless stated) | EHLO `AUTH` line | curl sent |
| --- | --- | --- |
| `--sasl-ir` | `LOGIN PLAIN` | `AUTH PLAIN AHUAcA==` |
| `--sasl-ir --login-options AUTH=LOGIN` | `EXTERNAL OAUTHBEARER XOAUTH2 LOGIN PLAIN` | `AUTH LOGIN dQ==`, then `cA==` after `334 UGFzc3dvcmQ6` |
| `--login-options AUTH=login` | `LOGIN PLAIN` | `AUTH LOGIN`, `dQ==` after `334 VXNlcm5hbWU6`, `cA==` after `334 UGFzc3dvcmQ6` |
| `--sasl-ir --sasl-authzid z` | `EXTERNAL LOGIN PLAIN` | `AUTH PLAIN egB1AHA=` |
| `--sasl-ir --sasl-authzid z` | `EXTERNAL LOGIN` | `AUTH LOGIN dQ==` (the identity is not sent) |
| `--sasl-ir --oauth2-bearer tok` | `PLAIN LOGIN` | no `AUTH`; exit 67 `Login denied` |
| `--sasl-ir --oauth2-bearer tok -u u:` | `EXTERNAL OAUTHBEARER XOAUTH2 LOGIN PLAIN` | `AUTH OAUTHBEARER` + base64 of `n,a=u,^Ahost=127.0.0.1^Aport=18125^Aauth=Bearer tok^A^A` |
| `--sasl-ir --oauth2-bearer tok` (no `-u`) | same | `n,a=,^Ahost=...` (empty user) |
| `--sasl-ir --login-options AUTH=XOAUTH2 --oauth2-bearer tok -u u:` | same | `AUTH XOAUTH2 dXNlcj11AWF1dGg9QmVhcmVyIHRvawEB` |
| `--sasl-ir --login-options AUTH=EXTERNAL -u u:` (with or without `--sasl-authzid z`) | same | `AUTH EXTERNAL dQ==` |
| `--login-options AUTH=EXTERNAL` | `EXTERNAL PLAIN` | no `AUTH`; exit 67 |
| `--login-options AUTH=BOGUS` | `LOGIN PLAIN` | exit 3 before connecting |
| `--sasl-ir`, server answers `334 abc` after PLAIN / XOAUTH2 `334 <error>` | | no reply; exit 67 |
| none | `plain` | `AUTH PLAIN`, `AHUAcA==` after `334 ` |

(`^A` is byte `0x01`.) Also observed, for the SMTP handler: when the `AUTH` line is the
**last** line of the EHLO reply (`250 AUTH ...`), curl ignores its last mechanism, so
`250 AUTH LOGIN PLAIN` yields `AUTH LOGIN` and `250 AUTH PLAIN` alone yields exit 67.
ADR-0121's measurements used a middle `250-AUTH` line and are unaffected.

## Decision

1. **PLAIN comes before LOGIN.** The order is EXTERNAL, GSSAPI, DIGEST-MD5, CRAM-MD5, NTLM,
   OAUTHBEARER, XOAUTH2, PLAIN, LOGIN. This corrects ADR-0121 §2's list, whose own
   measurement table already showed PLAIN chosen while LOGIN was offered.
2. **An authorization identity does not skip LOGIN.** PLAIN wins because it ranks first;
   with only LOGIN usable, LOGIN is chosen and the identity is not sent.
3. **A bearer token excludes PLAIN and LOGIN.** With `--oauth2-bearer`, only OAUTHBEARER and
   XOAUTH2 are usable among the built mechanisms. (DIGEST-MD5, CRAM-MD5 and NTLM with a
   token are for BL-537 and BL-538 to measure.)
4. **Every built mechanism has an initial response, LOGIN's being the user name.** The
   handler sends `InitialResponse` on the command line under `--sasl-ir`, and otherwise in
   answer to the server's first challenge (RFC 4422 §5); `Respond` answers the challenges
   after that. So LOGIN's `Respond` gives the password, OAUTHBEARER's gives one `0x01` byte
   for an error continuation, and every other unexpected challenge gets `null` (exit 67).
   This replaces the contract's statement that LOGIN has no initial response, which could
   not produce curl's `AUTH LOGIN dQ==`.
5. **EXTERNAL sends the user name** and ignores `--sasl-authzid`; PLAIN sends
   `authzid NUL user NUL password`; XOAUTH2 `user=<u>^Aauth=Bearer <t>^A^A`; OAUTHBEARER
   `n,a=<u>,^Ahost=<h>^Aport=<p>^Aauth=Bearer <t>^A^A`. A missing user, password or token is
   sent empty. All are encoded with `CredentialEncoding` (ADR-0022).
6. **OAUTHBEARER's port waits for the contract.** `SaslRequest` carries no port, so until a
   follow-up task adds one, `Begin` leaves the `port=` field out (curl's form for port 0).
   `SaslAuthenticator.OAuthBearerMessage` already takes the port and is pinned to curl's
   bytes with it.
7. An `AUTH=` naming a mechanism curl does not know is refused by curl with exit 3 before
   connecting; that is the parser's job. The authenticator answers such a name with `null`.

## Consequences

- The Abstractions XML documentation of `ISaslExchange.InitialResponse` (LOGIN listed as
  having none) and the missing port on `SaslRequest` are fixed by a follow-up task, which
  also switches `Begin` to send the port.
- The SMTP handler must reproduce the last-mechanism quirk of a final `250 AUTH` line, or
  record why not.
- ADR-0121 §2's order is superseded by point 1.

## Alternatives considered

- **LOGIN with no initial response, as the contract says.** Cannot send `AUTH LOGIN dQ==`
  under `--sasl-ir`, so it is not a drop-in replacement.
- **Passing `--sasl-ir` into the exchange.** `SaslRequest` has no such member and the
  contract makes inline sending the handler's decision; the RFC 4422 convention gives the
  same bytes either way without a contract change.
- **Guessing the port from the service name.** Wrong for `smtps` and any explicit port.
