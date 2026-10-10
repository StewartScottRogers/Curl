---
id: BL-1954
title: Wire SSH user authentication into the upstream case runner and screening
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1953]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1954 — Wire SSH user authentication into the upstream case runner and screening

## Goal

The upstream case runner routes %SSHPORT to the SSH stand-in and gives %USER, %SFTP_PWD, %SCP_PWD, %SSHSRVMD5 and %SSHSRVSHA256 values, so SSH cases that stop at authentication or a failed login are measured, not skipped.

## Context

Split from BL-1916 (cost cap). The runner half of the code is in stash 51b9f0bb6 ("darkfactory BL-1916 20261009-213949"); apply it by hash (`git stash apply 51b9f0bb6`), never pop, and keep only its Curl.Conformance.UnitLibrary and UnitTests changes (the stand-in classes are BL-1953). That work: UpstreamSshServer (injected, routes port 9003), %SSHPORT 9003, %USER, %SFTP_PWD and %SCP_PWD empty, %SSHSRVMD5/%SSHSRVSHA256 from the host key, the client key files in %LOGDIR/server/; screening lets sftp/scp cases run only when the expected exit code is 2, 60 or 67 and otherwise skips naming BL-1917/BL-1918; UpstreamConformanceTests passes the server, and SshAuthenticationCase_RunThroughCurl_IsMeasuredNotSkipped (606, 607, 628, 629, 630, 631, 656). Read BL-1916's Notes and Curl.Conformance.UnitLibrary\CLAUDE.md first. Cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData; never read a cache under %LOCALAPPDATA%\Curl\gap.

## Acceptance criteria

- [ ] SshAuthenticationCase_RunThroughCurl_IsMeasuredNotSkipped shows at least 5 of cases 606, 607, 628, 629, 630, 631, 656 measured (Passed or a real Curl difference), on Windows with the RSA key from BL-1953.
- [ ] A screening test in Curl.Conformance.UnitTests pins that no case gets the skip reason "the harness has no value for %USER, %SFTP_PWD, %SCP_PWD", and pins the BL-1917/BL-1918 skip reasons.
- [ ] Any case that newly passes is on the passing-cases ratchet list; failures in Curl itself are not fixed here.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for SSH.

## Notes

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
