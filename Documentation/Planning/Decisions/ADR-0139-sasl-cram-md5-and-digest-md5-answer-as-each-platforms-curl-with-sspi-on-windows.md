# ADR-0139 — SASL CRAM-MD5 and DIGEST-MD5 answer as each platform's curl, DIGEST-MD5 as SSPI on Windows

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-537.

## Context

BL-536 built `SaslAuthenticator` with PLAIN, LOGIN, EXTERNAL, XOAUTH2 and OAUTHBEARER and
held CRAM-MD5 and DIGEST-MD5 as not offered. Measured on 2026-09-28 with
`Record-CurlExchange.ps1 -Smtp` against curl 8.21.0 (mingw, Schannel) on Windows and, with
`-Curl wsl.exe -ListenAddress <WSL host address>`, curl 8.18.0 (OpenSSL) on Ubuntu:

- Both builds rank DIGEST-MD5, then CRAM-MD5, above PLAIN, use them only with a user and no
  `--oauth2-bearer` (exit 67 otherwise), and never send `--sasl-authzid` with them.
  CRAM-MD5 is `user <hex HMAC-MD5 of the challenge keyed with the password>` in both.
- DIGEST-MD5 differs. The OpenSSL build uses curl's own code
  (`Curl_auth_create_digest_md5_message`); the Schannel build hands the challenge to Windows
  SSPI, which writes a different message:

| | OpenSSL build | Schannel build (SSPI) |
| --- | --- | --- |
| Field order | `username, realm, nonce, cnonce, nc, digest-uri, response, qop` | `username, realm, nonce, digest-uri, cnonce, nc, response, qop[, charset]` |
| `nc` | quoted, `"00000001"` | bare, `00000001` |
| `realm` | the challenge's, empty when absent | the domain from `-u dom\user` or `dom/user` (backslash first), else empty |
| `charset=utf-8` in the challenge | ignored | echoed, and the user name sent in UTF-8 |
| `algorithm` | must be exactly `md5-sess` | `md5-sess` in any case |
| No `qop` | cancelled (`*`, exit 67) | answered as `qop=auth` |
| No `nonce`, no `algorithm`, `qop="auth-int"` only | cancelled (`*`, exit 67) | exit 94, nothing sent |

  Both send a 32-digit lower-case hex client nonce, `digest-uri="<service>/<host>"`, and
  answer the server's `rspauth` with an empty line. Every measured response hash was
  reproduced, SSPI's included, which also confirmed RFC 2831's rule that under
  `charset=utf-8` A1 is hashed in ISO 8859-1 when the name fits it.

## Decision

`SaslAuthenticator` answers CRAM-MD5 and DIGEST-MD5 and ranks them where curl does.
DIGEST-MD5 follows the platform's curl, the standing rule: `SaslDigestMd5.AnswerAsSspi` on
Windows, `SaslDigestMd5.AnswerAsCurl` elsewhere, chosen by the constructor's
`answerDigestMd5AsSspi` (the one-argument constructor passes `OperatingSystem.IsWindows()`,
so `Curl.Console` needs no change). The client nonce is injected, as HTTP Digest's is, and
defaults to `DigestClientNonce.CreateRandomHex`.

SSPI's exit 94 cannot yet be expressed: `ISaslExchange.Respond` only answers bytes or
`null`, which the handlers turn into `*` and exit 67. Until BL-781 widens that contract, the
three challenges SSPI rejects are answered `null`.

## Consequences

Scripts see the same mechanism choice and, for DIGEST-MD5, the same message format as the
curl on their platform. A name outside ISO 8859-1 under `charset=utf-8` follows RFC 2831
unmeasured, because the reference build receives its arguments in the ANSI code page. With
no user name the Schannel build lets SSPI use the logged-on Windows user; this
implementation sends an empty user name, as it does for every other mechanism.

## Alternatives considered

- **One DIGEST-MD5 format everywhere.** Simpler, but either the Windows or the Linux output
  would differ from that platform's curl, which the drop-in rule forbids.
- **Reusing HTTP Digest's parser (`DigestChallengeParameters`).** It reads quoted pairs as
  curl's HTTP code does; curl's SASL code instead finds each key by substring and cuts it at
  fixed buffer sizes, so reusing it would diverge on odd challenges.

## Amendment (2026-09-29, BL-781)

Decided by Claude under Stewart's delegation.

The contract now carries SSPI's exit 94. `ISaslExchange.RespondAsync` may throw
`SaslAuthenticationFailedException` (in `Curl.Protocol.Abstractions`), carrying an exit code
and message, the same shape as HTTP's `HttpAuthenticationFailedException` (ADR-0181).
`SaslAuthenticator` constructed with `answerDigestMd5AsSspi: true` throws it, exit 94 with
"An authentication function returned an error", for a challenge with no nonce, no
`algorithm` or no `auth` in its `qop`; the OpenSSL path still answers `null` (cancel, exit 67).
The SMTP, IMAP and POP3 sessions catch it and fail the transfer with its exit code and
message, sending nothing more.

Measured with curl 8.21.0 Schannel on 2026-09-29 (`Record-CurlExchange.ps1 -Smtp`, `-Imap`,
`-Pop3`, challenge `realm="localhost",qop="auth",algorithm=md5-sess,charset=utf-8`): on all
three protocols curl sent nothing after `AUTH DIGEST-MD5` / `AUTHENTICATE DIGEST-MD5` - no
answer, no `*`, no `QUIT` or `LOGOUT` - and wrote
`curl: (94) An authentication function returned an error` to stderr.

An exception rather than a third return value because the failure ends the transfer at once
from deep inside each handler's exchange loop, as it does in curl, and every other
`ISaslExchange` stays unchanged.
