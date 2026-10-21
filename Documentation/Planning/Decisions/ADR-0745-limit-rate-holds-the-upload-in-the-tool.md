# ADR-0745 — `--limit-rate` holds a `-T` or `-F` upload in the curl tool's runner

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2938
- Decided by Claude under Stewart's delegation.

## Context

Finding AF-0300: `--form f=@f.txt --limit-rate 10` against `Record-CurlExchange.ps1` made
curl 8.22.0 (Schannel) send the request head and then the body 10 bytes a second, while Curl sent
the whole 396-byte request in about 0.4 s and exited 0. curl's `--limit-rate` sets both
`CURLOPT_MAX_RECV_SPEED_LARGE` and `CURLOPT_MAX_SEND_SPEED_LARGE`; Curl held only the body output
(`CurlCommandRunner.RateLimited`, ADR-0528, ADR-0552), and nothing read `MaxSendSpeed`.

## Decision

- The runner holds the upload where it already holds the output: `CurlCommandRunner.SendRateLimited`
  wraps each attempt's `-T` source and `-F` `StreamBody` content in `RateLimitedUploadStream`, on the
  runner's clock, when `--limit-rate` is above 0. Every protocol that reads `ITransferContext.Upload`
  or the form body is limited alike, with no change to a protocol library.
- `RateLimitedUploadStream` limits asynchronous reads with `Curl.Core`'s `RateLimitedStream` (one
  second's worth per read, a wait before each later one) and passes seeking, the length and the
  position through, so a resend after a redirect or an authentication round can still rewind the
  upload; it forwards `IRewindableUpload.CanRewind`. A synchronous read is not limited. It does not
  own the upload, which its opener disposes as before.
- A `-d` / `--json` body (`BytesBody`) is not yet limited (BL-2957). A reset that arrives while the
  upload waits ends with curl's exit 56 through the HTTP body writer (BL-2956).

## Consequences

The finding's command line now sends its body at 10 bytes a second, as curl does. A libcurl user
setting `CURLOPT_MAX_SEND_SPEED_LARGE` through `Libcurl` is not yet limited by the engine; the
tool's runner is where the command line's limit lives, as it is for the output.
