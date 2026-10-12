// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Skinning.Select
{
    public partial class LegacyFooterUser : CompositeDrawable
    {
        private Sprite background = null!;
        private OsuSpriteText usernameText = null!;
        private OsuTextFlowContainer infoText = null!;
        private OsuSpriteText rankText = null!;
        private Sprite rulesetIcon = null!;
        private Sprite levelBar = null!;
        private UpdateableAvatar avatar = null!;
        private SkinnableSound hoverSound = null!;

        [Resolved]
        private LocalUserStatisticsProvider? userStatisticsProvider { get; set; }

        /// <summary>
        /// Statistics to show instead of the logged-in player's for the current ruleset (a ranking screen's, for its score).
        /// </summary>
        public IBindable<Users.UserStatistics?>? Statistics { get; init; }

        /// <summary>
        /// The player to show when <see cref="Statistics"/> come without one.
        /// </summary>
        public APIUser? User { get; init; }

        /// <summary>
        /// The ruleset to show the icon of, instead of the current one.
        /// </summary>
        public RulesetInfo? Ruleset { get; init; }

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        // Who's shown: clicking opens their profile, like stable's.
        private APIUser? shownUser;

        // Statistics don't always come with their user (not from every server): the logged-in user then.
        private APIUser userOf(Users.UserStatistics statistics) => statistics.User ?? User ?? api.LocalUser.Value;

        [BackgroundDependencyLoader]
        private void load()
        {
            AutoSizeAxes = Axes.Both;

            // reference: https://github.com/peppy/osu-stable-reference/blob/c34a74fb61c17c5667486a12548485d1f03baa2e/osu!/Online/Drawable/User.cs#L622
            // with minor adjustments to position/size specifications to better match visually (besides multiplying by 1.6)
            InternalChildren = new Drawable[]
            {
                usernameText = new OsuSpriteText
                {
                    Position = new Vector2(50.5f, -2f) * 1.6f,
                    Font = OsuFont.Default.With(size: 14 * 1.6f),
                },
                infoText = new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: 10 * 1.6f))
                {
                    Position = new Vector2(50f, 11.5f) * 1.6f,
                },
                rulesetIcon = new Sprite
                {
                    Position = new Vector2(176, 0) * 1.6f,
                    Colour = Color4.White.Opacity(70),
                },
                rankText = new OsuSpriteText
                {
                    Position = new Vector2(200, 9) * 1.6f,
                    Font = OsuFont.Default.With(size: 36 * 1.6f * 0.9f),
                    Origin = Anchor.TopRight,
                },
                levelBar = new Sprite
                {
                    Texture = skins.DefaultClassicSkin.GetTexture("levelbar"),
                    // fixing the texture rectangle allows us to crop the texture by the drawable width.
                    TextureRelativeSizeAxes = Axes.Y,
                    TextureRectangle = new RectangleF(0, 0, 200, 1),
                    OriginPosition = new Vector2(-120, -62),
                    Colour = new Color4(252, 184, 6, 255),
                    Alpha = 0.7f,
                },
                new Sprite
                {
                    Texture = skins.DefaultClassicSkin.GetTexture("levelbar-bg"),
                    OriginPosition = new Vector2(-120, -62),
                    Alpha = 0.4f,
                },
                background = new Sprite
                {
                    Texture = skins.DefaultClassicSkin.GetTexture("user-bg"),
                    // https://github.com/peppy/osu-stable-reference/blob/c34a74fb61c17c5667486a12548485d1f03baa2e/osu!/Online/Drawable/User.cs#L520-L521
                    Colour = new Color4(1, 1, 1, 255),
                    Blending = BlendingParameters.Additive,
                    Position = new Vector2(-4) * 1.6f,
                },
                avatar = new UpdateableAvatar(isInteractive: false, showUserPanelOnHover: false)
                {
                    Size = new Vector2(74),
                    Position = new Vector2(23.5f) * 1.6f,
                    Origin = Anchor.Centre,
                },
                hoverSound = new SkinnableSound(new SampleInfo("click-short")),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (Statistics != null)
            {
                Statistics.BindValueChanged(_ => updateDisplay(), true);
                return;
            }

            if (userStatisticsProvider != null)
                userStatisticsProvider.StatisticsUpdated += onStatisticsUpdated;

            ruleset.BindValueChanged(_ => updateDisplay(), true);
            selectedMods?.BindValueChanged(_ => updateDisplay());
        }

        // Reprise: with Relax or Autopilot on, the rank and pp shown are that mode's.
        [Resolved(CanBeNull = true)]
        private IBindable<IReadOnlyList<Mod>>? selectedMods { get; set; }

        private RulesetInfo currentRuleset => Ruleset ?? ruleset.Value.ForMods(selectedMods?.Value ?? Array.Empty<Mod>());

        private void onStatisticsUpdated(UserStatisticsUpdate statistics)
        {
            if (currentRuleset.ShortName == statistics.Ruleset.ShortName)
                updateDisplay();
        }

        private void updateDisplay()
        {
            var shownRuleset = currentRuleset;
            var statistics = Statistics != null ? Statistics.Value : userStatisticsProvider?.GetStatisticsFor(shownRuleset);

            if (statistics == null)
            {
                // Before they come (a given player still shows).
                usernameText.Text = User?.Username ?? string.Empty;
                avatar.User = shownUser = User;
                infoText.Text = string.Empty;
                rankText.Text = string.Empty;
                rulesetIcon.Hide();
            }
            else
            {
                usernameText.Text = userOf(statistics).Username;
                infoText.Clear();
                // As stable writes them (whatever the system's number format): "1,292pp", "94.24%".
                infoText.AddText(string.Create(CultureInfo.InvariantCulture, $"Performance:{statistics.PP ?? 0:N0}pp"));
                infoText.NewLine();
                infoText.AddText(string.Create(CultureInfo.InvariantCulture, $"Accuracy:{statistics.Accuracy:0.00}%"));
                infoText.NewLine();
                infoText.AddText($"Lv{statistics.Level.Current}");

                if (!statistics.GlobalRank.HasValue)
                    rankText.Hide();
                else
                {
                    int rank = statistics.GlobalRank.Value;

                    rankText.Text = $"#{rank}";

                    if (rank > 100000)
                        rankText.Colour = new Color4(255, 255, 255, 40);
                    else if (rank > 50000)
                        rankText.Colour = new Color4(255, 255, 255, 60);
                    else if (rank > 1000)
                        rankText.Colour = new Color4(255, 255, 255, 80);
                    else if (rank > 10)
                        rankText.Colour = new Color4(255, 255, 255, 100);
                    else if (rank > 1)
                        rankText.Colour = new Color4(244, 218, 73, 120);
                    else
                        rankText.Colour = new Color4(88, 171, 248, 120);
                }

                rulesetIcon.Alpha = 70 / 255f;
                rulesetIcon.Texture = skins.DefaultClassicSkin.GetTexture($"mode-{shownRuleset.ShortName}-small");

                if (statistics.Level.Progress == 0)
                    levelBar.Hide();
                else
                {
                    levelBar.Width = 198 * statistics.Level.Progress / 100f;
                    levelBar.Show();
                }

                avatar.User = shownUser = userOf(statistics);
            }
        }

        protected override bool OnHover(HoverEvent e)
        {
            background.FadeColour(new Color4(41, 41, 41, 255), 200);
            hoverSound.Play();
            return true;
        }

        protected override bool OnClick(ClickEvent e)
        {
            var user = shownUser ?? api.LocalUser.Value;

            if (user.OnlineID <= 1)
                return false;

            game?.ShowUser(user);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeColour(new Color4(1, 1, 1, 255), 200);
            base.OnHoverLost(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (userStatisticsProvider.IsNotNull())
                userStatisticsProvider!.StatisticsUpdated -= onStatisticsUpdated;

            base.Dispose(isDisposing);
        }
    }
}
