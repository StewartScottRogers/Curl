# ADR-0640 — `Curl.Api`'s HTTP, authentication and protocol option records: the transport records' pattern, the request keeping its own headers and body

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2503.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md` asks for HTTP, authentication and
per-protocol settings in `Curl.Api`, mapped onto the seam's `EasyHttpOptions`,
`EasyAuthenticationOptions` and `EasyProtocolOptions`, with the client-then-request layering ADR-0636
set for the transport records.

## Decision

- `CurlHttpOptions`, `CurlAuthenticationOptions`, `CurlFtpOptions`, `CurlSshOptions`, `CurlMailOptions`
  (SMTP's `EasySmtpOptions`; IMAP and POP3 have no options of their own), `CurlTftpOptions`,
  `CurlTelnetOptions` and `CurlRtspOptions` are records in `Curl.Api/Options/Protocol`, namespace
  `Curl.Api`, following ADR-0636: seam names, types, enums and libcurl 8.22.0 defaults; validated in
  `init` with `TransportOptionGuard` (an enum value libcurl does not name, `MaxRedirects` below -1,
  `StreamWeight` outside 1 to 256, `BlockSize` outside 8 to 65464, a negative `Expect100Timeout`, a
  `null` list or `StreamUri`); a record that is set replaces its whole group, `null` keeps it.
- They hang off `CurlClientOptions` and `CurlRequest` as `Http`, `Authentication`, `Ftp`, `Ssh`, `Mail`,
  `Tftp`, `Telnet` and `Rtsp`. `CurlProtocolOptions.Apply` puts the client's on in `CreateEasy` and the
  request's in `SendAsync`, each after the transport records.
- `CurlHttpOptions` leaves out what `CurlRequest` already owns or what needs another handle:
  `HttpHeaders`, `Post`, `PostFields`, `PostFieldSize`, `HttpGet`, `MimePost`, `HttpPost`,
  `MimeOptions` (the request's headers, method and body, and multipart bodies, BL-2506) and
  `StreamDependency` (an HTTP/2 parent handle). It sets the rest onto the group already there, so
  the request's headers and body, applied after it, survive. Its `UserAgent` is `null` to keep
  `CurlClientOptions.UserAgent`, as `CurlNetworkOptions.ConnectTimeout` keeps the client's timeout.
- The flag enums (`HttpAuthenticationMethods`, `PostRedirect`, `AltSvcControl`, `HstsControl`,
  `SshAuthenticationTypes`) are not checked bit by bit: libcurl takes any bits for them
  (`CURLAUTH_ANY` and `CURLSSH_AUTH_ANY` set every bit).
- `EasyWebSocketOptions` gets no record here: `CurlWebSocketOptions` already owns `CURLOPT_WS_OPTIONS`
  for `CurlClient.ConnectWebSocketAsync` (ADR-0633).
- `CurlAuthenticationOptions.ToString()` prints `<redacted>` for every password, combined
  user-and-password string, bearer token and signature key that is set, as
  `EasyAuthenticationOptions` does.
- The whole-group records implement the internal `IEasyOptionRecord<TEasy>` explicitly, so one
  generic `Pick` applies any of them with one branch and no delegate.

## Why

- The request's headers, method and body already map through `CurlRequestOptions`; a second place to
  set them would leave two answers to one question.
- The same rules as ADR-0636 keep the whole options surface predictable: one record per group, set
  whole, checked when built.

## Consequences

- A request that wants one HTTP setting changed from the client's builds its record from the client's:
  `client.Options.Http! with { ... }`.
