# ADR-0767 — An empty port in a URL without a scheme leaves it without one, so it is rejected

- Status: Accepted
- Date: 2026-10-22
- Task: BL-3175
- Decided by Claude under Stewart's delegation.

## Context

curl 8.22.0 (Schannel, measured with `Record-CurlExchange.ps1` on Windows, 2026-10-22) exits 3 with
`curl: (3) URL rejected: Port number was not a decimal number between 0 and 65535` for `http:`,
`1http://x/`, `127.0.0.1:` and `127.0.0.1:/`, and on Windows for `h://x/` (a drive prefix, not a
scheme), yet connects to port 80 for `http://127.0.0.1:/`. Its `Curl_parse_port` accepts an empty
port only when the URL named its own scheme. Curl prepended the guessed scheme before parsing, so the
parser saw `http://http:`, accepted the empty port and connected.

## Decision

`UrlSchemeGuesser.AddScheme` leaves a URL without a scheme when its authority (read as one with no
scheme, after the last `@`) ends in `:` and the scheme it would add is not `file`. The transfer URL
parser then reads it with no scheme and rejects the empty port with `CurlUrlRejection.BadPortNumber`,
as curl does, before any handler is called. A `file` scheme (from `--proto-default file`) is still
added, since a file URL has no port (upstream test1146's `Z:/dir/file`).

## Consequences

The runner needs no change: its existing parse of the transfer URL reports the rejection. A rejected
URL keeps the text as typed, which `%{url_effective}` prints for a URL curl rejects anyway.
