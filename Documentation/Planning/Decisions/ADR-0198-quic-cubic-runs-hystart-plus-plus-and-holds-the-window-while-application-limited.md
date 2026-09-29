# ADR-0198 — QUIC CUBIC runs HyStart++ and holds the window while application-limited

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-833.

## Context

BL-725 gave `Curl.Quic.UnitLibrary` RFC 9002's plain slow start and grew the window for
every acknowledged packet sent outside recovery. curl's ngtcp2 build does neither:
ngtcp2's CUBIC (`lib/ngtcp2_cc.c`, `ngtcp2_cc_cubic_cc_on_ack_recv`) runs HyStart++
(RFC 9406) in slow start with `NGTCP2_HS_MIN_RTT_THRESH` 4 ms, `NGTCP2_HS_MAX_RTT_THRESH`
16 ms, `NGTCP2_HS_MIN_RTT_DIVISOR` 8, `NGTCP2_HS_N_RTT_SAMPLE` 8,
`NGTCP2_HS_CSS_GROWTH_DIVISOR` 4 and `NGTCP2_HS_CSS_ROUNDS` 5, and neither its CUBIC
nor its Reno grows the window for acknowledgements of packets sent while
application-limited (RFC 9002 section 7.8). ngtcp2 learns both from its rate sampler
(`ngtcp2_rst`), which this library does not have.

## Decision

- **HyStart++ with ngtcp2's constants and order.** Per acknowledgement in slow start:
  the window first grows (by a quarter of the bytes in Conservative Slow Start), then a
  round starts when the acknowledgement covers a packet sent at or after the current
  round started, then the RTT is sampled. A round with at least 8 samples whose minimum
  reaches the last round's plus `clamp(last / 8, 4 ms, 16 ms)` enters Conservative Slow
  Start; a round minimum below the baseline that started it returns to slow start; the
  start of its fifth round ends slow start at the window reached, with a new cubic epoch
  (`W_max` = the window, `K` = 0), as ngtcp2 sets `epoch_start`, `w_max` and `w_est`.
- **Rounds by send time, samples per acknowledgement.** ngtcp2 ends a round when a packet
  sent after the round's start is delivered, counted in delivered bytes. Without a rate
  sampler, `QuicCubicCongestionController` compares the packet's send time with the time
  the round started, which is the same event. One call to `OnPacketsAcknowledged` is one
  acknowledgement and takes one sample of `QuicRttEstimator.LatestRtt`, as ngtcp2 counts
  `rtt_sample_count` per ACK.
- **HyStart++ only in the first slow start.** RFC 9406 section 4.2 says HyStart++ SHOULD
  be used only for the initial slow start, when ssthresh is unset; slow start after
  persistent congestion is plain RFC 9002 slow start.
- **Application-limited is decided per flush.** `QuicClientHandshake` asks the assembler
  for packets up to the available window; when what it sent in flight is less than that
  allowance, the application or flow control ran out first, and every packet of that
  flush carries `QuicSentPacket.IsApplicationLimited`. Probes and CONNECTION_CLOSE, which
  ignore the window, are never marked. This is ngtcp2 Reno's per-packet `is_app_limited`;
  it is simpler than ngtcp2 CUBIC's `rs.is_app_limited && !is_cwnd_limited` and differs
  only when a round mixes limited and unlimited flushes.
- **What the flag does.** NewReno skips growth for those packets. CUBIC skips growth for
  them in slow start (HyStart++ still samples their RTT, as ngtcp2 does) and in
  congestion avoidance, where the time spent limited is added to the epoch start on the
  next unlimited acknowledgement, as ngtcp2 subtracts `app_limited_duration`.

## Consequences

- A handshake never fills the 12000-byte initial window, so it leaves the window where it
  started; `QuicClientHandshakeTests` pins that.
- A transfer that fills the window grows it as before, and a rising RTT in the first
  slow start ends it before loss does.
