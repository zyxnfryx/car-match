#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CarMatch.Editor
{
    // Resources/Audio: SFX decompress on load (Vorbis), the music_* beds stream
    // so their loops stays sample-accurate.
    public sealed class AudioImport : AssetPostprocessor
    {
        const string Folder = "Assets/_Project/Resources/Audio/";

        void OnPreprocessAudio()
        {
            var path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(Folder, System.StringComparison.Ordinal)) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = !path.Contains("/music_");
            var s = importer.defaultSampleSettings;
            bool music = path.Contains("/music_");
            s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = music ? 0.7f : 0.6f;
            s.preloadAudioData = !music;
            importer.defaultSampleSettings = s;
            importer.loadInBackground = music;
        }
    }
}
#endif
