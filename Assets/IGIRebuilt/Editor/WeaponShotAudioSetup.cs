using System.IO;
using UnityEditor;
using UnityEngine;

namespace IGIRebuilt.Editor
{
    internal static class WeaponShotAudioSetup
    {
        public static AudioClip SingleRound(string id, AudioClip source, float secondsPerShot)
        {
            if (source == null || !source.name.EndsWith("_loop")) return source;
            // Periods measured from the retail WAVs, which contain repeated gunfire at their own cadence.
            float period = id == "MP5SD" ? .063f : id == "UZIX2" ? .028f : id == "UZI" ? .087f :
                id == "M16A2" ? .099f : id == "JACKHAMMER" ? .315f : .094f;
            int frequency = 0, channels = 0, format = 0, bits = 0;
            byte[] data = null;
            // Read PCM from the source file; editor import can leave compressed clips unloaded.
            using (var reader = new BinaryReader(File.OpenRead(AssetDatabase.GetAssetPath(source))))
            {
                string Tag() => System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));
                if (Tag() != "RIFF") throw new System.Exception("Expected retail WAV: " + source.name);
                reader.ReadInt32(); if (Tag() != "WAVE") throw new System.Exception("Invalid WAV: " + source.name);
                while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
                {
                    string tag = Tag(); int length = reader.ReadInt32(); long next = reader.BaseStream.Position + length + (length & 1);
                    if (tag == "fmt ")
                    {
                        format = reader.ReadInt16(); channels = reader.ReadInt16(); frequency = reader.ReadInt32();
                        reader.ReadInt32(); reader.ReadInt16(); bits = reader.ReadInt16();
                    }
                    else if (tag == "data") data = reader.ReadBytes(length);
                    reader.BaseStream.Position = next;
                }
            }
            if (format != 1 || bits != 16 || channels < 1 || data == null) throw new System.Exception("Expected 16-bit PCM gunshot: " + source.name);
            int frames = Mathf.Min(data.Length / (channels * 2), Mathf.RoundToInt(Mathf.Min(period, secondsPerShot) * frequency));
            const string folder = "Assets/IGIRebuilt/Audio/WeaponShots";
            Directory.CreateDirectory(folder);
            string path = folder + "/" + id + ".wav";
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                int bytes = frames * channels * 2;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + bytes);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1);
                writer.Write((short)channels); writer.Write(frequency); writer.Write(frequency * channels * 2);
                writer.Write((short)(channels * 2)); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
                int fade = Mathf.Max(1, frequency / 200);
                for (int frame = 0; frame < frames; frame++)
                    for (int channel = 0; channel < channels; channel++)
                    {
                        float gain = Mathf.Clamp01((frames - 1f - frame) / fade);
                        short sample = System.BitConverter.ToInt16(data, (frame * channels + channel) * 2);
                        writer.Write((short)Mathf.RoundToInt(sample * gain));
                    }
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings; settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM; importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
