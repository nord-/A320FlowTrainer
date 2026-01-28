#!/usr/bin/env python3
"""
Genererar ljudfiler för A320 flows med Google TTS (gTTS).

Användning:
    pip install gtts
    python generate_audio_gtts.py

Skapar alla ljudfiler i mappen 'audio/'
"""

import json
import os
from pathlib import Path

try:
    from gtts import gTTS
except ImportError:
    print("ERROR: gTTS not installed. Run: pip install gtts")
    exit(1)

# Ladda audio-lista
with open('audio_files.json', 'r', encoding='utf-8') as f:
    audio_files = json.load(f)

# Skapa output-mapp
audio_dir = Path('audio')
audio_dir.mkdir(exist_ok=True)

print(f"Generating {len(audio_files)} audio files...")
print("-" * 50)

for i, af in enumerate(audio_files):
    filename = af['filename'].replace('.wav', '.mp3')  # gTTS skapar mp3
    filepath = audio_dir / filename
    
    if filepath.exists():
        print(f"  [{i+1}/{len(audio_files)}] SKIP (exists): {filename}")
        continue
    
    try:
        tts = gTTS(text=af['text'], lang='en', slow=False)
        tts.save(str(filepath))
        print(f"  [{i+1}/{len(audio_files)}] OK: {filename}")
    except Exception as e:
        print(f"  [{i+1}/{len(audio_files)}] ERROR: {filename} - {e}")

print("-" * 50)
print(f"Done! Audio files saved in '{audio_dir}/'")
print("\nNOTE: gTTS creates .mp3 files. Update your C# app to use .mp3 instead of .wav")
