---
id: BL-1211
title: Show the Audit tab token's expiry and the full renewal steps on the board page
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [.github/board]
requirement: none
created: 2026-10-02
completed:
---
# BL-1211 — Show the Audit tab token's expiry and the full renewal steps on the board page

## Goal

The Audit tab tells Stewart when his saved token expires, warns him a week before, and on an expired or refused token shows the complete steps to make a new one, so he never has to remember how.

## Context

- Stewart, 2026-10-02: "when you need a new token because this one expires, please tell me how to do it. I will forget everything." His token is a fine-grained personal access token with a 30-day expiry (BL-1185), made 2026-10-02.
- GitHub sends the expiry with every API response to a request made with a fine-grained token, in the `GitHub-Authentication-Token-Expiration` header (e.g. `2026-11-01 12:00:00 -0700`). The page must read it with the request it already makes when a move reads the finding, or with one `GET https://api.github.com/repos/<repo>` when the token is saved and once per page load while one is saved, and with nothing more. Check first that the header is readable from the page's script (CORS exposes it), and say in Notes what was found; if it is not exposed, show the date Stewart's token was saved plus 30 days, labelled as an estimate.
- An expired, revoked or wrongly scoped token gets 401 (or 403 with a message naming the token). The page then shows the renewal steps and keeps the tab read-only until a new token is saved.
- The steps, word for word, so the page and Claude's memory say the same thing:
  1. Open https://github.com/settings/personal-access-tokens/new
  2. Name: `Curl board audit tab`
  3. Expiration: 30 days
  4. Resource owner: StewartScottRogers
  5. Repository access: Only select repositories, then Curl
  6. Permissions, Repository permissions, Contents: Read and write (Metadata read-only is added by itself; everything else No access)
  7. Generate token and copy it
  8. Here: press Forget token, paste the new token, save
  9. Optional: delete the expired token at https://github.com/settings/personal-access-tokens
- The link opens in a new tab. The steps are plain text, no browser dialogs. The token itself is never shown or logged.
- The page is not an audit path or a guard file; the task changes only `.github/board`.

## Acceptance criteria

- [ ] With a token saved, the Audit tab shows "Token expires <date> (in N days)" from the header, or the labelled estimate when the header is not readable.
- [ ] From 7 days before the expiry, that line is a warning and the renewal steps are shown under it.
- [ ] A 401 or token-refused 403 on any request made with the token shows "Your token has expired or was refused" and the renewal steps, and hides the move buttons until a new token is saved.
- [ ] A link in the token area ("How do I make a token?") shows the same steps at any time, token or not.
- [ ] Saving a token costs at most one extra API request, and a page load with a token saved at most one; Notes records the count from the browser's network panel.
- [ ] The token appears in no URL, console message or element text.
- [ ] Works at phone width and in light and dark colour schemes.
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes
## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
