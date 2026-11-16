---
id: BL-5292
title: Save a cookie whose Domain equals the host as a domain cookie
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cookies.UnitLibrary/SetCookieParser.cs, Curl.Cookies.UnitLibrary/NetscapeCookieFile.cs, Curl.Cookies.UnitTests/DomainEqualsHostCookieTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5292 — Save a cookie whose Domain equals the host as a domain cookie

## Goal

A cookie set with a `Domain` attribute equal to the request host is stored as a domain cookie and written to the jar with a leading dot and the include-subdomains flag `TRUE`, as upstream curl 8.21.0 does, so upstream test cases 798 and 1136 pass with the port's built `curl.exe`.

## Context

Filed from Surl's Phase 7 measurement of the port (Stewart approved filing on the port's board, 2026-11-16): Surl `Documentation/Conformance/CurlPort/Port-Board-Handoff.md`, entry 15 (github.com/StewartScottRogers/Surl). The port was measured at commit `b42c319ba940f66452b2b6f3e9910d46825b08fe` (port `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7`), on Windows x64, 2026-11-06, beside upstream curl 8.21.0, the oracle: where the two differ on the same upstream test case, the port is the one in the wrong.

- **Finding:** Surl `Documentation/Conformance/CurlPort/Upstream-Test-Cases.md`, UTC-1.
- **Cases (2):** `test798` "HTTP cookies in a folded header", `test1136` "Check cookies against PSL" (upstream tag `curl-8_21_0`), played by Surl's `CurlPortBesideUpstreamCurlOverUpstreamTestCasesTests`, Surl commit `7bf55f26a8eecda5f936db7e32f596770ee7adf5`.
- **Command lines:** `curl http://localhost:<port>/we/want/798 -b none -c log/jar798.txt`; `curl http://www.example.ck/1136 http://www.ck/1136 http://z-1.compute-1.amazonaws.com/1136 -b none -c log/jar1136.txt -x 127.0.0.1:<port>`.
- **Upstream curl 8.21.0 (correct):** reference build, SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`: both cases pass; jar lines `#HttpOnly_.localhost	TRUE	/p4	TRUE	0	flavor	tasty` and `.z-1.compute-1.amazonaws.com	TRUE	/	FALSE	0	test5	forbidden5`.
- **Port (defect):** `<file>` fails at jar line 5: `#HttpOnly_localhost	FALSE	/p4	...` and `z-1.compute-1.amazonaws.com	FALSE	/	...` (no leading dot, flag `FALSE`).
- **What decides:** RFC 6265 section 5.3 step 6; curl 8.21.0 `lib/cookie.c` (`tailmatch` set whenever a `domain=` is accepted; `get_netscape_format`); the cases' `<file>` sections.
- **Where to start:** `Curl.Cookies.UnitLibrary/SetCookieParser.cs`, `NetscapeCookieFile.cs`.
- **Ratchet puzzle:** 798 and 1136 are already listed in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` (since October), yet the built `curl.exe` at `b42c319` fails their `<file>` check when Surl plays them. Find out why the port's runner does not see this (for example, an in-process path that differs from the executable, or a `<file>` comparison that is skipped or normalised) and record it in Notes; if the runner itself is wrong, file a follow-up task for it.

## Acceptance criteria

- [ ] A port unit test shows a cookie with `domain=<the host>` written as `.<host>	TRUE	...`.
- [ ] The built `curl.exe` writes the `<file>` sections of upstream cases 798 and 1136 byte for byte, shown by the commands above against a test server and recorded in Notes.
- [ ] Notes say why the port's own runner listed 798 and 1136 as passing while the built executable failed them.

## Notes

## Log

- 2026-11-16: Created.
