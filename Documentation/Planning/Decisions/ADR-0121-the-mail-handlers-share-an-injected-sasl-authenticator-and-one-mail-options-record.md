# ADR-0121 — The mail handlers share an injected SASL authenticator and one mail options record

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-533.

## Context

The conformance audit of 2026-09-28 (row 34, Blocker) found `smtp`, `smtps`, `pop3`,
`pop3s`, `imap` and `imaps` unsupported: `Curl.Protocol.Smtp.UnitLibrary`,
`Curl.Protocol.Pop3.UnitLibrary` and `Curl.Protocol.Imap.UnitLibrary` are empty. Row 31
lists the mail options (`--mail-from`, `--mail-rcpt`, `--mail-auth`,
`--mail-rcpt-allowfails`, `--upload-flags`) and row 23 the SASL ones (`--sasl-authzid`,
`--sasl-ir`, `--login-options`). Before any of the three handlers is written, their shape
has to be fixed, because they share most of it:

- **SASL (RFC 4422).** All three authenticate with the same mechanisms (SMTP `AUTH`,
  RFC 4954; POP3 `AUTH`, RFC 5034; IMAP `AUTHENTICATE`, RFC 3501) and curl picks among them
  with one preference order. A protocol library may not reference another protocol library
  (root `CLAUDE.md`), and ADR-0120 lets it reference only Abstractions and the hand-built
  libraries, so the mechanism code cannot live in one handler and be borrowed by the others.
- **TLS.** Each scheme has an implicit-TLS twin (`smtps`, `pop3s`, `imaps`) and an in-band
  upgrade (`STARTTLS`, POP3's `STLS`, IMAP's `STARTTLS`) governed by `--ssl` and
  `--ssl-reqd`, as FTP's `AUTH TLS` is.
- **Options.** Every mail option needs a way from the parser to the handler.
- **URLs.** Each scheme reads its path differently.

The precedents: HTTP authentication crosses the protocol boundary through
`IHttpAuthenticator`, declared in Abstractions, implemented in
`Curl.Authentication.UnitLibrary` and injected by `Curl.Console` (ADR-0014). FTP upgrades
its control connection with `AUTH TLS` through the injected `ITlsProvider` and reads
`ITransferContext.SslLevel` (`TransportSecurityLevel`) (ADR-0102). HTTP's own options ride
on one nullable record, `ITransferContext.Http` (`HttpRequestOptions`). Timeouts are the
connector's and the runner's (ADR-0117), and the endpoint variables are recorded by the
composition (ADR-0119).

### What curl 8.21.0 does (measured)

Measured on Windows against the Schannel build (`curl 8.21.0 ... Schannel ... Kerberos
NTLM SPNEGO SSPI`) with `Record-CurlExchange.ps1 -Smtp`, `-Pop3` and `-Imap`, 2026-09-28.
Unless stated, the server answered every `AUTH` with a failure so that curl's first choice
shows, and curl ran with `-u u:p`.

**Mechanism preference.** EHLO offered `AUTH EXTERNAL GSSAPI DIGEST-MD5 CRAM-MD5 NTLM
OAUTHBEARER XOAUTH2 LOGIN PLAIN SCRAM-SHA-256`; after each run the chosen mechanism was
removed from the list:

| Offered (after removals) | curl sent |
| --- | --- |
| all ten | `AUTH DIGEST-MD5` |
| without DIGEST-MD5 | `AUTH CRAM-MD5` |
| without CRAM-MD5 | `AUTH NTLM` |
| without NTLM | `AUTH PLAIN` |
| without PLAIN | `AUTH LOGIN` |
| `EXTERNAL GSSAPI OAUTHBEARER XOAUTH2 SCRAM-SHA-256` | no `AUTH`; `curl: (67) Login denied` |

Further runs:

- `-u 'DOM\u:p'` and `-u 'u@DOM:p'` with GSSAPI offered: `AUTH GSSAPI` first. A user name
  with no domain skips GSSAPI.
- `--oauth2-bearer tok` with everything offered: `AUTH OAUTHBEARER`; with OAUTHBEARER
  removed: `AUTH XOAUTH2`. Without a bearer token neither is ever chosen.
- `--login-options AUTH=EXTERNAL -u u:`: `AUTH EXTERNAL`. With a password, or without the
  explicit `AUTH=EXTERNAL`, EXTERNAL is never chosen.
- `--sasl-authzid z` with `EXTERNAL LOGIN PLAIN` offered: `AUTH PLAIN` (LOGIN cannot carry
  an authorization identity, so it is skipped).
- `--sasl-ir --login-options AUTH=PLAIN`: `AUTH PLAIN AHUAcA==` (the initial response on the
  command line). Without `--sasl-ir`: `AUTH PLAIN`, then the response after the `334`.
- `--login-options AUTH=*`: same choice as no option (`AUTH DIGEST-MD5`).
- `--login-options AUTH=CRAM-MD5` with only `LOGIN PLAIN` offered: no `AUTH` is sent, exit
  67 `Login denied`.
- `--login-options AUTH=LOGIN` with URL `smtp://u:p;AUTH=PLAIN@host/x`: `AUTH LOGIN` - the
  command-line option wins over the URL's login options.
- No `AUTH` in the EHLO reply, with `-u u:p`: curl sends no `AUTH` and delivers the mail
  (exit 0).

**POP3 and IMAP fallbacks.**

- POP3, CAPA offering `SASL CRAM-MD5 PLAIN`, `APOP` and `USER`, greeting carrying a
  timestamp: `AUTH CRAM-MD5`; when that fails, exit 67 with no fallback to APOP or USER.
- POP3, CAPA offering `APOP` and `USER` only: `APOP u <md5 of timestamp + password>`
  (APOP before USER/PASS).
- IMAP, CAPABILITY offering no `AUTH=`: `A002 LOGIN u p`, then `SELECT INBOX`,
  `UID FETCH 1 BODY[]`, `LOGOUT`, for URL `imap://host/INBOX;UID=1`. Tags run `A001`,
  `A002`, ...

**STARTTLS.**

- `--ssl-reqd`, EHLO without `STARTTLS`: no `STARTTLS` sent; exit 64,
  `curl: (64) STARTTLS not supported.`
- `--ssl`, `STARTTLS` advertised but answered `454`: curl carries on in plaintext
  (`MAIL FROM` next, no second EHLO), exit 0.
- `--ssl-reqd`, `STARTTLS` answered `220`: TLS handshake, then a second `EHLO`, then
  `MAIL FROM`.

**URL.** `smtp://host/x` sends `EHLO x`; `smtp://host/` with `-T NUL` sends `EHLO NUL`
(the command line appends the upload's file name to a URL ending in `/`, as for FTP).

## Decision

### 1. SASL: `ISaslAuthenticator` in Abstractions, implemented in `Curl.Authentication.UnitLibrary`

The contract follows ADR-0014's `IHttpAuthenticator`: declared in
`Curl.Protocol.Abstractions.UnitLibrary` (namespace `Curl.Protocol.Abstractions`),
implemented once in `Curl.Authentication.UnitLibrary`, constructed by `Curl.Console` and
passed to each mail handler's constructor. A handler built without one authenticates only
with its protocol's plain commands (POP3 `USER`/`PASS`/`APOP`, IMAP `LOGIN`).

```csharp
public interface ISaslAuthenticator
{
    // The mechanism curl 8.21.0 would pick from the server's list, or null when none can
    // be used with this request (the handler then falls back as §5 says, or fails 67).
    string? ChooseMechanism(SaslRequest request, IReadOnlyList<string> offeredMechanisms);

    // Starts one exchange for the chosen mechanism.
    ISaslExchange Begin(string mechanism, SaslRequest request);
}

public interface ISaslExchange
{
    string Mechanism { get; }

    // The initial response (RFC 4422 §3.3), or null when the mechanism has none (LOGIN,
    // CRAM-MD5, DIGEST-MD5). Raw bytes; an empty array is a present-but-empty response.
    byte[]? InitialResponse { get; }

    // The answer to one server challenge (decoded from base64 by the handler). Null means
    // the exchange cannot answer it: the handler cancels (`*`) and fails 67.
    byte[]? Respond(ReadOnlySpan<byte> challenge);
}

public sealed record SaslRequest(
    NetworkCredential? Credential,       // -u / URL user info
    string? AuthorizationIdentity,       // --sasl-authzid
    string? BearerToken,                 // --oauth2-bearer
    string? RequiredMechanism,           // AUTH=<mech> from the login options; null or "*" = any
    string ServiceName,                  // "smtp", "pop" or "imap"; --service-name overrides
    string Host);                        // the URL's host, for GSSAPI and DIGEST-MD5
```

- The exchange works in raw bytes; base64, the `334`/`+` prefixes, `=` for an empty
  initial response and the line-length rules are each handler's, because they differ per
  protocol.
- Whether the initial response goes on the command line is the handler's decision:
  `MailRequestOptions.SaslInitialResponse` (`--sasl-ir`) must be set, and for IMAP the
  server must also advertise `SASL-IR`.
- Mechanisms are named by their SASL names (upper case, as RFC 4422 registers them); a
  server's list is compared case-insensitively.
- The ranking lives in the authenticator alone (`Curl.Authentication.UnitLibrary`), as HTTP's
  does in `HttpAuthSchemeRanking`. Mechanisms not yet built are treated as not offered, so
  the order holds as BL-536, BL-537 and BL-538 land one by one.
- NTLM and GSSAPI will draw their tokens from the hand-built `Curl.Ntlm.UnitLibrary` and
  `Curl.Kerberos.UnitLibrary` that ADR-0120 names (not yet created; BL-525 decides the
  token source); SASL GSSAPI uses the raw Kerberos
  mechanism (RFC 4752), not SPNEGO, on every platform.

### 2. The mechanism preference order

> Amended by ADR-0123 (BL-536): PLAIN ranks before LOGIN, an authorization identity does
> not skip LOGIN, and a bearer token excludes PLAIN and LOGIN. Items 8 and 9 below are
> superseded.

Measured above, so this is the order `ChooseMechanism` implements, first usable wins:

1. `EXTERNAL` - only when the login options say `AUTH=EXTERNAL`.
2. `GSSAPI` - only when the user name carries a domain (`DOM\u` or `u@DOM`).
3. `DIGEST-MD5`
4. `CRAM-MD5`
5. `NTLM`
6. `OAUTHBEARER` - only with a bearer token.
7. `XOAUTH2` - only with a bearer token.
8. `LOGIN` - skipped when an authorization identity is given.
9. `PLAIN`

`SCRAM-*` is never chosen by this build and is not offered. `AUTH=<mech>` restricts the
choice to that one mechanism (none offered: `null`, exit 67); `AUTH=*` or no option
means any. The login options come from `--login-options` when given, otherwise from the
URL's `;` options (`CurlUrl.Options`); the command line wins, as measured.

### 3. The options: one `MailRequestOptions` record on `ITransferContext`

One new member, mirroring `ITransferContext.Http`:

```csharp
MailRequestOptions? Mail { get; }   // null for every scheme but smtp(s), pop3(s), imap(s)
```

`MailRequestOptions` is a `sealed record` in Abstractions with `init` properties, each
defaulting to curl's "not given":

| Member | Type | Option |
| --- | --- | --- |
| `From` | `string?` | `--mail-from` |
| `Recipients` | `IReadOnlyList<string>` (empty) | `--mail-rcpt`, repeatable |
| `Auth` | `string?` | `--mail-auth` |
| `RecipientAllowFails` | `bool` | `--mail-rcpt-allowfails` |
| `UploadFlags` | `IReadOnlyList<string>` (empty) | `--upload-flags` (IMAP `APPEND`) |
| `CustomCommand` | `string?` | `-X` / `--request` for a mail scheme |
| `LoginOptions` | `string?` | `--login-options` |
| `SaslAuthorizationIdentity` | `string?` | `--sasl-authzid` |
| `SaslInitialResponse` | `bool` | `--sasl-ir` |
| `BearerToken` | `string?` | `--oauth2-bearer` |
| `ServiceName` | `string?` | `--service-name` (wired by BL-630/BL-631; null means the scheme's default) |

Existing members serve the rest unchanged: `Credentials` (`-u`), `SslLevel`, `ListOnly`
(`-l`, POP3 `LIST`), `NoBody` (`-I`), `ConvertLineEndings` (`--crlf`), `Upload` (`-T`),
`ConnectTimeout`/`MaxTime` and `Events`/`Progress`. `Curl.Console` builds the record for
the six mail schemes only (BL-539); the context stays null-by-default for every existing
handler.

### 4. TLS per scheme and security level

TLS is always through the injected `ITlsProvider`, as FTP does (ADR-0102); a mail handler
never constructs an `SslStream`. A handshake failure is exit 35, a certificate failure 60,
as they are for FTP.

| Scheme | `SslLevel` | Behaviour |
| --- | --- | --- |
| `smtps`, `pop3s`, `imaps` | any | TLS from the first byte, before the greeting is read. |
| `smtp`, `pop3`, `imap` | `None` | Never upgrades. |
| `smtp`, `pop3`, `imap` | `Try` (`--ssl`) | Upgrades when the capability list advertises it (EHLO `STARTTLS`, CAPA `STLS`, CAPABILITY `STARTTLS`); a refusal carries on in plaintext without re-reading capabilities. |
| `smtp`, `pop3`, `imap` | `Required` (`--ssl-reqd`) | Not advertised: exit 64 `STARTTLS not supported.` without sending it. A refusal: exit 64 (text measured by BL-540, BL-547, BL-553). |

After a successful upgrade the handler re-reads the capabilities (SMTP sends `EHLO` again,
POP3 `CAPA`, IMAP `CAPABILITY` unless the tagged `OK` carried them), and only then
authenticates. The default ports are 25/465, 110/995 and 143/993.

### 5. Authentication per protocol

- **SMTP:** `AUTH` with `ChooseMechanism` over EHLO's `AUTH` list. No list, or no
  credentials and no bearer token: no authentication, the mail goes out (measured).
  A list but no usable mechanism: exit 67 `Login denied`.
- **POP3:** SASL first when CAPA lists `SASL`; if it lists mechanisms, a failed SASL
  exchange is exit 67 with no fallback (measured). Without SASL: `APOP` when the greeting
  carries a timestamp, otherwise `USER`/`PASS`. `AUTH=+APOP` and `AUTH=USER` in the login
  options force those (BL-548 measures them).
- **IMAP:** `AUTHENTICATE` when CAPABILITY lists `AUTH=` mechanisms, otherwise `LOGIN`
  (quoted as RFC 3501 requires), refused when `LOGINDISABLED` is advertised.

### 6. The URL model per scheme

Each handler reads its own URL from `CurlUrl` (path, query and `Options`), in a
`<Scheme>Url` type inside its own library:

- **SMTP** `smtp://host[:port]/[<domain>]`: the URL-decoded path is the `EHLO`/`HELO`
  domain; an empty path uses curl's default (BL-540 measures it).
- **POP3** `pop3://host[:port]/[<msgnum>]`: a message number means `RETR <n>` (`LIST <n>`
  with `-l`, `DELE <n>`/`TOP` through `-X`); no number means `LIST`.
- **IMAP** `imap://host[:port]/[<mailbox>][;UIDVALIDITY=<v>][/;UID=<n> | /;MAILINDEX=<n>][/;SECTION=<s>][/;PARTIAL=<o.l>][?<search>]`:
  a mailbox with a `UID` or `MAILINDEX` means `SELECT` then `FETCH`; a mailbox with a
  query means `SEARCH <query>`; a mailbox alone with an upload means `APPEND`; no mailbox
  means `LIST "" *`. The `;` parameters may follow the mailbox with or without a `/`
  (measured `INBOX;UID=1`).

### 7. Line reading is duplicated per library

Each library owns its reader: SMTP's three-digit replies with `-` continuations, POP3's
`+OK`/`-ERR` with dot-terminated multi-line bodies, IMAP's tagged and untagged responses
with `{n}` literals. What they have in common - read bytes to CRLF from `IConnection` - is
a few lines, the same size as FTP's `FtpControlChannel` and HTTP's `HttpLineReader`, which
are already separate. Sharing it would mean either a new public type in Abstractions (a
contract every protocol task then serialises on) or a horizontal reference ADR-0120 forbids.

### 8. Timeouts and endpoints

A mail handler follows ADR-0117: it passes the transfer's cancellation token to every read
and write and reports bytes through `ITransferProgress`; the connector and the runner turn
a timeout into exit 28. Following ADR-0119, it reports no endpoint variables itself - the
composition's decorators record the first connection, and a STARTTLS upgrade keeps that
connection's endpoints.

## Consequences

- Unblocks BL-534 (the `ISaslAuthenticator`, `ISaslExchange`, `SaslRequest` and
  `MailRequestOptions` contract), BL-535 (the CLI options), BL-536, BL-537 and BL-538 (the
  mechanisms and ranking in `Curl.Authentication.UnitLibrary`), BL-539 (the console
  fills `Mail` and injects the authenticator), BL-540 to BL-546 (SMTP), BL-547 to BL-552
  (POP3) and BL-553 to BL-559 (IMAP).
- The three handlers never reference each other or `Curl.Authentication.UnitLibrary`; their
  tests pass a fake `ISaslAuthenticator`.
- Adding a mechanism later (SCRAM, if a future build chooses it) changes only
  `Curl.Authentication.UnitLibrary`.
- Each library carries a small line reader of its own; a bug fixed in one is not fixed in
  the others.

## Alternatives considered

- **SASL in a new hand-built `Curl.Sasl.UnitLibrary` referenced by the three handlers.**
  ADR-0120 would allow it by amendment, but the mechanisms need credentials, the bearer
  token and the NTLM and Kerberos token sources that `Curl.Authentication.UnitLibrary`
  already gathers for HTTP; a second home would split one concern across two libraries,
  and the handlers would build the mechanisms themselves instead of receiving them.
- **SASL code in each handler.** Three copies of nine mechanisms and one ranking; rejected
  outright.
- **One flat member per option on `ITransferContext`.** Eleven members that are null for
  every other scheme; the `Http` record is the precedent for grouping a protocol family's
  options.
- **A shared `MailLineReader` in Abstractions.** Rejected in §7.
- **Exchanging base64 strings instead of bytes.** Would push each protocol's framing into
  the authenticator; IMAP's literal and SMTP's `=` differ.
