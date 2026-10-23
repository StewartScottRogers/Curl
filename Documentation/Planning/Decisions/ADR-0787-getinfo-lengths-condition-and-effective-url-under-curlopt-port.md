# ADR-0787: getinfo's download length, condition unmet, file:// file time and effective URL under CURLOPT_PORT

- Status: Accepted
- Date: 2026-10-23
- Decision: Decided by Claude under Stewart's delegation (BL-3194)
- Amends: [ADR-0672](ADR-0672-curlopt-port-rewrites-the-transfer-url.md) (the effective URL part)

## Context

Stewart's Libcurl conformance report 23 (2026-10-22) measured libcurl 8.22.0 against Curl's
`curl_easy_getinfo` answers and found four differences: `CURLINFO_CONDITION_UNMET` was 0 for a
plain 304; `CURLINFO_CONTENT_LENGTH_DOWNLOAD_T` took the last `Content-Length` whatever else the
response said; `CURLINFO_EFFECTIVE_URL` gained the `CURLOPT_PORT` port; and a `file://`
transfer reported no file time and no length. The curl tool has no `CURLOPT_PORT` and no
`CONDITION_UNMET` output, so `Record-CurlExchange.ps1` cannot measure these. The report's
values are the measurement.

## Decision

`TransferInfoFromReport` answers:

- `CONDITION_UNMET` 1 for every 304 response, a time condition set or not, and still for an
  unmet `-z` the handler reports.
- `CONTENT_LENGTH_DOWNLOAD_T` -1 when `CURLOPT_FAILONERROR` stopped the transfer (exit 22), when
  the response is chunked (`Transfer-Encoding` naming `chunked`, so a conflicting
  `Content-Length` is ignored), and when `Content-Length` lines differ. It is 0 for a 204 and
  for a 304 that sends a length; a 304 with no length stays -1.
- For a `file://` URL, the length the `-I` pseudo-headers carry, else the bytes a successful
  transfer moved (libcurl's `file_do` sets the download size from the file's size), and -1
  after a failure. `CURLINFO_FILETIME` under `CURLOPT_FILETIME` comes from the result's source
  time whether or not the handler made a report, because the file handler makes none for a plain
  download.

`UrlEffective.WithoutPortOverride` gives back the URL as set (normalised) in place of the
effective URL when the effective URL is the URL `CURLOPT_PORT` rewrote, the URL came from
`CURLOPT_URL` and the transfer followed no redirect. In upstream's `lib/url.c`, `state.url` is
fixed in `parseurlandfillconn` before `parse_remote_port` sets the port on the URL handle. A
`CURLOPT_CURLU` URL keeps the port, as upstream test1906 pins.

## Consequences

ADR-0672's "the effective URL carries it" now holds only for a `CURLOPT_CURLU` URL.
`ManagedEasyTransferTests.PerformAsync_PortOptionOverAUrlWithNoPort_ConnectsToItAndSendsItInTheHostHeader`
now expects `http://127.0.0.1/522`. A 204 with no `Content-Length` answers 0, the report's
unqualified value.
