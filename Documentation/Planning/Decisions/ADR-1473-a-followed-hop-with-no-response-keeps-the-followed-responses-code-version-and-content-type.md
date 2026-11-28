# ADR-1473: A followed hop with no response keeps the followed response's code, version and content type

- Status: Accepted
- Date: 2026-11-28
- Decided by Claude under Stewart's delegation (BL-6095).

## Context

Under `-L`, `RedirectFollower`'s merged report takes the last hop's `TransferReport`. A hop that
fails before any response arrives has none, so Curl printed `000` for `%{http_code}` when `-m`
ended it, and an empty `%{content_type}` and `%{http_version}` `0` when `-m` ended it or its
connection was refused.

curl 8.21.0 (`C:\Program Files\Git\ucrt64\bin\curl.exe`, measured with
`Record-CurlExchange.ps1`: a 302 carrying `Content-Type: text/x-first` to a listener that never
answers, with `-sS -L -m 2`, and to a port nothing listens on) prints `302`, `1.1` and
`text/x-first` in both cases, an empty `%header{...}` and `%{header_json}` `{}` for the first
hop's headers, and `%{num_headers}` `4`, the first response's header count.

## Decision

When the chain ends in failure and the last hop's report has no HTTP version, `RedirectFollower`
gives the merged report the followed response's HTTP version, and its status code and content type
where the hop reported none. curl overwrites those only when a response arrives. A status code the
hop's report already carries stays: `Curl.Engine` puts the earlier hop's on a hop that never
connected.

A successful hop with no HTTP response is left as it is: it was not measured.

## Consequences

`%header{...}` and `%{header_json}` stay empty, as curl's are. `%{num_headers}` still prints `0`
where curl prints the followed response's count: `TransferReport` has no property that counts
headers without naming their source, so that is a task of its own.
