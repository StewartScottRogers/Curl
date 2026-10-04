---
id: BL-1267
title: Fix AF-0011: Curl.Protocol.Ssh.UnitLibrary exposes no byte-level parser entry point, so its packet and message readers cannot be fuzzed
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-02
---
# BL-1267 — Fix AF-0011: Curl.Protocol.Ssh.UnitLibrary exposes no byte-level parser entry point, so its packet and message readers cannot be fuzzed

## Goal

The defect the audit office reported as AF-0011 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0011 (Low, security auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0011-curl-protocol-ssh-unitlibrary-exposes-no-byte-leve.md`.

Location: `Curl.Protocol.Ssh.UnitLibrary`

Location: `Curl.Protocol.Ssh.UnitLibrary`

dotnet run Audit/Tools/Fuzz/Fuzz.cs -- --target ssh --iterations 200000 --seed 863949067 printed 'ssh: Curl.Protocol.Ssh.UnitLibrary exposes no public reader or decoder of raw bytes (public types: SshProtocolHandler, SshAlgorithmPreferences, ISshRandomSource, SystemSshRandomSource); nothing to fuzz.' and exited 2. Every SSH packet, key-exchange, authentication and SFTP reader is internal, so hostile-server bytes reach them untested by the fuzzer.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet run Audit/Tools/Fuzz/Fuzz.cs -- --target ssh --iterations 1000 --seed 1 --out $env:TEMP\fuzz-ssh; $LASTEXITCODE
```

- Expected: The target fuzzes the SSH packet and message readers and exits 0 or 1.
- Actual: ssh: Curl.Protocol.Ssh.UnitLibrary exposes no public reader or decoder of raw bytes ...; nothing to fuzz. Exit 2.

The finding closes only when a later re-audit by the security auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] ~~The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.~~ Split (see Notes): the library now exposes public byte-level readers, `SshWireDecoders`, each covered by `SshWireDecodersTests`; running the reproduction needs the fuzzer pointed at them, which is BL-1285's criterion.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Plan: one public static class `Curl.Protocol.Ssh.SshWireDecoders` runs the internal
  readers over raw bytes - `CountWholePacketsAsync` (packets, through `SshPacketReader`
  over a new internal `SshByteArrayConnection`), `TryInflatePayload` (zlib),
  `TryDecodeKexInit`, `TryDecodeSftpAttributes`, `TryDecodeHostKeySignature` (name, key
  blob, signature blob, exchange hash, through the named `ISshSignatureVerifier`). The
  refusals the transport already treats as a malformed server become the return value;
  anything else escapes so the fuzzer sees it as a crash. ADR-0394 records it.
- The reproduction could not be run here: the fuzzer lives under the audit folder, which
  the audit guard refuses a dark factory lane to read, run or change (ADR-0267). Whether
  it finds the readers by reflection or needs its `ssh` target rewritten is unknown from
  here, so the wiring is filed as BL-1285 (interactive only, depends on this task). The
  first criterion was split for that reason; the finding still closes only on re-audit.
- Added `Curl.Protocol.Ssh.UnitTests` to `touches` for the new tests; no other task in
  Doing on `origin/work/dark-factory` names it.
- Results: build 0 warnings, 0 errors; fast tests all green, `Curl.Protocol.Ssh.UnitTests`
  1625 passed (14 new).

## Log

- 2026-10-03: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. Curl.Protocol.Ssh.UnitLibrary exposes its server-byte readers through public SshWireDecoders; fuzzer wiring filed as BL-1285
