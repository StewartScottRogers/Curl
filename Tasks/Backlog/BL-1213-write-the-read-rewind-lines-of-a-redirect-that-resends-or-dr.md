---
id: BL-1213
title: Write the [READ] rewind lines of a redirect that resends or drops a request body
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1213 — Write the [READ] rewind lines of a redirect that resends or drops a request body

## Goal

Under `--trace-config read` a `-L` hop after a request with a body writes curl 8.21.0's rewind lines, and plain `-v` writes `Need to rewind upload for next request`.

## Context

- Split from BL-1189 (ADR-0383). Measured 2026-10-02 with `Record-CurlExchange.ps1`, `-d ab -L` to `/a` answered `302`/`307` `Location: /b` (`Connection: close`): after the 302's status line `[READ] client reader needs rewind before next request` and the plain `-v` line `Need to rewind upload for next request`; then `[READ] client_reset, will rewind reader` in place of `clear readers` before `shutting down connection #0` (or `left intact`); then `[READ] client start, rewind readers` before `Issue another request to this URL`. A 307 resends the body with the buffer reader's three lines again; a 302 sends GET.
- Curl today writes none of these; `Need to rewind upload for next request` is missing even under plain `-v`.

## Acceptance criteria

- [ ] Re-measured with `Record-CurlExchange.ps1` for `-d ab -L` on a 302 and a 307, closing and kept-alive; stderr in Notes.
- [ ] Tests pin each case's lines, the `-v` line without `--trace-config read`, and no `[READ]` line without `read` or `all`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-02: Created.
