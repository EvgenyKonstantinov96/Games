"""Compose the original ZSR score and effects. Requires NumPy and ffmpeg."""

from pathlib import Path
import subprocess
from uuid import NAMESPACE_URL, uuid5

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/Resources/Audio"
RATE = 32000
BEATS = 64
OUT.mkdir(parents=True, exist_ok=True)
rng = np.random.default_rng(2305)


def ogg(name, data, stream=False):
    path = OUT / (name + ".ogg")
    peak = np.max(np.abs(data))
    # Vorbis transients can decode taller than their PCM peaks.
    data = data * min(1, .70 / max(peak, .001))
    pcm = np.rint(data * 32767).astype("<i2")
    channels = 1 if pcm.ndim == 1 else pcm.shape[1]
    subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-f", "s16le", "-ar", str(RATE),
                    "-ac", str(channels), "-i", "-", "-c:a", "libvorbis", "-q:a",
                    "4" if stream else "5", str(path)], input=pcm.tobytes(), check=True)
    guid = uuid5(NAMESPACE_URL, "ZSR/Audio/" + name).hex
    (OUT / (path.name + ".meta")).write_text(f"""fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: {2 if stream else 0}
    sampleRateSetting: 0
    sampleRateOverride: 32000
    compressionFormat: 1
    quality: 0.65
    conversionMode: 0
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 0
  preloadAudioData: {0 if stream else 1}
  loadInBackground: 1
  ambisonic: 0
  3D: 0
""")


def sine(t, midi):
    return np.sin(2*np.pi * 440 * 2**((midi-69)/12) * t)


def loop(name, tempo, run):
    beat = 60/tempo
    samples = round(BEATS*beat*RATE)
    mix = np.zeros((samples,2))

    def place(audio, time, gain, pan=0):
        idx = (round(time*RATE) + np.arange(audio.size)) % samples
        fade = np.minimum(1,np.minimum(np.arange(audio.size),np.arange(audio.size)[::-1])/(RATE*.005))
        signal = audio * fade * gain
        np.add.at(mix[:,0],idx,signal*np.sqrt((1-pan)*.5))
        np.add.at(mix[:,1],idx,signal*np.sqrt((1+pan)*.5))

    # D minor, B-flat, F and C, with suspended inversions on the second half.
    chords = ((38,53,57,62),(34,53,57,62),(41,53,57,60),(36,55,60,62),
              (38,53,57,64),(34,50,53,57),(41,55,57,60),(36,50,55,60),
              (31,50,55,58),(34,50,55,62),(38,53,57,62),(33,52,55,61),
              (38,53,57,65),(34,53,58,62),(41,53,57,64),(36,52,55,60))
    for bar,(bass,*notes) in enumerate(chords):
        clock = bar*beat*4
        t = np.arange(round(beat*5.1*RATE))/RATE
        cloud = np.sin(np.pi*t/(beat*5.1))**.95
        for voice,note in enumerate(notes):
            pad = (sine(t,note)+.18*sine(t,note+12)+.24*sine(t*(1.002-voice*.002),note))*cloud
            place(pad,clock-.50*beat,.065 if run else .085,(voice-1)*.58)
        pattern=(0,.75,1.5,2.5,3.25) if run and bar not in (7,11,15) else (0,2.5)
        for j,when in enumerate(pattern):
            t=np.arange(round(RATE*beat*.69))/RATE
            low=sine(t,bass+(12 if run and j==3 else 0))+.14*sine(t,bass+12)
            place(low*np.exp(-t/(beat*.46)),clock+when*beat,.23 if run else .15)
        if run:
            for when in (0,1.5,2.5):
                t=np.arange(int(RATE*.28))/RATE
                kick=np.sin(2*np.pi*(48*t+7*(1-np.exp(-t*19))))*np.exp(-t*19)
                place(kick,clock+when*beat,.36)
            for j in range(8):
                t=np.arange(int(RATE*.13))/RATE
                noise=rng.normal(0,.30,t.size)
                metal=(noise-np.r_[0,noise[:-1]])*np.exp(-t*(46 if j%2 else 31))
                place(metal,clock+beat*j/2,.054 if j%2 else .082,(-1 if j%2 else 1)*.38)
            for when in (1,3):
                t=np.arange(int(RATE*.23))/RATE
                radio=(rng.standard_normal(t.size)*.55+np.sin(2*np.pi*174*t)*.27)*np.exp(-t*31)
                place(radio,clock+beat*when,.19)
        strikes=(.5,2.0,3.5) if bar%2==0 else (1.5,3.0)
        for j,when in enumerate(strikes):
            t=np.arange(round(beat*1.75*RATE))/RATE
            note=notes[(j+bar)%3]+12
            bell=(sine(t,note)+.19*sine(t,note+19)+.08*sine(t,note+28))*np.exp(-t/(beat*.72))
            place(bell,clock+beat*when,.088 if run else .12,(-1 if j%2 else 1)*.37)
            place(bell,clock+beat*(when+.69),.029 if run else .048,(-1 if j%2==0 else 1)*.62)
    mixed = np.tanh(mix*1.12)*.88
    mixed = (mixed + np.roll(mixed,1,axis=0))*.5
    ogg(name, mixed, stream=True)


def burst(name, duration, body, tail, amount):
    t = np.arange(round(RATE*duration))/RATE
    noise = rng.standard_normal(t.size)
    grit = (noise - np.r_[0,noise[:-1]]) * np.exp(-t/(.012+duration*.04))
    low = np.convolve(noise,np.ones(17)/17,mode="same") * np.exp(-t/(duration*.23))
    tone = np.sin(2*np.pi*(body*t+10*(1-np.exp(-t*17))))*np.exp(-t/(duration*.19))
    echo = np.roll(low + tone, round(RATE*tail)) * .23
    echo[:round(RATE*tail)] = 0
    sound = np.tanh((.49*grit+.65*low+.56*tone+echo)*amount)
    sound *= np.minimum(1,np.minimum(t,t[::-1])/.002)
    ogg(name,sound)


if __name__ == "__main__":
    loop("Menu", 75, False)
    loop("Run", 100, True)
    burst("Pistol", .19, 111, .068, 1.14)
    burst("Rifle", .24, 84, .081, 1.48)
    burst("Shotgun", .36, 65, .098, 1.62)
    burst("Minigun", .11, 148, .044, .92)
    burst("Drone", .13, 219, .042, .69)
    burst("Blast", .62, 38, .104, 1.53)
