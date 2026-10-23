# ADR-0779: CURLOPT_NOBODY keeps header lines out of the body, except FTP's

- Status: Accepted
- Date: 2026-10-22
- Decided by Claude under Stewart's delegation (BL-3199)

## Context

`TransferContextFactory.HeaderOutputOf` sent a transfer's header lines to its body output under
`EasyTransferOptions.NoBody` as well as under `EasyBehaviourOptions.IncludeHeader`, because the curl tool's
`-I` shows the headers. For a `Libcurl` or `Curl.Api` handle that meant `CURLOPT_NOBODY` 1 handed the
response head to `CURLOPT_WRITEFUNCTION`, which libcurl never does without `CURLOPT_HEADER`.

Measured on curl 8.22.0 (Windows, Schannel build) with `-I --no-include`, which is `CURLOPT_NOBODY` 1 and
`CURLOPT_HEADER` 0 (the tool's `-I` sets both; BL-3199 Notes):

- HTTP (`Record-CurlExchange.ps1`): standard output is empty; with `-I` alone it holds the head.
- `file://`: standard output is empty.
- FTP, file URL (a loopback FTP responder): standard output holds `Last-Modified`, `Content-Length` and
  `Accept-ranges`, as upstream's `ftp.c` `client_write_header` forces `include_header` for those lines
  ("For historic reasons, FTP never played this game"); `-D` holds the server replies with those lines
  between them.
- FTP, directory URL: standard output is empty (no `SIZE` or `REST` is sent).

## Decision

Header lines go to the body output under `CURLOPT_HEADER` (`-i`, and `-I`, which sets it), or under
`CURLOPT_NOBODY` alone only for an `ftp` or `ftps` URL (`TransferContextFactory.WritesHeaderLinesToBody`);
never for a WebSocket URL, as before. Every other scheme's go only to the header output.

## Consequences

A `CURLOPT_NOBODY` handle's write callback gets no bytes over HTTP or for an FTP directory, and FTP's
`-I` lines for a file, as libcurl's does. The curl tool's `-I` is unchanged; `-I --no-include` now prints
nothing over HTTP, as curl 8.22.0 does. The task's "zero body bytes over FTP" holds for a directory URL;
for a file URL the engine matches upstream instead, pinned in `NobodyWriteCallbackTests` and
`CurlEasyNobodyTests`.
