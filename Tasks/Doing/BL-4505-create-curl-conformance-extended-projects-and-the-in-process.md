---
id: BL-4505
title: Create Curl.Conformance.Extended projects and the in-process curl harness with TCP and UDP routes and a scripted reference server
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitLibrary/Harness, Curl.Conformance.Extended.UnitLibrary/Curl.Conformance.Extended.UnitLibrary.csproj, Curl.Conformance.Extended.UnitTests/Harness, Curl.Conformance.Extended.UnitTests/Curl.Conformance.Extended.UnitTests.csproj, Curl.slnx, Curl.Console/Curl.Console.csproj, Curl.Mcp/Curl.Mcp.csproj, Curl.Mcp.UnitLibrary/Curl.Mcp.UnitLibrary.csproj]
requirement: none
created: 2026-11-02
completed:
---
# BL-4505 — Create Curl.Conformance.Extended projects and the in-process curl harness with TCP and UDP routes and a scripted reference server

## Goal

The projects Curl.Conformance.Extended.UnitLibrary and Curl.Conformance.Extended.UnitTests exist and run a real curl command line in process against in-memory reference servers over TCP and UDP routes, reporting exit code, stdout, stderr and each side's wire bytes.

## Context

Split from BL-4501 (over its cost cap; BL-4501 is in Deferred). Read BL-4501 and BL-4462 (Deferred) for the plan. Reuse Curl.Conformance.UnitLibrary and Curl.Console/InProcessCurl.cs. The UnitLibrary references no extension (VerifyExtensionIsolation); do not change Directory.Build.props; no package; tests are platform-neutral. Use the new-project skill. ExtendedConformanceHarness.RunAsync takes the curl entry point as a delegate because the UnitLibrary cannot reference Curl.Console.

## Acceptance criteria

- [ ] Both projects exist and sit together in one /Conformance/Extended/ folder in Curl.slnx; the UnitLibrary references no Curl.Extension.* or Curl.Extended.Protocol.* project; Directory.Build.props is unchanged.
- [ ] Curl.Conformance.Extended.UnitTests is in the InternalsVisibleTo lists of Curl.Console, Curl.Mcp and Curl.Mcp.UnitLibrary.
- [ ] A test in Curl.Conformance.Extended.UnitTests/Harness runs a real curl command line in process (redis:// against the scripted server) and asserts exit code, stdout, stderr and the client's wire bytes.
- [ ] Tests in Harness cover a TCP route, a UDP route (tftp) and CURL_EXTENSIONS=0 reaching the run (exit code 1), with no real socket, file outside a temporary directory, or process.
- [ ] Building the two projects is clean with warnings as errors and their fast tests are green; no test needs TestCategory=Integration.

## Notes

- Split from BL-4501.
- Continues the stashed work of BL-4501.
- The stash holds both projects, the slnx folder, the InternalsVisibleTo lines, CLAUDE.md and Harness/ (ReferenceRoute, ScriptedReferenceServer, ScriptStep, ScriptedDatagramServer, WireExchange, ReferenceConnection, ReferenceCertificate). redis:// sends RESP3 HELLO 3 first; the script answers %0 then +PONG. The TLS test hung; leave it out here (BL-4506).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: e6698f51 22785ee6 3eae45eb fd086cd3 4887121b.
