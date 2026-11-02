# ADR-1195: A CoAP Block2 transfer whose ETag changes between blocks fails with exit 56

- Status: Accepted
- Date: 2026-11-02
- Task: BL-4471 (measured), BL-4563 (fix)
- Decided by Claude under Stewart's delegation

## Context

RFC 7959 section 2.4 says that blocks of different representations, which a changed ETag shows, are not to be
combined into one body. BL-4471's conformance case `CoapBlock2Tests.EtagChangesBetweenBlocks_TheMixedBodyIsNotWrittenAndExit56`
(requirement `Coap:RFC7959-2.4-4`) found that `CoapBlockwiseTransfer` ignores ETag and joins such blocks, exiting 0.
The RFC lets a client either fail or fetch the representation again from block 0; upstream curl has no CoAP, so
there is no curl behaviour to match.

## Decision

A Block2 block whose ETag option differs from the first block's (an ETag appearing, disappearing or changing) fails
the transfer with exit 56 (`CurlExitCode.RecvError`) and nothing written, as `CoapBlockwiseTransfer` already does
for a block out of order or short (ADR-1057). The conformance case pins that answer and is listed in
`KnownFailures.txt` against BL-4563 until the library does it.

## Consequences

- No retry loop is added: a resource that keeps changing cannot hold curl in a fetch with no bound.
- A script sees the same exit code for every inconsistent block-wise response.
