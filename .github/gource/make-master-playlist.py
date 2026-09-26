"""Writes the HLS master playlist for the qualities render.sh produced, to stdout.

Usage: make-master-playlist.py <hls directory>

Each subdirectory holding an index.m3u8 is one quality. ffmpeg's own master playlist
does not name every codec a browser needs to decide what it can play, so this one is
written by hand: RESOLUTION, FRAME-RATE and CODECS from ffprobe, and BANDWIDTH as the
peak segment bitrate measured from the files on disk.
"""
import json
import os
import re
import subprocess
import sys
import tempfile

H264_PROFILES = {'Baseline': 0x42, 'Constrained Baseline': 0x42, 'Main': 0x4D, 'High': 0x64}


def probe(directory):
    """The video stream of one quality, as ffprobe reports it.

    The init segment alone lacks the level and frame rate, so ffprobe reads it together
    with the first media segment, which is what a player would read first.
    """
    first = sorted(f for f in os.listdir(directory) if f.endswith('.m4s'))[0]
    with tempfile.NamedTemporaryFile(suffix='.mp4', delete=False) as sample:
        for name in ('init.mp4', first):
            with open(os.path.join(directory, name), 'rb') as part:
                sample.write(part.read())
    try:
        text = subprocess.run(
            ['ffprobe', '-v', 'error', '-select_streams', 'v:0', '-show_streams', '-of', 'json', sample.name],
            check=True, capture_output=True, encoding='utf-8').stdout
    finally:
        os.remove(sample.name)
    return json.loads(text)['streams'][0]


def codec_string(stream):
    """The RFC 6381 codec string a browser checks before choosing a quality."""
    level = int(stream.get('level', -99))
    if stream['codec_name'] == 'h264':
        profile = H264_PROFILES.get(stream.get('profile'), 0x64)
        return f'avc1.{profile:02X}00{level:02X}'
    if stream['codec_name'] == 'av1':
        if level < 0:
            level = 16 if stream['height'] > 2160 else 12 if stream['height'] > 1080 else 8
        return f'av01.0.{level:02d}M.08'
    raise SystemExit(f'make-master-playlist.py: no codec string for {stream["codec_name"]}')


def peak_bitrate(directory):
    """The highest bits-per-second of any one segment in a media playlist."""
    lines = open(os.path.join(directory, 'index.m3u8'), encoding='utf-8').read().splitlines()
    peak, duration = 0, None
    for line in lines:
        match = re.match(r'#EXTINF:([\d.]+)', line)
        if match:
            duration = float(match.group(1))
        elif line and not line.startswith('#') and duration:
            size = os.path.getsize(os.path.join(directory, line))
            peak = max(peak, int(size * 8 / duration))
            duration = None
    return peak


def main():
    root = sys.argv[1]
    variants = []
    for name in sorted(os.listdir(root)):
        directory = os.path.join(root, name)
        if not os.path.isfile(os.path.join(directory, 'index.m3u8')):
            continue
        stream = probe(directory)
        num, den = stream['avg_frame_rate'].split('/')
        variants.append((peak_bitrate(directory), stream, codec_string(stream),
                         float(num) / float(den), name))
    variants.sort(key=lambda v: v[0], reverse=True)
    out = ['#EXTM3U', '#EXT-X-VERSION:7', '#EXT-X-INDEPENDENT-SEGMENTS']
    for bandwidth, stream, codecs, rate, name in variants:
        out.append(f'#EXT-X-STREAM-INF:BANDWIDTH={bandwidth},RESOLUTION={stream["width"]}x{stream["height"]},'
                   f'FRAME-RATE={rate:.3f},CODECS="{codecs}"')
        out.append(f'{name}/index.m3u8')
    sys.stdout.write('\n'.join(out) + '\n')


if __name__ == '__main__':
    main()
