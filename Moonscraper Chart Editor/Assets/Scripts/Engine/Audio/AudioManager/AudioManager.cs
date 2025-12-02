// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using ManagedBass;
using ManagedBass.Enc;
using ManagedBass.Fx;
using ManagedBass.Opus;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;

namespace MoonscraperEngine.Audio
{
    /// <summary>
    /// A wrapper around a custom audio engine, cause Unity's is quite bad for rhythm games. 
    /// Current custom engine is Un4seen's Bass and ManagedBass. See licensing information on whether you'd allowed to use it. Currently under Non-Commerical for Moonscraper, hence why it's allowed to be here.
    /// </summary>
    public static class AudioManager
    {
        public static bool isDisposed { get; private set; }
        static List<AudioStream> liveAudioStreams = new List<AudioStream>();
        private const int c_oggEncodingQuality = 8;         // https://wiki.hydrogenaudio.org/index.php?title=Recommended_Ogg_Vorbis#Recommended_Encoder_Settings
        private const int c_oggEncodingQualityKbps = 256;
        static List<int> pluginHandles = new List<int>();   // For any calls to BASS_PluginLoad

        #region Memory
        public static bool Init(out string errString)
        {
            errString = string.Empty;
            isDisposed = false;

            bool success = false;

            // Load ManagedBass
            {
				UnityEngine.Debug.Log($"Bass version: {Bass.Version}");

#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
                if (!Bass.Configure(Configuration.IncludeDefaultDevice, true))
                {
                    UnityEngine.Debug.LogWarning($"BASS_SetConfig dev default {Bass.LastError}");
                }
#endif

                if (!Bass.Configure(Configuration.UpdateThreads, 2))
                {
                    UnityEngine.Debug.LogError($"BASS_SetConfig update threads {Bass.LastError}");
                }

                success = Bass.Init(-1, 44100, DeviceInitFlags.Default | DeviceInitFlags.Latency, IntPtr.Zero);
                if (!success)
                {
                    Errors errorCode = Bass.LastError;

                    if (errorCode != Errors.Already)
                    {
                        UnityEngine.Debug.Log("Unable to initialise ManagedBass on default device. Will attempt to initialise with other devices.");

                        // Device 0 is always NoSoundDevice, starting at device 1
                        for (var a = 1; Bass.GetDeviceInfo(a, out DeviceInfo info); a++)
                        {
                            if (info.IsEnabled && !info.IsInitialized)
                            {
                                success = Bass.Init(a, 44100, DeviceInitFlags.Default | DeviceInitFlags.Latency, IntPtr.Zero);
                                if (success)
                                {
                                    break;
                                }
                            }
                        }

                        if (!success)
                        {
                            errString = "Failed ManagedBass initialisation. Error code " + errorCode;
                            UnityEngine.Debug.LogError(errString);
                        }
                    }
                    else
                    {
                        UnityEngine.Debug.Log("ManagedBass already initialised on current device.");
                    }
                }
                else
                {
                    UnityEngine.Debug.Log("ManagedBass initialised");
                }
            }

            // Load bass fx plugin
            {
                System.Version bassFxVersion = BassFx.Version;  // Call this and load bass_fx plugin immediately
                UnityEngine.Debug.Log("Bass FX version = " + bassFxVersion);
            }

            return success;
        }

        public static void Dispose()
        {
            if (liveAudioStreams.Count > 0)
            {
                UnityEngine.Debug.LogWarning("Disposing of audio manager but there are still " + liveAudioStreams.Count + " streams remaining. Remaining streams will be cleaned up by the audio manager.");
            }

            // Free any remaining streams 
            for (int i = liveAudioStreams.Count - 1; i >= 0; --i)
            {
                FreeAudioStream(liveAudioStreams[i]);
            }

            UnityEngine.Debug.Assert(liveAudioStreams.Count == 0, "Failed to free " + liveAudioStreams.Count + " remaining audio streams");

            foreach(int pluginHandle in pluginHandles)
            {
                if (!Bass.PluginFree(pluginHandle))
                {
                    UnityEngine.Debug.LogError($"Failed to free plugin handle {pluginHandle}, {Bass.LastError}");
                }
            }

            if (!Bass.Free())
            {
                UnityEngine.Debug.LogError($"Failed to free bass, {Bass.LastError}");
            }

            UnityEngine.Debug.Log("Freed Bass Audio memory");
            isDisposed = true;
        }

        public static bool FreeAudioStream(AudioStream stream)
        {
            bool success = false;

            if (isDisposed)
            {
                UnityEngine.Debug.LogError("Trying to free a stream when Bass has not been initialised");
                return false;
            }

            if (StreamIsValid(stream))
            {
                if (stream.GetType() == typeof(OneShotSampleStream))
                    success = Bass.SampleFree(stream.audioHandle);
                else
                    success = Bass.StreamFree(stream.audioHandle);

                if (!success)
                    UnityEngine.Debug.LogError("Error while freeing audio stream " + stream.audioHandle + ", Error Code " + Bass.LastError);
                else
                {
                    UnityEngine.Debug.Log("Successfully freed audio stream");
                    if (!liveAudioStreams.Remove(stream))
                    {
                        UnityEngine.Debug.LogError("Freed a stream, however it wasn't tracked by the audio manager?");
                    }
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("Attempted to free an invalid audio stream");
            }

            return success;
        }

        public static bool ConvertToOgg(string sourcePath, string destPath)
        {
            const string EXTENTION = ".ogg";
            UnityEngine.Debug.Assert(destPath.EndsWith(EXTENTION));

            bool success = false;

            if (sourcePath.EndsWith(EXTENTION))
            {
                UnityEngine.Debug.Log("{0} is already an ogg file, copying instead");

                // Re-encoding is slow as hell, speed this up
                System.IO.File.Copy(sourcePath, destPath, true);

                success = true;
            }
            else
            {
                string commandLine = null;
                string inputFile = sourcePath;
                string outputFile = destPath;

#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
                string encoderDirectory = UnityEngine.Application.streamingAssetsPath;
                UnityEngine.Debug.Assert(File.Exists(Path.Combine(encoderDirectory, "oggenc2.exe")));

                commandLine = Path.Combine(encoderDirectory, "oggenc2.exe") + " -q " + c_oggEncodingQuality + " -";
#elif (UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX)
                commandLine = "ffmpeg -i - -c:a libvorbis -b:a " + c_oggEncodingQualityKbps + "k";
#endif

                if (commandLine == null)
                {
                    UnityEngine.Debug.LogErrorFormat("Unable to encode ogg file from {0} to {1}. Platform not implemented.", inputFile, outputFile);

                    return false;
                }

                int stream = StreamCreateFile(inputFile, 0, 0, BassFlags.AsyncFile | BassFlags.Decode);

                if (stream == 0)
                {
                    UnityEngine.Debug.LogErrorFormat("Failed to load input file {0}. BASS error {1}", inputFile, Bass.LastError);
                }

                int encoder = BassEnc_Ogg.Start(stream, commandLine, EncodeFlags.AutoFree, outputFile);

                if (encoder == 0)
                {
                    UnityEngine.Debug.LogErrorFormat("Unable start encoder. BASS Error {0}", Bass.LastError);
                }
                else
                {
                    success = true;

                    while (Bass.ChannelIsActive(stream) == PlaybackState.Playing)
                    {
                        int status = Bass.ChannelGetLevel(stream);
                        if (status < 0)
                        {
                            success = false;

                            UnityEngine.Debug.LogErrorFormat("Unable to encode ogg file from {0} to {1}. BASS Error {2}", inputFile, outputFile, Bass.LastError);

                            break;
                        }
                    }
                }

                Bass.StreamFree(stream);
            }

            return success;
        }

        #endregion

        #region Stream Loading

        static int StreamCreateFile(string file, long offset, long length, BassFlags flags)
        {
            int audioStreamHandle = Bass.CreateStream(file, offset, length, flags);
            if (audioStreamHandle == 0)
            {
                // Try an opus stream instead as a fallback
                audioStreamHandle = BassOpus.CreateStream(file, offset, length, flags);
            }

            return audioStreamHandle;
        }

        public static AudioStream LoadStream(string filepath)
        {
            int audioStreamHandle = StreamCreateFile(filepath, 0, 0, BassFlags.Decode);
            if (audioStreamHandle == 0)
            {
                throw new Exception(String.Format("Failed to load audio file: BASS error {0}", Bass.LastError));
            }

            var newStream = new AudioStream(audioStreamHandle);
            liveAudioStreams.Add(newStream);
            return newStream;
        }

        public static TempoStream LoadTempoStream(string filepath)
        {
            int audioStreamHandle = StreamCreateFile(filepath, 0, 0, BassFlags.Decode | BassFlags.AsyncFile | BassFlags.Prescan);
            if (audioStreamHandle == 0)
            {
                throw new Exception(String.Format("Failed to load audio file: BASS error {0}", Bass.LastError));
            }

            audioStreamHandle =  BassFx.TempoCreate(audioStreamHandle, BassFlags.FxFreeSource);

            if (audioStreamHandle == 0)
            {
                throw new Exception(String.Format("Failed to create tempo stream: BASS error {0}", Bass.LastError));
            }

            var newStream = new TempoStream(audioStreamHandle);
            liveAudioStreams.Add(newStream);
            return newStream;
        }

        public static OneShotSampleStream LoadSampleStream(string filepath, int maxSimultaneousPlaybacks)
        {
            UnityEngine.Debug.Assert(System.IO.File.Exists(filepath), "Filepath " + filepath + " does not exist");

            int audioStreamHandle = Bass.SampleLoad(filepath, 0, 0, maxSimultaneousPlaybacks, BassFlags.Default);

            if (audioStreamHandle == 0)
            {
                UnityEngine.Debug.LogError($"Failed to load sample for path {filepath}, {Bass.LastError}");
            }

            var newStream = new OneShotSampleStream(audioStreamHandle, maxSimultaneousPlaybacks);
            liveAudioStreams.Add(newStream);
            return newStream;
        }

        public static OneShotSampleStream LoadSampleStream(UnityEngine.AudioClip clip, int maxSimultaneousPlaybacks)
        {
            var newStream = LoadSampleStream(clip.GetWavBytes(), maxSimultaneousPlaybacks);

            return newStream;
        }

        public static OneShotSampleStream LoadSampleStream(byte[] streamBytes, int maxSimultaneousPlaybacks)
        {
            int audioStreamHandle = Bass.SampleLoad(streamBytes, 0, streamBytes.Length, maxSimultaneousPlaybacks, BassFlags.Default);

            if (audioStreamHandle == 0)
            {
                UnityEngine.Debug.LogError($"Failed to load sample for sample stream bytes, {Bass.LastError}");
            }

            var newStream = new OneShotSampleStream(audioStreamHandle, maxSimultaneousPlaybacks);
            liveAudioStreams.Add(newStream);
            return newStream;
        }

        public static void RegisterStream(AudioStream stream)
        {
            liveAudioStreams.Add(stream);
        }

        #endregion

        #region Attributes

        public static float GetAttribute(AudioStream audioStream, AudioAttributes attribute)
        {
            float value = 0;
            if (!Bass.ChannelGetAttribute(audioStream.audioHandle, (ChannelAttribute)attribute, out value))
            {
                UnityEngine.Debug.LogError($"Failed to get audiostream attribute {attribute} for handle {audioStream.audioHandle}, {Bass.LastError}");
            }
            return value;
        }

        public static void SetAttribute(AudioStream audioStream, AudioAttributes attribute, float value)
        {
            if (!Bass.ChannelSetAttribute(audioStream.audioHandle, (ChannelAttribute)attribute, value))
            {
                UnityEngine.Debug.LogError($"Failed to set audiostream attribute {attribute} for handle {audioStream.audioHandle}, {Bass.LastError}");
            }
        }

        public static float GetAttribute(TempoStream audioStream, TempoAudioAttributes attribute)
        {
            float value = 0;
            if (!Bass.ChannelGetAttribute(audioStream.audioHandle, (ChannelAttribute)attribute, out value))
            {
                UnityEngine.Debug.LogError($"Failed to get tempo stream attribute {attribute} for handle {audioStream.audioHandle}, {Bass.LastError}");
            }
            return value;
        }

        public static void SetAttribute(TempoStream audioStream, TempoAudioAttributes attribute, float value)
        {
            if (!Bass.ChannelSetAttribute(audioStream.audioHandle, (ChannelAttribute)attribute, value))
            {
                UnityEngine.Debug.LogError($"Failed to set tempo stream attribute {attribute} for handle {audioStream.audioHandle}, {Bass.LastError}");
            }
        }

        #endregion

        #region Helper Functions

        public static bool StreamIsValid(AudioStream audioStream)
        {
            return audioStream != null && audioStream.isValid;
        }

        #endregion

    }
}
