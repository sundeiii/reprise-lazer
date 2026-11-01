// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics.Containers;
using osu.Game.Audio;
using osu.Game.Online.Multiplayer;
using osu.Game.Skinning;

namespace osu.Game.Screens.OnlinePlay.Multiplayer
{
    public partial class MultiplayerRoomSounds : CompositeDrawable
    {
        [Resolved]
        private MultiplayerClient client { get; set; } = null!;

        private AudioManager audio = null!;

        // Reprise: joining and leaving are looked up when played (StableSoundsStore.Get: a stable skin's own match-join/match-leave).
        [Resolved(canBeNull: true)]
        private SkinManager? skins { get; set; }

        private Sample? hostChangedSample;
        private Sample? userKickedSample;

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            this.audio = audio;
            hostChangedSample = audio.Samples.Get(@"Multiplayer/host-changed");
            userKickedSample = audio.Samples.Get(@"Multiplayer/player-kicked");
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            client.UserJoined += onUserJoined;
            client.UserLeft += onUserLeft;
            client.UserKicked += onUserKicked;
            client.HostChanged += onHostChanged;
        }

        private void onUserJoined(MultiplayerRoomUser user)
            => Scheduler.AddOnce(() => StableSoundsStore.Get(audio, skins, @"Multiplayer/player-joined")?.Play());

        private void onUserLeft(MultiplayerRoomUser user)
            => Scheduler.AddOnce(() => StableSoundsStore.Get(audio, skins, @"Multiplayer/player-left")?.Play());

        private void onUserKicked(MultiplayerRoomUser user)
            => Scheduler.AddOnce(() => userKickedSample?.Play());

        private void onHostChanged(MultiplayerRoomUser? host)
        {
            if (host != null)
                Scheduler.AddOnce(() => hostChangedSample?.Play());
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (client.IsNotNull())
            {
                client.UserJoined -= onUserJoined;
                client.UserLeft -= onUserLeft;
                client.UserKicked -= onUserKicked;
                client.HostChanged -= onHostChanged;
            }
        }
    }
}
