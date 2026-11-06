"""Restore the original alarm by default; importing another recording is explicit."""
from pathlib import Path
import argparse
import math
import subprocess
import struct
import wave

rate = 22050
output = Path(__file__).resolve().parents[1] / 'Assets' / 'FireSafety' / 'Audio' / 'TrainingAlarm.wav'
parser = argparse.ArgumentParser()
parser.add_argument('--source', type=Path, help='Explicitly import a replacement recording; its original is preserved.')
parser.add_argument('--demo-tone', action='store_true', help='Explicitly replace the supplied alarm with a generated comparison tone.')
arguments = parser.parse_args()
if arguments.source is not None:
    if not arguments.source.is_file():
        raise SystemExit('Alarm source not found. Supply its path with --source; the existing alarm was left unchanged.')
    output.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run(['ffmpeg', '-nostdin', '-hide_banner', '-loglevel', 'error', '-y', '-i', str(arguments.source), '-vn', '-ac', '1', '-ar', str(rate), '-c:a', 'pcm_s16le', str(output)], check=True)
    print(f'Imported {arguments.source.name}; original left unchanged.')
    raise SystemExit(0)
samples = bytearray()
duration=4 if arguments.demo_tone else 2
for index in range(rate * duration):
    time = index / rate
    pulse = time % 0.5
    frequency = 880 if int(time / 0.5) % 2 == 0 else 660
    envelope = max(0, min(1, pulse / 0.015, (0.38 - pulse) / 0.02)) if pulse < 0.38 else 0
    tone = math.sin(2 * math.pi * frequency * time) + 0.18 * math.sin(4 * math.pi * frequency * time)
    sample=0.52*envelope*tone/1.18 if arguments.demo_tone else 0.45*math.sin(2*math.pi*(750*time-120*math.cos(2*math.pi*time)/(2*math.pi)))
    samples.extend(struct.pack('<h', int(32767 * sample)))
output.parent.mkdir(parents=True, exist_ok=True)
with wave.open(str(output), 'wb') as audio:
    audio.setparams((1, 2, rate, 0, 'NONE', 'not compressed'))
    audio.writeframes(samples)
print(f'Created {output.name}: '+('4-second comparison tone.' if arguments.demo_tone else 'original 2-second synthesized alarm loop.'))
