#!/usr/bin/env python3
"""
Genererar ljudfiler för A320 flows med Piper TTS.

Installation av Piper:
    # Option 1: pip (om tillgängligt)
    pip install piper-tts
    
    # Option 2: Ladda ner binary från GitHub
    # https://github.com/rhasspy/piper/releases
    
    # Ladda ner en röstmodell, t.ex. Joe:
    # https://huggingface.co/rhasspy/piper-voices/tree/main/en/en_US/joe/medium

Användning:
    python generate_audio_piper.py --model path/to/en_US-joe-medium.onnx

Skapar alla ljudfiler i mappen 'audio/'
"""

import json
import os
import subprocess
import argparse
from pathlib import Path

def generate_with_piper_cli(text, output_path, model_path, piper_exe="piper"):
    """Generera ljud med piper CLI."""
    # Escape quotes in text
    text_escaped = text.replace('"', '\\"')
    cmd = f'echo "{text_escaped}" | "{piper_exe}" --model "{model_path}" --output_file "{output_path}"'
    result = subprocess.run(cmd, shell=True, capture_output=True, text=True)
    return result.returncode == 0

def generate_with_piper_python(text, output_path, model_path):
    """Generera ljud med piper Python-modul."""
    try:
        from piper import PiperVoice
        import wave
        
        voice = PiperVoice.load(model_path)
        
        with wave.open(str(output_path), 'wb') as wav_file:
            voice.synthesize(text, wav_file)
        
        return True
    except ImportError:
        return None  # Modul inte installerad
    except Exception as e:
        print(f"    Error: {e}")
        return False

def main():
    parser = argparse.ArgumentParser(description='Generate audio files with Piper TTS')
    parser.add_argument('--model', '-m', required=True, help='Path to Piper model (.onnx file)')
    parser.add_argument('--piper', '-p', default='piper', help='Path to piper executable')
    parser.add_argument('--use-cli', action='store_true', help='Use Piper CLI instead of Python module')
    args = parser.parse_args()
    
    model_path = Path(args.model)
    if not model_path.exists():
        print(f"ERROR: Model not found: {model_path}")
        print("\nDownload a model from:")
        print("  https://huggingface.co/rhasspy/piper-voices/tree/main/en/en_US/joe/medium")
        exit(1)
    
    # Ladda audio-lista
    with open('audio_files.json', 'r', encoding='utf-8') as f:
        audio_files = json.load(f)
    
    # Skapa output-mapp
    audio_dir = Path('audio')
    audio_dir.mkdir(exist_ok=True)
    
    print(f"Generating {len(audio_files)} audio files with Piper...")
    print(f"Model: {model_path}")
    print("-" * 50)
    
    success = 0
    failed = 0
    skipped = 0
    
    for i, af in enumerate(audio_files):
        filename = af['filename']
        filepath = audio_dir / filename
        
        if filepath.exists():
            print(f"  [{i+1}/{len(audio_files)}] SKIP (exists): {filename}")
            skipped += 1
            continue
        
        text = af['text']
        
        if args.use_cli:
            ok = generate_with_piper_cli(text, filepath, model_path, args.piper)
        else:
            ok = generate_with_piper_python(text, filepath, model_path)
            if ok is None:
                print("Python piper module not available, falling back to CLI...")
                ok = generate_with_piper_cli(text, filepath, model_path, args.piper)
        
        if ok:
            print(f"  [{i+1}/{len(audio_files)}] OK: {filename}")
            success += 1
        else:
            print(f"  [{i+1}/{len(audio_files)}] FAILED: {filename}")
            failed += 1
    
    print("-" * 50)
    print(f"Done! Success: {success}, Failed: {failed}, Skipped: {skipped}")
    print(f"Audio files saved in '{audio_dir}/'")

if __name__ == '__main__':
    main()
