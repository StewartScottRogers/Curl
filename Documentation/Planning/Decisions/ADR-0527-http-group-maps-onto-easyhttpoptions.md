# ADR-0527: The HTTP command-line group maps onto EasyHttpOptions

- Status: Accepted
- Date: 2026-10-15
- Task: BL-2307 (ADR-0506 plan phase 1 step 4)
- Decided by Claude under Stewart's delegation.

## Context

ADR-0506 moves the engine from the curl tool's `CommandLineOptions` to the `Curl.Api` option
groups. `Curl.Console`'s `EasyOptionsFromCommandLine` is the tool's `config2setopts.c`, one method
per group; BL-2307 adds the HTTP group. The engine classes that read HTTP settings are many
(`HttpRequestOptionsMapping`, `HttpVersionMapping`, `RedirectPolicyMapping`,
`MultipartFormPartMapping`, `CookieEngine`, `AltSvcTransferCache`, `HstsCacheFile`, three of
`TransferPerformer`'s steps and `TransferContextFactory`), so the retarget is split.

## Decision

1. `EasyOptionsFromCommandLine.HttpOf` builds `EasyHttpOptions`. Where it differs from the
   `CURLOPT_*` lines `--libcurl` writes, the command line's behaviour wins:
   - `HttpHeaders` holds the `-H` values and then the `--json` headers no `-H` names, as
     `--libcurl` writes them. The `--etag-compare` `If-None-Match` lines are read per transfer, so
     they are added there, not here.
   - `UserAgent` stays `null` without `-A`: `--libcurl` writes the tool's `curl/<version>`, which
     the engine's HTTP handler already supplies.
   - `HttpVersion` maps every version option (`--http2`, `--http3` and `--http3-only` included),
     although the Schannel build's `--libcurl` writes only 1.0 and 1.1; no option is
     `CURL_HTTP_VERSION_NONE`.
   - `-L` and `--location-trusted` are `CURLFOLLOW_ALL` and `--follow` `CURLFOLLOW_OBEYCODE`, as
     `--libcurl` writes `1L` and `2L`. `MaxRedirects` is the tool's 50 when not given, as
     `--libcurl` always writes `CURLOPT_MAXREDIRS`.
   - `--raw` turns `HttpContentDecoding` and `HttpTransferDecoding` off, as the tool sets
     `CURLOPT_HTTP_CONTENT_DECODING` and `CURLOPT_HTTP_TRANSFER_DECODING` to 0.
   - The `-d` family and `--json` body is `PostFields` with its length as `PostFieldSize`; `-G`
     moves it into the URL, which no group holds, so it leaves both at their defaults. `Post` and
     `HttpGet` stay off: `--libcurl` writes neither.
   - `--expect100-timeout` is set only when positive, so 0 keeps libcurl's one second.
   - `-b` strings join into `Cookie` with `"; "`, the `-b` files are `CookieFiles` in order (an
     empty `-b` is a file name that only turns the engine on), `-j` is `CookieSession`.
   - `--alt-svc` and `--hsts` set the file and the control bits setting the file gives in libcurl
     (`CURLALTSVC_H1|H2|H3`, `CURLHSTS_ENABLE`), since the record's `None` means off.
   - `-X` and `-I` are `EasyTransferOptions`' (BL-2308) and `--path-as-is` `EasyNetworkOptions`'
     (ADR-0513), so `HttpOf` sets neither. `--no-npn` has no function in curl 8, so nothing maps.
2. `EasyHttpOptions` gains `SslEnableAlpn` (`CURLOPT_SSL_ENABLE_ALPN`, default on) for
   `--no-alpn`, which ADR-0514 left to this group.
3. The `-F` body (`MimePost`) is BL-2344: the record cannot yet tell `name=<file`'s cleared file
   name from `name=@file`'s base name. The engine retarget is BL-2345 (request, version and
   redirect mappings) and BL-2346 (cookies, alt-svc and HSTS); BL-2310 waits on all three.

## Consequences

No engine class reads `EasyHttpOptions` yet, so behaviour is unchanged. The follow-up tasks can
each move one set of classes against a mapping that is already tested option by option.
