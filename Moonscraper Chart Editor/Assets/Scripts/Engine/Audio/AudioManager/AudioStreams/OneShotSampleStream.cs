// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using ManagedBass;

namespace MoonscraperEngine.Audio
{
    public class OneShotSampleStream : AudioStream
    {
        float _volume;
        float _pan;
        public bool onlyPlayIfStopped = false;

        public OneShotSampleStream(int handle, int maxChannels) : base(handle)
        {
        }

        public override float volume
        {
            get { return _volume; }
            set { _volume = value; }
        }

        public override float pan
        {
            get { return _pan; }
            set { _pan = value; }
        }

        public override bool Play(float playPoint = 0, bool restart = false)
        {
            int channel = Bass.SampleGetChannel(audioHandle, BassFlags.Default);

            bool isPlaying = Bass.ChannelIsActive(channel) != PlaybackState.Stopped && Bass.ChannelIsActive(channel) != PlaybackState.Paused;
            if (onlyPlayIfStopped && isPlaying)
            {
                return false;
            }

            if (channel != 0)
            {
                if (!Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, volume))
                {
                    UnityEngine.Debug.LogError($"Failed to set volume attribute on one shot stream channel {channel}");
                }

                if (!Bass.ChannelSetAttribute(channel, ChannelAttribute.Pan, pan))
                {
                    UnityEngine.Debug.LogError($"Failed to set pan attribute on one shot stream channel {channel}");
                }

                if (!Bass.ChannelPlay(channel, restart))
                {
                    UnityEngine.Debug.LogError($"Failed to play one shot stream channel {channel}");
                }

                return true;
            }
            else
                UnityEngine.Debug.LogError($"Error when getting oneshot channel stream: {Bass.LastError}, {audioHandle}");

            return false;
        }

        public override void Stop()
        {
            // Unsupported, stops when completed
        }
    }
}
