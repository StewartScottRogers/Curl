# ADR-0699: A library easy handle keeps the authentication scheme its last perform picked

- Status: Accepted
- Date: 2026-10-18
- Task: BL-2709
- Decided by Claude under Stewart's delegation.

## Context

libcurl 8.22.0 keeps `data->state.authhost.picked` on the easy handle: `Curl_pretransfer` resets only
`want` to `CURLOPT_HTTPAUTH`, and `Curl_http_output_auth` sets `picked` from `want` only when none is
picked. So a second `curl_easy_perform` on one handle reports `CURLINFO_HTTPAUTH_USED` as the scheme
the first picked even when it gets a 200 with no challenge (upstream test694, NTLM), and a kept Basic
is sent before any challenge. `curl_easy_reset` clears it; `curl_easy_duphandle` starts without it.

The task's context suggested masking the kept scheme by the next perform's wanted schemes.

## Decision

`ManagedEasyTransfer` keeps the last perform's `TransferReport.AuthSchemePicked` (unchanged through a
perform that ends with no result or report) and gives it to its own `TransferContextFactory` as
`KeptAuthSchemePicked`, which every context's `HttpRequestOptions.AuthSchemePicked` starts from;
`Reset` clears it and `Duplicate` starts with none. It is not masked: libcurl does not mask `picked`,
and the HTTP handler's `AllowedSchemesOf` already sends a kept scheme only when it is one the transfer
allows, and never sends NTLM or Negotiate before a challenge. The curl tool's runner, which builds its
own factory per transfer, is unchanged.

## Consequences

Upstream case 694 passes. A library handle that picked Basic sends Basic on its next perform's first
request, as libcurl does; the whole upstream ratchet stays green with it.
