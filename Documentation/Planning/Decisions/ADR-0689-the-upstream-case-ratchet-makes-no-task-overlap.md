# ADR-0689: The upstream case ratchet makes no task overlap, and git merges it line by line

- Status: Accepted
- Date: 2026-10-18
- Decided by Claude under Stewart's delegation.

## Context

On 2026-10-18, 42 of the board's 58 Backlog tasks named
`Curl.Conformance.UnitTests/PassingUpstreamCases.txt` in `touches`, because each adds or removes
upstream cases on the ratchet (ADR-0013). `task-board.ps1` keeps two tasks whose `touches`
overlap from running at once. So `capacity` reported 1, and a `-Lanes Auto` shift ran a single
lane for hours, even though most of those tasks change different projects.

Two lanes adding different case numbers to the list can make git conflict only when their
lines land side by side. The list is one case number per line. `UpstreamCaseRatchet` reads it
into a set (`ToHashSet`), so line order and a repeated line change nothing.

## Decision

- `.gitattributes` gives the file `merge=union`, git's built-in driver. A merge, rebase or
  cherry-pick keeps both sides' lines instead of stopping at a conflict.
- `task-board.ps1`'s `Test-Overlap` skips every path in `$script:SharedListFiles`. The ratchet
  is the only one. Two tasks that both name it can run at once, and it does not overlap
  `Curl.Conformance.UnitTests`, the folder it is in. Tasks still name it in `touches` when they
  edit it, so `touches` stays a true list of what changes.
- `Test-TaskBoardOverlap.ps1` pins both halves through the real script's `capacity`.

## Consequences

- With the ratchet set aside, about 15 of the 58 Backlog tasks can run at once, by the same
  overlap rule. A `-Lanes Auto` shift can grow past one lane again.
- A union merge keeps a line one side deleted while the other side changed lines right next to
  it. A case that one lane takes off the ratchet can come back that way. The ratchet then
  fails the build on that case, as for any listed case that fails, and the lane that sees the
  failure takes the line off again. That is a rare, visible failure, not a silent one.
- GitHub's own pull-request merge does not run merge drivers. The shift-end merge from
  `work/dark-factory` to `master` does not conflict on this file, because `master` only
  gets the file from `work/dark-factory`.
