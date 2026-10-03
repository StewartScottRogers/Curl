---
id: BL-1249
title: Release approved audit findings as Curl tasks from a button on the board page's Audit tab
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [.github/board, .github/workflows/release-approved-findings.yml]
requirement: none
created: 2026-10-02
completed:
---
# BL-1249 — Release approved audit findings as Curl tasks from a button on the board page's Audit tab

## Goal

Stewart turns his approved audit findings into Curl tasks from the board page's Audit tab, with one button for all of them and one per card, without a Claude session.

## Context

- Stewart, 2026-10-02: "Can you give me one or more buttons to release these approved audits?" Today an interactive session runs `Audit/Tools/New-TasksFromAcceptedFindings.ps1` (Audit/Triage.md step 3). It files one Curl task per `accepted` finding whose `task` is `none`, writes the task's ID into the finding, and the tasks go to `work/dark-factory` and the findings to `audit`.
- Decided by Claude under Stewart's delegation: the button starts a GitHub Actions workflow, so the filing logic stays in the one script and nothing is reimplemented in the page.
- **New workflow `.github/workflows/release-approved-findings.yml`:**
  - `workflow_dispatch`, with an optional input `findings` (comma-separated `AF-####`, empty meaning every approved finding without a task). `runs-on: windows-latest`, because the script is Windows PowerShell; `concurrency: release-findings`, no cancel; `permissions: contents: write, pull-requests: write`.
  - Steps:
    1. Check out `work/dark-factory` (full history) into one folder and `audit` into another.
    2. Run the script from the audit checkout with `CLAUDE_PROJECT_DIR` set to the factory checkout.
    3. With an input, file only the findings named: add a `-Id` filter to the script only if it has none (that is an audit path, so a separate interactive task), otherwise skip the input and release them all.
    4. Commit the new task files on `work/dark-factory` as `chore(tasks): release audit findings <ids> as <BL ids>`, then `git pull --rebase`, then `task-board.ps1 dedupe -Since <the upstream commit rebased onto>`, then push, retrying the rebase 3 times. If dedupe renumbers a task, rewrite that ID in its finding's `task:` line before the audit commit.
    5. Commit the findings on `audit` as `audit: findings <ids> become <BL ids>` and push, then open the `audit` pull request if none is open (body: what was released). Never merge it: an interactive session merges audit pull requests once CI is green.
    6. Write the released pairs to the job summary.
  - GitHub only dispatches a workflow that exists on the default branch, so it works once this reaches `master` through the shift-end merge.
- **Page (`.github/board/site/index.html`):**
  - The Audit tab shows "Release approved findings (N)", N being the approved findings whose `task` is `none`, and each such Approved card gets a "Release" button.
  - Both call `POST /repos/<repo>/actions/workflows/release-approved-findings.yml/dispatches` with `ref: master` and the `findings` input, using the saved token, then show "Releasing... (run link)" and, once the finding's `task:` line names a task on the next refresh, "Released as BL-####".
  - A 404 from the dispatch shows "Available after the next merge to master".
  - A 403 or 401 shows that the token needs "Actions: Read and write" as well as "Contents: Read and write", with the steps to edit the token.
  - No browser dialogs, and the token appears in no URL, console message or element text.
- **Token:** Stewart's token gains Repository permissions, Actions: Read and write. Update the renewal steps the page shows (BL-1211) to include it.
- Not an audit path or a guard file: the task changes only `.github/board` and the new workflow. The workflow writes `Audit/Findings` at run time on the `audit` branch, which is where findings live, as Stewart's action.

## Acceptance criteria

- [ ] `release-approved-findings.yml` exists with the trigger, input, runner, concurrency and permissions above, and its steps commit tasks to `work/dark-factory` (with rebase, dedupe against the upstream commit, and retries) and findings to `audit`, and open but never merge the audit pull request.
- [ ] Its script step runs `New-TasksFromAcceptedFindings.ps1 -WhatIf` first and prints that to the job summary, so a run shows what it will file before it files it.
- [ ] The Audit tab shows the release-all button with the right count, and a Release button on exactly the approved cards whose `task` is `none`.
- [ ] Pressing one sends exactly one dispatch request (checked in the browser's network panel against a stubbed response, Notes records it), and shows the 404, 401/403 and success messages above.
- [ ] The renewal steps on the page name both permissions.
- [ ] Works at phone width, in light and dark colour schemes; no token in URLs, the console or element text.
- [ ] `dotnet build` is clean and the fast tests are green.
- [ ] Notes says that the end-to-end run (dispatch, tasks on `work/dark-factory`, findings on `audit`) is checked by an interactive session after the next merge to `master`.

## Notes
## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
