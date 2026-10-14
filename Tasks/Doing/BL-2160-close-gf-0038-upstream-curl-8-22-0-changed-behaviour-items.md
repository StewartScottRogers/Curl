---
id: BL-2160
title: Close GF-0038: Upstream curl 8.22.0 changed behaviour items
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2161, BL-2162, BL-2163, BL-2164, BL-2165, BL-2166, BL-2167, BL-2168, BL-2169, BL-2171, BL-2172, BL-2173, BL-2174, BL-2175, BL-2176]
touches: [Gap]
lane: no
requirement: none
created: 2026-10-13
completed:
---
# BL-2160 — Close GF-0038: Upstream curl 8.22.0 changed behaviour items

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0038 (Upstream curl 8.22.0 changed behaviour items), so a later gap analysis measures each of `behaviour:test1062`, `behaviour:test1088`, `behaviour:test1113`, `behaviour:test1133`, `behaviour:test1136`, `behaviour:test1177`, `behaviour:test1253`, `behaviour:test1255`, `behaviour:test1309`, `behaviour:test1315`, `behaviour:test1396`, `behaviour:test1398`, `behaviour:test1462`, `behaviour:test1479`, `behaviour:test1498`, `behaviour:test1538`, `behaviour:test1554`, `behaviour:test1557`, `behaviour:test1560`, `behaviour:test1609`, `behaviour:test1616`, `behaviour:test162`, `behaviour:test1625`, `behaviour:test1627`, `behaviour:test1641`, `behaviour:test1642`, `behaviour:test1677`, `behaviour:test1678`, `behaviour:test1712`, `behaviour:test1740`, `behaviour:test1741`, `behaviour:test1961`, `behaviour:test1978`, `behaviour:test1985`, `behaviour:test2010`, `behaviour:test2039`, `behaviour:test2052`, `behaviour:test2054`, `behaviour:test2055`, `behaviour:test2057`, `behaviour:test2059`, `behaviour:test2064`, `behaviour:test2067`, `behaviour:test2072`, `behaviour:test2087`, `behaviour:test2093`, `behaviour:test2094`, `behaviour:test2100`, `behaviour:test2109`, `behaviour:test2110`, `behaviour:test2113`, `behaviour:test2114`, `behaviour:test2115`, `behaviour:test2116`, `behaviour:test2117`, `behaviour:test2118`, `behaviour:test2119`, `behaviour:test2305`, `behaviour:test2311`, `behaviour:test2318`, `behaviour:test234`, `behaviour:test2349`, `behaviour:test2397`, `behaviour:test2405`, `behaviour:test2407`, `behaviour:test2414`, `behaviour:test258`, `behaviour:test2606`, `behaviour:test2885`, `behaviour:test3002`, `behaviour:test3003`, `behaviour:test3004`, `behaviour:test3005`, `behaviour:test3006`, `behaviour:test3023`, `behaviour:test311`, `behaviour:test316`, `behaviour:test320`, `behaviour:test3203`, `behaviour:test321`, `behaviour:test3211`, `behaviour:test322`, `behaviour:test3223`, `behaviour:test3224`, `behaviour:test3225`, `behaviour:test3226`, `behaviour:test3227`, `behaviour:test3228`, `behaviour:test3229`, `behaviour:test3302`, `behaviour:test3303`, `behaviour:test3304`, `behaviour:test3306`, `behaviour:test345`, `behaviour:test409`, `behaviour:test433`, `behaviour:test459`, `behaviour:test467`, `behaviour:test485`, `behaviour:test5000`, `behaviour:test5001`, `behaviour:test5002`, `behaviour:test5003`, `behaviour:test5004`, `behaviour:test5005`, `behaviour:test5006`, `behaviour:test5007`, `behaviour:test5008`, `behaviour:test5009`, `behaviour:test5010`, `behaviour:test5011`, `behaviour:test5012`, `behaviour:test5013`, `behaviour:test5014`, `behaviour:test5015`, `behaviour:test5016`, `behaviour:test5017`, `behaviour:test5018`, `behaviour:test5019`, `behaviour:test5020`, `behaviour:test5021`, `behaviour:test5022`, `behaviour:test5023`, `behaviour:test5024`, `behaviour:test5025`, `behaviour:test5026`, `behaviour:test5027`, `behaviour:test5028`, `behaviour:test529`, `behaviour:test546`, `behaviour:test550`, `behaviour:test557`, `behaviour:test565`, `behaviour:test574`, `behaviour:test644`, `behaviour:test687`, `behaviour:test688`, `behaviour:test716`, `behaviour:test73`, `behaviour:test739`, `behaviour:test798`, `behaviour:test838`, `behaviour:test845`, `behaviour:test884`, `behaviour:test890`, `behaviour:test91`, `behaviour:test943`, `behaviour:test949`, `behaviour:test956`, `behaviour:test958`, `behaviour:test964` as `match`.

## Context

- Finding: GF-0038, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: 8.22.0.
- Items: `behaviour:test1062`, `behaviour:test1088`, `behaviour:test1113`, `behaviour:test1133`, `behaviour:test1136`, `behaviour:test1177`, `behaviour:test1253`, `behaviour:test1255`, `behaviour:test1309`, `behaviour:test1315`, `behaviour:test1396`, `behaviour:test1398`, `behaviour:test1462`, `behaviour:test1479`, `behaviour:test1498`, `behaviour:test1538`, `behaviour:test1554`, `behaviour:test1557`, `behaviour:test1560`, `behaviour:test1609`, `behaviour:test1616`, `behaviour:test162`, `behaviour:test1625`, `behaviour:test1627`, `behaviour:test1641`, `behaviour:test1642`, `behaviour:test1677`, `behaviour:test1678`, `behaviour:test1712`, `behaviour:test1740`, `behaviour:test1741`, `behaviour:test1961`, `behaviour:test1978`, `behaviour:test1985`, `behaviour:test2010`, `behaviour:test2039`, `behaviour:test2052`, `behaviour:test2054`, `behaviour:test2055`, `behaviour:test2057`, `behaviour:test2059`, `behaviour:test2064`, `behaviour:test2067`, `behaviour:test2072`, `behaviour:test2087`, `behaviour:test2093`, `behaviour:test2094`, `behaviour:test2100`, `behaviour:test2109`, `behaviour:test2110`, `behaviour:test2113`, `behaviour:test2114`, `behaviour:test2115`, `behaviour:test2116`, `behaviour:test2117`, `behaviour:test2118`, `behaviour:test2119`, `behaviour:test2305`, `behaviour:test2311`, `behaviour:test2318`, `behaviour:test234`, `behaviour:test2349`, `behaviour:test2397`, `behaviour:test2405`, `behaviour:test2407`, `behaviour:test2414`, `behaviour:test258`, `behaviour:test2606`, `behaviour:test2885`, `behaviour:test3002`, `behaviour:test3003`, `behaviour:test3004`, `behaviour:test3005`, `behaviour:test3006`, `behaviour:test3023`, `behaviour:test311`, `behaviour:test316`, `behaviour:test320`, `behaviour:test3203`, `behaviour:test321`, `behaviour:test3211`, `behaviour:test322`, `behaviour:test3223`, `behaviour:test3224`, `behaviour:test3225`, `behaviour:test3226`, `behaviour:test3227`, `behaviour:test3228`, `behaviour:test3229`, `behaviour:test3302`, `behaviour:test3303`, `behaviour:test3304`, `behaviour:test3306`, `behaviour:test345`, `behaviour:test409`, `behaviour:test433`, `behaviour:test459`, `behaviour:test467`, `behaviour:test485`, `behaviour:test5000`, `behaviour:test5001`, `behaviour:test5002`, `behaviour:test5003`, `behaviour:test5004`, `behaviour:test5005`, `behaviour:test5006`, `behaviour:test5007`, `behaviour:test5008`, `behaviour:test5009`, `behaviour:test5010`, `behaviour:test5011`, `behaviour:test5012`, `behaviour:test5013`, `behaviour:test5014`, `behaviour:test5015`, `behaviour:test5016`, `behaviour:test5017`, `behaviour:test5018`, `behaviour:test5019`, `behaviour:test5020`, `behaviour:test5021`, `behaviour:test5022`, `behaviour:test5023`, `behaviour:test5024`, `behaviour:test5025`, `behaviour:test5026`, `behaviour:test5027`, `behaviour:test5028`, `behaviour:test529`, `behaviour:test546`, `behaviour:test550`, `behaviour:test557`, `behaviour:test565`, `behaviour:test574`, `behaviour:test644`, `behaviour:test687`, `behaviour:test688`, `behaviour:test716`, `behaviour:test73`, `behaviour:test739`, `behaviour:test798`, `behaviour:test838`, `behaviour:test845`, `behaviour:test884`, `behaviour:test890`, `behaviour:test91`, `behaviour:test943`, `behaviour:test949`, `behaviour:test956`, `behaviour:test958`, `behaviour:test964`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.22.0 release tarball.
- Touches: none. The finding named no Curl project outside the office, so this task has no touches and runs alone.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

- behaviour:test1062: changed in 8.22.0.

- behaviour:test1088: changed in 8.22.0.

- behaviour:test1113: changed in 8.22.0.

- behaviour:test1133: changed in 8.22.0.

- behaviour:test1136: changed in 8.22.0.

- behaviour:test1177: changed in 8.22.0.

- behaviour:test1253: changed in 8.22.0.

- behaviour:test1255: changed in 8.22.0.

- behaviour:test1309: changed in 8.22.0.

- behaviour:test1315: changed in 8.22.0.

- behaviour:test1396: changed in 8.22.0.

- behaviour:test1398: changed in 8.22.0.

- behaviour:test1462: changed in 8.22.0.

- behaviour:test1479: changed in 8.22.0.

- behaviour:test1498: changed in 8.22.0.

- behaviour:test1538: changed in 8.22.0.

- behaviour:test1554: changed in 8.22.0.

- behaviour:test1557: changed in 8.22.0.

- behaviour:test1560: changed in 8.22.0.

- behaviour:test1609: added in 8.22.0.

- behaviour:test1616: changed in 8.22.0.

- behaviour:test162: changed in 8.22.0.

- behaviour:test1625: changed in 8.22.0.

- behaviour:test1627: changed in 8.22.0.

- behaviour:test1641: changed in 8.22.0.

- behaviour:test1642: changed in 8.22.0.

- behaviour:test1677: changed in 8.22.0.

- behaviour:test1678: added in 8.22.0.

- behaviour:test1701: removed in 8.22.0.

- behaviour:test1712: changed in 8.22.0.

- behaviour:test1740: added in 8.22.0.

- behaviour:test1741: added in 8.22.0.

- behaviour:test1961: added in 8.22.0.

- behaviour:test1978: changed in 8.22.0.

- behaviour:test1985: added in 8.22.0.

- behaviour:test2010: changed in 8.22.0.

- behaviour:test2039: changed in 8.22.0.

- behaviour:test2052: changed in 8.22.0.

- behaviour:test2054: changed in 8.22.0.

- behaviour:test2055: changed in 8.22.0.

- behaviour:test2057: changed in 8.22.0.

- behaviour:test2059: changed in 8.22.0.

- behaviour:test2064: changed in 8.22.0.

- behaviour:test2067: changed in 8.22.0.

- behaviour:test2072: changed in 8.22.0.

- behaviour:test2087: changed in 8.22.0.

- behaviour:test2093: added in 8.22.0.

- behaviour:test2094: added in 8.22.0.

- behaviour:test2100: changed in 8.22.0.

- behaviour:test2109: added in 8.22.0.

- behaviour:test2110: added in 8.22.0.

- behaviour:test2113: added in 8.22.0.

- behaviour:test2114: added in 8.22.0.

- behaviour:test2115: added in 8.22.0.

- behaviour:test2116: added in 8.22.0.

- behaviour:test2117: added in 8.22.0.

- behaviour:test2118: added in 8.22.0.

- behaviour:test2119: added in 8.22.0.

- behaviour:test2305: added in 8.22.0.

- behaviour:test2311: changed in 8.22.0.

- behaviour:test2318: added in 8.22.0.

- behaviour:test234: changed in 8.22.0.

- behaviour:test2349: added in 8.22.0.

- behaviour:test2397: added in 8.22.0.

- behaviour:test2405: changed in 8.22.0.

- behaviour:test2407: changed in 8.22.0.

- behaviour:test2414: added in 8.22.0.

- behaviour:test258: changed in 8.22.0.

- behaviour:test2606: added in 8.22.0.

- behaviour:test2885: added in 8.22.0.

- behaviour:test3002: changed in 8.22.0.

- behaviour:test3003: changed in 8.22.0.

- behaviour:test3004: changed in 8.22.0.

- behaviour:test3005: changed in 8.22.0.

- behaviour:test3006: changed in 8.22.0.

- behaviour:test3023: changed in 8.22.0.

- behaviour:test311: changed in 8.22.0.

- behaviour:test316: changed in 8.22.0.

- behaviour:test320: changed in 8.22.0.

- behaviour:test3203: changed in 8.22.0.

- behaviour:test321: changed in 8.22.0.

- behaviour:test3211: changed in 8.22.0.

- behaviour:test322: changed in 8.22.0.

- behaviour:test3223: added in 8.22.0.

- behaviour:test3224: added in 8.22.0.

- behaviour:test3225: added in 8.22.0.

- behaviour:test3226: added in 8.22.0.

- behaviour:test3227: added in 8.22.0.

- behaviour:test3228: added in 8.22.0.

- behaviour:test3229: added in 8.22.0.

- behaviour:test323: removed in 8.22.0.

- behaviour:test324: removed in 8.22.0.

- behaviour:test3302: changed in 8.22.0.

- behaviour:test3303: changed in 8.22.0.

- behaviour:test3304: changed in 8.22.0.

- behaviour:test3306: added in 8.22.0.

- behaviour:test345: changed in 8.22.0.

- behaviour:test409: added in 8.22.0.

- behaviour:test433: changed in 8.22.0.

- behaviour:test459: changed in 8.22.0.

- behaviour:test467: changed in 8.22.0.

- behaviour:test485: changed in 8.22.0.

- behaviour:test5000: added in 8.22.0.

- behaviour:test5001: added in 8.22.0.

- behaviour:test5002: added in 8.22.0.

- behaviour:test5003: added in 8.22.0.

- behaviour:test5004: added in 8.22.0.

- behaviour:test5005: added in 8.22.0.

- behaviour:test5006: added in 8.22.0.

- behaviour:test5007: added in 8.22.0.

- behaviour:test5008: added in 8.22.0.

- behaviour:test5009: added in 8.22.0.

- behaviour:test5010: added in 8.22.0.

- behaviour:test5011: added in 8.22.0.

- behaviour:test5012: added in 8.22.0.

- behaviour:test5013: added in 8.22.0.

- behaviour:test5014: added in 8.22.0.

- behaviour:test5015: added in 8.22.0.

- behaviour:test5016: added in 8.22.0.

- behaviour:test5017: added in 8.22.0.

- behaviour:test5018: added in 8.22.0.

- behaviour:test5019: added in 8.22.0.

- behaviour:test5020: added in 8.22.0.

- behaviour:test5021: added in 8.22.0.

- behaviour:test5022: added in 8.22.0.

- behaviour:test5023: added in 8.22.0.

- behaviour:test5024: added in 8.22.0.

- behaviour:test5025: added in 8.22.0.

- behaviour:test5026: added in 8.22.0.

- behaviour:test5027: added in 8.22.0.

- behaviour:test5028: added in 8.22.0.

- behaviour:test529: changed in 8.22.0.

- behaviour:test546: changed in 8.22.0.

- behaviour:test550: changed in 8.22.0.

- behaviour:test557: changed in 8.22.0.

- behaviour:test565: changed in 8.22.0.

- behaviour:test574: changed in 8.22.0.

- behaviour:test644: changed in 8.22.0.

- behaviour:test687: changed in 8.22.0.

- behaviour:test688: changed in 8.22.0.

- behaviour:test716: changed in 8.22.0.

- behaviour:test73: changed in 8.22.0.

- behaviour:test739: changed in 8.22.0.

- behaviour:test798: changed in 8.22.0.

- behaviour:test838: changed in 8.22.0.

- behaviour:test845: changed in 8.22.0.

- behaviour:test884: changed in 8.22.0.

- behaviour:test890: changed in 8.22.0.

- behaviour:test91: changed in 8.22.0.

- behaviour:test943: changed in 8.22.0.

- behaviour:test949: changed in 8.22.0.

- behaviour:test956: changed in 8.22.0.

- behaviour:test958: changed in 8.22.0.

- behaviour:test964: changed in 8.22.0.

Suggestion, copied from the finding:

Bring these items to Curl when the target moves to 8.22.0.

## Acceptance criteria

- [ ] `behaviour:test1062`: Curl answers what curl 8.22.0 answers, `upstream test1062 passes`, so the item measures `match`.
- [ ] `behaviour:test1088`: Curl answers what curl 8.22.0 answers, `upstream test1088 passes`, so the item measures `match`.
- [ ] `behaviour:test1113`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test1133`: Curl answers what curl 8.22.0 answers, `upstream test1133 passes`, so the item measures `match`.
- [ ] `behaviour:test1136`: Curl answers what curl 8.22.0 answers, `upstream test1136 passes`, so the item measures `match`.
- [ ] `behaviour:test1177`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test1253`: Curl answers what curl 8.22.0 answers, `upstream test1253 passes`, so the item measures `match`.
- [ ] `behaviour:test1255`: Curl answers what curl 8.22.0 answers, `upstream test1255 passes`, so the item measures `match`.
- [ ] `behaviour:test1309`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test1315`: Curl answers what curl 8.22.0 answers, `upstream test1315 passes`, so the item measures `match`.
- [ ] `behaviour:test1396`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test1398`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test1462`: Curl answers what curl 8.22.0 answers, `upstream test1462 passes`, so the item measures `match`.
- [ ] `behaviour:test1479`: Curl answers what curl 8.22.0 answers, `upstream test1479 passes`, so the item measures `match`.
- [ ] `behaviour:test1498`: Curl answers what curl 8.22.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [ ] `behaviour:test1538`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test1554`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test1557`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test1560`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test1609`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test1616`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test162`: Curl answers what curl 8.22.0 answers, `upstream test162 passes`, so the item measures `match`.
- [ ] `behaviour:test1625`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test1627`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test1641`: Curl answers what curl 8.22.0 answers, `upstream test1641 passes`, so the item measures `match`.
- [ ] `behaviour:test1642`: Curl answers what curl 8.22.0 answers, `upstream test1642 passes`, so the item measures `match`.
- [ ] `behaviour:test1677`: Curl answers what curl 8.22.0 answers, `upstream test1677 passes`, so the item measures `match`.
- [ ] `behaviour:test1678`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test1712`: Curl answers what curl 8.22.0 answers, `the reference curl's output`, so the item measures `match`.
- [ ] `behaviour:test1740`: Curl answers what curl 8.22.0 answers, `the reference curl's output`, so the item measures `match`.
- [ ] `behaviour:test1741`: Curl answers what curl 8.22.0 answers, `upstream test1741 passes`, so the item measures `match`.
- [ ] `behaviour:test1961`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test1978`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test1985`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test2010`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test2039`: Curl answers what curl 8.22.0 answers, `upstream test2039 passes`, so the item measures `match`.
- [ ] `behaviour:test2052`: Curl answers what curl 8.22.0 answers, `upstream test2052 passes`, so the item measures `match`.
- [ ] `behaviour:test2054`: Curl answers what curl 8.22.0 answers, `upstream test2054 passes`, so the item measures `match`.
- [ ] `behaviour:test2055`: Curl answers what curl 8.22.0 answers, `upstream test2055 passes`, so the item measures `match`.
- [ ] `behaviour:test2057`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test2059`: Curl answers what curl 8.22.0 answers, `upstream test2059 passes`, so the item measures `match`.
- [ ] `behaviour:test2064`: Curl answers what curl 8.22.0 answers, `upstream test2064 passes`, so the item measures `match`.
- [ ] `behaviour:test2067`: Curl answers what curl 8.22.0 answers, `upstream test2067 passes`, so the item measures `match`.
- [ ] `behaviour:test2072`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test2087`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test2093`: Curl answers what curl 8.22.0 answers, `upstream test2093 passes`, so the item measures `match`.
- [ ] `behaviour:test2094`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test2100`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test2109`: Curl answers what curl 8.22.0 answers, `upstream test2109 passes`, so the item measures `match`.
- [ ] `behaviour:test2110`: Curl answers what curl 8.22.0 answers, `upstream test2110 passes`, so the item measures `match`.
- [ ] `behaviour:test2113`: Curl answers what curl 8.22.0 answers, `upstream test2113 passes`, so the item measures `match`.
- [ ] `behaviour:test2114`: Curl answers what curl 8.22.0 answers, `upstream test2114 passes`, so the item measures `match`.
- [ ] `behaviour:test2115`: Curl answers what curl 8.22.0 answers, `upstream test2115 passes`, so the item measures `match`.
- [ ] `behaviour:test2116`: Curl answers what curl 8.22.0 answers, `upstream test2116 passes`, so the item measures `match`.
- [ ] `behaviour:test2117`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test2118`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test2119`: Curl answers what curl 8.22.0 answers, `upstream test2119 passes`, so the item measures `match`.
- [ ] `behaviour:test2305`: Curl answers what curl 8.22.0 answers, `reference curl exits 23; stdout 0 bytes: `, so the item measures `match`.
- [ ] `behaviour:test2311`: Curl answers what curl 8.22.0 answers, `upstream test2311 passes`, so the item measures `match`.
- [ ] `behaviour:test2318`: Curl answers what curl 8.22.0 answers, `upstream test2318 passes`, so the item measures `match`.
- [ ] `behaviour:test234`: Curl answers what curl 8.22.0 answers, `upstream test234 passes`, so the item measures `match`.
- [ ] `behaviour:test2349`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test2397`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test2405`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test2407`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test2414`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test258`: Curl answers what curl 8.22.0 answers, `upstream test258 passes`, so the item measures `match`.
- [ ] `behaviour:test2606`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test2885`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test3002`: Curl answers what curl 8.22.0 answers, `upstream test3002 passes`, so the item measures `match`.
- [ ] `behaviour:test3003`: Curl answers what curl 8.22.0 answers, `upstream test3003 passes`, so the item measures `match`.
- [ ] `behaviour:test3004`: Curl answers what curl 8.22.0 answers, `upstream test3004 passes`, so the item measures `match`.
- [ ] `behaviour:test3005`: Curl answers what curl 8.22.0 answers, `upstream test3005 passes`, so the item measures `match`.
- [ ] `behaviour:test3006`: Curl answers what curl 8.22.0 answers, `upstream test3006 passes`, so the item measures `match`.
- [ ] `behaviour:test3023`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test311`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test316`: Curl answers what curl 8.22.0 answers, `upstream test316 passes`, so the item measures `match`.
- [ ] `behaviour:test320`: Curl answers what curl 8.22.0 answers, `upstream test320 passes`, so the item measures `match`.
- [ ] `behaviour:test3203`: Curl answers what curl 8.22.0 answers, `upstream test3203 passes`, so the item measures `match`.
- [ ] `behaviour:test321`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test3211`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test322`: Curl answers what curl 8.22.0 answers, `upstream test322 passes`, so the item measures `match`.
- [ ] `behaviour:test3223`: Curl answers what curl 8.22.0 answers, `upstream test3223 passes`, so the item measures `match`.
- [ ] `behaviour:test3224`: Curl answers what curl 8.22.0 answers, `upstream test3224 passes`, so the item measures `match`.
- [ ] `behaviour:test3225`: Curl answers what curl 8.22.0 answers, `upstream test3225 passes`, so the item measures `match`.
- [ ] `behaviour:test3226`: Curl answers what curl 8.22.0 answers, `upstream test3226 passes`, so the item measures `match`.
- [ ] `behaviour:test3227`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test3228`: Curl answers what curl 8.22.0 answers, `upstream test3228 passes`, so the item measures `match`.
- [ ] `behaviour:test3229`: Curl answers what curl 8.22.0 answers, `upstream test3229 passes`, so the item measures `match`.
- [ ] `behaviour:test3302`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test3303`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test3304`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test3306`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test345`: Curl answers what curl 8.22.0 answers, `upstream test345 passes`, so the item measures `match`.
- [ ] `behaviour:test409`: Curl answers what curl 8.22.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [ ] `behaviour:test433`: Curl answers what curl 8.22.0 answers, `upstream test433 passes`, so the item measures `match`.
- [ ] `behaviour:test459`: Curl answers what curl 8.22.0 answers, `the reference curl's output`, so the item measures `match`.
- [ ] `behaviour:test467`: Curl answers what curl 8.22.0 answers, `upstream test467 passes`, so the item measures `match`.
- [ ] `behaviour:test485`: Curl answers what curl 8.22.0 answers, `upstream test485 passes`, so the item measures `match`.
- [ ] `behaviour:test5000`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5001`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5002`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5003`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5004`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5005`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5006`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5007`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5008`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5009`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5010`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5011`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5012`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5013`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5014`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5015`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5016`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5017`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5018`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5019`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5020`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5021`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5022`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5023`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5024`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5025`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5026`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5027`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test5028`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test529`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test546`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test550`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test557`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test565`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test574`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test644`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test687`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test688`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test716`: Curl answers what curl 8.22.0 answers, `upstream test716 passes`, so the item measures `match`.
- [ ] `behaviour:test73`: Curl answers what curl 8.22.0 answers, `upstream test73 passes`, so the item measures `match`.
- [ ] `behaviour:test739`: Curl answers what curl 8.22.0 answers, `upstream test739 passes`, so the item measures `match`.
- [ ] `behaviour:test798`: Curl answers what curl 8.22.0 answers, `upstream test798 passes`, so the item measures `match`.
- [ ] `behaviour:test838`: Curl answers what curl 8.22.0 answers, `upstream test838 passes`, so the item measures `match`.
- [ ] `behaviour:test845`: Curl answers what curl 8.22.0 answers, `upstream test845 passes`, so the item measures `match`.
- [ ] `behaviour:test884`: Curl answers what curl 8.22.0 answers, `upstream test884 passes`, so the item measures `match`.
- [ ] `behaviour:test890`: Curl answers what curl 8.22.0 answers, `upstream test890 passes`, so the item measures `match`.
- [ ] `behaviour:test91`: Curl answers what curl 8.22.0 answers, `upstream test91 passes`, so the item measures `match`.
- [ ] `behaviour:test943`: Curl answers what curl 8.22.0 answers, `upstream test943 passes`, so the item measures `match`.
- [ ] `behaviour:test949`: Curl answers what curl 8.22.0 answers, `upstream test949 passes`, so the item measures `match`.
- [ ] `behaviour:test956`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test958`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test964`: Curl answers what curl 8.22.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- 2026-10-13, lane 1: split. Measured every item through the ratchet on Windows
  (`dotnet test Curl.Conformance.UnitTests --filter TestCategory=Conformance`, trx):
  - 48 of the 154 items already pass and are on `PassingUpstreamCases.txt`.
  - Pass but not yet listed: 320, 1062, 1498, 2059, 2318 (BL-2172 lists them), and
    2087, 3023, which need `Schannel` and so would skip, and fail the ratchet, on Linux
    and macOS (BL-2176).
  - Failing, cause already filed: 838, 884, 943 (BL-2161); 2110, 2114, 2115, 2116
    (BL-2162); 2113 (BL-2163); 3225 (BL-2164); 409 (BL-2165); 3203 (BL-2166);
    3229 (BL-2167); 2109 (BL-2168); 2305 (BL-2169); 5000-5025 httpsig (BL-2171).
  - Failing, filed now: 459, 1712 config-file warning (BL-2172); 1740 `--silent` and
    `Note:` (BL-2173); 2885 TAB before `secure` (BL-2174); 1609, 5028, 2407 harness
    port variables (BL-2175).
  - Not CLI behaviour the harness can run: libtests skipped for `<client><tool>`
    (529, 546, 550, 565, 574, 1113, 1554, 1978, 1985, 2118, 2397, 2405, 5000, 5004);
    debug-build-only cases skipped for feature `Debug` (644, 687, 688, 2057, 2094,
    2100, 2117, 2349, 5008, 5010, 5026, 5027); `!win32` cases skipped on Windows (956,
    958, 964); 2072 needs Unix; 321 needs a shell; and cases with no ratchet row at all
    (unit tests or libtests the harness lists no row for: 322, 323, 324, 557, 1177,
    1309, 1396, 1398, 1538, 1557, 1560, 1616, 1625, 1627, 1678, 1701, 1961, 2414,
    2606, 3211, 3227, 3302, 3303, 3304, 3306).
- Interactive only: the last group can only measure `match` or `excluded` through a
  gap-office decision under `Gap/`, an audit path no lane may read or file a task for
  (ADR-0433, BL-1209). Remove `lane: no` once an interactive session has recorded, in
  the gap office, which of those cases are `excluded` (with a reason) and filed tasks
  for any it decides Curl must run; the dependencies above cover everything else.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Backlog. Split after measuring all 154 items: waits on BL-2161..BL-2169, BL-2171..BL-2176; interactive only for the gap-office exclusions of libtest, debug-build, unit-test and platform-only cases
- 2026-10-14: Backlog -> Doing. interactive session: gap-office exclusions and re-measure
