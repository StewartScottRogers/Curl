---
id: BL-752
title: Match curl's --remove-on-error warning for a non-regular output file off Windows
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-494]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-752 — Match curl's --remove-on-error warning for a non-regular output file off Windows

## Goal

A failed `--remove-on-error` transfer whose output file is not a regular file (`-o /dev/null` on Linux and macOS) prints what the OpenSSL curl 8.21.0 prints there, byte for byte.

## Context

- BL-494 made `--remove-on-error` delete a failed transfer's output file through `IOutputPaths.TryDeleteFile` (`Curl.Console/PhysicalOutputPaths.cs`), and print `Warning: Failed removing: <file>` whenever that returns false, as the Windows curl 8.21.0 does for `-o NUL` (measured, BL-494 Notes).
- curl's `post_per_transfer` stats the file first and, for something that is not a regular file, is believed to print a different warning (`Skipping removal; not a regular file: <file>`) instead of trying to unlink it. That was not measured: the lane had only the Windows curl.
- Start at `CurlCommandRunner.RemoveOutputFileOfFailedTransferAsync`.

## Acceptance criteria

- [ ] Measured on a Linux or macOS curl 8.21.0: `curl --remove-on-error -o /dev/null <URL cut short>` stderr and exit code copied into Notes.
- [ ] `IOutputPaths` tells a non-regular file apart from a failed delete, and `Curl.Console.UnitTests` pins the measured line in a test marked `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` or through the fake, platform-neutral.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
