---
id: BL-1285
title: Point the audit fuzzer's ssh target at SshWireDecoders
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1267]
touches: [Audit/Tools/Fuzz/Fuzz.cs, Audit/Instructions/Security.md]
lane: no
requirement: none
created: 2026-10-02
completed: 2026-10-07
---
# BL-1285 — Point the audit fuzzer's ssh target at SshWireDecoders

## Goal

The audit office's fuzzer (`Fuzz.cs` in the audit tools' `Fuzz` folder) fuzzes the SSH readers through `Curl.Protocol.Ssh.SshWireDecoders`, so AF-0011's reproduction (`--target ssh`) exits 0 or 1 instead of 2.

## Context

Interactive only (`lane: no`): the change is to the audit office's fuzzer, which the dark
factory may not read or change (ADR-0267). Its `touches` should be set to the fuzzer's
folder by the interactive session; a lane cannot name an audit path.

BL-1267 (ADR-0394) gave `Curl.Protocol.Ssh.UnitLibrary` a public static class
`SshWireDecoders`. Each method returns its refusal of malformed bytes as a value and lets
any other exception escape, which is the crash the fuzzer looks for:

- `ValueTask<int> CountWholePacketsAsync(ReadOnlyMemory<byte> bytes, CancellationToken)` - unprotected binary packets, back to back.
- `bool TryInflatePayload(ReadOnlyMemory<byte>)` - one zlib-compressed packet payload.
- `bool TryDecodeKexInit(ReadOnlyMemory<byte>)` - a `KEXINIT` payload, message number 20 first.
- `bool TryDecodeSftpAttributes(ReadOnlyMemory<byte>)` - SFTP attributes, flags first.
- `bool TryDecodeHostKeySignature(ReadOnlyMemory<byte>)` - SSH strings: algorithm name, host key blob, signature blob; then the exchange hash.

Give the `ssh` target a seed corpus per method (a well-formed packet, compressed payload,
`KEXINIT`, attributes, and an `ssh-ed25519` key with its signature) and list the methods
in the app's header comment. Then rerun AF-0011's reproduction.

## Acceptance criteria

- [x] `dotnet run <fuzzer>/Fuzz.cs -- --target ssh --iterations 1000 --seed 1 --out $env:TEMP\fuzz-ssh` exits 0 or 1 and prints the summary line.
- [x] The fuzzer's `--self-test` still passes.
- [x] Any crash it finds is filed as its own task against `Curl.Protocol.Ssh.UnitLibrary`.

## Notes

- 2026-10-07 (interactive, audit branch): the `ssh` target in `Audit/Tools/Fuzz/Fuzz.cs` now runs every `SshWireDecoders` method (CountWholePacketsAsync, TryInflatePayload, TryDecodeKexInit, TryDecodeSftpAttributes, TryDecodeHostKeySignature) on each input, seeded with a two-packet stream, a zlib-compressed KEXINIT, a KEXINIT, SFTP attributes with every field flag, and the RFC 8032 test-1 ssh-ed25519 key and signature. `--self-test` 15/15 PASS. The reproduction prints `ssh: iterations 1000, inputs/s 494, crashes 0, hangs 0, saved 0` and exits 0; 50,000 iterations at seed 863949067 found no crash or hang, so no crash task was filed. `Audit/Instructions/Security.md` no longer calls the target unfuzzable. Commit 9e30af266 on `audit`. AF-0011 closes only on a re-audit by the security auditor.

## Log

- 2026-10-02: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Fixed interactively on the audit branch; see Notes.
