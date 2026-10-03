---
id: BL-1282
title: Point the audit fuzzer's ssh target at SshWireDecoders
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1267]
touches: []
lane: no
requirement: none
created: 2026-10-02
completed:
---
# BL-1282 — Point the audit fuzzer's ssh target at SshWireDecoders

## Goal

The audit office's fuzzer (`Fuzz.cs` in the audit tools' `Fuzz` folder) fuzzes the SSH readers through `Curl.Protocol.Ssh.SshWireDecoders`, so AF-0011's reproduction (`--target ssh`) exits 0 or 1 instead of 2.

## Context

Interactive only (`lane: no`): the change is to the audit office's fuzzer, which the dark
factory may not read or change (ADR-0267). Its `touches` should be set to the fuzzer's
folder by the interactive session; a lane cannot name an audit path.

BL-1267 (ADR-0393) gave `Curl.Protocol.Ssh.UnitLibrary` a public static class
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

- [ ] `dotnet run <fuzzer>/Fuzz.cs -- --target ssh --iterations 1000 --seed 1 --out $env:TEMP\fuzz-ssh` exits 0 or 1 and prints the summary line.
- [ ] The fuzzer's `--self-test` still passes.
- [ ] Any crash it finds is filed as its own task against `Curl.Protocol.Ssh.UnitLibrary`.

## Notes

## Log

- 2026-10-02: Created.
