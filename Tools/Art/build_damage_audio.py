"""Create original impact, bite, acid and spit effects from deterministic synthesis."""
import numpy as np
from build_audio import RATE, ogg

rng = np.random.default_rng(42871)
for name, duration in (("Bite", .36), ("Claw", .29), ("AcidHit", .44), ("Spit", .32)):
    t = np.arange(round(RATE*duration))/RATE
    noise = rng.normal(0, 1, len(t))
    low = np.convolve(noise, np.ones(35)/35, mode="same")
    thud = np.sin(2*np.pi*(78*t+1.4*(1-np.exp(-t*24))))*np.exp(-t*24)
    envelope = (1-np.exp(-t*700))*np.exp(-t*11)
    if name == "Bite":
        crack = np.maximum(0, np.cos(t*2*np.pi*31))**18 * noise
        sound = (.60*thud+.34*crack+.65*low)*envelope
    elif name == "Claw":
        scrape = np.convolve(noise, np.ones(5)/5, mode="same")
        sound = (.72*thud+.40*scrape)*envelope
    else:
        gurgle = np.sin(2*np.pi*(137*t+.8*np.sin(t*37)))*np.exp(-t*6)
        sound = (.52*low+.18*noise+.30*gurgle)*envelope
        if name == "AcidHit": sound += .15*noise*np.exp(-t*13)
    sound *= np.minimum(1, (duration-t)/.012)
    ogg(name, np.tanh(sound*1.8))
