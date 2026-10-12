// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Extensions;
using osu.Game.Rulesets;
using osu.Game.Users.Drawables;
using osuTK;

namespace osu.Game.Overlays.BeatmapSet
{
    /// <summary>
    /// The website's difficulty band (osu!'s "Beatmap Info" design proposal): the shown difficulty's line (its star rating, name, mappers,
    /// length and BPM), then its difficulties as icons in their colours, a pill under the chosen one.
    /// The difficulty under the mouse is shown until it leaves.
    /// </summary>
    public partial class BeatmapPicker : Container
    {
        public readonly DifficultiesContainer Difficulties;

        public readonly Bindable<APIBeatmap?> Beatmap = new Bindable<APIBeatmap?>();

        /// <summary>
        /// The difficulty shown: the one under the mouse, else the chosen one.
        /// </summary>
        public readonly Bindable<APIBeatmap?> ShownBeatmap = new Bindable<APIBeatmap?>();

        private APIBeatmapSet? beatmapSet;

        public APIBeatmapSet? BeatmapSet
        {
            get => beatmapSet;
            set
            {
                if (value == beatmapSet) return;

                beatmapSet = value;
                updateDisplay();
            }
        }

        private readonly FillFlowContainer line;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public BeatmapPicker()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            Child = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 9),
                Children = new Drawable[]
                {
                    line = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 22,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(7, 0),
                    },
                    Difficulties = new DifficultiesContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Spacing = new Vector2(5),
                        // Room for the chosen one's pill, over the band's bottom line.
                        Padding = new MarginPadding { Bottom = 13 },
                        OnLostHover = () => ShownBeatmap.Value = Beatmap.Value,
                    },
                }
            };

            Beatmap.ValueChanged += b =>
            {
                ShownBeatmap.Value = b.NewValue;
                updateDifficultyButtons();
            };

            ShownBeatmap.ValueChanged += b => showBeatmap(b.NewValue);
        }

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            // From the start (the mode is chosen as soon as a set arrives, maybe before this is loaded).
            ruleset.BindValueChanged(_ => updateDisplay());
            updateDisplay();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Whatever changed meanwhile.
            updateDisplay();

            // done here so everything can bind in intialization and get the first trigger
            Beatmap.TriggerChange();
        }

        private void updateDisplay()
        {
            Difficulties.Clear();

            if (BeatmapSet != null)
            {
                // The website's: a mode with its own difficulties lists only those, one without lists the converts.
                var own = BeatmapSet.Beatmaps.Where(b => b.Ruleset.MatchesOnlineID(ruleset.Value)).ToArray();
                var shown = own.Length > 0 ? own : (BeatmapSet.Converts ?? Array.Empty<APIBeatmap>()).Where(b => b.Ruleset.MatchesOnlineID(ruleset.Value)).ToArray();

                Difficulties.ChildrenEnumerable = shown.OrderBy(b => b.StarRating)
                                                       .Select(b => new DifficultySelectorButton(b, b.Convert ? new RulesetInfo { OnlineID = 0 } : null)
                                                       {
                                                           State = DifficultySelectorState.NotSelected,
                                                           OnHovered = beatmap => ShownBeatmap.Value = beatmap,
                                                           OnClicked = beatmap => { Beatmap.Value = beatmap; },
                                                       });
            }

            // If a selection is already made, try and maintain it.
            if (Beatmap.Value != null)
                Beatmap.Value = Difficulties.FirstOrDefault(b => b.Beatmap.OnlineID == Beatmap.Value.OnlineID)?.Beatmap;

            // Else just choose the first available difficulty for now.
            Beatmap.Value ??= Difficulties.FirstOrDefault()?.Beatmap;

            if (IsLoaded)
                showBeatmap(ShownBeatmap.Value);

            updateDifficultyButtons();
        }

        // The website's difficulty line: the star rating pill, the name (16), "mapped by" its mappers with their avatars (only avatars past
        // three), and its length and BPM at the end.
        private void showBeatmap(APIBeatmap? beatmap)
        {
            line.Clear();

            if (beatmap == null)
                return;

            line.Add(new StarRatingDisplay(new StarDifficulty(beatmap.StarRating, 0), StarRatingDisplaySize.Small)
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
            });

            line.Add(new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = beatmap.DifficultyName,
                Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold),
            });

            var mappers = beatmapSet != null ? SetPerson.MappersOf(beatmapSet, beatmap) : Array.Empty<SetPerson>();

            if (mappers.Length > 0)
            {
                var mappedBy = new FillFlowContainer
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(4, 0),
                    Child = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        // Level with the larger name before it.
                        Y = -1,
                        Text = BeatmapsetsStrings.ShowDetailsMappedBy(string.Empty),
                        Font = OsuFont.GetFont(size: 14),
                        Colour = colourProvider.Content2,
                    },
                };

                bool compact = mappers.Length > 3;

                // 8 apart from each other (the website's), 4 after "mapped by".
                for (int i = 0; i < mappers.Length; i++)
                {
                    mappedBy.Add(new MapperLink(mappers[i], compact, colourProvider)
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Margin = new MarginPadding { Left = i > 0 ? 4 : 0 },
                    });
                }

                line.Add(mappedBy);
            }

            line.Add(new LengthPill(beatmap, colourProvider)
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
            });
        }

        protected override void Update()
        {
            base.Update();

            // The length pill at the line's end.
            var pill = line.OfType<LengthPill>().FirstOrDefault();

            if (pill != null)
            {
                float used = line.Where(d => d != pill).Sum(d => d.DrawWidth + line.Spacing.X);
                float margin = Math.Max(0, line.DrawWidth - used - pill.DrawWidth);

                if (pill.Margin.Left != margin)
                    pill.Margin = new MarginPadding { Left = margin };
            }
        }

        private void updateDifficultyButtons()
        {
            Difficulties.Children.ToList().ForEach(diff => diff.State = diff.Beatmap == Beatmap.Value ? DifficultySelectorState.Selected : DifficultySelectorState.NotSelected);
        }

        // A mapper: their avatar and name (only the avatar in a collab of more than three, the name in its tooltip). osu! users (imported
        // sets) link to osu!.
        private partial class MapperLink : FillFlowContainer, IHasTooltip
        {
            public LocalisableString TooltipText { get; }

            public MapperLink(SetPerson person, bool compact, OverlayColourProvider colours)
            {
                AutoSizeAxes = Axes.Both;
                Direction = FillDirection.Horizontal;
                Spacing = new Vector2(4, 0);
                TooltipText = compact ? person.Username : string.Empty;

                Add(SetPersonDisplay.Avatar(person, compact ? 20 : 17.5f).With(a =>
                {
                    a.Anchor = Anchor.CentreLeft;
                    a.Origin = Anchor.CentreLeft;
                    // Level with the text's letters (its box has room above them).
                    a.Y = 1;
                }));

                if (compact)
                    return;

                var name = new LinkFlowContainer(t =>
                {
                    t.Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold);
                    t.Colour = colours.Light1;
                })
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    AutoSizeAxes = Axes.Both,
                };

                SetPersonDisplay.AddName(name, person);
                Add(name);
            }
        }

        // "Length 3:21  BPM 180" on a dark pill (the drain length in its tooltip).
        private partial class LengthPill : CircularContainer, IHasTooltip
        {
            public LocalisableString TooltipText { get; }

            public LengthPill(APIBeatmap beatmap, OverlayColourProvider colours)
            {
                AutoSizeAxes = Axes.X;
                Height = 22;
                Masking = true;
                TooltipText = LocalisableString.Interpolate($"Length (Drain length: {formatLength(beatmap.HitLength)})");

                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.Background5.Opacity(0.6f),
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(16, 0),
                        Padding = new MarginPadding { Horizontal = 20 },
                        Children = new[]
                        {
                            pair("Length", formatLength(beatmap.Length), colours),
                            pair("BPM", beatmap.BPM.ToLocalisableString(@"0.##"), colours),
                        }
                    },
                };
            }

            private static string formatLength(double milliseconds) => TimeSpan.FromMilliseconds(milliseconds).ToString(milliseconds >= 3600000 ? @"h\:mm\:ss" : @"m\:ss");

            private static Drawable pair(LocalisableString label, LocalisableString value, OverlayColourProvider colours)
            {
                var text = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold))
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    AutoSizeAxes = Axes.Both,
                };

                text.AddText(label);
                text.AddText(@" ");
                text.AddText(value, t => t.Colour = colours.Content2);
                return text;
            }
        }

        public partial class DifficultiesContainer : FillFlowContainer<DifficultySelectorButton>
        {
            public Action? OnLostHover;

            protected override void OnHoverLost(HoverLostEvent e)
            {
                base.OnHoverLost(e);
                OnLostHover?.Invoke();
            }
        }

        /// <summary>
        /// A difficulty: its mode's icon in its colour (20), faded unless under the mouse or chosen, with a pill in its colour under it when chosen.
        /// </summary>
        public partial class DifficultySelectorButton : OsuClickableContainer, IStateful<DifficultySelectorState>
        {
            private const float transition_duration = 150;
            private const float size = 20;

            private readonly DifficultyIcon icon;
            private readonly Circle indicator;

            public readonly APIBeatmap Beatmap;

            public Action<APIBeatmap>? OnHovered;
            public Action<APIBeatmap>? OnClicked;
            public event Action<DifficultySelectorState>? StateChanged;

            private DifficultySelectorState state = DifficultySelectorState.NotSelected;

            public DifficultySelectorState State
            {
                get => state;
                set
                {
                    if (value == state) return;

                    state = value;

                    StateChanged?.Invoke(State);

                    if (IsLoaded)
                        updateState();
                }
            }

            public DifficultySelectorButton(APIBeatmap beatmapInfo, IRulesetInfo? ruleset)
            {
                Beatmap = beatmapInfo;
                Size = new Vector2(size);

                Children = new Drawable[]
                {
                    icon = new DifficultyIcon(beatmapInfo, ruleset)
                    {
                        TooltipType = DifficultyIconTooltipType.None,
                        Size = new Vector2(size),
                        Alpha = 0.85f,
                    },
                };

                // Outside the button's content (which clips), under it.
                AddInternal(indicator = new Circle
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.TopCentre,
                    RelativeSizeAxes = Axes.X,
                    Height = 8,
                    Y = 5,
                    Alpha = 0,
                    Scale = new Vector2(0.3f, 1),
                });
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                indicator.Colour = colours.ForStarDifficulty(Beatmap.StarRating);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                updateState();
                FinishTransforms(true);
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateState();
                OnHovered?.Invoke(Beatmap);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateState();
                base.OnHoverLost(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                OnClicked?.Invoke(Beatmap);
                return base.OnClick(e);
            }

            private void updateState()
            {
                bool selected = State == DifficultySelectorState.Selected;

                icon.FadeTo(selected || IsHovered ? 1 : 0.85f, transition_duration);
                indicator.FadeTo(selected ? 1 : 0, 250, Easing.OutQuint);
                indicator.ScaleTo(new Vector2(selected ? 1 : 0.3f, 1), 250, Easing.OutQuint);
            }
        }

        public enum DifficultySelectorState
        {
            Selected,
            NotSelected,
        }
    }
}
