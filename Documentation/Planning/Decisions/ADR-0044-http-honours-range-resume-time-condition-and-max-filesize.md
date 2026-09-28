# ADR-0044 — How the HTTP handler honours `-r`, `-C`, `-z` and `--max-filesize`

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

`ITransferContext` already carries `Range`, `ResumeFrom`, `TimeCondition` and `MaxFileSize`,
and `TransferResult` carries `SourceLastWriteTimeUtc` and `TimeConditionUnmet`. BL-178 makes the
HTTP handler honour them. curl 8.21.0 (mingw, Schannel) was measured against a loopback server
with `Record-CurlExchange.ps1`; every command and its bytes are in the BL-178 Notes. The
measurements settle most of the behaviour. A few choices were left open, and this record makes
them.

## Decision

- **Where the headers go.** `Range` sits after `Authorization` and before `User-Agent`;
  `If-Modified-Since` or `If-Unmodified-Since` sits after the cookie engine's `Cookie` and
  before the `-H` values. Each is left out when an `-H` value names it, as measured.
- **`-C` wins over `-r`.** curl refuses the two together on the command line (exit 2), so a
  handler never sees both from `Curl.Console`. If a caller passes both, the resume is sent.
  `-C 0` is no resume, as measured: no `Range` and no resume check.
- **A request with a body sends `Content-Range` instead of `Range`** (BL-306, measured there).
  A `-d` body or a `-T` upload with `-r` sends `Content-Range: bytes R/L` in the `Range` slot,
  after `Host` and before `User-Agent`: R is the `-r` text as typed and L
  the body's length, or `-1` when it is unknown (`-T -`). `-X PUT` makes no difference. A `-F`
  form sends neither header, as curl sends none for a multipart post. An `-H Content-Range`
  value replaces curl's, in the `-H` position. A resumed `-T` upload's own `Content-Range`
  (ADR-0057) takes the place of the range's.
- **The `-r` text goes out verbatim, and HTTP never refuses it** (BL-386, amended 2026-09-27).
  libcurl copies `data->state.range`, the option text, into `Range: bytes=<text>` and
  `Content-Range: bytes <text>/L` without parsing it. Measured on curl 8.21.0 (BL-386 Notes):
  `0-9,20-29`, `1-2abc`, `abc`, `-0`, `3-1` and `a-3` are all sent as typed, exit 0, where
  `file://` answers `abc`, `-0` and `3-1` with exit 33. So `ITransferContext.RangeText` carries
  the text as given and the HTTP handler sends it, while `Range` stays the one range
  `ByteRangeParser` reads for the handlers that serve bytes themselves; `Curl.Console` skips
  the exit-33 refusal for an `http` or `https` URL, where `Range` is then `null` and
  `RangeText` is not. A `-z` condition is not compared whenever `RangeText` is set, as curl
  compares none when a range was asked for. *Why a second member rather than text only:* the
  `file://` and FTP handlers need a validated range, and parsing it once in `Curl.Console`
  keeps their exit 33 in one place.
- **`Last-Modified` is read in the three HTTP-date forms** RFC 9110 obliges a recipient to
  accept (IMF-fixdate, RFC 850, asctime), written by hand with `DateTimeOffset.TryParseExact`.
  curl's own date parser accepts more spellings. A value in none of the three is an unknown
  time, which meets any `-z` condition. For `garbage` that is what curl does too (measured).
  The last `Last-Modified` header wins, as in curl.
- **The `--max-filesize` body limit counts encoded bytes** with `--compressed`, the same bytes
  `%{size_download}` counts. This is not measured for an encoded body. A discarded body (a 3xx
  under `-L`, a 401 answered by a retry) is not held to the limit, but its Content-Length is
  still checked, because curl checks it while it parses the head.
- **An unmet `-z` condition reports a 304.** When a response's `Last-Modified` fails the
  condition, the result has `TimeConditionUnmet` set and a `ResponseCode` of 304. That is what
  curl's `%{response_code}` shows (measured: `304 0`). The head that was really received is
  still written to the header output.

## Consequences

Good:

- Every rule is tested on recorded bytes through `ScriptedConnection`, 1-byte reads included,
  with no socket.
- `-R` over HTTP gets its source time from `TransferResult.SourceLastWriteTimeUtc`, which
  `Curl.Console` already applies.

Costs and caveats:

- An `ftp` URL forwarded to an HTTP proxy (ADR-0056) still has its `-r` text refused with
  exit 33 when it names no range, since `Curl.Console` exempts only `http` and `https`; what
  curl does there is not measured.
- A `Last-Modified` in a spelling curl accepts but RFC 9110 does not list is treated as unknown,
  so the body is delivered where curl might skip it.

## Alternatives considered

- **Port curl's `Curl_getdate`.** Rejected for now. It is several hundred lines of lenient
  parsing, and servers send IMF-fixdate. Revisit if a real server's spelling is found missing.
- **Report the received status for an unmet `-z` condition.** Rejected because it breaks
  drop-in `-w '%{response_code}'` output.
