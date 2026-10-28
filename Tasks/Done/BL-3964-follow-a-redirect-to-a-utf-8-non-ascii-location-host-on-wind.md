---
id: BL-3964
title: Follow a redirect to a UTF-8 non-ASCII Location host on Windows as curl does (upstream test1448, CGR-059)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3963]
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3964 — Follow a redirect to a UTF-8 non-ASCII Location host on Windows as curl does (upstream test1448, CGR-059)

## Goal

On Windows, a redirect whose `Location` host is typed as UTF-8 (upstream test1448, `åäö.se`) is followed or refused exactly as real curl 8.22.0 does.

## Context

Second part of BL-3953 (CGR-059). After BL-3963 makes `CurlUrlHost` read a host as Windows-1252 on Windows, upstream test1448 (redirect to a UTF-8 IDN `åäö.se`) no longer follows the redirect. Real curl sees the raw UTF-8 bytes of a `Location` host, so first measure real Windows curl on test1448 with Record-CurlExchange.ps1, then decide where a `Location` host enters `CurlUrlHost` and which reading it gets. Keep or hold the case in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` per platform, as the measurement says.

## Acceptance criteria

- [x] Real curl and Curl, run on test1448 and one more `Location` host with a non-ASCII byte, give the same exit code, stdout bytes, stderr text and request bytes on Windows; the commands and results are recorded in Notes.
- [x] A focused unit test in `Curl.Protocol.Abstractions.UnitTests` pins how a `Location` host is read and fails before the fix.
- [x] The test1448 line in `PassingUpstreamCases.txt` is right for the measured answer on each platform, and the ratchet check passes.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3953, which went over its cost cap.
- BL-3963 took test1448 off `PassingUpstreamCases.txt` (it failed on Windows once a host is read as Windows-1252), so CI stays green until this task lands; put it back for the platforms the measurement says. Measured by BL-3963: real Windows curl 8.22.0 ignores `LC_ALL=C.UTF-8` (`%C3%BC` still gives `xn--14-ria7423a`), so the test's `<setenv>` does not make it read UTF-8.
- Continues the stashed work of BL-3953.

- Measured (Record-CurlExchange.ps1, -s -S -i -L, reference curl 8.22.0 mingw Schannel vs Curl.Console curl.exe, both identical in exit code, stderr, stdout.bin and request.bin hashes):
  - (a) test1448 command, URL host as UTF-8 bytes C3 A5 C3 A4 C3 B6 in ANSI argv, `--resolve xn--4cab6c.se:18466:127.0.0.1 --connect-to 127.0.0.1:18466:127.0.0.1:18466`: exit 6 `curl: (6) Could not resolve host: xn--bbab6cyjbb.se`, nothing sent.
  - (b) URL host typed åäö (E5 E4 F6), Location `http://åäö.se:18466/14480001`: first request `Host: xn--4cab6c.se:18466`, then exit 6 `Could not resolve host: xn--bbab6cyjbb.se`.
  - (c) Location `http://�.se:18467/2`, `--resolve xn--tda.se:18467:127.0.0.1`: exit 0, second request `Host: xn--tda.se:18467`.
- Decision (ADR-0949): real Windows curl does not pass test1448 (it reads the Location bytes as Windows-1252), and Curl already matches it after BL-3963, so no production change. The ratchet list has no per-platform form and a listed case must pass everywhere, so 1448 stays off `PassingUpstreamCases.txt` (unlisted pass on Linux/macOS, unlisted failure on Windows); touches on CurlUrlHost.cs and the list left unchanged.
- Unit tests `CurlUrlTests.Parse_LocationHostOfUtf8Bytes_*` pin each platform reading (OSCondition). The Windows one fails before BL-3963 (15190cb15), when the host was read as UTF-8 and test1448 passed on Windows with xn--4cab6c.se. Platform-neutral otherwise; fast tests green on Windows; CI covers Linux/macOS.
- The stash (BL-3953 work) was already landed by BL-3963; nothing carried over.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 36ff046c 425054c3 eb77ae41 779790c0.
- 2026-10-28: Doing -> Done. A redirect to a UTF-8 Location host on Windows fails exit 6 on xn--bbab6cyjbb.se exactly as curl 8.22.0 does; test1448 stays off the ratchet (ADR-0949)
