# ADR-1337: A Curl.Api stream body to an extension scheme is read up front as -d data

- Status: Accepted
- Date: 2026-11-22
- Task: BL-5754
- Decided by Claude under Stewart's delegation.

## Context

ADR-1336 (BL-5758) gives an extension's handler a `Curl.Api` byte body as `-d` data: the engine copies
`CURLOPT_POSTFIELDS` onto the context's `PostData` for a URL whose scheme curl does not serve. A stream body
(`CurlContent.FromStream`) is not post fields but a `CURLOPT_READFUNCTION` post, so the handler saw no data:
a `tools/call` body streamed to `mcp://`, `mcps://` or `mcp+stdio:` listed the tools instead of calling one.

## Decision

`ManagedEasyTransfer.TransferSettingsOf` reads a read-callback post (`CURLOPT_POST`, no `CURLOPT_POSTFIELDS`,
no `CURLOPT_UPLOAD`) to an extension URL up front, to `CURLOPT_POSTFIELDSIZE` bytes when that is not negative,
else until the callback returns 0 or answers with a code rather than a count, and gives the bytes to the handler
as `PostData`, as the console reads `-d @file` before the transfer starts. Curl's own schemes are unchanged.

A read callback that cannot be served fails the transfer and no handler runs on a partial body (BL-5766, ADR-1339),
as `CallbackReadStream` does on the HTTP path: `CURL_READFUNC_ABORT` ends it with `CurlCode.AbortedByCallback` (42,
`operation aborted by callback`); `CURL_READFUNC_PAUSE` (there is no transfer to pause) ends it with
`CurlCode.ReadError` (26, `Read callback asked for PAUSE when not supported`); more bytes than were asked for ends
it with `CurlCode.ReadError` (`read function returned funny value`). A `Curl.Api` stream that throws is such an abort.

## Consequences

Every extension that takes `-d` data receives a `Curl.Api` stream body the same as a byte body. The stream is
read whole into memory before the handler runs, as `-d` data always is.
