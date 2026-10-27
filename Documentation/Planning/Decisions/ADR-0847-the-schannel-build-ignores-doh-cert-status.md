# ADR-0847: The Schannel build ignores --doh-cert-status, as it ignores --cert-status

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3683)
- Relates to: ADR-0795, ADR-0152, CGR-154 of the 2026-10-25 curl gap report in `Documentation/Reviews`

## Context

ADR-0795 found that curl 8.22.0's Schannel build ignores `--cert-status`, so `TlsClientOptionsMapping.Of`
asks for the stapled certificate status only outside Windows. `TlsClientOptionsMapping.DohOf` still copied
`--doh-cert-status` onto every DoH handshake, so on Windows a DoH server that staples no OCSP response failed
every lookup with exit 6 (`Could not resolve host`). Measured 2026-10-26 on Windows with
`Record-CurlExchange.ps1 -DohPort 21801 -Port 21802` and
`curl -sS --doh-insecure --doh-cert-status --doh-url https://localhost:21801/dns-query http://foo.test:21802/x`:
the reference curl (ucrt64 curl 8.22.0) has both DoH queries (A and AAAA) answered and connects to the target.

## Decision

`DohOf` sets `RequireCertificateStatus` through the same `RequiresCertificateStatus(verifyStatus,
matchesSchannelBuild)` that `Of` uses: on for `--doh-cert-status` outside Windows, off on Windows. The
OpenSSL build keeps asking for and checking the stapled status on the DoH handshake.

## Consequences

On Windows `--doh-cert-status` changes nothing, as on the reference; Linux and macOS are unchanged.
`Cgr154Tests` pins each platform's answer.
