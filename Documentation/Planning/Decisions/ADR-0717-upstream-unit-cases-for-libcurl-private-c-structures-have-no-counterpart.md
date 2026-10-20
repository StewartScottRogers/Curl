# ADR-0717 — Upstream's unit cases for libcurl's private C data structures have no C# counterpart

- Status: Accepted
- Date: 2026-10-19
- Task: BL-2849
- Decided by Claude under Stewart's delegation.

## Context

ADR-0711 (BL-2840) runs upstream curl 8.22.0's `unittest` cases as ported unit programs and
leaves eighteen of them to BL-2849: 1300, 1305, 1306, 1309, 1330, 1602, 1603, 1616, 1661, 2600,
2601, 2606, 3211, 3212, 3213, 3300, 3301 and 3306. Each tests a private C data structure,
allocator or build hook of libcurl (`tests/unit/unit<n>.c`). BL-2849 decides each by ADR-0711's
rule: a reason is lasting only where the case tests a C structure or allocator Curl does not
have; never "hard", never "the BCL lacks a primitive".

For each case this run searched the repository for a Curl type with that observable behaviour
(a hand-written list, hash table, splay tree, bit set, buffer queue, buffer reference, thread
pool or thread queue). There is none: Curl uses `List<T>`, `Dictionary<,>`, `HashSet<>`,
`SortedDictionary<,>`/`PriorityQueue<,>`, `System.Collections.BitArray`, `Memory<byte>`,
`Task`s, the .NET thread pool and `Channel<T>`, so no Curl code implements what these programs
check. The two places that might have: the bounded queue (2601) and the thread pool with failing
starts (3306), do not exist as Curl code either - Curl's transfers read from `IConnection`
streams and run on `Task`s.

## Decision

All eighteen are `no counterpart`. No port task is filed, since there is nothing to port. Each
reason names the C-only thing the case tests.

| Case | Row | The C-only thing it tests |
| --- | --- | --- |
| 1300 | no counterpart | `Curl_llist`, libcurl's intrusive doubly linked list; Curl uses `List<T>`/`LinkedList<T>` |
| 1305 | no counterpart | libcurl's private hash table behind the DNS cache; Curl uses `Dictionary<,>` |
| 1306 | no counterpart | the same program as 1305 (`<tool>` is `unit1305`), so the same reason |
| 1309 | no counterpart | the splay tree libcurl keys its timer list on; Curl has no splay tree and orders timers with .NET collections |
| 1330 | no counterpart | `curl_dbg_*` memory tracking (`TrackMemory`), a C allocator wrapper; managed memory has no malloc/free to track |
| 1602 | no counterpart | libcurl's hash table with integer keys; Curl uses `Dictionary<int,>` |
| 1603 | no counterpart | libcurl's hash table with string keys; Curl uses `Dictionary<string,>` |
| 1616 | no counterpart | `uint_hash`, libcurl's integer-keyed hash table variant; Curl uses `Dictionary<,>` |
| 1661 | no counterpart | `bufref`, a C struct that borrows a `char *` buffer and a free callback; Curl passes `Memory<byte>`/`string` |
| 2600 | no counterpart | connection filter chain connect and destroy (`Debug`, `!win32`); Curl connects through `IConnection`, with no filter chain |
| 2601 | no counterpart | `bufq`, libcurl's chunked buffer queue with a chunk pool; Curl reads and writes `Stream`s and `Channel<T>`s |
| 2606 | no counterpart | `Curl_conn_adjust_pollset`, adjusting a C `poll` set; Curl waits on `Task`s and has no pollset |
| 3211 | no counterpart | `uint_bset`, a C bitset over small integers; Curl uses `BitArray` where it needs one |
| 3212 | no counterpart | `uint_hashset`, a C hash set of integers; Curl uses `HashSet<int>` |
| 3213 | no counterpart | `uint32_tbl` and `uint_spbset`, a C integer table and sparse bitset; Curl uses `Dictionary<,>`/`HashSet<>` |
| 3300 | no counterpart | libcurl's own thread pool (`Curl_thrdpool`); Curl runs work on the .NET thread pool through `Task`s |
| 3301 | no counterpart | libcurl's thread queue (`Curl_thrdq`); Curl uses `Channel<T>` |
| 3306 | no counterpart | the thread queue's retry of failed thread starts (`Debug`, `CURL_DBG_THRDPOOL_FAIL_STARTS=5`); `Task` scheduling cannot fail to start a thread the way `pthread_create` does |

None of the reasons is that a port is hard or that the BCL lacks a primitive: each is that Curl
has no code doing what the case checks. If Curl later hand-builds one of these structures (for
example a bounded queue of its own), the case gets a port task depending on BL-2841 then.

## Consequences

- ADR-0711's rows for these eighteen cases agree with this table (all `no counterpart (BL-2849)`);
  the gap scorecard keeps them excluded with these reasons.
- No project, option or `--ai-help` text changes.
