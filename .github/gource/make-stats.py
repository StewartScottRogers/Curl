"""Writes stats.json, the numbers the Gource viewer page shows, to stdout.

Usage: make-stats.py <gource log> <width> <height> <fps>

History numbers (commits, contributors, dates) cover every branch, the same history the
animation draws. Codebase numbers (projects, C# files and lines, tests) describe the
checked-out commit the render ran on.
"""
import json
import os
import re
import subprocess
import sys
import time

BRANCHES = ['--exclude=gource', '--branches', '--exclude=origin/gource',
            '--exclude=origin/HEAD', '--remotes']


def git(*args):
    return subprocess.run(['git', *args], check=True, capture_output=True, encoding='utf-8').stdout


def main():
    log, width, height, fps = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), int(sys.argv[4])
    rows = [line.split('|') for line in open(log, encoding='utf-8').read().splitlines()]
    tracked = git('ls-files').splitlines()
    cs_files = [p for p in tracked if p.endswith('.cs')]
    cs_lines = sum(sum(1 for _ in open(p, encoding='utf-8', errors='replace')) for p in cs_files if os.path.isfile(p))
    tests = sum(len(re.findall(r'\[(?:TestMethod|DataTestMethod)\b', open(p, encoding='utf-8', errors='replace').read()))
                for p in cs_files if '.UnitTests' in p and os.path.isfile(p))
    stats = {
        'commits': len(git('rev-list', *BRANCHES).split()),
        'contributors': sorted({r[1] for r in rows}),
        'firstCommit': int(rows[0][0]),
        'lastCommit': int(rows[-1][0]),
        'filesTouched': len({r[3] for r in rows}),
        'projects': sum(1 for p in tracked if p.endswith('.csproj')),
        'csharpFiles': len(cs_files),
        'csharpLines': cs_lines,
        'tests': tests,
        'pullRequestsMerged': len(re.findall(r'^Merge pull request #', git('log', '--merges', '--format=%s', *BRANCHES), re.M)),
        'tasksDone': sum(1 for p in tracked if re.match(r'Tasks/Done/(?:.+/)?BL-\d+.*\.md$', p)),
        'renderedAt': int(time.time()),
        'width': width,
        'height': height,
        'fps': fps,
    }
    json.dump(stats, sys.stdout, indent=2)
    sys.stdout.write('\n')


if __name__ == '__main__':
    main()
