# ADR-0043 — `-w` variables with a fixed value print it, and those without a source stay unknown

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

BL-284 asked `TransferWriteOutVariables` (in `Curl.Output`) to supply every `-w` variable
curl 8.21.0 knows that no other task covers: `referer`, `filename_effective`,
`url.<part>`, `urle.<part>`, `certs`, `num_certs`, `ssl_verify_result`,
`proxy_ssl_verify_result`, `proxy_used`, `num_retries`, `conn_id`, `xfer_id`,
`time_queue`, `tls_earlydata` and `ftp_entry_path`. ADR-0015 ("What the report does not
carry") left each of them to a later ADR.

Measured on 2026-09-26 with the reference build (curl 8.21.0, mingw, Schannel), one
variable per run, `curl -s -o out.bin -w "%{<name>}" <url>`, for
`file:///Z:/bl284tmp/wo.txt`, `http://u:p@127.0.0.1:18284/wo.txt?q=1#frag` (a loopback
`python -m http.server`), `https://self-signed.badssl.com/` (exit 60) and
`https://example.com/`; the bytes are in BL-284's Notes. In short:

- `ssl_verify_result` and `proxy_ssl_verify_result` printed `0` every time, the failed
  verification included: Schannel never sets the verify result.
- `tls_earlydata`, `num_retries` and `proxy_used` printed `0`; `ftp_entry_path` printed
  nothing.
- `time_queue` printed a few tens of microseconds (`0.000083`, `0.000038`, `0.000034`).
- `certs` and `num_certs` printed nothing and `0` for file:// and http://, but `num_certs`
  printed `4` for `https://example.com/` without `--certinfo`.
- `referer`, `filename_effective`, `conn_id`, `xfer_id` and every `url.`/`urle.` part
  depend on the command line, the process or the URL's parts.

`BL-284` may change only `Curl.Output`.

## Decision

- **Supply what is fixed for every transfer this tool can run.** `ssl_verify_result` and
  `proxy_ssl_verify_result` print `0` (Schannel); `tls_earlydata` prints `0` (Schannel
  sends no early data and `--tls-earlydata` is not parsed); `num_retries` prints `0`
  (`--retry` is not parsed); `ftp_entry_path` prints nothing (the FTP handler is an empty
  scaffold). The task that adds `--retry`, the FTP handler, or an OpenSSL-matching verify
  result on Linux and macOS replaces the fixed value with a source.
- **`time_queue` is measured from `TransferTimings.Started` to itself.** The handler's
  start is when the transfer leaves the queue, so by ADR-0035's rule it prints
  `0.000001` with timings and `0.000000` without.
- **Leave the rest unknown until their source exists**, rather than print a value that is
  right only sometimes. Each has a task:
  - `url.<part>`, `urle.<part>`: BL-304, after BL-292's `CurlUrl`, because a part must be
    split as curl's URL API splits it.
  - `referer`, `filename_effective`, `conn_id`, `xfer_id`: BL-305, constructor inputs from
    `Curl.Console` once BL-235 wires `-w` there.
  - `proxy_used`: BL-302, a `TransferReport` member set by the handler, since proxies are
    parsed and `0` would be wrong through one.
  - `certs`, `num_certs`: BL-303, from the TLS handshake, since curl prints the chain for
    https:// even without `--certinfo`.

## Consequences

- Six more variables print curl's bytes today; the fixed five cannot drift for file://
  and http://.
- The fixed values are a promise about the rest of the tool: adding `--retry`, FTP,
  `--tls-earlydata` or an OpenSSL-style verify result without revisiting this class would
  print a stale `0`. The class's remarks say so.
- The eight unknown groups still print curl's unknown-variable warning until their tasks
  land.
- `time_queue` never matches curl's tens of microseconds; the times cannot be compared
  byte for byte across runs anyway (ADR-0035).

## Alternatives considered

- **Print curl's value for file:// and http:// for every variable now.** `num_certs`,
  `proxy_used`, `conn_id` and `xfer_id` would then be silently wrong for https://, a
  proxy, or a second URL, which is worse than the honest warning.
- **Add constructor parameters with defaults for the command-line values.** Nothing would
  pass them until `Curl.Console` does, so the variables would print defaults that look
  measured; the wiring belongs with BL-235's.
- **Parse `url.<part>` with `System.Uri`.** It splits file URLs, user info and default
  ports differently from curl's URL API; BL-292 exists to fix exactly that.

## Amendment, 2026-09-29 (BL-664): what each variable prints now

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

The Decision above was true on 2026-09-26. Since then `--retry` has been parsed and run
(`Curl.Core.UnitLibrary/TransferRetrier.cs`), the FTP handler has been built, and every
variable left unknown has gained a source. The original text stands as the record of that
day; this is what `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` prints as of this
amendment. Every variable this ADR names is now known, so none prints curl's
unknown-variable warning.

Still fixed, on every platform:

- `ssl_verify_result` and `proxy_ssl_verify_result` print `0`, curl 8.21.0's Schannel
  value, even for a failed verification. That matches the Windows build only; BL-661
  reports the OpenSSL verify code off Windows.
- `tls_earlydata` prints `0`. `--tls-earlydata` is now parsed
  (`CommandLineOptions.TlsEarlyData`) and passed on as `TlsClientOptions.AllowEarlyData`,
  but no TLS provider applies it yet (BL-710), so no early data is sent and `0` is still
  true. BL-906, after BL-710, reports the bytes sent.
- `time_queue` is still measured from `TransferTimings.Started` to itself, as decided.

Now given a source:

- `num_retries` prints `RetryCount`, which `Curl.Console` sets to the retries
  `TransferRetrier` announced for the transfer; `0` when it ran once (BL-513).
- `ftp_entry_path` prints `TransferReport.FtpEntryPath`, the directory the FTP server's
  `257` reply to `PWD` names; nothing, and `null` in `%{json}`, for a transfer that is not
  FTP or whose reply named none (BL-514).
- `url.<part>` and `urle.<part>` are read with `CurlUrl` from the URL as given and from
  the effective URL; a missing part or an unparsable URL prints nothing (BL-304).
- `referer`, `filename_effective`, `conn_id` and `xfer_id` are set by `Curl.Console`
  through the `Referer`, `OutputFileName`, `ConnectionId` and `TransferId` properties
  (BL-305); `conn_id` is `-1` when no connection was used.
- `proxy_used` prints `1` when `TransferReport.UsedProxy` is set, else `0` (BL-302,
  ADR-0058).
- `certs` and `num_certs` print and count `TransferReport.PeerCertificates`, the chain
  the TLS provider captured; nothing and `0` without TLS (BL-303, ADR-0054).

The warning in Consequences still holds for the three fixed values: BL-661 and BL-906
must replace the constant in `TransferWriteOutVariables` when they land.

## Amendment, 2026-10-01 (BL-906): where `tls_earlydata` comes from

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

`tls_earlydata` is no longer a constant. It prints
`TransferWriteOutVariables.TlsEarlyDataSent`, set by the caller and `0` by default. Its
source is a new `ITransferEvents.ReportTlsEarlyData(long bytes)` (default: does nothing),
which `HandBuiltTlsProvider` reports once its deferred `--tls-earlydata` handshake
(BL-1105, ADR-0337) completes, as curl 8.21.0's `lib/vtls/openssl.c` calls
`Curl_pgrsEarlyData`:

- the bytes sent as 0-RTT early data when the server accepted them;
- their negation when it rejected them (`CURLINFO_EARLYDATA_SENT_T`'s documented
  "negative when the sent data was rejected");
- nothing for an HTTPS proxy's connection, nor without early data, so `0` stays.

Measured 2026-10-01 with `Record-CurlExchange.ps1 -Tls`: curl 8.21.0's Schannel build
refuses `--ssl-sessions` ("the installed libcurl version does not support this", exit 2)
and printed `0|0|` for `-sk --tls-earlydata -w "%{tls_earlydata}|"` over two transfers, so
`0` is the Windows answer. No OpenSSL-build curl was available, so the OpenSSL value is
pinned from the curl-8_21_0 source and documentation above.

`ConnectionOpenedCapturingTransferEvents` (Abstractions) and
`HandshakeCapturingTransferEvents` (Networking) pass the event on. `Curl.Console`'s own
event decorators and `RunningTransferState`, which must carry it to
`TransferWriteOutVariables` as BL-661 carries the verify result, were held by another lane
when this landed; BL-1150 finishes that wiring, and until it does `curl -w` still prints
`0`. With `ssl_verify_result` and `proxy_ssl_verify_result` sourced by BL-661 and this, only
`time_queue` remains fixed.
