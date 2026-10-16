# ADR-0553 — `-w` reads the libcurl variables from a `TransferInfo`

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2319.

## Context

ADR-0506's phase 2 step 5: upstream's `src/tool_writeout.c` reads every libcurl-measured `-w`
variable with `curl_easy_getinfo`, and keeps the tool's own (`%{urlnum}`, `%{filename_effective}`,
`%{exitcode}`, `%{errormsg}`, `%{onerror}`, `%{stderr}`/`%{stdout}`, the headers) to itself.
`Curl.Output`'s `TransferWriteOutVariables` worked every variable out from the `TransferReport`
directly, and `Curl.Engine`'s `TransferInfoFromReport` (BL-2317) worked the same values out a second
time for `curl_easy_getinfo`. `-w`'s bytes must not change.

## Decision

1. `Curl.Output.UnitLibrary` references `Curl.Api.UnitLibrary` (which references no protocol library)
   for `TransferInfo`.
2. `Curl.Output`'s new `WriteOutTransferInfo.FromReport` is the one place a report becomes the
   `TransferInfo` fields `-w` reads: the texts, response and `CONNECT` codes, HTTP version, counts,
   sizes, end points, times, speeds, referer, RTSP session ID and verify results. `Curl.Engine`'s
   `TransferInfoFromReport` starts from it and adds only what no `-w` variable reads (auth bits,
   RTSP CSeqs, `Content-Length`, OS errno, proxy error, scheme and protocol, file time, `Retry-After`,
   cert info).
3. `TransferWriteOutVariables` gains a constructor taking a `TransferInfo`, the `TransferResult`, the
   given URL and the URL number, and reads every libcurl variable from the info. Its report-only
   constructor stays (its tests pin it unchanged) and builds the info with `FromReport`; its
   `Referer`, `ConnectionId`, `TransferId`, `SslVerifyResult`, `ProxySslVerifyResult` and
   `TlsEarlyDataSent` properties read and set the info's fields.
4. Still read from the report or the result, as upstream's tool reads them outside
   `curl_easy_getinfo`, or because `TransferInfo` holds them in another form: `%{size_delivered}`,
   `%{num_headers}` and the headers, `%{certs}` and `%{num_certs}` (printed from the certificates with
   `PeerCertificateText`, while `CURLINFO_CERTINFO` is filled only under `CURLOPT_CERTINFO`),
   `%{exitcode}`, `%{errormsg}`, `%{url}`, `%{urlnum}`, `%{num_retries}` and
   `%{filename_effective}`.
5. `CurlCommandRunner.WriteOutVariables` builds the info with `FromReport` and puts the run's own
   answers on it (the scheme, `Referer`, `%{conn_id}`, `%{xfer_id}`, both verify results and the early
   data bytes), for serial and `-Z` transfers alike; reading the easy handle's `Info` directly waits
   until the handle builds the attempt itself (BL-2394) and `-Z` has a handle (phase 3).
6. `TransferInfo` answers a port with no connection as `0` where libcurl answers `-1`, so `-w` prints
   `%{remote_port}` `-1` for a primary port of `0`, and `%{local_port}` the local port when there is
   a local address, else `0` when there is a primary port (curl's unconnected TFTP socket) and `-1`
   otherwise: the same bytes as before for every report a transfer makes.

## Consequences

- One report-to-info conversion instead of two, so `-w` and `curl_easy_getinfo` cannot drift apart.
- `-w` output is byte for byte unchanged: every existing `-w` test in `Curl.Output.UnitTests` and
  `Curl.Console.UnitTests` passes unchanged.
