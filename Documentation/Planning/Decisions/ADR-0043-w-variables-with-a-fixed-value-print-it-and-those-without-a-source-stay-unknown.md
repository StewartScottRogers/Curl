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
