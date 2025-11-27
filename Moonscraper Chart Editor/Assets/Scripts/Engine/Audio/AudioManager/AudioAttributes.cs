// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using ManagedBass;

namespace MoonscraperEngine.Audio
{
    public enum AudioAttributes
    {
        Pan = ChannelAttribute.Pan,
        Volume = ChannelAttribute.Volume,
    }

    public enum TempoAudioAttributes
    {
        Frequency = ChannelAttribute.Frequency,
        Tempo = ChannelAttribute.Tempo,
        TempoPitch = ChannelAttribute.Pitch,
    }
}
