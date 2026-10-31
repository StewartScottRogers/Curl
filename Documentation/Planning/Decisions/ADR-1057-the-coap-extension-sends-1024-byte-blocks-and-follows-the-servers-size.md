# ADR-1057 — The CoAP extension sends 1024-byte blocks, follows the server's size, and writes a block-wise body whole

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4247.

## Context

RFC 7959 lets a CoAP client carry a request payload (Block1) or a response payload (Block2) that is larger
than one datagram across several exchanges. It leaves the client to choose its block size, when to
negotiate, whether to keep the token, and how to treat a block that repeats or skips. Upstream curl has no
CoAP, so the extension (ADR-0999, ADR-1047) follows the RFC and takes the simplest choice that stays robust.

## Decision

`Curl.Extended.Protocol.Coap.UnitLibrary/Blockwise/CoapBlockwiseTransfer` wraps each `CoapExchange`:

- **Upload (`-d`, `-T`).** A payload of 1024 bytes or less goes out whole, with no Block1 option. A larger
  one goes out in 1024-byte blocks (SZX 6), each with Block1, the next after each 2.31 Continue. When a
  2.31's Block1 asks for a smaller SZX, the next block continues at the same offset at that size (Figure
  8). A 4.13 Request Entity Too Large whose Block1 asks for a smaller SZX starts the payload again at block
  0 at that size (section 2.9.3), so a server that cannot take a whole small payload gets it in blocks.
  Any other response, or the response to the last block, is the final response (4.08 included).
- **Download.** The client does not ask for a size early (no Block2 on the first request); when a
  response carries Block2 with M set, it asks for the next block, by byte offset, at the size the
  server's last block used, repeating the request's method and options without payload or Block1
  (Figure 10). A server shrinking its size mid-transfer is followed at the same offset.
- **Output.** Blocks are joined in memory and the final response is written once, whole, so `-f` and
  `--fail-with-body` see one response code as for a single response. A repeated block (a retransmitted
  response) is written only once; a block past the next one, or a block with M set shorter than its size,
  fails with exit 56, as does a Block option with the reserved SZX 7.
- **Messages.** Each message takes the next Message ID after the request's random one; all keep the
  request's token, which RFC 7959 section 2.3 allows.
- **`-T`.** An upload is a PUT unless `-X` names a method, as with HTTP; the file is read whole first.

## Consequences

- A CoAP body is held in memory; CoAP bodies are small (Block numbers stop at 2^20 blocks).
- Size1 and Size2 are neither sent nor read, and the ETag of each block is not compared; those can come
  with a later task if a server needs them.
