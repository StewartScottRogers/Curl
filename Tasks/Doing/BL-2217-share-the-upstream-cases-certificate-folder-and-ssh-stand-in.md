---
id: BL-2217
title: Share the upstream cases' certificate folder and SSH stand-in construction so the gap tool can run HTTPS, FTPS, SFTP and SCP cases
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.Conformance.SshServer.UnitLibrary, Curl.Conformance.SshServer.UnitTests]
requirement: none
created: 2026-10-14
completed:
---
# BL-2217 — Share the upstream cases' certificate folder and SSH stand-in construction so the gap tool can run HTTPS, FTPS, SFTP and SCP cases

## Goal

Move the construction of the test certificate folder and of the SSH stand-in out of `UpstreamConformanceTests` into public library code, so the gap tool can run the HTTPS, FTPS, SMTPS, POP3S, IMAPS, SFTP and SCP cases with a few lines.

## Context

Source: Stewart's 2026-10-14 request to raise the gap office's behaviour score (aim 97-99%) by making the shared in-process upstream-case harness run cases it leaves unmeasured. Counts are from the 8.22.0 gap run 2026-10-13_1406 (150 unmeasured behaviour cases).

Finding: `needs-server:sftp` (38), `scp` (14), `https` (16), `ftps` (8), `smtps`, `pop3s`, `imaps` (1 each) are not missing stand-ins. `Gap/Tools/ConvertTo-BehaviourMeasurement.ps1` labels any skipped case `needs-server:<first server not http>`, and `Gap/Tools/Measure-UpstreamCases.cs` calls `new UpstreamCaseRunner(RunCurlAsync, platform, TimeProvider.System, timeLimit)` and `runner.RunAsync(number, testFile, logDirectory)` with no `UpstreamSshServer` and no `certificateDirectory`, so every such case skips for `%SSHPORT` or `%CERTDIR` / `%HTTPSPORT`. The stand-ins exist (`SshServerConnector`, `HttpsServerConnector`, `FtpsServerConnector`, `MailTlsServerConnector`) and `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs` runs them: it builds the `UpstreamSshServer` (lines ~356-370: RSA client key and RSA host key fingerprints on Windows, Ed25519 elsewhere) and `GenerateCertificates()` (~line 476, `UpstreamTestCertificateGenerator.Generate(<tests>/certs, <out>/certs)`).

The tool lives under `Gap/`, which a dark factory lane may not edit, so this task only supplies the shared code; an interactive session then makes the small edit to the tool (pass `sshServer`, `certificateDirectory`, and the release's `certs` folder) and moves `$HarnessServers` in the converter. Do not touch `Gap/`.

Where to start: `Curl.Conformance.UnitLibrary` may not reference `Curl.Protocol.Ssh.UnitLibrary`, but `Curl.Conformance.SshServer.UnitLibrary` already does and can reference `Curl.Conformance.UnitLibrary` (check the project graph has no cycle). Add there a public factory (for example `UpstreamSshServerFactory.Create(bool windowsBuildOfCurl)`) returning the `UpstreamSshServer`; add to `Curl.Conformance.UnitLibrary` a public helper (for example `UpstreamCertificateFolder.Prepare(string certsSourceDirectory, string outputDirectory)`) wrapping `UpstreamTestCertificateGenerator` that returns the folder to pass as `certificateDirectory`, generating once and reusing. Then make `UpstreamConformanceTests` use both.

## Acceptance criteria

- [ ] A public factory in `Curl.Conformance.SshServer.UnitLibrary` returns the `UpstreamSshServer` that `UpstreamConformanceTests` built inline; the inline construction is gone and `UpstreamConformanceTests` still passes the same cases.
- [ ] A public helper in `Curl.Conformance.UnitLibrary` generates (or reuses) the certificate folder from a `certs` source folder and returns the folder to pass as `certificateDirectory`; `GenerateCertificates()` in `UpstreamConformanceTests` calls it.
- [ ] Unit tests for both in `Curl.Conformance.UnitTests` / `Curl.Conformance.SshServer.UnitTests`, platform-neutral (the Windows RSA / elsewhere Ed25519 choice is a parameter tested both ways).
- [ ] `Curl.Conformance.UnitLibrary/CLAUDE.md` and `Curl.Conformance.SshServer.UnitLibrary/CLAUDE.md` name the two entry points and say they exist for the gap tool.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` (and the SshServer library) shows no member outside a gate; `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] Notes records the exact edit the interactive session must make to `Gap/Tools/Measure-UpstreamCases.cs` and `ConvertTo-BehaviourMeasurement.ps1` (about 80 cases: sftp 38, scp 14, https 16, ftps 8, smtps/pop3s/imaps 3).

## Notes

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
