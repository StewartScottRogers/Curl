# ADR-0972: `-J` names keep bytes that are not valid UTF-8 as their raw bytes off Windows

Status: Accepted. Decided by Claude under Stewart's delegation (BL-3858).

## Context

ADR-0912 (BL-3580, CGR-051) reads a `Content-Disposition` `filename=` value in the platform's
encoding: the ANSI code page on Windows, UTF-8 elsewhere. Plain UTF-8 turns a byte outside a
valid sequence into U+FFFD, so off Windows `filename="caf\xE9.txt"` created `caf\357\277\275.txt`.
curl 8.18.0 OpenSSL on Linux, measured through WSL with `Record-CurlExchange.ps1` on 2026-10-28,
creates the file named 63 61 66 E9 2E 74 78 74 and prints those raw bytes for
`%{filename_effective}`.

## Decision

Off Windows `ContentDispositionFileName.EncodingFor` gives `InvalidUtf8ByteEscape.Encoding`, the
UTF-8 that keeps each such byte as its U+DC00 + byte surrogate (ADR-0920). The output file then
opens through `RawByteFileName` (BL-3853, BL-3947) under the raw bytes, exactly as an `-o` name
holding such a byte does, with no new native call. Windows keeps the ANSI code page unchanged.

`%{filename_effective}` and the `File exists` warning print the name as every other escaped
output name prints today; making write-out and warnings give the raw byte back is the same gap
for `-o $'\xff'` and is left to BL-4027.

## Consequences

A valid UTF-8 name reads as before; an invalid one opens under the bytes curl uses.
