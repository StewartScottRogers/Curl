---
id: BL-1681
title: Fix AF-0062: Hostile zlib-compressed SSH payload throws ZLibException out of SshZlibDecompressor.Decompress and SshWireDecoders.TryInflatePayload instead of a refusal
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1681 — Fix AF-0062: Hostile zlib-compressed SSH payload throws ZLibException out of SshZlibDecompressor.Decompress and SshWireDecoders.TryInflatePayload instead of a refusal

## Goal

The defect the audit office reported as AF-0062 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0062 (Critical, security auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0062-hostile-zlib-compressed-ssh-payload-throws-zlibexc.md`.

Location: `Curl.Protocol.Ssh.UnitLibrary/Compression/SshZlibDecompressor.cs:46`

Location: `Curl.Protocol.Ssh.UnitLibrary/Compression/SshZlibDecompressor.cs:46`

Fuzz.cs --target ssh --iterations 200000 --seed 264985340 (seed = first eight hex digits of the audited commit, 0x0fcb5afc) reported 'crashes 3, hangs 0, saved 1' and exited 1. Saved input ssh-1.bin (158 bytes, iteration 153303, hex 78201261646266616563e7e0e4e2e6e1e5e31760606050492e2d2a4b35323535b4d42dce48343235d3494d4ec900b375f3328b4b0a8c4ccd181818248a8b33745353c00a758a8a13210a20721a89a9c5864616bac925453ac91989c919894606ba05f93995564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e065224901555fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe246551). Stack: System.IO.Compression.ZLibException: The underlying compression routine returned an unexpected error code. at Inflater.Inflate -> DeflateStream.ReadCore -> SshZlibDecompressor.Decompress (SshZlibDecompressor.cs:46 'while ((read = inflater.Read(block)) > 0)') -> SshWireDecoders.TryInflatePayload (SshWireDecoders.cs:55). The input's zlib header 0x78 0x20 sets FDICT (preset dictionary), which the BCL inflater reports as ZLibException, an IOException rather than an InvalidDataException. Decompress's contract (doc comment) and TryInflatePayload's catch (InvalidDataException only, line 58) cover only InvalidDataException, so the public decoder throws instead of returning false. The production path is SshPacketReader.cs:97 ('decompressor.Decompress(payload)'), where any server that negotiated zlib can send this byte pattern and the exception escapes as an untyped IOException rather than the decompression failure curl reports. Replay: dotnet run Audit/Tools/Fuzz/Fuzz.cs -- --replay <ssh-1.bin> --target ssh prints 'replay ssh-1.bin on ssh: crash: System.IO.Compression.ZLibException ...'.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet run Audit/Tools/Fuzz/Fuzz.cs -- --target ssh --iterations 200000 --seed 264985340 --out $env:TEMP\fuzz-ssh; $LASTEXITCODE
```

- Expected: ssh: iterations 200000, ... crashes 0, hangs 0, saved 0 and exit code 0
- Actual: ssh: iterations 200000, inputs/s 798, crashes 3, hangs 0, saved 1 and exit code 1; fuzz-ssh\ssh-1.txt holds System.IO.Compression.ZLibException at SshZlibDecompressor.Decompress line 46

The finding closes only when a later re-audit by the security auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-07 (lane 9): Fix written and verified locally, then left uncommitted for the shift to stash. `SshZlibDecompressor.Decompress` now reads through a private `ReadInflated` that catches the inflater's `IOException` (the BCL's `ZLibException` is internal, so it cannot be named) and rethrows it as `InvalidDataException("The SSH packet is not a valid continuation of the zlib stream.")`. `TryInflatePayload` and `SshPacketReader` then refuse the FDICT header (78 20) as their contracts say. Regression tests: `SshZlibDecompressorTests.Decompress_HeaderAskingForAPresetDictionary_ThrowsInvalidDataExceptionNotZLibException` and `SshWireDecodersTests.TryInflatePayload_HeaderAskingForAPresetDictionary_ReturnsFalse`; all 9 decompressor and inflate tests pass. The new catch block needs those tests to keep 100% coverage, so `touches` now includes Curl.Protocol.Ssh.UnitTests. BL-1518 (Doing) also touches that project, so the task goes back to Backlog until BL-1518 releases it. If the stash is lost, redo the fix from this note: it takes about 10 minutes.

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Needs Curl.Protocol.Ssh.UnitTests for its regression tests, which BL-1518 (Doing) touches; fix is written and verified, see Notes
