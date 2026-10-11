---
id: BL-2071
title: Redraw the gap dashboard's trend chart so it reads cleanly with hundreds of runs
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [.github/gaps]
lane: no
requirement: none
created: 2026-10-11
completed: 2026-10-11
---
# BL-2071 — Redraw the gap dashboard's trend chart so it reads cleanly with hundreds of runs

## Goal

The gap dashboard's Trend chart looks professional and stays readable with hundreds of runs.

## Context

Stewart, 2026-10-11, with a screenshot of the board's Gaps tab: "Please make this graph much more professional. I need it to be good for hundreds of data points." The chart in `.github/gaps/site/index.html` drew a 640×220 viewBox scaled to the full width. At desktop width that made its labels about 35 px and its dots 20 px. It placed runs evenly by index, put a 5 px dot on every point, and labelled only the first and last stamp.

## Acceptance criteria

- [x] The chart is drawn at its container's real width (redrawn on resize), so ticks stay 11 px and lines 1.5 px (overall 2.5 px).
- [x] Runs sit on a time axis read from their stamps, with round hour, day or month ticks. The percentage axis has round gridlines ending at 100%.
- [x] Dots show only while runs are sparse. Hovering, tapping or using the arrow keys on the chart shows a guide line and a tooltip with the run's time, commit, platform and every visible score, sorted.
- [x] The legend buttons show and hide each line and give its latest score. Range buttons (All, 90, 30, 7 days) appear when the history is longer than the range.
- [x] `.github/gaps/fixtures/long-history.json` holds 300 made-up runs. The page draws it and `fixtures/data.json` with no console error, checked in Chrome on 2026-10-11.

## Notes

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Done. Trend chart redrawn: real-width SVG, time axis, hover guide and tooltip, legend toggles, range picker; checked with 300 runs.
