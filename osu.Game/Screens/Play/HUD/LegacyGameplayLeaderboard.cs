// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Configuration;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Skinning;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Play.HUD
{
    /// <summary>
    /// osu!stable's in-game leaderboard (with a legacy skin and <see cref="OsuSetting.StableLayouts"/>, instead of the game's): at
    /// the left from 202 down in its 640x480, 36 apart, at most 6 around the player's own score; each a dark box with the name, the
    /// score and combo ("-" before any score, as stable's), and the position large and faint; the player's own blue, flashing with the
    /// skin's explosions when passing someone. From the 2015 client.
    /// </summary>
    public partial class LegacyGameplayLeaderboard : CompositeDrawable
    {
        // Stable's 640x480 values, 1.6 times in the 1024x768 layout.
        private const float scale = 1.6f;
        private const float top = 202;
        private const float spacing = 36;
        private const int max_shown = 6;

        private const double move_duration = 600;

        [Resolved]
        private IGameplayLeaderboardProvider? leaderboardProvider { get; set; }

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        private readonly IBindableList<GameplayLeaderboardScore> scores = new BindableList<GameplayLeaderboardScore>();
        private readonly Dictionary<GameplayLeaderboardScore, Entry> entries = new Dictionary<GameplayLeaderboardScore, Entry>();

        private Bindable<bool> stableLayouts = null!;
        private Bindable<bool> leaderboardVisible = null!;
        private Bindable<ScoringMode> scoringMode = null!;
        private Bindable<float> uiScale = null!;

        private Container<Entry> flow = null!;
        private Container explosions = null!;

        public LegacyGameplayLeaderboard()
        {
            RelativeSizeAxes = Axes.Both;
        }

        /// <summary>
        /// Whether stable's leaderboard replaces the game's: stable layouts on, with a legacy skin.
        /// </summary>
        public static bool IsActive(OsuConfigManager config, SkinManager skinManager) =>
            config.Get<bool>(OsuSetting.StableLayouts) && skinManager.CurrentSkin.Value is LegacySkin;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            stableLayouts = config.GetBindable<bool>(OsuSetting.StableLayouts);
            leaderboardVisible = config.GetBindable<bool>(OsuSetting.GameplayLeaderboard);
            scoringMode = config.GetBindable<ScoringMode>(OsuSetting.ScoreDisplayMode);
            uiScale = config.GetBindable<float>(OsuSetting.UIScale);

            InternalChild = new DrawSizePreservingFillContainer
            {
                TargetDrawSize = new Vector2(1024, 768),
                Strategy = DrawSizePreservationStrategy.Minimum,
                Children = new Drawable[]
                {
                    flow = new Container<Entry> { RelativeSizeAxes = Axes.Both },
                    explosions = new Container { RelativeSizeAxes = Axes.Both },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (leaderboardProvider != null)
            {
                scores.BindTo(leaderboardProvider.Scores);
                scores.BindCollectionChanged((_, _) => rebuild(), true);
            }

            scoringMode.BindValueChanged(_ =>
            {
                foreach (var entry in entries.Values)
                    entry.UpdateText(scoringMode.Value);
            });
        }

        private void rebuild()
        {
            foreach (var removed in entries.Keys.Except(scores).ToArray())
            {
                flow.Remove(entries[removed], true);
                entries.Remove(removed);
            }

            foreach (var score in scores.Where(s => !entries.ContainsKey(s)))
            {
                // Stable's own (from its osu!ui.dll) for skins without one; the classic skin has none.
                var entry = new Entry(score, skin.GetTexture(@"menu-button-background") ?? textures.Get(@"Stable/menu-button-background"));
                entries[score] = entry;
                flow.Add(entry);

                score.TotalScore.BindValueChanged(_ => entry.UpdateText(scoringMode.Value));
                score.Combo.BindValueChanged(_ => entry.UpdateText(scoringMode.Value));
                score.DisplayOrder.BindValueChanged(_ => Scheduler.AddOnce(layout));
                entry.UpdateText(scoringMode.Value);
            }

            Scheduler.AddOnce(layout);
        }

        private bool laidOut;
        private int lastOwnIndex = -1;

        // The skin's, else the classic skin's (as stable's default).
        private Texture? getTexture(string name) => skin.GetTexture(name) ?? skinManager.DefaultClassicSkin.GetTexture(name);

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private TextureStore textures { get; set; } = null!;

        // Stable's: sorted, at most 6 shown from the one that keeps the player's own the last of them.
        private void layout()
        {
            var sorted = entries.Values.OrderBy(e => e.Score.DisplayOrder.Value).ToList();
            int own = sorted.FindIndex(e => e.Score.Tracked);
            int first = own > max_shown - 1 ? own - (max_shown - 1) : 0;

            for (int i = 0; i < sorted.Count; i++)
            {
                var entry = sorted[i];
                bool shown = i >= first && i - first < max_shown;

                entry.Place.Value = entry.Score.Position.Value ?? i + 1;

                var target = new Vector2(0, (top + spacing * (i - first)) * scale);
                float alpha = !shown ? 0 : entry.Score.Tracked ? 1 : 0.8f - (float)i / sorted.Count * 0.3f;

                if (!laidOut)
                {
                    entry.Position = target;
                    entry.Alpha = alpha;
                }
                else
                {
                    entry.MoveTo(target, move_duration, Easing.OutQuint);
                    entry.FadeTo(alpha, move_duration, Easing.OutQuint);
                }
            }

            if (own >= 0)
            {
                sorted[own].SetOwnColour(own == 0);

                // Stable's: passing someone flashes the player's own entry (back to its colour) and its explosions.
                if (laidOut && lastOwnIndex >= 0 && own < lastOwnIndex)
                    explode(sorted[own]);

                lastOwnIndex = own;
            }

            laidOut = sorted.Count > 0;
        }

        // Stable's: "scoreboard-explosion-2" stretching 16 wide in 0.2 seconds and fading in 0.4, "scoreboard-explosion-1" growing a
        // third taller and fading in 0.7, both added at the entry's left, 15 down.
        private void explode(Entry entry)
        {
            entry.Flash();

            var at = entry.Position + new Vector2(0, 15 * scale);

            addExplosion(@"scoreboard-explosion-2", at, 400, e => e.ScaleTo(new Vector2(16, 1.2f), 200, Easing.Out));
            addExplosion(@"scoreboard-explosion-1", at, 700, e => e.ScaleTo(new Vector2(1, 1.3f), 700, Easing.Out));
        }

        /// <summary>How many explosions have been shown (they're gone within 0.7 seconds: for tests).</summary>
        public int ExplosionsShown { get; private set; }

        private void addExplosion(string name, Vector2 at, double fade, Action<Sprite> grow)
        {
            if (getTexture(name) is not Texture texture)
                return;

            ExplosionsShown++;

            var explosion = new Sprite
            {
                Texture = texture,
                Origin = Anchor.CentreLeft,
                Position = at,
                Blending = BlendingParameters.Additive,
            };

            explosions.Add(explosion);
            grow(explosion);
            explosion.FadeOut(fade).Expire();
        }

        protected override void Update()
        {
            base.Update();

            Alpha = stableLayouts.Value && skinManager.CurrentSkin.Value is LegacySkin && leaderboardVisible.Value ? 1 : 0;

            // With the HUD, in stable's 16:9 area (the skin's leaderboard panel is there).
            StableHudArea.Apply(this, true, uiScale.Value);
        }

        private partial class Entry : CompositeDrawable
        {
            private const float avatar_size = 25;
            private const float text_x = 4 + avatar_size;

            public readonly GameplayLeaderboardScore Score;
            public readonly Bindable<int> Place = new Bindable<int>();

            private readonly TruncatingSpriteText name;
            private readonly OsuSpriteText details;
            private readonly OsuSpriteText rank;

            private readonly Drawable background;

            public Entry(GameplayLeaderboardScore score, Texture? buttonBackground)
            {
                Score = score;
                AutoSizeAxes = Axes.Both;

                InternalChildren = new Drawable[]
                {
                    // Stable's: the skin's menu button background at 0.6, from 460 across and 20 down in it (its right end, the
                    // width of the name), darkened (black, 100 of 255); the player's own blue.
                    background = buttonBackground != null
                        ? new Sprite
                        {
                            Texture = buttonBackground,
                            Size = buttonBackground.DisplaySize,
                            OriginPosition = new Vector2(460, 20),
                            Scale = new Vector2(0.6f),
                            Colour = new Color4(0, 0, 0, 100),
                            BypassAutoSizeAxes = Axes.Both,
                        }
                        : new Box
                        {
                            Size = new Vector2(84, 29) * scale,
                            Colour = new Color4(0, 0, 0, 100),
                        },
                    // Not stable's: the player's avatar at the left (theirs on the server, else their osu! one by name), the rest after it.
                    new Container
                    {
                        Position = new Vector2(2, 2) * scale,
                        Size = new Vector2(avatar_size) * scale,
                        Masking = true,
                        CornerRadius = 2 * scale,
                        Child = new DelayedLoadWrapper(() => new DrawableAvatar(score.User) { RelativeSizeAxes = Axes.Both }, 0)
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                    },
                    // Stable's: cut to 90 wide with "...".
                    name = new TruncatingSpriteText
                    {
                        Position = new Vector2(text_x, 1) * scale,
                        Font = StableFont.Get(14),
                        Text = score.User.Username,
                        MaxWidth = (90 - text_x) * scale,
                    },
                    details = new OsuSpriteText
                    {
                        Position = new Vector2(text_x, 15) * scale,
                        Font = StableFont.Get(10),
                    },
                    // Stable's at 90, whose text ends about 84.5 in (inside skins' panels): the glyph's end there.
                    rank = new OsuSpriteText
                    {
                        Position = new Vector2(84.5f, 0) * scale,
                        Origin = Anchor.TopRight,
                        Font = StableFont.Get(30),
                        Colour = new Color4(255, 255, 255, 120),
                    },
                };

                Place.BindValueChanged(p => rank.Text = p.NewValue > 0 ? p.NewValue.ToString(CultureInfo.InvariantCulture) : "--", true);
            }

            public void SetOwnColour(bool first) =>
                background.Colour = first ? new Color4(97, 190, 255, 150) : new Color4(31, 115, 153, 150);

            public void Flash() => background.FlashColour(Color4.White, 200);

            public void UpdateText(ScoringMode mode)
            {
                long total = Score.GetDisplayScore(mode);
                details.Text = string.Create(CultureInfo.InvariantCulture, $"{(total > 0 ? total.ToString("0,0", CultureInfo.InvariantCulture) : "-")} ({Score.Combo.Value}x)");
            }
        }
    }
}
