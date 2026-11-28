"""
Reprise: Relax tapping profiles from real replays.

Reads osu!standard replays (.osr) with their maps (.osu) and measures how the player taps, in real time (a DT play's times
divided by 1.5): for each note (circle or slider head), the gap since the note before, which key hit it, whether that's the
other key than the press before (alternating), how long the key was held (circles), how early or late it was hit, and how long
after a slider's end the key was let go. Grouped by the gap before the note (streams to slow jumps), each measure is kept as
its percentiles (101 points), which the game samples from (osu.Game.Rulesets.Osu/Mods/RelaxTapping.cs).

Usage: python analyse.py <replays dir with one folder per player and manifest.json> <output dir>
"""

import json
import lzma
import math
import os
import struct
import sys

# The gaps (real milliseconds) the notes are grouped by: under 80 is fast streams, 400 and up slow jumps or after a pause.
BUCKET_EDGES = [80, 110, 140, 180, 250, 400, math.inf]
BREAK_GAP = 1000

# Replay key bits: M1, M2, K1 (with M1), K2 (with M2).
M1, M2, K1, K2 = 1, 2, 4, 8

MOD_DT, MOD_HT, MOD_NC = 64, 256, 512


def read_uleb(data, pos):
    result, shift = 0, 0
    while True:
        b = data[pos]
        pos += 1
        result |= (b & 0x7F) << shift
        if not b & 0x80:
            return result, pos
        shift += 7


def read_string(data, pos):
    marker = data[pos]
    pos += 1
    if marker == 0x00:
        return None, pos
    length, pos = read_uleb(data, pos)
    return data[pos:pos + length].decode('utf-8', 'replace'), pos + length


def read_osr(path):
    data = open(path, 'rb').read()
    pos = 0
    mode = data[pos]
    pos += 1
    pos += 4  # version
    beatmap_hash, pos = read_string(data, pos)
    player, pos = read_string(data, pos)
    _, pos = read_string(data, pos)  # replay hash
    pos += 2 * 6 + 4 + 2 + 1  # 300s..misses, score, combo, perfect
    mods = struct.unpack_from('<i', data, pos)[0]
    pos += 4
    _, pos = read_string(data, pos)  # life bar
    pos += 8  # timestamp
    length = struct.unpack_from('<i', data, pos)[0]
    pos += 4
    frames_text = lzma.decompress(data[pos:pos + length]).decode('ascii', 'replace')

    frames = []
    t = 0
    for part in frames_text.split(','):
        bits = part.split('|')
        if len(bits) < 4:
            continue
        w, keys = int(bits[0]), int(float(bits[3]))
        if w == -12345:  # the seed frame
            continue
        t += w
        frames.append((t, keys))
    return {'mode': mode, 'hash': beatmap_hash, 'player': player, 'mods': mods, 'frames': frames}


def read_osu(path):
    """The notes: (start, kind, end) with kind 'circle', 'slider' or 'spinner', and the map's OD."""
    section = None
    od, slider_mult = 5.0, 1.4
    timing = []  # (time, beat_length, uninherited)
    objects = []
    for raw in open(path, encoding='utf-8', errors='replace'):
        line = raw.strip()
        if not line or line.startswith('//'):
            continue
        if line.startswith('['):
            section = line[1:-1]
            continue
        if section == 'Difficulty':
            key, _, value = line.partition(':')
            if key == 'OverallDifficulty':
                od = float(value)
            elif key == 'SliderMultiplier':
                slider_mult = float(value)
        elif section == 'TimingPoints':
            parts = line.split(',')
            if len(parts) >= 2:
                uninherited = len(parts) < 7 or parts[6].strip() == '1'
                timing.append((float(parts[0]), float(parts[1]), uninherited))
        elif section == 'HitObjects':
            parts = line.split(',')
            start, kind = int(float(parts[2])), int(parts[3])
            if kind & 1:
                objects.append((start, 'circle', start))
            elif kind & 2:
                slides, length = int(parts[6]), float(parts[7])
                objects.append((start, 'slider', (slides, length)))
            elif kind & 8:
                objects.append((start, 'spinner', int(parts[5])))

    timing.sort(key=lambda x: x[0])

    def beat_at(time):
        beat, sv = 500.0, 1.0
        for t, b, uninherited in timing:
            if t > time:
                break
            if uninherited:
                beat, sv = b, 1.0
            elif b < 0:
                sv = min(10.0, max(0.1, -100.0 / b))
        return beat, sv

    notes = []
    for start, kind, extra in objects:
        if kind == 'slider':
            slides, length = extra
            beat, sv = beat_at(start)
            duration = length / (slider_mult * 100 * sv) * beat * slides
            notes.append((start, kind, start + duration))
        else:
            notes.append((start, kind, extra))
    return notes, od


def presses_of(frames):
    """Key presses: (time, key 'L'/'R', release time). K1/K2 when used, else the mouse buttons."""
    presses = []
    held = {}
    prev = 0
    for t, keys in frames:
        for key, bits in (('L', (K1, M1)), ('R', (K2, M2))):
            now_down = bool(keys & bits[0]) or (bool(keys & bits[1]) and not keys & (K1 | K2))
            was_down = bool(prev & bits[0]) or (bool(prev & bits[1]) and not prev & (K1 | K2))
            if now_down and not was_down:
                held[key] = len(presses)
                presses.append([t, key, None])
            elif was_down and not now_down and key in held:
                presses[held.pop(key)][2] = t
        prev = keys
    return presses


def analyse(replay, notes, od):
    mods = replay['mods']
    rate = 1.5 if mods & (MOD_DT | MOD_NC) else 0.75 if mods & MOD_HT else 1.0
    window = 200 - 10 * od  # the 50's window, in the map's time
    presses = presses_of(replay['frames'])
    used = set()
    rows = []
    last_note_start = None
    last_press = None
    p = 0
    for start, kind, end in notes:
        if kind == 'spinner':
            last_note_start, last_press = end, None
            continue
        # The first press not used yet within the note's window.
        while p < len(presses) and presses[p][0] < start - window:
            p += 1
        hit = None
        for i in range(p, len(presses)):
            if presses[i][0] > start + window:
                break
            if i not in used:
                hit = i
                break
        if hit is None:
            last_note_start = start
            continue
        used.add(hit)
        t, key, release = presses[hit]
        gap = (start - last_note_start) / rate if last_note_start is not None else math.inf
        row = {
            'gap': gap,
            'offset': (t - start) / rate,
            'switch': None if last_press is None or gap >= BREAK_GAP else key != last_press,
            'previous': None if last_press is None or gap >= BREAK_GAP else last_press,
            'key': key,
            'hold': (release - t) / rate if release is not None and kind == 'circle' else None,
            'slider_release': (release - end) / rate if release is not None and kind == 'slider' else None,
        }
        rows.append(row)
        last_note_start, last_press = start, key
    return rows


def percentile(values, q):
    x = (len(values) - 1) * q
    lo, hi = math.floor(x), math.ceil(x)
    return values[lo] + (values[hi] - values[lo]) * (x - lo)


def deciles(values):
    """
    101 points evenly through the values (every percentile), the ends at the 0.5th and 99.5th: the game draws evenly between
    neighbouring points, so with few points (or the very worst value at an end) one rare outlier would stand for a whole slice.
    """
    values = sorted(values)
    if not values:
        return None
    qs = [0.005] + [i / 100 for i in range(1, 100)] + [0.995]
    return [round(percentile(values, q), 2) for q in qs]


def profile(name, rows, replays):
    buckets = []
    low = 0
    for edge in BUCKET_EDGES:
        inside = [r for r in rows if low <= r['gap'] < edge]
        switches = [r['switch'] for r in inside if r['switch'] is not None]
        holds = [r['hold'] for r in inside if r['hold'] is not None and 0 < r['hold'] < 1000]
        offsets = [r['offset'] for r in inside]
        from_left = [r['switch'] for r in inside if r['previous'] == 'L']
        from_right = [r['switch'] for r in inside if r['previous'] == 'R']
        buckets.append({
            'maxGap': None if edge == math.inf else edge,
            'notes': len(inside),
            'alternate': round(sum(switches) / len(switches), 3) if switches else 0.5,
            # By the key before: how often the other one comes next (a player favouring one key leaves it less often).
            'switchFromLeft': round(sum(from_left) / len(from_left), 3) if len(from_left) >= 10 else None,
            'switchFromRight': round(sum(from_right) / len(from_right), 3) if len(from_right) >= 10 else None,
            'hold': deciles(holds),
            'offset': deciles(offsets),
        })
        low = edge
    # A bucket with too few notes borrows its neighbour's measures (the nearest with enough).
    for i, b in enumerate(buckets):
        if b['notes'] >= 30:
            continue
        near = sorted((j for j in range(len(buckets)) if buckets[j]['notes'] >= 30), key=lambda j: abs(j - i))
        if near:
            src = buckets[near[0]]
            for k in ('alternate', 'switchFromLeft', 'switchFromRight', 'hold', 'offset'):
                if b['notes'] < 10 or b[k] is None:
                    b[k] = src[k]
    after_break = [r['key'] for r in rows if r['gap'] >= BREAK_GAP]
    slider_releases = [r['slider_release'] for r in rows if r['slider_release'] is not None and -200 < r['slider_release'] < 500]
    offsets = [r['offset'] for r in rows]
    mean = sum(offsets) / len(offsets)
    ur = 10 * math.sqrt(sum((o - mean) ** 2 for o in offsets) / len(offsets))
    return {
        'name': name,
        'replays': replays,
        'notes': len(rows),
        'firstKeyLeft': round(after_break.count('L') / len(after_break), 3) if after_break else 0.5,
        'buckets': buckets,
        'sliderRelease': deciles(slider_releases),
        'summary': {
            'alternate': round(sum(1 for r in rows if r['switch']) / max(1, sum(1 for r in rows if r['switch'] is not None)), 3),
            'leftShare': round(sum(1 for r in rows if r['key'] == 'L') / len(rows), 3),
            'meanOffset': round(mean, 2),
            'unstableRate': round(ur, 1),
        },
    }


def main():
    source, out = sys.argv[1], sys.argv[2]
    manifest = json.load(open(os.path.join(source, 'manifest.json')))
    os.makedirs(out, exist_ok=True)
    by_player = {}
    for entry in manifest:
        by_player.setdefault(entry['player'], []).append(entry)

    for name, entries in by_player.items():
        rows, used = [], 0
        for e in entries:
            osr = os.path.join(source, name, f"{e['score']}.osr")
            osu = os.path.join(source, name, f"{e['beatmap']}.osu")
            if not (os.path.exists(osr) and os.path.exists(osu)):
                continue
            replay = read_osr(osr)
            if replay['mode'] != 0:
                continue
            notes, od = read_osu(osu)
            got = analyse(replay, notes, od)
            # A replay whose presses didn't line up with its map (a wrong .osu) is left out.
            if len(got) < 0.8 * sum(1 for n in notes if n[1] != 'spinner'):
                print(f'  {name} {e["score"]}: only {len(got)} of {len(notes)} notes matched, left out')
                continue
            rows += got
            used += 1
        if not rows:
            continue
        p = profile(name, rows, used)
        json.dump(p, open(os.path.join(out, f'{name}.json'), 'w'), indent=1)
        s = p['summary']
        print(f"{name}: {used} replays, {p['notes']} notes, alternates {s['alternate']:.0%}, mean offset {s['meanOffset']} ms, UR {s['unstableRate']}")
        for b in p['buckets']:
            hold = b['hold'][50] if b['hold'] else '-'
            off = b['offset'][50] if b['offset'] else '-'
            print(f"   gap <{b['maxGap'] or 'inf':>4}: {b['notes']:5} notes, alternate {b['alternate']:.0%}, hold median {hold} ms, offset median {off} ms")


if __name__ == '__main__':
    main()
