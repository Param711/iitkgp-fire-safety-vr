"""Check that the shipped local voice/spray WAVs contain usable PCM samples."""
from array import array
from pathlib import Path
import math
import json
import wave

audio_dir = Path(__file__).resolve().parents[1] / 'Assets' / 'FireSafety' / 'Audio'
paths = sorted(path for path in audio_dir.glob('*.wav') if path.name != 'FireCrackle.wav')
course = json.loads((audio_dir.parent / 'Resources' / 'FireSafetyTheory.json').read_text(encoding='utf-8'))
expected_count = 17 + len(course['pages']) + 2 * len(course['questions'])
assert len(paths) == expected_count, f'Expected {expected_count} audio files, found {len(paths)}'
total_seconds = 0.0
for path in paths:
    with wave.open(str(path), 'rb') as wav:
        assert wav.getnchannels() == 1 and wav.getsampwidth() == 2, path.name
        assert wav.getframerate() == 22050, path.name
        duration = wav.getnframes() / wav.getframerate()
        samples = array('h', wav.readframes(wav.getnframes()))
    peak = max(abs(sample) for sample in samples) / 32768
    rms = math.sqrt(sum(sample * sample for sample in samples) / len(samples)) / 32768
    assert duration > 0.5 and peak > 0.03 and rms > 0.005, f'Silent/empty clip: {path.name}'
    total_seconds += duration
print(f'PASS: {len(paths)} non-silent mono PCM clips; {total_seconds:.1f} seconds total.')
print('This checks asset data, not the PC/headset volume or physical audio output.')
