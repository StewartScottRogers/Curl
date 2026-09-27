# ADR-0012 — Relicense from GPL-3.0 to MIT

- **Status:** Accepted
- **Date:** 2026-09-26

## Context

The repository was created under GPL-3.0. Curl is a drop-in replacement for the `curl`
command-line tool, and curl itself is under the curl licence
(<https://curl.se/docs/copyright.html>), a permissive MIT-style licence. curl is
everywhere because anyone can embed and ship it; a copyleft replacement cannot go
everywhere curl goes, which works against the product's goal.

Open question 1 in `Documentation/Product/Product-Overview.md` asked whether GPL-3.0 was
intentional or should become MIT or Apache-2.0 before the first public release, and it
blocked that release. Stewart chose MIT on 2026-09-26 (BL-004).

Curl is written clean-room from RFCs, the curl man page and observable behaviour, not by
translating curl's C, so no upstream code constrains the choice.

## Decision

Curl is licensed under the MIT licence. `LICENSE.txt` holds the standard MIT text with
the notice `Copyright (c) 2026 Stewart Scott Rogers`.

## Consequences

- Anyone may embed, modify and redistribute Curl, including in closed-source products,
  as they can with curl. The only condition is keeping the copyright and permission
  notice.
- Contributions from here on are made under MIT.
- MIT carries no explicit patent grant, unlike Apache-2.0. For a command-line tool that
  implements published protocols this is an accepted risk.
- The clean-room rule stays: copying upstream C would now also mean carrying the curl
  licence notice alongside MIT.

## Alternatives considered

- **Keep GPL-3.0.** Copyleft blocks the embed-in-anything use that makes a drop-in curl
  replacement worth having.
- **Apache-2.0.** Permissive with an explicit patent grant, but longer, with a NOTICE-file
  obligation, and further from curl's own short MIT-style licence. Stewart chose MIT.
