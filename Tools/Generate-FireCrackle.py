"""Regenerate the source snapshot's deterministic, original procedural fire loop."""
from pathlib import Path
import random
import struct
import wave

destination = Path(__file__).resolve().parents[1]/'Assets/FireSafety/Audio/FireCrackle.wav'
rng = random.Random(711)
rate = 22050
samples = []
low = 0.0
for index in range(rate*3):
    low = .9*low + .1*rng.uniform(-1,1)
    value = .38*low + .06*rng.uniform(-1,1)
    if rng.random() < .002:
        value += rng.uniform(-.38,.38)
    samples.append(value)
for index in range(400):
    mix = index/400
    samples[index] = samples[index]*mix + samples[-400+index]*(1-mix)
destination.parent.mkdir(parents=True,exist_ok=True)
with wave.open(str(destination),'wb') as audio:
    audio.setparams((1,2,rate,0,'NONE','not compressed'))
    audio.writeframes(b''.join(struct.pack('<h',int(max(-1,min(1,sample))*32767)) for sample in samples))
print('Created FireCrackle.wav: three-second deterministic procedural loop.')
