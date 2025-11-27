// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using ManagedBass;
using UnityEngine;
using System.Collections.Generic;

namespace MoonscraperEngine.Audio
{
    public class AudioStream
    {
        public int audioHandle { get; private set; }
        bool isDisposed { get { return audioHandle == 0; } }
        List<int> childSyncedStreams = new List<int>();

        public virtual float volume
        {
            get { return AudioManager.GetAttribute(this, AudioAttributes.Volume); }
            set { AudioManager.SetAttribute(this, AudioAttributes.Volume, value); }
        }

        public virtual float pan
        {
            get { return AudioManager.GetAttribute(this, AudioAttributes.Pan); }
            set { AudioManager.SetAttribute(this, AudioAttributes.Pan, value); }
        }

        public AudioStream(int handle)
        {
            audioHandle = handle;
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                if (AudioManager.FreeAudioStream(this))
                {
                    audioHandle = 0;
                    Debug.Log("Audio sample disposed");
                }
            }
        }

        public bool isValid
        {
            get { return !isDisposed; }
        }

        public virtual bool Play(float playPoint, bool restart = false)
        {
            CurrentPositionSeconds = playPoint;

            if (!Bass.ChannelPlay(audioHandle, restart))
            {
                Debug.LogError($"AudioStream BASS_ChannelPlay error on {this.GetType()} handle {audioHandle}: {Bass.LastError}");
            }

            return true;
        }

        public bool PlaySynced(float playPoint, IList<AudioStream> streamsToSync)
        {
            foreach(var stream in streamsToSync)
            {
                if (stream != null && stream.isValid)
                {
                    stream.CurrentPositionSeconds = playPoint;
                    SyncWithStream(stream);
                }
            }

            return Play(playPoint, false);
        }

        public virtual void Stop()
        {
            if (!Bass.ChannelStop(audioHandle))
            {
                Debug.LogError($"AudioStream BASS_ChannelStop error on handle {audioHandle}: {Bass.LastError}");
            }

            // Synchronisation is only temporary as user may add or remove streams between different play sessions. 
            foreach (int stream in childSyncedStreams)
            {
                if (!Bass.ChannelRemoveLink(this.audioHandle, stream))
                {
                    var bassError = Bass.LastError;
                    Debug.LogError($"AudioStream ClearSyncedStreams error on handle {this.audioHandle}: {bassError}");
                }
            }

            childSyncedStreams.Clear();
        }

        public long ChannelLengthInBytes()
        {
            return Bass.ChannelGetLength(audioHandle, PositionFlags.Bytes);
        }

        public float ChannelLengthInSeconds()
        {
            return (float)Bass.ChannelBytes2Seconds(audioHandle, ChannelLengthInBytes());
        }

        public long ChannelSecondsToBytes(double position)
        {
            return Bass.ChannelSeconds2Bytes(audioHandle, position);
        }

        public float CurrentPositionSeconds
        {
            get
            {
                long bytePos = Bass.ChannelGetPosition(audioHandle);
                double elapsedtime = Bass.ChannelBytes2Seconds(audioHandle, bytePos);
                return (long)elapsedtime;
            }
            set
            {
                if (!Bass.ChannelSetPosition(audioHandle, ChannelSecondsToBytes((double)value)))
                {
                    var bassError = Bass.LastError;
                    Debug.LogError($"AudioStream BASS_ChannelSetPosition error on {this.GetType()} handle {audioHandle}: {bassError}");
                }
            }
        }

        public bool GetChannelLevels(ref float[] levels, float length)
        {
            return Bass.ChannelGetLevel(audioHandle, levels, length, LevelRetrievalFlags.Stereo);
        }

        public bool IsPlaying()
        {
            return isValid && Bass.ChannelIsActive(audioHandle) == PlaybackState.Playing;
        }

        // Call this before playing any audio
        void SyncWithStream(AudioStream childStream)
        {
            if (audioHandle == childStream.audioHandle)
                return;

            if (Bass.ChannelSetLink(this.audioHandle, childStream.audioHandle))
            {
                childSyncedStreams.Add(childStream.audioHandle);
            }
            else
            {
                var bassError = Bass.LastError;
                Debug.LogError("AudioStream SyncWithStream error: " + bassError);
            }
        }
    }
}