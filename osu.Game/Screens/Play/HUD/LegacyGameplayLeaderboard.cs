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
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osu.Game.Online.API;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Skinning;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Play.HUD
{
    /// <summary>
    /// osu!stable's in-game leaderboard (with a legacy skin and <see cref="OsuSetting.StableGameplayLeaderboard"/>, instead of the game's), as
    /// stable's own Scoreboard and ScoreboardEntry draw it: at the left, 36 apart in its 640x480 (from 205 down in osu!, 264 in taiko, 135 in
    /// mania, 100 in catch); the #1 always at the top, then up to 5 more ending with the player's own; each the skin's menu button background
    /// tinted (others blue, the player's own white, their personal best orange, friends pink), the name, the score and highest combo in the
    /// skin's scoreentry digits, and the position large and faint. It shows in breaks (and before and after the objects), and all the time when
    /// the in-game leaderboard is toggled on (Tab); never once the play has passed or failed. Passing someone flashes the player's own entry
    /// with the skin's explosions.
    /// </summary>
    public partial class LegacyGameplayLeaderboard : CompositeDrawable
    {
        // Stable's 640x480 values, 1.6 times in the 1024x768 layout.
        private const float scale = 1.6f;
        private const float spacing = 36;
        private const int max_shown = 6;

        private const double move_duration = 600;

        // Stable's: 0.08 of the way a 60th of a second.
        private const float fade_per_second = 0.08f * 60;

        /// <summary>
        /// Whether the play is in a break (or before or after its objects): stable's leaderboard shows then.
        /// </summary>
        public IBindable<bool>? IsBreakTime { get; init; }

        [Resolved]
        private IGameplayLeaderboardProvider? leaderboardProvider { get; set; }

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private GameplayState? gameplayState { get; set; }

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private RealmKeyBindingStore? keyBindingStore { get; set; }

        private readonly IBindableList<GameplayLeaderboardScore> scores = new BindableList<GameplayLeaderboardScore>();
        private readonly Dictionary<GameplayLeaderboardScore, Entry> entries = new Dictionary<GameplayLeaderboardScore, Entry>();

        private Bindable<bool> stableLayouts = null!;
        private Bindable<bool> leaderboardVisible = null!;
        private Bindable<ScoringMode> scoringMode = null!;
        private Bindable<float> uiScale = null!;

        private Container content = null!;
        private Container<Entry> flow = null!;
        private Container explosions = null!;
        private OsuSpriteText status = null!;

        private float top = 205;

        public LegacyGameplayLeaderboard()
        {
            RelativeSizeAxes = Axes.Both;

            // Updated while hidden: it decides in Update whether it shows (it never came back once hidden otherwise).
            AlwaysPresent = true;
        }

        /// <summary>
        /// Whether stable's leaderboard replaces the game's: stable layouts on, with a legacy skin.
        /// </summary>
        public static bool IsActive(OsuConfigManager config, SkinManager skinManager) =>
            config.Get<bool>(OsuSetting.StableGameplayLeaderboard) && skinManager.CurrentSkin.Value is LegacySkin;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            stableLayouts = config.GetBindable<bool>(OsuSetting.StableGameplayLeaderboard);
            leaderboardVisible = config.GetBindable<bool>(OsuSetting.GameplayLeaderboard);
            scoringMode = config.GetBindable<ScoringMode>(OsuSetting.ScoreDisplayMode);
            uiScale = config.GetBindable<float>(OsuSetting.UIScale);

            // Stable's per ruleset (Ruleset.ScoreboardPosition).
            top = gameplayState?.Ruleset.RulesetInfo.OnlineID switch
            {
                1 => 264,
                2 => 100,
                3 => 135,
                _ => 205,
            };

            InternalChild = new DrawSizePreservingFillContainer
            {
                TargetDrawSize = new Vector2(1024, 768),
                Strategy = DrawSizePreservationStrategy.Minimum,
                Child = content = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        flow = new Container<Entry> { RelativeSizeAxes = Axes.Both },
                        explosions = new Container { RelativeSizeAxes = Axes.Both },
                        // Stable's "Hit <Tab> to toggle scoreboard!" over it at the start, when it's toggled on: bold, fading over 6 seconds.
                        status = new OsuSpriteText
                        {
                            Origin = Anchor.BottomLeft,
                            Position = new Vector2(0, (top - 5) * scale),
                            Font = StableFont.Get(12, bold: true),
                            Alpha = 0,
                        },
                    },
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

            if (leaderboardVisible.Value && keyBindingStore != null)
            {
                status.Text = $"Hit <{keyBindingStore.GetBindingsStringFor(GlobalAction.ToggleInGameLeaderboard)}> to toggle scoreboard!";
                status.FadeIn().FadeOut(6000);
            }
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
                var entry = new Entry(score, skin.GetTexture(@"menu-button-background") ?? textures.Get(@"Stable/menu-button-background"), kindOf(score));
                entries[score] = entry;
                flow.Add(entry);

                score.TotalScore.BindValueChanged(_ => entry.UpdateText(scoringMode.Value));
                score.Combo.BindValueChanged(_ => entry.UpdateText(scoringMode.Value));
                score.DisplayOrder.BindValueChanged(_ => Scheduler.AddOnce(layout));
                score.HasQuit.BindValueChanged(q => entry.SetQuit(q.NewValue), true);
                entry.UpdateText(scoringMode.Value);
            }

            Scheduler.AddOnce(layout);
        }

        private EntryKind kindOf(GameplayLeaderboardScore score)
        {
            if (score.Tracked)
                return EntryKind.Own;

            // osu!'s own: its player id is osu!'s, not one of ours.
            if (score.IsBancho)
                return EntryKind.Other;

            int userId = score.User.OnlineID;

            if (userId > 0 && userId == api.LocalUser.Value.OnlineID)
                return EntryKind.PersonalBest;

            if (userId > 0 && api.LocalUserState.Friends.Any(f => f.TargetID == userId))
                return EntryKind.Friend;

            return EntryKind.Other;
        }

        private bool laidOut;
        private int lastOwnIndex = -1;

        // The skin's, else the classic skin's (as stable's default).
        private Texture? getTexture(string name) => skin.GetTexture(name) ?? skinManager.DefaultClassicSkin.GetTexture(name);

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private TextureStore textures { get; set; } = null!;

        // Stable's (Scoreboard.Reposition): the #1 always first; then, from the one that keeps the player's own the last of 6, as many as fit.
        private void layout()
        {
            var sorted = entries.Values.OrderBy(e => e.Score.DisplayOrder.Value).ToList();
            int own = sorted.FindIndex(e => e.Score.Tracked);
            int first = own > max_shown - 1 ? own - (max_shown - 1) : 0;
            if (first > 0)
                first++;

            int slot = 0;

            for (int i = 0; i < sorted.Count; i++)
            {
                var entry = sorted[i];
                bool shown = (i >= first && slot < max_shown) || i == 0;

                // Stable's: the place, blank for the player's own when it isn't known (past the end of the online scores).
                entry.Place.Value = entry.Score.Tracked ? entry.Score.Position.Value : entry.Score.Position.Value ?? i + 1;

                // Stable's multiplayer layout (ScoreboardEntryExtended): each entry 32.4 to the right, its avatar in the room that leaves.
                var target = new Vector2(Entry.AVATAR_OFFSET, (top + spacing * slot)) * scale;

                // Stable's: others fainter further down the list; the player's own and their personal best as they are.
                float alpha = !shown ? 0 : entry.Kind is EntryKind.Own or EntryKind.PersonalBest ? 1 : 0.8f - (float)i / sorted.Count * 0.3f;

                if (!laidOut)
                {
                    entry.Position = target;
                    entry.Alpha = alpha;
                }
                else
                {
                    entry.MoveTo(target, move_duration, Easing.Out);
                    entry.FadeTo(alpha, move_duration, Easing.Out);
                }

                if (shown)
                    slot++;
            }

            if (own >= 0)
            {
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

        /// <summary>Whether stable would show it now (breaks, or toggled on; never after the play passed or failed).</summary>
        public bool ShouldShow =>
            gameplayState?.HasPassed != true && gameplayState?.HasFailed != true
                                             && (leaderboardVisible.Value || IsBreakTime?.Value != false);

        protected override void Update()
        {
            base.Update();

            Alpha = stableLayouts.Value && skinManager.CurrentSkin.Value is LegacySkin ? 1 : 0;

            // Stable's: in and out at the same pace, not eased.
            float step = (float)(Time.Elapsed / 1000) * fade_per_second;
            content.Alpha = Math.Clamp(content.Alpha + (ShouldShow ? step : -step), 0, 1);

            // With the HUD, in stable's 16:9 area (the skin's leaderboard panel is there).
            StableHudArea.Apply(this, true, uiScale.Value);
        }

        private enum EntryKind
        {
            Other,
            Own,
            PersonalBest,
            Friend,
        }

        private partial class Entry : CompositeDrawable
        {
            // Stable's multiplayer entries (ScoreboardEntryExtended): 32.4 to the right, the avatar at most 51 pixels (of 1024x768) across,
            // centred 15.6 left of the entry and 12.51 down (on the card's middle).
            public const float AVATAR_OFFSET = 32.4f;
            private const float avatar_size = 51;
            private static readonly Vector2 avatar_centre = new Vector2(-15.6f, 12.51f);

            // Stable's menu button background is made for a 699x103 template (its origin, 470 by 20, is in it); the classic skin's is
            // half that, so each is drawn as the template's size.
            private static readonly Vector2 template_size = new Vector2(699, 103);

            // Stable's ScoreboardEntry: the right end of the name, score and combo, and of the position.
            private const float right = 82;

            public readonly GameplayLeaderboardScore Score;
            public readonly EntryKind Kind;
            public readonly Bindable<int?> Place = new Bindable<int?>();

            private readonly TruncatingSpriteText name;
            private readonly LegacySpriteText scoreText;
            private readonly LegacySpriteText comboText;
            private readonly LegacySpriteText rank;

            private readonly Drawable background;
            private readonly Color4 colour;

            public Entry(GameplayLeaderboardScore score, Texture? buttonBackground, EntryKind kind)
            {
                Score = score;
                Kind = kind;
                AutoSizeAxes = Axes.Both;

                // Stable's colours (Scoreboard.Add, SetTrackingEntry, Player.InitializeOnlineData).
                colour = kind switch
                {
                    EntryKind.Own => new Color4(250, 250, 250, 100),
                    EntryKind.PersonalBest => new Color4(255, 69, 0, 150),
                    EntryKind.Friend => new Color4(255, 97, 175, 180),
                    _ => new Color4(31, 115, 153, 150),
                };

                InternalChildren = new Drawable[]
                {
                    // Stable's: the skin's menu button background at 0.62, from 470 across and 20 down in it (its right end, the width of
                    // the name), tinted.
                    background = buttonBackground != null
                        ? new Sprite
                        {
                            Texture = buttonBackground,
                            Size = new Vector2(template_size.X, template_size.X * buttonBackground.DisplayHeight / buttonBackground.DisplayWidth),
                            OriginPosition = new Vector2(470, 20 * template_size.X * buttonBackground.DisplayHeight / buttonBackground.DisplayWidth / template_size.Y),
                            Scale = new Vector2(0.62f),
                            Colour = colour,
                            BypassAutoSizeAxes = Axes.Both,
                        }
                        : new Box
                        {
                            Size = new Vector2(84, 29) * scale,
                            Colour = colour,
                        },
                    // Stable's at 82 (right-aligned), in the skin's digits, 2.2 times and faint, behind the text.
                    rank = new LegacySpriteText(LegacyFont.ScoreEntry)
                    {
                        Position = new Vector2(right, -2) * scale,
                        Origin = Anchor.TopRight,
                        Scale = new Vector2(2.2f),
                        Colour = new Color4(255, 255, 255, 80),
                        FixedWidth = true,
                    },
                    // Stable's multiplayer avatar, for every entry here (theirs on the server, else their osu! one by name).
                    new Container
                    {
                        Position = avatar_centre * scale,
                        Origin = Anchor.Centre,
                        Size = new Vector2(avatar_size),
                        BypassAutoSizeAxes = Axes.Both,
                        Child = new DelayedLoadWrapper(() => new DrawableAvatar(score.User) { RelativeSizeAxes = Axes.Both }, 0)
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                    },
                    // Stable's: 14, cut to 90 wide with "...".
                    name = new TruncatingSpriteText
                    {
                        Position = new Vector2(0, -2) * scale,
                        Font = StableFont.Get(14),
                        Text = score.User.Username,
                        MaxWidth = 90 * scale,
                    },
                    // Stable's: the score at the left and the combo (light blue) at 82, in the skin's digits, 18 down.
                    scoreText = new LegacySpriteText(LegacyFont.ScoreEntry)
                    {
                        Position = new Vector2(2, 18) * scale,
                        FixedWidth = true,
                    },
                    comboText = new LegacySpriteText(LegacyFont.ScoreEntry)
                    {
                        Position = new Vector2(right, 18) * scale,
                        Origin = Anchor.TopRight,
                        Colour = new Color4(153, 251, 255, 255),
                        FixedWidth = true,
                    },
                };

                Place.BindValueChanged(p => rank.Text = p.NewValue > 0 ? p.NewValue.Value.ToString(CultureInfo.InvariantCulture) : string.Empty, true);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                // Stable's overlaps: 2.5 between the score's and combo's digits, 3 between the position's.
                scoreText.Spacing = comboText.Spacing = new Vector2(-2.5f, 0);
                rank.Spacing = new Vector2(-3, 0);
            }

            public void Flash() => background.FlashColour(Color4.White, 200);

            // Stable's: a player who isn't passing (in multiplayer: one who quit) has their name in red.
            public void SetQuit(bool quit) => name.FadeColour(quit ? new Color4(236, 39, 81, 255) : Color4.White, 150);

            public void UpdateText(ScoringMode mode)
            {
                long total = Score.GetDisplayScore(mode);
                int combo = Score.Combo.Value;

                // Stable's: "-" before any score, and the combo as "1,234x" (nothing at 0).
                scoreText.Text = total > 0 ? total.ToString("#,#", CultureInfo.InvariantCulture) : "-";
                comboText.Text = combo > 0 ? combo.ToString("#,#", CultureInfo.InvariantCulture) + "x" : string.Empty;
            }
        }
    }
}
