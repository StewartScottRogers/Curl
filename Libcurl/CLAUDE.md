# Libcurl

The libcurl-faithful programming interface to Curl (ADR-0506): named `Libcurl`, with no `Curl.`
prefix, to match upstream's library name. A naming exception, with `Curl.Console` and `Curl.Api`.
Root namespace `Libcurl`; each area is a folder and namespace (`Libcurl/Global` is `Libcurl.Global`),
with its tests in the same folder of `Libcurl.UnitTests`.

`Global` holds `CurlGlobal` (`curl_global_init`, `_init_mem`, `_sslset`, `_trace`) and `CurlLibrary`,
the object `Init` returns over an injected `ICurlEngine`, or over a new `ManagedCurlEngine` when none is
given; its `DisposeAsync` is `curl_global_cleanup`
(ADR-0556). `CurlLibrary` is `partial`: each handle area adds its public creating method
(`curl_easy_init` and the rest) as a `CurlLibrary` part in its own folder, over the internal
`Create*` methods in `Global`. Tests that count inits use `CurlGlobal.InitCounting` with their own
`GlobalInitCounter`, never the process-wide count, since tests run in parallel.

`Options` holds the options table (ADR-0560): `CurlOption` and `CurlOptions` (`curl_easy_option_by_name`,
`_by_id`, `_next`) generated from curl 8.22.0's `lib/easyoptions.c` and `curl.h`, and the internal
`EasyOptionDispatch` `SetOption` goes through. Each option group adds its setter rows by implementing
its `Add<Group>Setters` partial method in an `EasyOptionSetters` part in `Options/<Group>`, keeping the
namespace `Libcurl.Options`; an option no group sets yet answers `NotBuiltIn`. `Options/Behaviour` sets
the `EasyBehaviourOptions` options: an on/off option is on for any value but 0, the two permission
options take 0 to 0777, and an object option given a value its field cannot hold answers
`BadFunctionArgument`, as libcurl's `setopt.c` does. `Options/Network` sets the `EasyNetworkOptions`
options with `setopt.c`'s range checks, and turns the `PROTOCOLS` bitmasks into `PROTOCOLS_STR` names
(ADR-0568); `CURLU` still answers `NotBuiltIn` until BL-2462. `Options/Transfer` sets the
`EasyTransferOptions` options with `setopt.c`'s checks, each `_LARGE` or `_MS` alias setting the same
field as the option it extends, and clamps the two buffer sizes rather than refusing them (ADR-0572). `Options/Proxy` sets the
`EasyProxyOptions` options with `setopt.c`'s checks, keeps a copy of the `PROXYHEADER` list, and
turns `HAPROXYPROTOCOL` on whenever `HAPROXY_CLIENT_IP` is set (ADR-0576). `Options/Tls` sets the `EasyTlsOptions`
options (ADR-0580): `TlsPeerOptionSetters.Origin` and `.Proxy` write the origin's options and their
`PROXY_*` twins with one method each, blobs are copied, and `SSLENGINE` with a name,
`SSLENGINE_DEFAULT` and `SSL_FALSESTART` answer `NotBuiltIn`, Curl having no crypto engines or False Start.

`Options/Callbacks` implements `AddCallbackSetters` once, calling one partial method per callback area
(`AddReadWriteCallbackSetters`, `AddProgressCallbackSetters` and the rest), which each area implements in
an `EasyOptionSetters` part in `Libcurl/Callbacks/<Area>`, keeping the namespace `Libcurl.Options`
(ADR-0573). `Callbacks/ReadWrite` (not `Data`: the `data/` path rule refuses that folder name) sets
`CURLOPT_WRITEFUNCTION`, `READFUNCTION` and `HEADERFUNCTION` (their own delegate type or `null`, any other
delegate `BadFunctionArgument`) and the `*DATA` pointers: the delegate's closure is its user data, so a
`Stream` is the `FILE *` of libcurl's default function (`StreamDataCallbacks`, unless the caller's own
function is set) and any other value is taken and changes nothing. `Callbacks/Progress` sets
`CURLOPT_PROGRESSFUNCTION`, `XFERINFOFUNCTION` and `DEBUGFUNCTION` the same way, which the engine already
calls (ADR-0543); `XFERINFODATA` (`PROGRESSDATA` is the same option) and `DEBUGDATA` take any value and
change nothing (ADR-0577).

`Misc/ErrorText` holds `CurlErrorText`: `Multi`, `Share` and `Url` are `curl_multi_strerror`,
`curl_share_strerror` and `curl_url_strerror` with upstream's test1538 strings and fallbacks, and
`Header` is the header API's text, which upstream has no function for (ADR-0562). Each is a table
lookup, so a new upstream code is one appended string. `curl_easy_strerror` is still
`Curl.Console`'s `CurlEasyErrorText` until BL-2450 moves it.

`Misc/DatesAndLists` holds `CurlDate` and `CurlSlist` (ADR-0578). `CurlDate.Parse` is `curl_getdate`
over `Curl.Protocol.Abstractions`' `CurlDateParser` (the plan's "Curl.Core date parser"), answering -1
for text that is not a date. `CurlSlist` is `struct curl_slist` as an `IReadOnlyList<string>`, with
`Append` (`curl_slist_append`, a `null` list starting one) and `FreeAll` (`curl_slist_free_all`,
emptying it). Every list-valued option setter copies the list when it is set, so a list changed or
freed afterwards leaves the option as set; a new list setter must copy too.

`Easy` holds `CurlEasy`, the easy handle `CurlLibrary.EasyInit` (`curl_easy_init`) returns over the
engine's `IEasyTransfer` (ADR-0563): `SetOption` goes through `EasyOptionDispatch` and writes the
changed `EasyOptions` back to the transfer only on `Ok`; `PerformAsync` is the native shape, and
`Perform` blocks on it; `Upkeep` (`curl_easy_upkeep`) blocks on `UpkeepAsync` the same way, and these
two and `WebSocket`'s blocking calls are Libcurl's only sanctioned sync-over-async bridges. `Pause` is `curl_easy_pause` and passes the
engine only the `Receive` and `Send` bits, dropping the rest as libcurl does. `Dispose` and
`DisposeAsync` are `curl_easy_cleanup` and dispose the transfer once; a member used afterwards throws
`ObjectDisposedException`. `GetInfo` is `curl_easy_getinfo` and goes through `Info`'s dispatch.
`Reset` is `curl_easy_reset` and `DupHandle` is `curl_easy_duphandle`, each a call to the transfer's
`Reset` or `Duplicate`: what survives each (connections kept by a reset, only the options carried by a
duplicate) is the engine's contract on `IEasyTransfer`, not Libcurl's.

`WebSocket` holds `CurlEasyWebSocket`, libcurl's WebSocket API as `CurlEasy` extension methods over the
transfer's `IEasyWebSocket` (ADR-0585): `WebSocketRecv` (`curl_ws_recv`), `WebSocketSend` (`curl_ws_send`)
and `WebSocketStartFrame` (`curl_ws_start_frame`) block on their `Async` forms and keep libcurl's out
parameters, `WebSocketRecv` answering 0 bytes and no frame unless `Ok`; `WebSocketMeta` is `curl_ws_meta`. A
`null` or cleaned-up handle answers `BadFunctionArgument` (`WebSocketMeta` `null`) without the engine;
every other check is the engine's.

`Multi` holds `CurlMulti`, the multi handle `CurlLibrary.MultiInit` (`curl_multi_init`) returns over
the engine's `IMultiTransfer` (ADR-0570). It is `partial`: each multi area adds its members in its own
folder under `Libcurl/Multi`, keeping the namespace `Libcurl.Multi`. `Multi/Core` has `AddHandle`,
`RemoveHandle`, `PerformAsync`, `InfoRead` and `DisposeAsync` (`curl_multi_cleanup`); it maps each
added engine transfer back to its `CurlEasy`, so `InfoRead` answers a `CurlMessage` naming the handle
the caller added. A `null` or cleaned-up `CurlEasy` answers `BadEasyHandle`, one this multi holds
already `AddedAlready`, both without calling the engine. `Multi/Settings` has `SetOption`
(`curl_multi_setopt`, every `CurlMultiOption` through `MultiOptionSetters` with `multi.c`'s checks,
writing `MultiOptions` back only on `Ok`), `GetHandles` (the engine's transfers mapped to their
`CurlEasy`), `WaitFds`, `GetInfo` (`curl_multi_get_offt`) and `NotifyEnable`/`NotifyDisable`; a count
or notification `multi.h` lacks answers `UnknownOption` without calling the engine (ADR-0582).
`Multi/Wait` has `WaitAsync`, `PollAsync`, `Wakeup`, `Timeout` and `FdSet` (ADR-0584): a negative
`timeout_ms` answers `BadFunctionArgument` without the engine, and `FdSet` fills `ISet<CurlSocket>`
read, write and exception sets from the engine's `GetWaitSockets` (`curl_multi_waitfds`), since the
seam has no `fdset`.

`Share` holds `CurlShare`, the share handle `CurlLibrary.ShareInit` (`curl_share_init`) returns over
the engine's `IShareHandle`, and `CurlShareOption` (ADR-0581). `SetOption` is `curl_share_setopt`:
`SHARE`/`UNSHARE` of `COOKIE` to `HSTS` and the seam's lock delegates go to the engine, `NONE`, `SHARE`,
an unknown kind or option, or a value of the wrong type answer `BadOption` without it, and `USERDATA` is
kept as `UserData`. `Cleanup` is `curl_share_cleanup`; after the engine's `Ok` every call answers
`Invalid`. `CURLOPT_SHARE` (`Options/Behaviour`) stores a `CurlShare`'s engine handle, and a cleaned-up
`CurlShare` or `null` detaches.

`Mime/Mime` holds `CurlMime` (`curl_mime`, from `CurlLibrary.MimeInit`) and `CurlMimePart` (from
`AddPart`), namespace `Libcurl.Mime` (ADR-0583): each `curl_mime_*` part setter is one call to the
engine's `IMimePartBuilder`, whose answer it returns; `Dispose` is `curl_mime_free`, after which the
handle and its parts throw `ObjectDisposedException`. It implements `EasyOptionSetters`'
`AddMimePostSetters`: `CURLOPT_MIMEPOST` takes a `CurlMime` (else `BadFunctionArgument`) and stores its
body as it is when set. `AddFormPostSetters` is declared for `Mime/Form`'s `CURLOPT_HTTPPOST`.

`Info` holds `CurlInfo` (every `CURLINFO_*` of curl 8.22.0's `curl.h` by name and number) and the
internal `EasyInfoDispatch` `CurlEasy.GetInfo` goes through (ADR-0566): an id `curl.h` lacks answers
`UnknownOption`, a known info no group reads yet `NotBuiltIn`. Each info group adds its reader rows by
implementing its `Add<Group>Readers` partial method in an `EasyInfoReaders` part in `Info/<Group>`,
keeping the namespace `Libcurl.Info`, with each reader a named method in an explicit
`new EasyInfoReader(...)` (a lambda or cached method group counts against the adding method's
complexity). `Info/Strings` reads the strings, lists, `PRIVATE` and `TLS_SSL_PTR`, which answers a
`CurlTlsSessionInfo` with the build's backend and no native pointer. `Info/Times` reads the times,
sizes, speeds, content lengths, `RETRY_AFTER` and `EARLYDATA_SENT_T`: a time's `_T` form is its
`TimeSpan` in whole (truncated) microseconds and its double form that count over 1,000,000, as
`getinfo.c` derives both; a size or speed is the same `long` in both forms. `Info/Longs` reads every `_LONG` info, the
`_SOCKET` `ACTIVESOCKET` and the `_OFF_T` `FILETIME_T`, `XFER_ID` and `CONN_ID`, each the
`TransferInfo` `long` as it is (`FILETIME` and `FILETIME_T` the same seconds); the two sockets stay -1.

Reference `Curl.Api.UnitLibrary` and `Curl.Engine.UnitLibrary` only (project references). The engine
reference is for `CurlGlobal.Init(flags)`, the parameterless `curl_global_init`, which builds a
`ManagedCurlEngine` (ADR-0579); every other member takes its `ICurlEngine` from the library, so tests
inject fakes. Never reference `Curl.Api` or a protocol library.

Held to the four quality gates in the root `CLAUDE.md`. Its tests live in `Libcurl.UnitTests`,
which sorts after every `Curl.*` project.
