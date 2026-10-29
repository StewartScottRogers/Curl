# ADR-0970: `-v` writes `[DNS] added H:P:A to cache` and brackets an IPv6 literal host in `Established connection`

Status: Accepted. Decided by Claude under Stewart's delegation (BL-3750, CGR-221).

## Context

curl 8.22.0 (Schannel, Windows) writes `* [DNS] added foo.test:20700:127.0.0.1 to cache` for a
`--resolve` entry, where 8.18 and 8.21 wrote `Added H:P:A to DNS cache`, and writes
`Established connection to [::1] (::1 port N)` for an IPv6 literal host (measured with
`curl.exe -s -v --resolve`).

## Decision

`TcpConnector` writes the 8.22 wording and brackets an IPv6 literal host name when it reports
the connection opened. The ` (non-permanent)` suffix stays after `cache`. Linux's 8.18 still
uses the old wording; the project follows the newest reference (8.22) for both platforms
rather than splitting the text by platform.
