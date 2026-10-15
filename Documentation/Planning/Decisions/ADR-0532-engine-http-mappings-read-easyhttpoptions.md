# ADR-0532 — The engine's HTTP request, version and redirect mappings read `EasyHttpOptions`

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2345.

## Context

ADR-0527 built `EasyOptionsFromCommandLine.HttpOf`. BL-2345 has `HttpRequestOptionsMapping`,
`HttpVersionMapping`, `RedirectPolicyMapping` and `TransferContextFactory`'s HTTP fields read the HTTP
settings from that record instead of `CommandLineOptions`, with behaviour byte for byte unchanged. Three
record members have no one-to-one counterpart in `HttpRequestOptions`, and the HTTP-version reads in
`EngineComposition` and `OptionGroupConnectionSettings` sit behind factories with about 120 test call sites.

## Decision

- **`HttpRequestOptionsMapping.Of(options, http, ...)` replaces `FromCommandLine`.** From the record:
  `HttpHeaders` (already holding `--json`'s two lines, so the mapping no longer adds them), `UserAgent`,
  `Referer`, `AutoReferer`, `RequestTarget`, `PostFields` (already `null` under `-G`), `FollowLocation`
  (anything but `None` follows), `MaxRedirects`, `HttpVersion`, `TransferEncoding`,
  `IgnoreContentLength`, `Http09Allowed` and `Expect100Timeout`, read directly because `HttpOf` already
  turns a missing or zero `--expect100-timeout` into curl's one second (`ContinueWaitOf` is gone).
  `-X`, `-f`, the authentication, proxy, `--unix-socket` and `--happy-eyeballs-timeout-ms` settings stay on
  `CommandLineOptions` until their groups' tasks (BL-2347, BL-2338, BL-2325, BL-2343).
- **`Compressed` is a set `AcceptEncoding`.** `--compressed` sets libcurl's `CURLOPT_ACCEPT_ENCODING` to
  `""` and nothing else sets it, so any non-null value turns the handler's `Compressed` on.
- **`Raw` is both decodings off.** `--raw` turns `HttpContentDecoding` and `HttpTransferDecoding` off
  together; the handler has one switch, so it is on only when both are off, and a caller that turns one
  off alone keeps the handler's decoding (it decodes nothing `--compressed` did not ask for anyway).
- **`RedirectPolicyMapping.Of(options, http)` replaces `FromCommandLine`.** `MaxRedirects`, the three
  `PostRedirect` bits, and `FollowLocation.ObeyCode` (`--follow`) as `DropsCustomMethodOnSwitchToGet`
  come from the record; `--location-trusted`, `--proto`, `--proto-redir` and
  `--disallow-username-in-url` stay on `CommandLineOptions` (the authentication and protocol groups).
- **`HttpVersionMapping` maps `Curl.Api`'s `HttpVersion`.** `None` is "no version option";
  `Http2Tls`, which no command line produces, asks for HTTP/1.1 over cleartext and offers
  `h2,http/1.1` over TLS, as libcurl's `CURL_HTTP_VERSION_2TLS` does. `FromRequested` bridges a
  `CommandLineOptions.HttpVersion` for the readers not yet handed the record (`EngineComposition`'s TCP
  connector, `OptionGroupConnectionSettings` and `AltSvcTransferCache`).
- **`TransferContextFactory.Create` takes the record as its second parameter**, and the runner builds it
  with `HttpOf` for each attempt, as it does for the form body (ADR-0531). `TransferContext.PostData`
  still reads `CommandLineOptions.PostData`, which non-HTTP handlers read even under `-G`.
- **The `EngineComposition` and `OptionGroupConnectionSettings` reads are split off as BL-2350**, so
  threading the record through `CreateTransports`, `CreateTcpConnector` and `CreatePoolingConnector` and
  their tests gets a run of its own; BL-2346 retargets `AltSvcTransferCache`. Between them they remove
  `FromRequested`.

## Consequences

`Curl.Api` and `Libcurl` can hand the engine an `EasyHttpOptions` for every request header, body,
encoding, version and redirect setting. Tests drive the three mappings with a record built directly.
