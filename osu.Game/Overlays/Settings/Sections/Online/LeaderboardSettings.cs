// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.Leaderboards;

namespace osu.Game.Overlays.Settings.Sections.Online
{
    /// <summary>
    /// Reprise: osu!'s own (bancho's) top scores on the leaderboards. The account's setting (the website's and BanchoBot's !bancho too):
    /// read when online, saved there when changed.
    /// </summary>
    public partial class LeaderboardSettings : SettingsSubsection
    {
        protected override LocalisableString Header => "Leaderboards";

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private LeaderboardManager? leaderboardManager { get; set; }

        private readonly Bindable<bool> banchoScores = new Bindable<bool>(true);
        private readonly IBindable<APIState> apiState = new Bindable<APIState>();

        private FormCheckBox checkBox = null!;
        private bool settingFromServer;

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(checkBox = new FormCheckBox
                {
                    Caption = "Show osu!'s own (bancho) scores",
                    HintText = "osu.ppy.sh's top scores mixed into the global leaderboards (and the normal plays on the Relax and Autopilot ones), "
                               + "with your place among them. Saved to your account: the same as the website's setting and BanchoBot's !bancho.",
                    Current = banchoScores,
                })
                {
                    Keywords = new[] { "bancho", "osu!", "ppy", "leaderboard", "scores", "global" },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            apiState.BindTo(api.State);
            apiState.BindValueChanged(state =>
            {
                checkBox.Current.Disabled = state.NewValue != APIState.Online;

                if (state.NewValue == APIState.Online)
                    fetch();
            }, true);

            banchoScores.BindValueChanged(enabled =>
            {
                if (settingFromServer)
                    return;

                var request = new BanchoScoresSettingRequest(enabled.NewValue);
                request.Success += _ => Schedule(refreshLeaderboard);
                api.Queue(request);
            });
        }

        private void fetch()
        {
            var request = new BanchoScoresSettingRequest();
            request.Success += response => Schedule(() =>
            {
                settingFromServer = true;
                bool wasDisabled = banchoScores.Disabled;
                banchoScores.Disabled = false;
                banchoScores.Value = response.Enabled;
                banchoScores.Disabled = wasDisabled;
                settingFromServer = false;
            });
            api.Queue(request);
        }

        // The shown leaderboard again, with or without them.
        private void refreshLeaderboard()
        {
            if (leaderboardManager?.CurrentCriteria is { } criteria)
                leaderboardManager.FetchWithCriteria(criteria, forceRefresh: true);
        }

        public override bool AcceptsFocus => false;
    }
}
