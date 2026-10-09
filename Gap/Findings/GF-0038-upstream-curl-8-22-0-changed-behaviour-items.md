---
id: GF-0038
title: Upstream curl 8.22.0 changed behaviour items
area: behaviour
key: behaviour:newest
severity: High
status: open
scope: newest
introduced-in: 8.22.0
opened: 2026-10-09_0050
closed:
regression: false
items: [behaviour:test1062, behaviour:test1088, behaviour:test1113, behaviour:test1133, behaviour:test1136, behaviour:test1177, behaviour:test1253, behaviour:test1255, behaviour:test1309, behaviour:test1315, behaviour:test1396, behaviour:test1398, behaviour:test1462, behaviour:test1479, behaviour:test1498, behaviour:test1538, behaviour:test1554, behaviour:test1557, behaviour:test1560, behaviour:test1609, behaviour:test1616, behaviour:test162, behaviour:test1625, behaviour:test1627, behaviour:test1641, behaviour:test1642, behaviour:test1677, behaviour:test1678, behaviour:test1701, behaviour:test1712, behaviour:test1740, behaviour:test1741, behaviour:test1961, behaviour:test1978, behaviour:test1985, behaviour:test2010, behaviour:test2039, behaviour:test2052, behaviour:test2054, behaviour:test2055, behaviour:test2057, behaviour:test2059, behaviour:test2064, behaviour:test2067, behaviour:test2072, behaviour:test2087, behaviour:test2093, behaviour:test2094, behaviour:test2100, behaviour:test2109, behaviour:test2110, behaviour:test2113, behaviour:test2114, behaviour:test2115, behaviour:test2116, behaviour:test2117, behaviour:test2118, behaviour:test2119, behaviour:test2305, behaviour:test2311, behaviour:test2318, behaviour:test234, behaviour:test2349, behaviour:test2397, behaviour:test2405, behaviour:test2407, behaviour:test2414, behaviour:test258, behaviour:test2606, behaviour:test2885, behaviour:test3002, behaviour:test3003, behaviour:test3004, behaviour:test3005, behaviour:test3006, behaviour:test3023, behaviour:test311, behaviour:test316, behaviour:test320, behaviour:test3203, behaviour:test321, behaviour:test3211, behaviour:test322, behaviour:test3223, behaviour:test3224, behaviour:test3225, behaviour:test3226, behaviour:test3227, behaviour:test3228, behaviour:test3229, behaviour:test323, behaviour:test324, behaviour:test3302, behaviour:test3303, behaviour:test3304, behaviour:test3306, behaviour:test345, behaviour:test409, behaviour:test433, behaviour:test459, behaviour:test467, behaviour:test485, behaviour:test5000, behaviour:test5001, behaviour:test5002, behaviour:test5003, behaviour:test5004, behaviour:test5005, behaviour:test5006, behaviour:test5007, behaviour:test5008, behaviour:test5009, behaviour:test5010, behaviour:test5011, behaviour:test5012, behaviour:test5013, behaviour:test5014, behaviour:test5015, behaviour:test5016, behaviour:test5017, behaviour:test5018, behaviour:test5019, behaviour:test5020, behaviour:test5021, behaviour:test5022, behaviour:test5023, behaviour:test5024, behaviour:test5025, behaviour:test5026, behaviour:test5027, behaviour:test5028, behaviour:test529, behaviour:test546, behaviour:test550, behaviour:test557, behaviour:test565, behaviour:test574, behaviour:test644, behaviour:test687, behaviour:test688, behaviour:test716, behaviour:test73, behaviour:test739, behaviour:test798, behaviour:test838, behaviour:test845, behaviour:test884, behaviour:test890, behaviour:test91, behaviour:test943, behaviour:test949, behaviour:test956, behaviour:test958, behaviour:test964]
touches: []
task:
tasks: []
---
# GF-0038 - Upstream curl 8.22.0 changed behaviour items

## Summary

Upstream curl 8.22.0 added, changed or removed behaviour items since 8.21.0.

## Evidence

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

## Suggestion

Bring these items to Curl when the target moves to 8.22.0.

## Measurements


## Log

- 2026-10-09_0050: Opened from the release diff 8.21.0 to 8.22.0.
