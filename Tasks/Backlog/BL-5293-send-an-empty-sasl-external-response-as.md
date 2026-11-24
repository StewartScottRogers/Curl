---
id: BL-5293
title: Send an empty SASL EXTERNAL response as =
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary/SaslAuthenticator.cs, Curl.Authentication.UnitTests/SaslExternalEmptyResponseTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5293 — Send an empty SASL EXTERNAL response as =

## Goal

With `AUTH=EXTERNAL` and no user name, the port answers the server's empty challenge with the line `=` over IMAP, POP3 and SMTP, as upstream curl 8.21.0 does, so upstream test cases 838, 884 and 943 pass with the port's built `curl.exe`.

## Context

Filed from Surl's Phase 7 measurement of the port (Stewart approved filing on the port's board, 2026-11-16): Surl `Documentation/Conformance/CurlPort/Port-Board-Handoff.md`, entry 16 (github.com/StewartScottRogers/Surl). The port was measured at commit `b42c319ba940f66452b2b6f3e9910d46825b08fe` (port `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7`), on Windows x64, 2026-11-06, beside upstream curl 8.21.0, the oracle: where the two differ on the same upstream test case, the port is the one in the wrong.

- **Finding:** Surl `Documentation/Conformance/CurlPort/Upstream-Test-Cases.md`, UTC-2.
- **Cases (3):** `test838` (IMAP), `test884` (POP3), `test943` (SMTP), "external authentication without credentials", played by Surl's `CurlPortBesideUpstreamCurlOverUpstreamTestCasesTests`, Surl commit `7bf55f26a8eecda5f936db7e32f596770ee7adf5`.
- **Command lines:** `curl 'imap://;AUTH=EXTERNAL@127.0.0.1:<port>/838/;MAILINDEX=1'`; `curl 'pop3://;AUTH=EXTERNAL@127.0.0.1:<port>/884'`; `curl 'smtp://;AUTH=EXTERNAL@127.0.0.1:<port>/943' --mail-rcpt recipient@example.com --mail-from sender@example.com -T -`.
- **Upstream curl 8.21.0 (correct):** reference build, SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`: sends `=` as the third protocol line and exits 0; all three cases pass.
- **Port (defect):** sends an empty line (`\r\n`) where `=\r\n` is expected (POP3, SMTP `<protocol>` line 3); over IMAP the server half answers only `=`, so the port waits until the case's 60-second limit.
- **What decides:** RFC 4422 section 4; RFC 4954 section 4, RFC 5034 section 4, RFC 4959 section 3; curl 8.21.0 `lib/curl_sasl.c` (`build_message` with an empty buffer sends `=`).
- **Where to start:** `Curl.Authentication.UnitLibrary/SaslAuthenticator.cs`.
- **Ratchet puzzle:** 838, 884 and 943 are already listed in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, yet the built `curl.exe` at `b42c319` fails them when Surl plays them. Find out why the port's runner does not see this and record it in Notes; if the runner itself is wrong, file a follow-up task for it.

## Acceptance criteria

- [ ] A port unit test shows the SASL EXTERNAL response with no user name encoded as `=`.
- [ ] The built `curl.exe` sends `=\r\n` as the third protocol line for the three command lines above against a test server, recorded in Notes.
- [ ] Notes say why the port's own runner listed 838, 884 and 943 as passing while the built executable failed them.

## Notes

Shares `SaslAuthenticator.cs` with BL-5281, so the two run one after the other.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: e7bd11d6 5df6465a 668b5e4d.

## Notes

- Decided not to change the code (ADR-1279, decided by Claude under Stewart's delegation). The platform's curl here is 8.22.0 (`curl --version`), and ADR-0928 measured it sending an empty line for LOGIN's empty answer; 8.22.0 changed test838/884/943 the same way (GF-0038). The port's `Encode*DeferredInitialResponse` and its tests pin the empty line on purpose.
- Why the runner listed 838, 884, 943 as passing: its upstream test data is 8.22.0's, whose expected `<protocol>` is the empty line. Surl's oracle plays 8.21.0's data, which expects `=`. The runner is not wrong; the oracle versions differ.
- Acceptance criteria 1 and 2 ask for `=`, contradicting the measured platform curl, so they are not met by design. Surl should move its oracle to curl 8.22.0 for these cases.
- 2026-11-16: Doing -> Deferred. Decided against: platform curl 8.22.0 sends the empty line, ADR-1279; Surl's 8.21.0 oracle differs
- 2026-11-18: Deferred -> Backlog. Stewart 2026-11-18: clear Deferred; returned to Backlog
- 2026-11-18: Backlog -> Doing. Criteria at claim: e7bd11d6 5df6465a 668b5e4d.
- 2026-11-18: Doing -> Blocked. Stewart: criteria 1 and 2 demand '=' but platform curl 8.22.0 sends an empty line (ADR-1279); approve rewording the criteria to 8.22.0 behaviour or drop the task?
- 2026-11-18: Blocked -> Deferred. Stewart 2026-11-18: closed as not a defect, curl 8.22.0 sends an empty line for an empty SASL EXTERNAL response (ADR-1279); do not reopen unless the target version moves
- 2026-11-24: Deferred -> Backlog. Undeferred by Stewart 2026-11-24 to give the idle lanes work.
