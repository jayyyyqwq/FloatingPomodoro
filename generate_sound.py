import wave
import math
import struct
import os

def generate_chime(filename, duration=1.0, frequency=880.0, sample_rate=44100):
    # Ensure Assets directory exists
    os.makedirs(os.path.dirname(filename), exist_ok=True)
    
    num_samples = int(duration * sample_rate)
    
    with wave.open(filename, 'w') as wav_file:
        wav_file.setnchannels(1)  # Mono
        wav_file.setsampwidth(2)  # 2 bytes per sample (16-bit)
        wav_file.setframerate(sample_rate)
        
        for i in range(num_samples):
            t = float(i) / sample_rate
            # Sine wave with exponential decay
            value = math.sin(2.0 * math.pi * frequency * t) * math.exp(-3 * t)
            
            # Scale to 16-bit integer range
            sample = int(value * 32767.0)
            
            wav_file.writeframes(struct.pack('h', sample))

if __name__ == "__main__":
    output_path = os.path.join("FloatingPomodoro", "Assets", "chime.wav")
    # Create a nice high-pitched chime
    generate_chime(output_path, duration=1.5, frequency=1000.0)
    print(f"Generated {output_path}")
