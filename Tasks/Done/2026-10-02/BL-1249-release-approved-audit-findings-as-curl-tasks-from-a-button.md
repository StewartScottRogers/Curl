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
completed: 2026-10-02
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

- [x] `release-approved-findings.yml` exists with the trigger, input, runner, concurrency and permissions above, and its steps commit tasks to `work/dark-factory` (with rebase, dedupe against the upstream commit, and retries) and findings to `audit`, and open but never merge the audit pull request.
- [x] Its script step runs `New-TasksFromAcceptedFindings.ps1 -WhatIf` first and prints that to the job summary, so a run shows what it will file before it files it.
- [x] The Audit tab shows the release-all button with the right count, and a Release button on exactly the approved cards whose `task` is `none`.
- [x] Pressing one sends exactly one dispatch request (checked in the browser's network panel against a stubbed response, Notes records it), and shows the 404, 401/403 and success messages above.
- [x] The renewal steps on the page name both permissions.
- [x] Works at phone width, in light and dark colour schemes; no token in URLs, the console or element text.
- [x] `dotnet build` is clean and the fast tests are green.
- [x] Notes says that the end-to-end run (dispatch, tasks on `work/dark-factory`, findings on `audit`) is checked by an interactive session after the next merge to `master`.

## Notes

- Decided by Claude under Stewart's delegation, recorded in ADR-0385: the workflow checks at run time (`Get-Command`) whether `New-TasksFromAcceptedFindings.ps1` declares `-Id` and `-WhatIf`. With `-Id` it files only the findings named; without it, it warns and releases every approved finding (the task's step 3 fallback). Without `-WhatIf` it refuses to run. A lane may not read the script (audit guard), so which case applies today is unknown here.
- Follow-up not filed: `task-board.ps1 new` with a `touches` naming the audit office's Tools folder was refused by the audit guard hook in this lane. An interactive session should check whether the script has `-Id` and, if not, file and do "Give New-TasksFromAcceptedFindings.ps1 an -Id filter" (`lane: no`).
- The workflow pairs findings with tasks from what changed, not from the script's output: findings whose `task:` line changed on the audit checkout, task files added on the factory checkout. It works whether the script commits or only edits files. After each rebase it runs `dedupe -Since <upstream>`, maps renumbered tasks back by file slug, amends the commit message with the final IDs, and rewrites the old ID in its finding before the audit commit.
- `GITHUB_ENV` and the job summary are written with BOM-less UTF-8: Windows PowerShell's `Out-File -Encoding utf8` writes a BOM that would corrupt the first variable's name.
- A pull request opened with `GITHUB_TOKEN` does not start CI by itself; the interactive session that merges the audit pull request may need to rerun CI on it.
- Page: a 403 from the dispatch does not turn the moves off (it means the token lacks Actions, while it still moves findings); a 401 does, as on every request. The dispatch sends `return_run_details: true` to link the run, and falls back to the workflow's runs page when the answer has no `html_url` (a 204).
- Fixtures: AF-0002 gained `task: none`; new AF-0009 (accepted, `task: BL-0999`) and AF-0010 (accepted, `task: none`).
- Browser check, 2026-10-02: headless Chrome via the DevTools protocol (a throwaway PowerShell script under %TEMP%), every request answered by the `Fetch` domain from the fixtures, with the dispatch stubbed. No token: no release buttons. With a token: "Release approved findings (2)", Release on exactly AF-0002 and AF-0010, AF-0009 shows "Released as BL-0999". Each press sent exactly one POST to `.../actions/workflows/release-approved-findings.yml/dispatches`, body `{"ref":"master","inputs":{"findings":"AF-0010"},"return_run_details":true}` (release-all: `"findings":""`), token only in the Authorization header. 404 showed "Available after the next merge to master."; 403 and 401 showed the need for Actions: Read and write as well as Contents: Read and write, with the edit steps; the moves stayed after the 403; a 200 showed "Releasing… (run)" linking the run. Token in no URL, console message, element text or HTML. At 390 px wide the page does not overflow (scrollWidth 390); light and dark screenshots read cleanly.
- The end-to-end run (dispatch, tasks on `work/dark-factory`, findings on `audit`) is checked by an interactive session after the next merge to `master`; GitHub dispatches only a workflow on the default branch.
- `dotnet build` clean; fast tests all green (0 failed).
## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. The board page's Release buttons dispatch release-approved-findings.yml, which files approved findings as Curl tasks
