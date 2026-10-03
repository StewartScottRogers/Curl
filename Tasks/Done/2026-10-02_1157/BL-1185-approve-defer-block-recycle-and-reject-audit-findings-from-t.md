---
id: BL-1185
title: Approve, defer, block, recycle and reject audit findings from the board page's Audit tab
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1184]
touches: [.github/board]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1185 — Approve, defer, block, recycle and reject audit findings from the board page's Audit tab

## Goal

From a finding's card on the board page's Audit tab, Stewart approves, defers, blocks, cycles back or rejects it, and the page commits that change to the finding's file on the `audit` branch with his own GitHub token.

## Context

- Stewart, 2026-10-02, chose this (option A) over copying a phrase into a Claude session or editing the file on GitHub.
- Moves (BL-1183): Waiting (`proposed`) → Approve (`accepted`), Defer (`deferred`), Block (`blocked`); Deferred → Back to waiting (`proposed`) or Reject (`rejected`); Blocked → Back to waiting. Offer only the moves a finding's status allows. Defer, Block and Reject ask for a one-line reason in an inline field; Approve and Back to waiting may leave it empty.
- Token: Stewart pastes a fine-grained GitHub personal access token (this repository only, Contents: read and write) into a field on the Audit tab. It is kept in the browser's `localStorage`, sent only to `api.github.com`, never logged or shown again, and a "Forget token" button removes it. Without a token the tab is read-only, as in BL-1184, with a note saying how to add one.
- Commit: `GET` the finding through the contents API on `ref=audit` for its `sha`, change `status:` and `reason:`, append `- <yyyy-MM-dd>: <old> -> <new>. <reason>` to `## Log`, and `PUT` it back to branch `audit` with message `chore(audit): <old> -> <new> <AF-id>`. A 409 (the file changed since it was read) reloads the finding and says so; nothing is overwritten.
- No browser `alert`, `confirm` or `prompt` dialogs: use inline controls and messages.
- An approved finding still needs an interactive session to run `New-TasksFromAcceptedFindings.ps1` (Triage step 3); after an Approve, the card says so.
- The page is not an audit path; the lane changes only `.github/board`. Its commits to `Audit/Findings` at run time are Stewart's, made with his token on the `audit` branch, which is where findings live.

## Acceptance criteria

- [x] Each card offers exactly the moves its status allows, and no Reject on a `proposed` or `blocked` finding.
- [x] With a token, each move makes one commit on `audit` changing only that finding's `status:`, `reason:` and one new `## Log` line, with the message above (checked against a scratch finding on a throwaway branch named by a page setting used only for this check; Notes records the commits).
- [x] The card moves to its new column without a page reload once the commit succeeds.
- [x] A 409 or any other failure leaves the card where it was and shows the reason inline.
- [x] Without a token, no move buttons are shown; "Forget token" removes it from `localStorage`.
- [x] The token appears in no URL, console message or element text (checked by searching the page's DOM and console after a move).
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- Delivered directly in `.github/board/site/index.html` (one page, no C#), as BL-1184 was: no seam or library for the `/feature` planning stages to plan.
- Token: a password field on the Audit tab, saved under `localStorage['curl-board-github-token']`, the field emptied at once; the token goes only into the `Authorization: Bearer` header of contents API requests. "Forget token" removes the key. Without it the form and a read-only note show and no card has move controls.
- Moves: one table, `findingMoves`, gives each status its buttons (proposed: Approve, Defer, Block; deferred: Back to waiting, Reject; blocked: Back to waiting). Each card with moves has one inline reason field; Defer, Block and Reject with it empty show "<move> needs a one-line reason." on the card and send nothing.
- Commit: `GET contents/<path>?ref=<audit branch>`, then `PUT` with that `sha`, `branch` and message `chore(audit): <old> -> <new> <AF-id>`. The text edit keeps the file's line endings (CRLF or LF) and a BOM if any, replaces the `status:` and `reason:` lines (inserting `reason:` after `status:` if missing) and appends `- <yyyy-MM-dd>: <old> -> <new>.[ <reason>]` after the last line of `## Log` (adding the section if missing).
- Choice: before writing, the page checks that the status it read is still the card's status; if not, it shows the finding as read and writes nothing - the same "changed since read" protection as a 409, caught one request earlier.
- Choice: after a move, the new text is cached under the new blob SHA the PUT returns, so the next refresh does not read raw.githubusercontent.com's possibly cached old text for the branch and jump the card back.
- Choice (security): the new preview override `?contents=` (the contents API base) is honoured only when the page is served from localhost and the override is on the page's own origin, so a crafted link to the published page cannot send the token anywhere but api.github.com.
- Choice: the "page setting" naming the branch for the check is the existing `?auditBranch=`, which already names the branch the tab reads; moves write to that same branch, so reading and writing can never disagree.
- Choice: move messages and half-typed reasons are kept by finding ID outside the cards, and the reason field being typed in keeps focus and caret across the 180 s redraw.
- Check: run against a local fake of the GitHub contents API (a throwaway C# file-based app outside the repository: serves `.github/board`, answers GET/PUT like GitHub, 409 on a stale `sha`, and drives headless Edge 154 over the DevTools protocol), not against GitHub itself: an unattended lane has no token of Stewart's and must not push branches. Commits it recorded, all on branch `audit`, each with the `sha` it read:
  - `chore(audit): proposed -> accepted AF-0001` - diff: `status: accepted`, `- 2026-10-02: proposed -> accepted.`; card moved to Approved without a reload, the tab label went to `Audit (1)`, and the card showed the `New-TasksFromAcceptedFindings.ps1` note.
  - `chore(audit): deferred -> rejected AF-0004` - `status: rejected`, `reason: Superseded by AF-0002`, one log line; card moved into the Rejected fold.
  - `chore(audit): proposed -> blocked AF-0003` answered 409: card stayed in Waiting, showed "the finding changed on audit since it was read (409). Reloaded it; nothing was overwritten."
  - `chore(audit): blocked -> proposed AF-0005` answered 500: card stayed in Blocked with the reason; the retry, then `proposed -> deferred` and `deferred -> proposed`, each made one commit with the previous commit's blob SHA.
  - Buttons per card matched the table exactly; accepted, rejected, closed and unreadable cards had none. CRLF line endings were kept.
  - Token search after the moves: not in `outerHTML`, `innerText` or any input's value; not in any of the 4 console entries (all network errors the check provoked); not in any URL the server saw. After "Forget token", `localStorage` held nothing and no move buttons remained.
- Live GitHub not yet exercised: CORS for `PUT` with an `Authorization` header is supported by api.github.com for fine-grained tokens, but the first real move will be Stewart's.
- Build clean; fast tests green across 33 assemblies, 0 failed.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. The board page's findings tab approves, defers, blocks, cycles back and rejects findings with a saved GitHub token, one commit per move
