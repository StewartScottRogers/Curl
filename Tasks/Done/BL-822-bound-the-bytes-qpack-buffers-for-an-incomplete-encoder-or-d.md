---
id: BL-822
title: Bound the bytes QPACK buffers for an incomplete encoder or decoder stream instruction
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-729]
touches: [Curl.Http3.UnitLibrary, Curl.Http3.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-822 — Bound the bytes QPACK buffers for an incomplete encoder or decoder stream instruction

## Goal

`QpackDecoder.ReadEncoderStream` and `QpackEncoder.ReadDecoderStream` fail with `QPACK_ENCODER_STREAM_ERROR` / `QPACK_DECODER_STREAM_ERROR` once an incomplete instruction's buffered bytes exceed a limit no valid instruction can reach, instead of buffering a peer's endless partial instruction without bound.

## Context

- BL-729 built QPACK in `Curl.Http3.UnitLibrary`; ADR-0164's Consequences record that an incomplete instruction is buffered without a size limit.
- A valid encoder stream instruction is at most a few integer bytes plus a name and value whose entry fits the advertised `SETTINGS_QPACK_MAX_TABLE_CAPACITY`; a string length above that capacity can be rejected as soon as its length integer is read. Decoder stream instructions are a single integer of at most 10 bytes.
- Check what nghttp3 1.15 does (`nghttp3_qpack.c`, `nghttp3_qpack_decoder_read_encoder`) before choosing the limit, and amend ADR-0164.

## Acceptance criteria

- [x] `Curl.Http3.UnitTests` show an encoder stream insert whose string length exceeds the advertised maximum table capacity fails with `QpackErrorCode.EncoderStreamError` before the string's bytes arrive. (Decoder stream instructions are single integers, which `QpackPrimitives.ReadInteger` already rejects past 62 bits, so the decoder stream is bounded already; a test pins that a run of `0x80` bytes fails with `QpackErrorCode.DecoderStreamError`.)
- [x] ADR-0164's Consequences state the limit.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Http3.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Bound chosen (ADR-0164 amended, decided by Claude under Stewart's delegation): each
  encoder stream string literal is limited to what can still fit an entry in the current
  dynamic table capacity (capacity - 32 - name characters already read), checked as soon
  as the length integer is read, so its bytes are never buffered. This is tighter than the
  advertised maximum the task names and exact: a longer string could only fail at insert.
  Huffman-coded lengths get ceil(30n / 8), 30 bits being the longest Huffman code.
- nghttp3 1.15 uses fixed caps instead (`NGHTTP3_QPACK_MAX_NAMELEN` 256,
  `NGHTTP3_QPACK_MAX_VALUELEN` 65536, `NGHTTP3_ERR_QPACK_HEADER_TOO_LARGE`); the capacity
  bound never rejects a valid instruction and is 0-sized under curl's own settings.
- `QpackPrimitives.ReadString` gained an optional `maximumLength`; field section decoding
  keeps the default (no practical limit), since a field section arrives whole.
- `QpackRequiredInsertCount.EntryOverhead` made public (inside the internal class) so the
  decoder reuses it rather than repeating 32.
- The decoder stream needed no code: a test pins that `ff` plus nine `80` continuation
  bytes fails with `DecoderStreamError`.
- Full fast run: one unrelated flaky failure in `Curl.Networking.UnitTests`
  (`AuthenticateAsClientAsync_WithCertStatusAndARevokedStapledResponse_FailsWithExit91AndTheReason`,
  Alert instead of Handshake); it passed on rerun (1464 passed).

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. QPACK encoder stream strings longer than the dynamic table can hold fail with EncoderStreamError before their bytes are buffered
