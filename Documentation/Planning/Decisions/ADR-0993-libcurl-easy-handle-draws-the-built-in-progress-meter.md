# ADR-0993: A Libcurl easy handle draws the built-in progress meter

Status: Accepted. Decided by Claude under Stewart's delegation (BL-4012).

## Context

libcurl draws its built-in progress meter on `CURLOPT_STDERR` when `CURLOPT_NOPROGRESS` is 0 and no
progress callback is set, or when the callback returns `CURL_PROGRESSFUNC_CONTINUE` (`pgrsupdate` in
`lib/progress.c`). The managed engine called the callback only, so nothing was drawn (CGR-282).

## Decision

`CallbackTransferProgress` passes every report the callback answers with
`ProgressCallbackResult.Continue` to a `TransferProgressRecorder` (the curl tool's meter, moved to the
engine by BL-4018), made on first use; a handle with no callback gets a callback that always continues.
The header lines are written before the first status line, each ended with `Environment.NewLine` as the
tool's are, to `CURLOPT_STDERR`, else the engine's standard error. Once the perform ends, the done draws
are written and, only for a handle with no callback, a newline, as libcurl's `Curl_pgrsDone` writes it
only when `progress.callback` is unset. A callback answering 0 draws nothing. No resume line is drawn.
`ManagedEasyTransfer.FollowFinishingMeterAsync` ends the meter in a `finally`, so an attempt that throws
still stops the meter's clock; a callback that aborted, or a transfer that never started, writes no done
draws (BL-4057).

The bytes were not measured against a compiled libcurl program on this lane (no C toolchain); they are
the tool's meter, which is libcurl's own and was measured against curl 8.21.0 (BL-131).
