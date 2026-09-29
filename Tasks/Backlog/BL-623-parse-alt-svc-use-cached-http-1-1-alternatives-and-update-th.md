---
id: BL-623
title: Parse --alt-svc, use cached HTTP/1.1 alternatives and update the cache from Alt-Svc
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-622, BL-878]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-623 — Parse --alt-svc, use cached HTTP/1.1 alternatives and update the cache from Alt-Svc

## Goal

`--alt-svc <file>` loads the cache, connects to a cached `h1` alternative for an HTTPS origin as curl 8.21.0 does (`h2` and `h3` alternatives are used by BL-733 once HTTP/2 and HTTP/3 run; until then this task skips them as curl skips an alternative whose HTTP version is not allowed), updates the cache from `Alt-Svc` response headers, and saves the file when the run ends; `--alt-svc ""` enables the feature without a file.

## Context

- Conformance audit 2026-09-28, row 20 (Major). Cache: BL-622.
- Connecting to an alternative means dialling a different host and port while keeping the origin's `Host` and TLS name; `--connect-to` (ADR-0079, `Curl.Networking.UnitLibrary/ConnectToMappings.cs`) already does exactly that and is the route to reuse.
- Measure `-v` lines when an alternative is used, and what the reference build does with an `h2`-only entry.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Tls -k` on two ports: a cached `h1` alternative, an `h2`-only entry, an expired entry; stdout, stderr and the saved file copied into Notes.
- [ ] `Curl.Cli.UnitTests` cover parsing; `Curl.Console.UnitTests` pin each measured case through fake connectors, file seams and `TimeProvider`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- 2026-09-29 (lane 3): moved back to Backlog on BL-878. Measuring showed the work needs
  three projects outside `touches`: `Curl.Protocol.Http.UnitLibrary` (the `Alt-Used` request
  header in curl's slot, and the `* Added alt-svc` lines, written while the head is read,
  before the `< Alt-Svc:` line), `Curl.Protocol.Abstractions.UnitLibrary` (a store the
  handler hands each `Alt-Svc` header to, like `ICookieStore`) and
  `Curl.Networking.UnitLibrary` (dialling the alternative, the `Alt-svc connecting` line and
  the `via` failure). Http and Abstractions are in BL-794's `touches` (Doing). BL-878 files
  those seams; this task then only wires `AltSvcCache` in Cli and Console.
- Wiring decided from the measurements below (for the ADR this task writes): lookup is
  `Find(H1, host, port, {H1})` for `https://` only and only when no `--connect-to` mapping
  matched (h2 and h3 are not allowed until BL-733, as curl skips a version it may not use);
  an entry whose destination is the origin itself is not used; the header is learned only
  over HTTPS, with the origin (not the alternative) as the source; the file is read before
  the first transfer and written at the end, also when it did not exist (the two comment
  lines); `--alt-svc ""` learns and uses without reading or writing a file.

### Measurements (curl 8.21.0 mingw Schannel, 2026-09-29 ~11:05 UTC)

Run from a scratch folder, `Record-CurlExchange.ps1 -Port 18443 -Tls` with
`-CurlArgs '-k','-v','--alt-svc','cache.txt',<url>`; `<exp>` is now + 1 day.

1. Cached `h1` alternative. File `h1 localhost 18499 h1 localhost 18443 "<exp>" 0 0`,
   URL `https://localhost:18499/` (nothing listens on 18499), response has
   `Alt-Svc: h1=":18443"; ma=60`. Exit 0, stdout `hi`. Request:
   `GET / HTTP/1.1` / `Host: localhost:18499` / `User-Agent: curl/8.21.0` / `Accept: */*` /
   `Alt-Used: localhost:18443`. stderr (progress lines cut):
   ```
   * Alt-svc connecting from [h1]localhost:18499 to [h1]localhost:18443
   * Host localhost:18443 was resolved.
   ...
   * Established connection to localhost (127.0.0.1 port 18443) from 127.0.0.1 port 55664 
   ...
   > Alt-Used: localhost:18443
   ...
   < HTTP/1.1 200 OK
   * Added alt-svc: localhost:18443 over h1
   < Alt-Svc: h1=":18443"; ma=60
   ...
   * Connection #0 to host localhost:18443 left intact
   ```
   Saved file (CR LF): the two comment lines, then
   `h1 localhost 18499 h1 localhost 18443 "20260929 11:06:35" 0 0` (ma=60 from the header).
2. `h2`-only entry `h1 localhost 18443 h2 localhost 18499 "<exp>" 0 0`, URL
   `https://localhost:18443/`: no `Alt-svc` line, no `Alt-Used`, served by 18443, exit 0;
   file written back unchanged after the comments.
3. `h2` entry then `h1` entry for origin 18499 (`-s -i`, no `-v`): the `h1` one is used
   (served on 18443), exit 0, both lines written back.
4. Expired entry (`"20200101 00:00:00"`): not used, file written with the comment lines only.
5. Entry pointing at the origin itself (`h1 localhost 18443 h1 localhost 18443`): not used
   (no `Alt-svc` line, no `Alt-Used`), written back.
6. Refused alternative `h1 localhost 18443 h1 localhost 18498`: exit 7, no stdout, stderr
   ```
   * Alt-svc connecting from [h1]localhost:18443 to [h1]localhost:18498
   * Host localhost:18498 was resolved.
   * IPv6: ::1
   * IPv4: 127.0.0.1
   *   Trying [::1]:18498...
   *   Trying 127.0.0.1:18498...
   * connect to ::1 port 18498 from :: port 59117 failed: Connection refused
   * connect to 127.0.0.1 port 18498 from 0.0.0.0 port 59118 failed: Connection refused
   * Failed to connect to localhost:18443 via localhost:18498 after 2234 ms: Could not connect to server
   * closing connection #0
   ```
   and the entry is written back.
7. No file, header `h2=":8443"; ma=60, h1="a.example:1", h3=":443"`: `-v` writes
   `* Added alt-svc: localhost:8443 over h2`, `... a.example:1 over h1`,
   `... localhost:443 over h3` right after `< HTTP/1.1 200 OK` and before `< Alt-Svc:`; the
   file is created with the three entries.
8. Plain `http://localhost:18443/` with an `Alt-Svc` header: no `Added` line, and the file
   is created with the comment lines only.
9. `--alt-svc ""` with two URLs: the first response's `Added alt-svc: localhost:18443 over h1`
   is learned, no file read or written; the second transfer (same origin, self entry) uses
   no alternative.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Waits on BL-878: Alt-Used, the Alt-Svc response hook and dialling an alternative need Http, Abstractions and Networking (Http and Abstractions held by BL-794)
