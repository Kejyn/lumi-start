using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lumivale.Core.Audio;
using Lumivale.Core.Bootstrap;
using Lumivale.Core.Settings;
using UnityEngine;

namespace Lumivale.Start.Game
{
    /// <summary>Starter-only dependency bridge; neither Core module knows about the other.</summary>
    public sealed class GameSettingsAudioBinding : ILumiService
    {
        private static readonly string[] Required = { "settings", "audio" };
        private readonly AudioClip music;
        private readonly AudioClip[] clips;
        private SettingsService settings;
        private AudioService audio;

        public GameSettingsAudioBinding(AudioClip music, AudioClip[] clips)
        {
            this.music = music;
            this.clips = clips == null ? throw new ArgumentNullException(nameof(clips)) :
                (AudioClip[])clips.Clone();
        }

        public string Id => "starter-settings-audio";
        public IReadOnlyCollection<string> Dependencies => Array.AsReadOnly(Required);

        public async Task InitializeAsync(ServiceContext context, CancellationToken cancellationToken)
        {
            settings = context.Get<SettingsService>();
            audio = context.Get<AudioService>();
            foreach (var clip in clips)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (clip == null) throw new InvalidOperationException("The Starter has a missing demo audio clip.");
                if (clip.loadState == AudioDataLoadState.Unloaded && !clip.LoadAudioData())
                    throw new InvalidOperationException($"Could not load demo audio '{clip.name}'.");
                while (clip.loadState == AudioDataLoadState.Loading)
                    await Awaitable.NextFrameAsync(cancellationToken);
                if (clip.loadState != AudioDataLoadState.Loaded)
                    throw new InvalidOperationException($"Demo audio '{clip.name}' is not loaded.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            settings.ApplyGraphics();
            Synchronize(settings.Current);
            settings.Changed += Synchronize;
            audio.PlayMusic(music, loop: true, fadeInSeconds: 0.35f);
        }

        public Task ShutdownAsync(CancellationToken cancellationToken)
        {
            if (settings != null) settings.Changed -= Synchronize;
            settings = null;
            audio = null;
            return Task.CompletedTask;
        }

        private void Synchronize(SettingsValues values)
        {
            audio.SetVolumes(values.MasterVolume, values.MusicVolume, values.SfxVolume);
        }
    }
}
