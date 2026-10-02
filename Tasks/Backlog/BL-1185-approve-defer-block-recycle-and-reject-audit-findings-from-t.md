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
completed:
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

- [ ] Each card offers exactly the moves its status allows, and no Reject on a `proposed` or `blocked` finding.
- [ ] With a token, each move makes one commit on `audit` changing only that finding's `status:`, `reason:` and one new `## Log` line, with the message above (checked against a scratch finding on a throwaway branch named by a page setting used only for this check; Notes records the commits).
- [ ] The card moves to its new column without a page reload once the commit succeeds.
- [ ] A 409 or any other failure leaves the card where it was and shows the reason inline.
- [ ] Without a token, no move buttons are shown; "Forget token" removes it from `localStorage`.
- [ ] The token appears in no URL, console message or element text (checked by searching the page's DOM and console after a move).
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-02: Created.
