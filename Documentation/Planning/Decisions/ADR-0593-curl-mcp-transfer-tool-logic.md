# ADR-0593: The `transfer` tool's logic: a checked request, one easy handle, the last response

- Status: Accepted
- Date: 2026-10-16
- Decided by Claude under Stewart's delegation (BL-2383)

## Context

ADR-0548 and `Documentation/Planning/Curl-Mcp.md` give the MCP server a `transfer` tool over
`Curl.Api.UnitLibrary`'s seam: a structured request (URL, method, headers, body, follow redirects,
timeout, insecure, proxy, user agent) in, status, headers, body and `TransferInfo` out, with no
option that reads or writes a local path. Several choices were left open: how the method maps to
libcurl's options, which URLs are accepted, what "libcurl's message" is, which response's headers
are returned, and how a request field is refused.

## Decision

- `McpTransferRequestCheck` checks the request before an easy handle exists, and names the first bad
  field in an `McpToolFailureKind.InvalidRequest` error (`Invalid <field>: <reason>`): the URL must
  contain `://` and parse as an absolute `Uri`; a `file:` URL is refused, because it reaches local
  files outside `CURL_MCP_ROOT` and the `curl` tool, which confines them, already offers it; the
  method and each header name must be RFC 9110 tokens; a header value must hold no CR or LF, so no
  header can be injected; the timeout must be from 0 to `int.MaxValue / 1000` seconds (NaN and
  infinity refused); a `HEAD` carries no body, as `curl -I -d` refuses; a base64 body must decode.
- `McpTransferOptions.Build` sets only what a field names, as the curl tool's own options would:
  `GET` without a body sets nothing, `HEAD` is `CURLOPT_NOBODY`, a body or `POST` is `CURLOPT_POST`
  with `CURLOPT_POSTFIELDS` (empty for a bodiless `POST`, as `-d ''`), and any other method, or a
  `GET` with a body, is `CURLOPT_CUSTOMREQUEST` (as `-X`). Headers become `Name: value`, an empty
  value `Name;` so libcurl sends it rather than removing it. `followRedirects` is
  `CURLFOLLOW_ALL` (`-L`), `insecure` turns off the origin's peer and host verification only (`-k`,
  not `--proxy-insecure`), and a timeout of 0 is libcurl's "never".
- `McpTransferTool` runs one easy handle from the injected `ICurlEngine`, disposed afterwards, with
  the write callback filling an `McpCappedCaptureStream` and the header callback an
  `McpResponseHeaderCollector`. The body is text or base64 cut to the cap (ADR-0555).
- Headers are the last response's: a status line starts a new list, so a redirect's, a `100`'s or a
  proxy `CONNECT`'s headers are dropped; order and repeats are kept, folded lines join the header
  before them, and lines are read as UTF-8.
- A failing `CurlCode` is `McpToolFailure.FromCurlExit` with the handle's `ErrorText`
  (`CURLOPT_ERRORBUFFER`), the detailed message libcurl gives, then `curl exit code N (Name)`. The
  `curl_easy_strerror` table lives in `Curl.Console`, which this library must not reference, and the
  error buffer says more than it.
- A failed transfer returns the error only, no partial status, headers or body. Cancellation follows
  `McpCurlTool`: a token cancelled before the handle, during the perform, or as it ends gives
  `McpToolFailure.Cancelled`.
- `TransferInfo` comes back as `McpTransferInfo`: times in seconds, sizes, speeds, effective URL and
  method, redirect count, content type, primary IP and port, and `CURLINFO_HTTP_VERSION` as `1.0`,
  `1.1`, `2` or `3`.

## Consequences

BL-2384 maps the MCP tool's JSON arguments to `McpTransferRequest` and the result to the tool
response, and supplies the managed engine. An agent wanting a `file:` URL uses the `curl` tool.
