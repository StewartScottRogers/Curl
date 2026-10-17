# Curl.Conformance.LibTest.UnitLibrary

C# ports of upstream curl 8.22.0's `tests/libtest` programs, written over `Libcurl`, which the
upstream case runner runs for a case whose `<client><tool>` line names `lib%TESTNUMBER` or
`libNNN` (phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md`, ADR-0506). Namespace
`Curl.Conformance.LibTest`.

## Shape

- `ILibTest` is one program: `ProgramName` (such as `lib500`) and `RunAsync(context, token)`,
  libtest's `test()`.
- `LibTestContext` is what `first.c` hands `test()`: `TestNumber` (`testnum`), `Arguments` (after
  the program name, copied, possibly none), `Url` (`argv[1]`, `null` when no argument was given, as `first.c` leaves `URL` for a case with no `<command>`) and `Argument2` to `Argument4`
  (`libtest_arg2` to `_arg4`, `null` when not given), and, set by the case runner, the `Engine`
  the program's `curl_global_init` takes (reaching the case's in-memory servers; the program's
  cleanup disposes it), its `StandardOutput` and its `StandardError`.
- `LibTestPrograms` is the table of ported programs by `ProgramName` the case runner looks a
  case's `<tool>` up in (BL-2556): an explicit list, never a reflection scan (AOT).
  `LibTestPrograms.Ported` is the table each port task appends its program to.
- `LibTestResult` is the exit code `main()` returns: `Success` (0), or `FromCurlCode` by number.
- `LibTestErrorCodes` is `first.h`'s `TEST_ERR_*` codes, 8.22.0's `CURLE_OBSOLETE*` values (ADR-0653).
- `LibTestSteps` is `first.h`'s step macros carrying `result` as `Result`: `GlobalInit`,
  `EasyInit`, `EasySetOption` (`easy_setopt`), `EasyPerformAsync`, `EasyGetInfo` and
  `SlistAppend`. Each answers whether the program goes on; on `false` the port goes to its
  cleanup and returns `ToLibTestResult()`. The multi steps (`MultiInit`, `MultiSetOption`,
  `MultiAddHandle`, `MultiRemoveHandle`, `MultiPerformAsync`, `MultiFdSet`, `MultiTimeout`,
  `MultiPollAsync`, `MultiWaitAsync`, `MultiWakeup`), `ShareInit`, `UrlInit` and
  `AbortOnTestTimeout` are in its other `partial` files (ADR-0655).
- `LibTestClock` is libtest's time over an injected `TimeProvider`: `Now` (`curlx_now`),
  `DifferenceMilliseconds` (`curlx_timediff_ms`), `StartTestTiming`, `WaitAsync` (`curlx_wait_ms`) and `Wait`, the same wait blocking inside a synchronous callback.
- `LibTestUtilities` is `testutil.c`: `SubUrl` (`tutil_suburl`) and `ThrowawayWrite`
  (`tutil_throwaway_cb`).
- `LibTestDebugTrace` is `testtrace.c`'s `libtest_debug_cb` and `debug_dump`; `LibTestCliTrace` is
  its `cli_debug_cb`, one instance per program, `CallbackFor(easy)` giving the `DebugCallback`.

- The ports are `Lib500` to `Lib509` (BL-2527), `Lib510` to `Lib519` (BL-2528), `Lib530` with its `Lib530Run` (BL-2595, ADR-0663), and `Lib525`, `Lib526` and `Lib533` (BL-2596), `Lib539` to `Lib553` (BL-2530; lib542 and lib549 are `LibOneVerboseTransfer` option lists), and `Lib554` to `Lib566` but lib557, lib561, lib563 and lib565 (BL-2531; lib557 is BL-2612), and `Lib567` to `Lib576` (BL-2532), and `Lib578`, `Lib579` (with `Lib579Progress`), `Lib582` (with `Lib582Run`, its socket-action loop), `Lib583`, `Lib589` to `Lib591` and `Lib597` to `Lib599` (BL-2533), and `Lib643` (with its `ByteReader`, which lib654 shares), `Lib650` to `Lib655`, `Lib658`, `Lib659` and `Lib661` (BL-2534), and `Lib666` to `Lib668`, `Lib670` (cases 670 to 673, with its `ReadThis` callbacks), `Lib674`, `Lib676` to `Lib678` (`Lib677.Exchange` is its connect-only loop), `Lib694` and `Lib695` (BL-2535; `LibTestSteps.EasyPerformOrWriteAsync` is the perform with a fixed failure line lib667, lib668 and lib676 share), and `Lib751`, `Lib753` (with its `TransferStatus` callbacks), `Lib757`, `Lib758` (the program as built without OpenSSL 3, which succeeds at once), `Lib766`, `Lib1156`, `Lib1301`, `Lib1308`, `Lib1396` and `Lib1398` (BL-2536; `LibTestAssertions` is `unitcheck.h`'s `fail_unless`, `abort_unless` and `fail` with the `unitfail` count lib1301, lib1308, lib1396 and lib1398 share), and `Lib1485` (with its `TransferStatus` callbacks), `Lib1500` to `Lib1502` (lib1502 serving cases 1502 to 1505, one cleanup order each), and `Lib1506` to `Lib1511` (BL-2537; `LibTestServerResolves` is the `serverN.example.com` resolve entries and URLs lib1506 and lib1510 share), and `Lib1512` to `Lib1515` (lib1514 serving cases 1514 and 1539, lib1515 cases 1515 and 1516), `Lib1517`, `Lib1518` (cases 1518, 1519 and 1543), `Lib1520`, `Lib1522` and `Lib1523` (BL-2538; lib1521 is BL-2642; `LibTestClock.Wait` is the blocking `curlx_wait_ms` lib1517's read callback makes), and `LibProxyTunnelUpload` (lib1525 to lib1527, the tunnelled upload with their header lists, as `Unified`, `Separate` and `ExpectContinue`), `Lib1528` to `Lib1532`, `Lib1533` (with its `PausedPoster` callbacks) and `Lib1534` (BL-2539), and `Lib1535` to `Lib1538`, `Lib1540` (with its `TransferStatus` callbacks), `Lib1541`, `Lib1542`, `Lib1545`, `Lib1549` and `Lib1550` (BL-2540), and `Lib1551` to `Lib1559` (BL-2541; lib1560 is BL-2652; `Lib1552.WaitLoopAsync` is the counted `curl_multi_wait` loop lib1553 shares), and `Lib1564`, `Lib1565` (its second thread a task handing handles over under a lock), `Lib1567` to `Lib1569`, `Lib1571` (cases 1571 to 1575 and 1581), `Lib1576` (cases 1576 to 1580), `Lib1582`, `Lib1587` (the program as built without OpenSSL, which succeeds at once) and `Lib1588` (BL-2542) (BL-2534 notes: `LibTestMimeCalls.FirstFailure` is the `if(!result) result = curl_mime_...` chain lib643 and lib652 share, and `LibTestSteps.EasyPerformOrReportAsync` the perform with its own failure line lib658 and lib659 share), where `LibTestArgumentFile` writes the progress lines lib578, lib579 and lib599 put in `libtest_arg2`'s file and `LibTestSteps.PerformWithMultiTimeoutUntilDoneAsync` is the `curl_multi_timeout`-driven loop lib591 and lib597 share, with `LibTestRtspRequests` the numbered stream URIs and upload request the RTSP ports share and `SingleMultiTransferAsync`'s `afterTransfer` the check lib573 makes once its loop ends. `LibTestUploadFile` opens the upload file lib505 and lib541 share; `LibTestIoctl` holds the `CURLOPT_IOCTLFUNCTION` constants. `LibTestRun` runs a port's public `TestAsync` after
  `curl_global_init` (`WithLibraryAsync` for a hand-written check, `WithMacroLibraryAsync` for
  `global_init`), so a test can hand `TestAsync` a cleaned-up library; every port that transfers sets
  `CURLOPT_WRITEDATA` to the program's stdout first, and `select_test` is `SelectTestAsync`, a
  `curl_multi_wait` for the same timeout (ADR-0662). `LibTestCountingAllocator` is `lib509.c`'s
  counting `curl_global_init_mem` callbacks.

## Rules

- Reference `Libcurl` and `Curl.Protocol.Abstractions.UnitLibrary` only; never a protocol library,
  `Curl.Api` or `Curl.Conformance.UnitLibrary` (the case runner references this library, not the
  other way round).
- Held to the `*.UnitLibrary` gates: 100% line and branch coverage, complexity at most 10.
