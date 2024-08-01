using Ryujinx.Audio.Common;
using Ryujinx.Audio.Integration;
using Ryujinx.Common.Logging;
using Ryujinx.Memory;
using Ryujinx.SDL2.Common;
using SDL;
using System;
using System.Collections.Concurrent;
using System.Threading;
using static Ryujinx.Audio.Integration.IHardwareDeviceDriver;
using static SDL.SDL3;

namespace Ryujinx.Audio.Backends.SDL2
{
    public class SDL2HardwareDeviceDriver : IHardwareDeviceDriver
    {
        private readonly ManualResetEvent _updateRequiredEvent;
        private readonly ManualResetEvent _pauseEvent;
        private readonly ConcurrentDictionary<SDL2HardwareDeviceSession, byte> _sessions;

        private readonly bool _supportSurroundConfiguration;

        public float Volume { get; set; }

        public unsafe SDL2HardwareDeviceDriver()
        {
            _updateRequiredEvent = new ManualResetEvent(false);
            _pauseEvent = new ManualResetEvent(true);
            _sessions = new ConcurrentDictionary<SDL2HardwareDeviceSession, byte>();

            SDL2Driver.Instance.Initialize();

            SDL_AudioSpec spec = new();
            SDL_AudioSpec* specPtr = &spec;

            int res = SDL_GetAudioDeviceFormat(SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK, specPtr, (int*)IntPtr.Zero);

            if (res != 0)
            {
                Logger.Error?.Print(LogClass.Application,
                    $"SDL_GetAudioDeviceFormat failed with error \"{SDL_GetError()}\"");

                _supportSurroundConfiguration = true;
            }
            else
            {
                _supportSurroundConfiguration = spec.channels >= 6;
            }

            Volume = 1f;
        }

        public static bool IsSupported => IsSupportedInternal();

        private unsafe static bool IsSupportedInternal()
        {
            // SDL_AudioStream* stream = OpenStream(SampleFormat.PcmInt16, Constants.TargetSampleRate, Constants.ChannelCountMax, null);
            //
            // if ((IntPtr)stream != 0)
            // {
            //     SDL_DestroyAudioStream(stream);
            // }
            //
            // return (IntPtr)stream != 0;
            return true;
        }

        public ManualResetEvent GetUpdateRequiredEvent()
        {
            return _updateRequiredEvent;
        }

        public ManualResetEvent GetPauseEvent()
        {
            return _pauseEvent;
        }

        public IHardwareDeviceSession OpenDeviceSession(Direction direction, IVirtualMemoryManager memoryManager, SampleFormat sampleFormat, uint sampleRate, uint channelCount)
        {
            if (channelCount == 0)
            {
                channelCount = 2;
            }

            if (sampleRate == 0)
            {
                sampleRate = Constants.TargetSampleRate;
            }

            if (direction != Direction.Output)
            {
                throw new NotImplementedException("Input direction is currently not implemented on SDL2 backend!");
            }

            SDL2HardwareDeviceSession session = new(this, memoryManager, sampleFormat, sampleRate, channelCount);

            _sessions.TryAdd(session, 0);

            return session;
        }

        internal bool Unregister(SDL2HardwareDeviceSession session)
        {
            return _sessions.TryRemove(session, out _);
        }

        private static SDL_AudioSpec GetSDL2Spec(SampleFormat requestedSampleFormat, uint requestedSampleRate, uint requestedChannelCount)
        {
            return new SDL_AudioSpec
            {
                channels = (byte)requestedChannelCount,
                format = GetSDL2Format(requestedSampleFormat),
                freq = (int)requestedSampleRate,
            };
        }

        internal static SDL_AudioFormat GetSDL2Format(SampleFormat format)
        {
            return format switch
            {
                SampleFormat.PcmInt8 => SDL_AudioFormat.SDL_AUDIO_U8,
                SampleFormat.PcmInt16 => SDL_AudioFormat.SDL_AUDIO_S16LE,
                SampleFormat.PcmInt32 => SDL_AudioFormat.SDL_AUDIO_S32LE,
                SampleFormat.PcmFloat => SDL_AudioFormat.SDL_AUDIO_F32LE,
                _ => throw new ArgumentException($"Unsupported sample format {format}"),
            };
        }

        internal unsafe static SDL_AudioStream* OpenStream(SampleFormat requestedSampleFormat, uint requestedSampleRate, uint requestedChannelCount)
        {
            SDL_AudioSpec desired = GetSDL2Spec(requestedSampleFormat, requestedSampleRate, requestedChannelCount);

            // SDL_AudioStream* stream = SDL_OpenAudioDeviceStream(SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK, );
            //
            // if ((IntPtr)stream == IntPtr.Zero)
            // {
            //     Logger.Error?.Print(LogClass.Application, $"SDL2 open audio device initialization failed with error \"{SDL_GetError()}\"");
            //
            //     return (SDL_AudioStream*)IntPtr.Zero;
            // }
            //
            // bool isValid = got.format == desired.format && got.freq == desired.freq && got.channels == desired.channels;
            //
            // if (!isValid)
            // {
            //     Logger.Error?.Print(LogClass.Application, "SDL2 open audio device is not valid");
            //     SDL_DestroyAudioStream(stream);
            //
            //     return (SDL_AudioStream*)IntPtr.Zero;
            // }
            //
            // return stream;
            return (SDL_AudioStream*)IntPtr.Zero;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (SDL2HardwareDeviceSession session in _sessions.Keys)
                {
                    session.Dispose();
                }

                SDL2Driver.Instance.Dispose();

                _pauseEvent.Dispose();
            }
        }

        public bool SupportsSampleRate(uint sampleRate)
        {
            return true;
        }

        public bool SupportsSampleFormat(SampleFormat sampleFormat)
        {
            return sampleFormat != SampleFormat.PcmInt24;
        }

        public bool SupportsChannelCount(uint channelCount)
        {
            if (channelCount == 6)
            {
                return _supportSurroundConfiguration;
            }

            return true;
        }

        public bool SupportsDirection(Direction direction)
        {
            // TODO: add direction input when supported.
            return direction == Direction.Output;
        }
    }
}
