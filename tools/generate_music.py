#!/usr/bin/env python3
"""Regenerate the four original What Should I Do? background tracks.

The arrangement and synthesis design follows the original audio production
package: original compositions, 22.05 kHz, stereo, instrumental, loop-friendly.
Run: python tools/generate_music.py
Requires: numpy and ffmpeg (for .ogg export).
"""
from pathlib import Path
import math
import subprocess
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Assets" / "Resources" / "Audio" / "Music"
OUTPUT.mkdir(parents=True, exist_ok=True)
SR = 22050
TAU = 2 * np.pi
CHORDS = {
    "C": [48, 52, 55, 60],
    "G": [43, 47, 50, 55],
    "Am": [45, 48, 52, 57],
    "F": [41, 45, 48, 53],
    "D": [50, 54, 57, 62],
    "Em": [40, 43, 47, 52],
}
SCALES = {
    "C": [60, 62, 64, 65, 67, 69, 71, 72, 74, 76],
    "F": [60, 62, 64, 65, 67, 69, 70, 72, 74, 76],
    "G": [59, 62, 64, 67, 69, 71, 74, 76],
}

def hz(midi):
    return 440.0 * 2 ** ((midi - 69) / 12)

def note(buf, start, duration, midi, amp, instrument, pan):
    pos = max(0, int(start * SR))
    if pos >= len(buf):
        return
    length = min(int((duration + 0.55) * SR), len(buf) - pos)
    t = np.arange(length, dtype=np.float32) / SR
    f = hz(midi)
    if instrument == "piano":
        sig = (np.sin(TAU*f*t) + 0.48*np.sin(TAU*2.01*f*t+0.08) +
               0.20*np.sin(TAU*3.98*f*t+0.18) + 0.08*np.sin(TAU*6.02*f*t+0.31))
        env = np.minimum(1, t/0.012) * (
            0.66*np.exp(-t/(0.42+duration*0.20)) + 0.34*np.exp(-t/(1.15+duration*0.25)))
    elif instrument == "pluck":
        sig = (np.sin(TAU*f*t) + 0.42*np.sin(TAU*2*f*t+0.12) +
               0.16*np.sin(TAU*3*f*t+0.24) + 0.06*np.sin(TAU*4*f*t+0.37))
        env = np.minimum(1, t/0.006)*np.exp(-t/(0.22+duration*0.23))
    elif instrument == "marimba":
        sig = (np.sin(TAU*f*t) + 0.55*np.sin(TAU*3.92*f*t+0.15) +
               0.16*np.sin(TAU*9.1*f*t+0.3))
        env = np.minimum(1, t/0.004)*np.exp(-t/(0.30+duration*0.18))
    elif instrument == "pad":
        sig = (0.75*np.sin(TAU*f*t) + 0.24*np.sin(TAU*2*f*t+0.15) +
               0.10*np.sin(TAU*3*f*t+0.3))
        release = np.where(t <= duration, 1.0, np.exp(-(t-duration)/0.28))
        env = np.minimum(1, t/0.22)*release
    elif instrument == "bass":
        sig = np.sin(TAU*f*t) + 0.20*np.sin(TAU*2*f*t)
        env = np.minimum(1, t/0.012)*np.exp(-t/(0.32+duration*0.20))
    else:
        sig = (0.9*np.sin(TAU*f*t) + 0.35*np.sin(TAU*2.7*f*t+0.1) +
               0.12*np.sin(TAU*4.8*f*t+0.2))
        env = np.minimum(1, t/0.005)*np.exp(-t/(0.18+duration*0.15))
    pan = float(np.clip(pan, -1, 1))
    left, right = math.sqrt((1-pan)/2), math.sqrt((1+pan)/2)
    sample = sig*env*amp
    buf[pos:pos+length, 0] += sample*left
    buf[pos:pos+length, 1] += sample*right

def percussion(buf, start, kind, amp):
    pos = int(start*SR)
    if pos >= len(buf):
        return
    duration = {"kick":.28, "hat":.07, "shaker":.12}[kind]
    length = min(int(duration*SR), len(buf)-pos)
    t = np.arange(length, dtype=np.float32)/SR
    rng = np.random.default_rng(404 + pos)
    if kind == "kick":
        sig = np.sin(TAU*(90*t - 45*t*t))*np.exp(-t*17)
    else:
        noise = rng.normal(0, 1, length).astype(np.float32)
        sig = np.concatenate([[0], np.diff(noise)])
        sig *= np.exp(-t*(65 if kind == "hat" else 28))
    buf[pos:pos+length, 0] += amp*sig*0.707
    buf[pos:pos+length, 1] += amp*sig*0.707

def render(name, bpm, progression, style):
    beat = 60/bpm
    bar_duration = 4*beat
    buf = np.zeros((int(32*bar_duration*SR),2), dtype=np.float32)
    for bar in range(32):
        chord = CHORDS[progression[bar%len(progression)]]
        start = bar*bar_duration
        pad_amp = {"menu":.020,"street":.016,"school":.025,"playground":.012}[style]
        for j, tone in enumerate(chord[1:]):
            note(buf,start,bar_duration*.95,tone+12,pad_amp/(1+.25*j),"pad",-.35+j*.22)
        bass_amp = {"menu":.045,"street":.032,"school":.030,"playground":.050}[style]
        note(buf,start,beat*1.65,chord[0]-12,bass_amp,"bass",-.05)
        note(buf,start+2*beat,beat*1.45,chord[0]-12,bass_amp*.72,"bass",.05)
        if style in ("menu","playground"):
            note(buf,start+3*beat,beat*.65,chord[2]-12,bass_amp*.42,"bass",0)
        if style == "menu":
            for k, ix in enumerate([0,2,1,2,0,2,1,2]):
                note(buf,start+k*beat/2,beat*.34,chord[ix]+12,.038,"piano",(-.18 if k%2==0 else .18))
        elif style == "street":
            for k, ix in enumerate([0,1,2,1]):
                note(buf,start+k*beat,beat*.42,chord[ix]+12,.030,"pluck",(-.16 if k%2==0 else .16))
        elif style == "school":
            for tone in chord[:3]:
                note(buf,start,beat*1.8,tone+12,.020,"piano",-.12)
                note(buf,start+2*beat,beat*1.6,tone+12,.016,"piano",.12)
            note(buf,start+beat*1.5,beat*.5,chord[2]+19,.015,"pluck",.22)
        else:
            for k,ix in enumerate([0,2,1,2,0,2,1,2]):
                note(buf,start+k*beat/2,beat*.28,chord[ix]+12,.033,"pizz",(-.22 if k%2==0 else .22))
        if style == "playground":
            percussion(buf,start,"kick",.055)
            percussion(buf,start+2*beat,"kick",.040)
            percussion(buf,start+beat,"hat",.010)
            percussion(buf,start+3*beat,"hat",.012)
        elif style == "menu" and bar%2 == 0:
            percussion(buf,start,"shaker",.008)
        elif style == "street" and bar%2 == 1:
            percussion(buf,start+2*beat,"shaker",.006)

        if style == "menu":
            scale, melody, interval, amplitude, instrument = SCALES["C"], [0,2,4,2,1,2,4,5,4,2,1,2,0,2,4,2], beat/2, .045, "pluck"
        elif style == "street":
            scale, melody, interval, amplitude, instrument = SCALES["F"], [2,4,5,4,2,1,0,1,2,4,5,7,5,4,2,1], beat, .032, "piano"
        elif style == "school":
            scale, melody, interval, amplitude, instrument = SCALES["C"], [4,2,1,0,2,4,5,4,2,1,0,1,2,4,2,0], beat, .027, "piano"
        else:
            scale, melody, interval, amplitude, instrument = SCALES["G"], [0,2,4,7,5,4,2,4,0,2,4,5,7,5,4,2], beat/2, .043, "marimba"
        if style in ("street","school") and bar%4 == 3:
            continue
        for j, ix in enumerate(melody):
            if style in ("menu","playground") and j in (3,7,11,15):
                continue
            if bar%4 in (2,3) and j in (6,7,14,15):
                ix = max(0, min(len(scale)-1, ix-1 if bar%2 else ix))
            pitch = scale[ix] + (12 if style in ("menu","playground") else 0)
            note(buf,start+j*interval,beat*(.42 if style=="menu" else .38 if style=="playground" else .60 if style=="street" else .80),pitch,amplitude,instrument,(-.2 if j%2==0 else .2))

    dry = buf.copy()
    for seconds, amount, cross in ((.17,.075,True),(.31,.035,False)):
        delay = int(seconds*SR)
        if cross:
            buf[delay:,0] += dry[:-delay,1]*amount
            buf[delay:,1] += dry[:-delay,0]*amount
        else:
            buf[delay:] += dry[:-delay]*amount

    buf = np.tanh(buf*1.25)/np.tanh(1.25)
    peak = float(np.max(np.abs(buf)))
    if peak > 0:
        buf *= .89/peak

    crossfade = int(.45*SR)
    mix = np.linspace(0,1,crossfade,dtype=np.float32)[:,None]
    seam = buf[-crossfade:]*(1-mix) + buf[:crossfade]*mix
    buf = np.concatenate((seam,buf[crossfade:-crossfade]))
    pcm = (np.clip(buf,-1,1)*32767).astype(np.int16)
    wav = OUTPUT/(name+".wav")
    with wave.open(str(wav),"wb") as output:
        output.setnchannels(2)
        output.setsampwidth(2)
        output.setframerate(SR)
        output.writeframes(pcm.tobytes())
    ogg = OUTPUT/(name+".ogg")
    subprocess.run(["ffmpeg","-y","-loglevel","error","-i",str(wav),"-c:a","libvorbis","-b:a","112k",str(ogg)],check=True)
    wav.unlink()  # Keep compressed assets in Git; download WAV masters from the original package.
    print(name,round(len(buf)/SR,1),"seconds",ogg.stat().st_size,"bytes")

if __name__ == "__main__":
    render("BGM_MainMenu",90,["C","G","Am","F"],"menu")
    render("BGM_Street",92,["F","C","G","Am"],"street")
    render("BGM_School",80,["C","Am","F","G"],"school")
    render("BGM_Playground",110,["G","D","Em","C"],"playground")
