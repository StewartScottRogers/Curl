---
id: AF-0062
title: Hostile zlib-compressed SSH payload throws ZLibException out of SshZlibDecompressor.Decompress and SshWireDecoders.TryInflatePayload instead of a refusal
auditor: security
severity: Critical
status: proposed
reason:
key: security:Curl.Protocol.Ssh.UnitLibrary/Compression/SshZlibDecompressor.cs:SshZlibDecompressor.Decompress:fuzz-crash
reproduction: none
task: none
tasks:
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0062 - Hostile zlib-compressed SSH payload throws ZLibException out of SshZlibDecompressor.Decompress and SshWireDecoders.TryInflatePayload instead of a refusal

## Summary

Critical finding from the security auditor at `Curl.Protocol.Ssh.UnitLibrary/Compression/SshZlibDecompressor.cs:46`: Hostile zlib-compressed SSH payload throws ZLibException out of SshZlibDecompressor.Decompress and SshWireDecoders.TryInflatePayload instead of a refusal.

## Evidence

Location: `Curl.Protocol.Ssh.UnitLibrary/Compression/SshZlibDecompressor.cs:46`

Fuzz.cs --target ssh --iterations 200000 --seed 264985340 (seed = first eight hex digits of the audited commit, 0x0fcb5afc) reported 'crashes 3, hangs 0, saved 1' and exited 1. Saved input ssh-1.bin (158 bytes, iteration 153303, hex 78201261646266616563e7e0e4e2e6e1e5e31760606050492e2d2a4b35323535b4d42dce48343235d3494d4ec900b375f3328b4b0a8c4ccd181818248a8b33745353c00a758a8a13210a20721a89a9c5864616bac925453ac91989c919894606ba05f93995564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e065224901555fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe246551). Stack: System.IO.Compression.ZLibException: The underlying compression routine returned an unexpected error code. at Inflater.Inflate -> DeflateStream.ReadCore -> SshZlibDecompressor.Decompress (SshZlibDecompressor.cs:46 'while ((read = inflater.Read(block)) > 0)') -> SshWireDecoders.TryInflatePayload (SshWireDecoders.cs:55). The input's zlib header 0x78 0x20 sets FDICT (preset dictionary), which the BCL inflater reports as ZLibException, an IOException rather than an InvalidDataException. Decompress's contract (doc comment) and TryInflatePayload's catch (InvalidDataException only, line 58) cover only InvalidDataException, so the public decoder throws instead of returning false. The production path is SshPacketReader.cs:97 ('decompressor.Decompress(payload)'), where any server that negotiated zlib can send this byte pattern and the exception escapes as an untyped IOException rather than the decompression failure curl reports. Replay: dotnet run Audit/Tools/Fuzz/Fuzz.cs -- --replay <ssh-1.bin> --target ssh prints 'replay ssh-1.bin on ssh: crash: System.IO.Compression.ZLibException ...'.

## Reproduction

Run from the repository root:

```powershell
dotnet run Audit/Tools/Fuzz/Fuzz.cs -- --target ssh --iterations 200000 --seed 264985340 --out $env:TEMP\fuzz-ssh; $LASTEXITCODE
```

- Expected: ssh: iterations 200000, ... crashes 0, hangs 0, saved 0 and exit code 0
- Actual: ssh: iterations 200000, inputs/s 798, crashes 3, hangs 0, saved 1 and exit code 1; fuzz-ssh\ssh-1.txt holds System.IO.Compression.ZLibException at SshZlibDecompressor.Decompress line 46

## Re-audits

## Log

- 2026-10-07: filed proposed.
