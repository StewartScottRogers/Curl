---
id: BL-149
title: Print the exit 60 sslcerts help block after a TLS verification failure in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-064]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-149 — Print the exit 60 sslcerts help block after a TLS verification failure in Curl.Console

## Goal

After the `curl: (60) <message>` line, `Curl.Console` writes to standard error the five-line help block ADR-0009 records for every exit 60 in both the Schannel and the OpenSSL build, byte for byte, and writes nothing extra after any other TLS exit.

## Context

- ADR-0009 (`Documentation/Planning/Decisions/ADR-0009-tls-behaviour-matches-the-platforms-usual-curl-build.md`) measured curl 8.21.0 (Schannel, Windows) and the OpenSSL build with `-sS`: every exit 60 (`CurlExitCode.PeerFailedVerification`) is followed by these five lines, identical in both builds:
  1. `More details here: https://curl.se/docs/sslcerts.html`
  2. (a blank line)
  3. `curl failed to verify the legitimacy of the server and therefore could not`
  4. `establish a secure connection to it. To learn more about this situation and`
  5. `how to fix it, please visit the webpage mentioned above.`
  Each line ends with `Environment.NewLine`: CRLF on Windows, LF elsewhere, as each build writes it.
- BL-064 put the TLS failure text in `Curl.Networking.UnitLibrary/TlsFailureMessages.cs`. `ConnectResult.ErrorMessage` carries only the one line after `curl: (NN) `. The help block was left out on purpose because curl's command-line tool prints it, not libcurl (see the `<remarks>` on `TlsFailureMessages`). So it belongs in `Curl.Console`.
- Where to start: `Curl.Console/CurlCommandRunner.cs`, `TransferAllAsync`. That method writes `FormatErrorLine(result)` through `WriteErrorLineAsync` when `ShowsErrors(options)` is true, meaning not `-s`, or `-s` together with `-S`. The help block goes directly after that line when `result.ExitCode == CurlExitCode.PeerFailedVerification`. `CurlExitCode` is in `Curl.Protocol.Abstractions.UnitLibrary/CurlExitCode.cs` (60 `PeerFailedVerification`, 35 `SslConnectError`, 77 `SslCacertBadfile`).
- The ADR measured only `-sS`. Before writing the silent-mode test, find out whether `-s` alone suppresses the help block along with the error line. Check curl 8.21.0's `src/tool_operate.c` (where `CURL_CA_CERT_ERRORMSG` is written, and whether that write sits under the same `showerror` check as the `curl: (%d) %s` line), or measure it with the reference binary if one is available. Put the source line or the measurement in Notes. Do not guess.
- Out of scope: BL-072 already covers printing `SslStreamTlsProvider.Warnings`. Do not print them here.
- Tests go in `Curl.Console.UnitTests`, for example a new `CurlCommandRunnerTlsFailureTests.cs`. Drive failures through the existing test doubles (`ScriptedConnector`, `RecordingConnector`) so that no socket is opened.

## Acceptance criteria

- [ ] A named test in `Curl.Console.UnitTests` asserts that after a transfer ending in `CurlExitCode.PeerFailedVerification`, standard error is exactly `curl: (60) <message>` followed by the five help-block lines above, each ended with `Environment.NewLine`, and nothing more.
- [ ] Named tests assert that after `CurlExitCode.SslConnectError` (35) and `CurlExitCode.SslCacertBadfile` (77), standard error holds only the `curl: (NN) <message>` line, with no help block.
- [ ] A named test pins the `-s` without `-S` behaviour that upstream shows (help block suppressed, or printed), and Notes cite the curl 8.21.0 source line or measurement it rests on.
- [ ] A named test asserts that with `-sS` the help block is printed after exit 60.
- [ ] No new test carries `TestCategory=Integration`.
- [ ] `dotnet build Curl.Console -warnaserror` is clean and `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes, including 100% line and branch coverage of the new code.

## Notes

- Filed as a follow-up to BL-064.

## Log

- 2026-09-26: Created.
