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
completed:
---
# BL-822 — Bound the bytes QPACK buffers for an incomplete encoder or decoder stream instruction

## Goal

`QpackDecoder.ReadEncoderStream` and `QpackEncoder.ReadDecoderStream` fail with `QPACK_ENCODER_STREAM_ERROR` / `QPACK_DECODER_STREAM_ERROR` once an incomplete instruction's buffered bytes exceed a limit no valid instruction can reach, instead of buffering a peer's endless partial instruction without bound.

## Context

- BL-729 built QPACK in `Curl.Http3.UnitLibrary`; ADR-0164's Consequences record that an incomplete instruction is buffered without a size limit.
- A valid encoder stream instruction is at most a few integer bytes plus a name and value whose entry fits the advertised `SETTINGS_QPACK_MAX_TABLE_CAPACITY`; a string length above that capacity can be rejected as soon as its length integer is read. Decoder stream instructions are a single integer of at most 10 bytes.
- Check what nghttp3 1.15 does (`nghttp3_qpack.c`, `nghttp3_qpack_decoder_read_encoder`) before choosing the limit, and amend ADR-0164.

## Acceptance criteria

- [ ] `Curl.Http3.UnitTests` show an encoder stream insert whose string length exceeds the advertised maximum table capacity fails with `QpackErrorCode.EncoderStreamError` before the string's bytes arrive. (Decoder stream instructions are single integers, which `QpackPrimitives.ReadInteger` already rejects past 62 bits, so the decoder stream is bounded already; a test pins that a run of `0x80` bytes fails with `QpackErrorCode.DecoderStreamError`.)
- [ ] ADR-0164's Consequences state the limit.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Http3.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
