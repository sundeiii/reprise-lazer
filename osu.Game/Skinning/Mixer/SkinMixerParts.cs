// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// The parts of a skin below are OsuSkinMixer's (https://github.com/rednir/OsuSkinMixer, src/Models/SkinOptions/SkinOption.cs at
// 091fb50e91b89e2b28e45f3190b093192ba113ed), used under its licence:
//
// MIT License
//
// Copyright (c) 2025 rednir
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
// CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
// SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

namespace osu.Game.Skinning.Mixer
{
    /// <summary>Reprise: the parts of a skin the mixer offers, grouped as OsuSkinMixer groups them.</summary>
    public static class SkinMixerParts
    {
        /// <summary>A fresh tree of them (each with its own choice).</summary>
        public static SkinMixerOption[] Create() => new SkinMixerOption[]
        {
            new SkinMixerGroup
            {
                Name = "Interface",
                Children = new SkinMixerOption[]
                {
                    new SkinMixerGroup
                    {
                        Name = "Main menu",
                        Children = new SkinMixerOption[]
                        {
                            new SkinMixerFileOption("welcome_text", false, "Welcome text (req. supporter)"),
                            new SkinMixerFileOption("menu-background", false, "Background image (req. supporter)"),
                            new SkinMixerFileOption("menu-snow", false, "Snow"),
    
                            new SkinMixerIniProperty("Colours", "MenuGlow"),
                        },
                    },
                    new SkinMixerGroup
                    {
                        Name = "Song select",
                        Children = new SkinMixerOption[]
                        {
                            new SkinMixerGroup
                            {
                                Name = "Ranking icons",
                                Children = new SkinMixerOption[]
                                {
                                    new SkinMixerFileOption("ranking-d-small", false, "D rank"),
                                    new SkinMixerFileOption("ranking-c-small", false, "C rank"),
                                    new SkinMixerFileOption("ranking-b-small", false, "B rank"),
                                    new SkinMixerFileOption("ranking-a-small", false, "A rank"),
                                    new SkinMixerFileOption("ranking-s-small", false, "S rank"),
                                    new SkinMixerFileOption("ranking-sh-small", false, "S rank (hidden)"),
                                    new SkinMixerFileOption("ranking-x-small", false, "SS rank"),
                                    new SkinMixerFileOption("ranking-xh-small", false, "SS rank (hidden)"),
                                },
                            },
                            new SkinMixerGroup
                            {
                                Name = "Button",
                                Children = new SkinMixerOption[]
                                {
                                    new SkinMixerFileOption("button-left", false),
                                    new SkinMixerFileOption("button-middle", false),
                                    new SkinMixerFileOption("button-right", false),
                                },
                            },
                            new SkinMixerGroup
                            {
                                Name = "Bottom bar buttons",
                                Children = new SkinMixerOption[]
                                {
                                    new SkinMixerFileOption("selection-mode*", false, allowedSuffixes: new[] { string.Empty, "-over" }),
                                    new SkinMixerFileOption("selection-mods*", false, allowedSuffixes: new[] { string.Empty, "-over" }),
                                    new SkinMixerFileOption("selection-options*", false, allowedSuffixes: new[] { string.Empty, "-over" }),
                                    new SkinMixerFileOption("selection-random*", false, allowedSuffixes: new[] { string.Empty, "-over" }),
                                }
                            },
                            new SkinMixerGroup
                            {
                                Name = "Gamemode icons (background)",
                                Children = new SkinMixerOption[]
                                {
                                    new SkinMixerFileOption("mode-osu", false),
                                    new SkinMixerFileOption("mode-taiko", false),
                                    new SkinMixerFileOption("mode-fruits", false),
                                    new SkinMixerFileOption("mode-mania", false),
                                },
                            },
                            new SkinMixerGroup
                            {
                                Name = "Gamemode icons (selection menu)",
                                Children = new SkinMixerOption[]
                                {
                                    new SkinMixerFileOption("mode-osu-med", false),
                                    new SkinMixerFileOption("mode-taiko-med", false),
                                    new SkinMixerFileOption("mode-fruits-med", false),
                                    new SkinMixerFileOption("mode-mania-med", false),
                                },
                            },
                            new SkinMixerGroup
                            {
                                Name = "Gamemode icons (bottom bar)",
                                Children = new SkinMixerOption[]
                                {
                                    new SkinMixerFileOption("mode-osu-small", false),
                                    new SkinMixerFileOption("mode-taiko-small", false),
                                    new SkinMixerFileOption("mode-fruits-small", false),
                                    new SkinMixerFileOption("mode-mania-small", false),
                                },
                            },
    
                            new SkinMixerFileOption("menu-back", false, "Back button", true),
                            new SkinMixerFileOption("menu-button-background", false, "Beatmap/scoreboard item"),
                            new SkinMixerFileOption("options-offset-tick", false, "Offset wizard tick"),
                            new SkinMixerFileOption("selection-tab", false, "Tab"),
                            new SkinMixerFileOption("star", false, "Difficulty star"),
                            new SkinMixerFileOption("songselect-bottom", false),
                            new SkinMixerFileOption("songselect-top", false),
    
                            new SkinMixerIniProperty("Colours", "SongSelectActiveText"),
                            new SkinMixerIniProperty("Colours", "SongSelectInactiveText"),
                        },
                    },
                    new SkinMixerGroup
                    {
                        Name = "Mod icons",
                        Children = new SkinMixerOption[]
                        {
                            new SkinMixerFileOption("selection-mod-autoplay", false, "Autoplay"),
                            new SkinMixerFileOption("selection-mod-cinema", false, "Cinema"),
                            new SkinMixerFileOption("selection-mod-doubletime", false, "Double Time"),
                            new SkinMixerFileOption("selection-mod-easy", false, "Easy"),
                            new SkinMixerFileOption("selection-mod-fadein", false, "Fade-in"),
                            new SkinMixerFileOption("selection-mod-flashlight", false, "Flashlight"),
                            new SkinMixerFileOption("selection-mod-halftime", false, "Half Time"),
                            new SkinMixerFileOption("selection-mod-hardrock", false, "Hard Rock"),
                            new SkinMixerFileOption("selection-mod-hidden", false, "Hidden"),
                            new SkinMixerFileOption("selection-mod-key1", false, "1K"),
                            new SkinMixerFileOption("selection-mod-key2", false, "2K"),
                            new SkinMixerFileOption("selection-mod-key3", false, "3K"),
                            new SkinMixerFileOption("selection-mod-key4", false, "4K"),
                            new SkinMixerFileOption("selection-mod-key5", false, "5K"),
                            new SkinMixerFileOption("selection-mod-key6", false, "6K"),
                            new SkinMixerFileOption("selection-mod-key7", false, "7K"),
                            new SkinMixerFileOption("selection-mod-key8", false, "8K"),
                            new SkinMixerFileOption("selection-mod-key9", false, "9K"),
                            new SkinMixerFileOption("selection-mod-keycoop", false, "Co-op"),
                            new SkinMixerFileOption("selection-mod-mirror", false, "Mirror"),
                            new SkinMixerFileOption("selection-mod-nightcore", false, "Nightcore"),
                            new SkinMixerFileOption("selection-mod-nofail", false, "No Fail"),
                            new SkinMixerFileOption("selection-mod-perfect", false, "Perfect"),
                            new SkinMixerFileOption("selection-mod-random", false, "Random"),
                            new SkinMixerFileOption("selection-mod-relax", false, "Relax"),
                            new SkinMixerFileOption("selection-mod-relax2", false, "Autopilot"),
                            new SkinMixerFileOption("selection-mod-scorev2", false, "ScoreV2"),
                            new SkinMixerFileOption("selection-mod-spunout", false, "Spun Out"),
                            new SkinMixerFileOption("selection-mod-suddendeath", false, "Sudden Death"),
                            new SkinMixerFileOption("selection-mod-target", false, "Target Practice"),
                            new SkinMixerFileOption("selection-mod-touchdevice", false, "Touch Device"),
                        },
                    },
                    new SkinMixerGroup
                    {
                        Name = "Results screen",
                        Children = new SkinMixerOption[]
                        {
                            new SkinMixerFileOption("ranking-panel", false, "Ranking panel"),
                            new SkinMixerFileOption("ranking-title", false, "Ranking title"),
                            new SkinMixerFileOption("ranking-d", false, "D rank"),
                            new SkinMixerFileOption("ranking-c", false, "C rank"),
                            new SkinMixerFileOption("ranking-b", false, "B rank"),
                            new SkinMixerFileOption("ranking-a", false, "A rank"),
                            new SkinMixerFileOption("ranking-s", false, "S rank"),
                            new SkinMixerFileOption("ranking-sh", false, "S rank (hidden)"),
                            new SkinMixerFileOption("ranking-x", false, "SS rank"),
                            new SkinMixerFileOption("ranking-xh", false, "SS rank (hidden)"),
                            new SkinMixerFileOption("ranking-accuracy", false, "Accuracy title"),
                            new SkinMixerFileOption("ranking-maxcombo", false, "Max combo title"),
                            new SkinMixerFileOption("ranking-graph", false, "Peformance graph"),
                            new SkinMixerFileOption("ranking-perfect", false, "Perfect combo text"),
                            new SkinMixerFileOption("ranking-retry", false, "Retry button"),
                            new SkinMixerFileOption("ranking-replay", false, "Watch replay button"),
                            new SkinMixerFileOption("pause-replay", false, "Watch replay button"),
                            new SkinMixerFileOption("ranking-winner", false, "Multi winner"),
                        },
                    },
                    new SkinMixerGroup
                    {
                        Name = "Pause/Fail screen",
                        Children = new SkinMixerOption[]
                        {
                            new SkinMixerFileOption("pause-overlay", false, "Pause overlay"),
                            new SkinMixerFileOption("fail-background", false, "Fail background"),
                            new SkinMixerFileOption("pause-continue", false, "Continue button"),
                            new SkinMixerFileOption("pause-retry", false, "Retry button"),
                            new SkinMixerFileOption("pause-back", false, "Back button"),
                            new SkinMixerFileOption("arrow-pause", false, "Selection arrow"),
                        },
                    },
                    new SkinMixerGroup
                    {
                        Name = "In-game",
                        Children = new SkinMixerOption[]
                        {
                            new SkinMixerGroup
                            {
                                Name = "Health bar",
                                Children = new SkinMixerOption[]
                                {
                                    new SkinMixerFileOption("scorebar-bg", false, "Background"),
                                    new SkinMixerFileOption("scorebar-colour", false, "Fill", true),
                                    new SkinMixerFileOption("scorebar-ki", false, "Passing zone marker"),
                                    new SkinMixerFileOption("scorebar-kidanger", false, "Warning zone marker"),
                                    new SkinMixerFileOption("scorebar-kidanger2", false, "Critical zone marker"),
                                    new SkinMixerFileOption("scorebar-marker", false, "Marker (override)"),
                                },
                            },
                            new SkinMixerGroup
                            {
                                Name = "Countdown",
                                Children = new SkinMixerOption[]
                                {
                                    new SkinMixerFileOption("ready", false, "Ready"),
                                    new SkinMixerFileOption("count3", false, "3"),
                                    new SkinMixerFileOption("count2", false, "2"),
                                    new SkinMixerFileOption("count1", false, "1"),
                                    new SkinMixerFileOption("go", false, "Go"),
                                },
                            },
                            new SkinMixerGroup
                            {
                                Name = "Fonts",
                                Children = new SkinMixerOption[]
                                {
                                    new SkinMixerFileOption("scoreentry-*", false, "Score entry", false, allowedSuffixes: new[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "comma", "dot", "percent", "x" }),
                                    new SkinMixerFileOption("score-*", false, "Score/accuracy/combo", false, allowedSuffixes: new[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "comma", "dot", "percent", "x" }),
    
                                    new SkinMixerIniProperty("Fonts", "ScorePrefix"),
                                    new SkinMixerIniProperty("Fonts", "ScoreOverlap"),
                                    new SkinMixerIniProperty("Fonts", "ComboPrefix"),
                                    new SkinMixerIniProperty("Fonts", "ComboOverlap"),
                                }
                            },
                            new SkinMixerGroup
                            {
                                Name = "Key overlay",
                                Children = new SkinMixerOption[]
                                {
                                    new SkinMixerFileOption("inputoverlay-background", false, "Background"),
                                    new SkinMixerFileOption("inputoverlay-key", false, "Key"),
    
                                    new SkinMixerIniProperty("Colours", "InputOverlayText"),
                                },
                            },
    
                            new SkinMixerFileOption("play-skip", false, "Skip button", true),
                            new SkinMixerFileOption("section-fail", false, "Section pass"),
                            new SkinMixerFileOption("section-pass", false, "Section fail"),
                            new SkinMixerFileOption("play-warningarrow", false, "Warning/selection arrows"),
                            new SkinMixerFileOption("arrow-warning", false, "Warning arrows"),
                            new SkinMixerFileOption("play-unranked", false, "Unranked text"),
                            new SkinMixerFileOption("masking-border", false, "Masking border"),
                            new SkinMixerFileOption("spinner-rpm", false, "Spinner RPM"),
    
                            new SkinMixerIniProperty("General", "ComboBurstRandom"),
                        },
                    },
                },
            },
            new SkinMixerGroup
            {
                Name = "Gameplay",
                Children = new SkinMixerOption[]
                {
                    new SkinMixerGroup
                    {
                        Name = "osu!",
                        Children = new SkinMixerOption[]
                        {
                            new SkinMixerGroup
                            {
                                Name = "Hitcircles",
                                Children = new SkinMixerOption[]
                                {
                                    new SkinMixerFileOption("approachcircle", false, "Approach circle"),
                                    new SkinMixerFileOption("default-*", false, "Numbers", false, allowedSuffixes: new[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" }),
                                    new SkinMixerFileOption("hitcircle", false, "Hitcircle"),
                                    new SkinMixerFileOption("hitcircleoverlay", false, "Hitcircle overlay"),
                                    new SkinMixerFileOption("hitcircleselect", false, "Editor selection"),
                                    new SkinMixerFileOption("target*", false, "Target practice hitcircle"),
    
                                    new SkinMixerIniProperty("General", "HitCircleOverlayAboveNumber"),
                                    new SkinMixerIniProperty("Fonts", "HitCirclePrefix"),
                                    new SkinMixerIniProperty("Fonts", "HitCircleOverlap"),
                                },
                            },
                            new SkinMixerGroup
                            {
                                Name = "Sliders",
                                Children = new SkinMixerOption[]
                                {
                                    new SkinMixerFileOption("sliderb", false, "Slider ball", true),
                                    new SkinMixerFileOption("sliderendcircle*", false, "Slider end circle", false, allowedSuffixes: new[] { string.Empty, "overlay" }),
                                    new SkinMixerFileOption("sliderfollowcircle", false, "Slider follow circle"),
                                    new SkinMixerFileOption("sliderscorepoint", false, "Slider tick"),
                                    new SkinMixerFileOption("sliderstartcircle*", false, "Slider start circle", allowedSuffixes: new[] { string.Empty, "overlay" }),
                                    new SkinMixerFileOption("reversearrow", false, "Reverse arrow"),
                                    new SkinMixerFileOption("sliderpoint*", false, "Legacy slider point", false, allowedSuffixes: new[] { "10", "30" }),
    
                                    new SkinMixerIniProperty("General", "AllowSliderBallTint"),
                                    new SkinMixerIniProperty("General", "SliderBallFlip"),
                                    new SkinMixerIniProperty("Colours", "SliderBall"),
                                    new SkinMixerIniProperty("Colours", "SliderBorder"),
                                    new SkinMixerIniProperty("Colours", "SliderTrackOverride"),
                                },
                            },
                            new SkinMixerGroup
                            {
                                Name = "Combo Colours",
                                Children = new SkinMixerOption[]
                                {
                                    new SkinMixerIniProperty("Colours", "Combo1"),
                                    new SkinMixerIniProperty("Colours", "Combo2"),
                                    new SkinMixerIniProperty("Colours", "Combo3"),
                                    new SkinMixerIniProperty("Colours", "Combo4"),
                                    new SkinMixerIniProperty("Colours", "Combo5"),
                                    new SkinMixerIniProperty("Colours", "Combo6"),
                                    new SkinMixerIniProperty("Colours", "Combo7"),
                                    new SkinMixerIniProperty("Colours", "Combo8"),
                                },
                            },
                            new SkinMixerGroup
                            {
                                Name = "Spinners",
                                Children = new SkinMixerOption[]
                                {
                                    new SkinMixerFileOption("spinner-approachcircle", false),
                                    new SkinMixerFileOption("spinner-background", false),
                                    new SkinMixerFileOption("spinner-bottom", false),
                                    new SkinMixerFileOption("spinner-circle", false),
                                    new SkinMixerFileOption("spinner-clear", false),
                                    new SkinMixerFileOption("spinner-glow", false),
                                    new SkinMixerFileOption("spinner-metre", false),
                                    new SkinMixerFileOption("spinner-middle", false),
                                    new SkinMixerFileOption("spinner-middle2", false),
                                    new SkinMixerFileOption("spinner-osu", false),
                                    new SkinMixerFileOption("spinner-spin", false),
                                    new SkinMixerFileOption("spinner-top", false),
    
                                    new SkinMixerIniProperty("General", "SpinnerFadePlayfield"),
                                    new SkinMixerIniProperty("General", "SpinnerNoBlink"),
                                    new SkinMixerIniProperty("Colours", "SpinnerBackground"),
                                    new SkinMixerIniProperty("Colours", "StarBreakAdditive"),
                                },
                            },
                            new SkinMixerGroup
                            {
                                Name = "Hit judgements",
                                Children = new SkinMixerOption[]
                                {
                                    new SkinMixerGroup
                                    {
                                        Name = "50",
                                        Children = new SkinMixerOption[]
                                        {
                                            new SkinMixerFileOption("hit50", false, "Image", true),
                                            new SkinMixerFileOption("particle50", false, "Particles"),
                                        },
                                    },
                                    new SkinMixerGroup
                                    {
                                        Name = "100",
                                        Children = new SkinMixerOption[]
                                        {
                                            new SkinMixerFileOption("hit100", false, "Image", true),
                                            new SkinMixerFileOption("hit100k", false, "Image (katsu)", true),
                                            new SkinMixerFileOption("particle100", false, "Particles"),
                                        },
                                    },
                                    new SkinMixerGroup
                                    {
                                        Name = "300",
                                        Children = new SkinMixerOption[]
                                        {
                                            new SkinMixerFileOption("hit300", false, "Image", true),
                                            new SkinMixerFileOption("hit300k", false, "Image (katsu)", true),
                                            new SkinMixerFileOption("hit300g", false, "Image (geki)", true),
                                            new SkinMixerFileOption("particle300", false, "Particles"),
                                        },
                                    },
                                    new SkinMixerFileOption("hit0", false, "Miss", true),
                                },
                            },
                            new SkinMixerFileOption("followpoint", false, "Follow points", true),
                            new SkinMixerFileOption("comboburst", false, "Combo burst"),
                        },
                    },
                    new SkinMixerGroup
                    {
                        Name = "osu!taiko",
                        Children = new SkinMixerOption[]
                        {
                            new SkinMixerGroup()
                            {
                                Name = "Hit judgements",
                                Children = new SkinMixerOption[]
                                {
                                    new SkinMixerFileOption("taiko-hit0", false, "Miss"),
                                    new SkinMixerFileOption("taiko-hit100", false, "100"),
                                    new SkinMixerFileOption("taiko-hit100k", false, "100k"),
                                    new SkinMixerFileOption("taiko-hit300", false, "300"),
                                    new SkinMixerFileOption("taiko-hit300g", false, "300g"),
                                    new SkinMixerFileOption("taiko-hit300k", false, "300k"),
                                },
                            },
                            new SkinMixerGroup()
                            {
                                Name = "Pippidon",
                                Children = new SkinMixerOption[]
                                {
                                    new SkinMixerFileOption("pippidonclear", false),
                                    new SkinMixerFileOption("pippidonfail", false),
                                    new SkinMixerFileOption("pippidonfailidle", false),
                                    new SkinMixerFileOption("pippidonfailkiai", false),
                                },
                            },
                            new SkinMixerFileOption("spinner-warning", false),
                            new SkinMixerFileOption("taiko-bar-left", false),
                            new SkinMixerFileOption("taiko-bar-right-glow", false),
                            new SkinMixerFileOption("taiko-bar-right", false),
                            new SkinMixerFileOption("taiko-barline", false),
                            new SkinMixerFileOption("taiko-drum-inner", false),
                            new SkinMixerFileOption("taiko-drum-outer", false),
                            new SkinMixerFileOption("taiko-flower-group", false),
                            new SkinMixerFileOption("taiko-glow", false),
                            new SkinMixerFileOption("taiko-roll-end", false),
                            new SkinMixerFileOption("taiko-roll-middle", false),
                            new SkinMixerFileOption("taiko-slider-fail", false),
                            new SkinMixerFileOption("taiko-slider", false),
                            new SkinMixerFileOption("taikobigcircle*", false, allowedSuffixes: new[] { string.Empty, "overlay"}),
                            new SkinMixerFileOption("taikohitcircle*", false, allowedSuffixes: new[] { string.Empty, "overlay"}),
                        },
                    },
                    new SkinMixerGroup
                    {
                        Name = "osu!catch",
                        Children = new SkinMixerOption[]
                        {
                            new SkinMixerFileOption("comboburst-fruits", false),
                            new SkinMixerFileOption("fruit-apple*", false),
                            new SkinMixerFileOption("fruit-bananas*", false),
                            new SkinMixerFileOption("fruit-catcher-fail", false),
                            new SkinMixerFileOption("fruit-catcher-idle", false),
                            new SkinMixerFileOption("fruit-catcher-kiai", false),
                            new SkinMixerFileOption("fruit-drop*", false),
                            new SkinMixerFileOption("fruit-grapes*", false),
                            new SkinMixerFileOption("fruit-orange*", false),
                            new SkinMixerFileOption("fruit-pear*", false),
                            new SkinMixerFileOption("fruit-ryuuta", false),
    
                            new SkinMixerIniProperty("CatchTheBeat", "HyperDash"),
                            new SkinMixerIniProperty("CatchTheBeat", "HyperDashFruit"),
                            new SkinMixerIniProperty("CatchTheBeat", "HyperDashAfterImage"),
                        },
                    },
                    new SkinMixerGroup
                    {
                        Name = "osu!mania",
                        Children = new SkinMixerOption[]
                        {
                            new SkinMixerFileOption("comboburst-mania", false),
                            new SkinMixerFileOption("mania*", false),
                            new SkinMixerFileOption("lighting*", false),
    
                            new SkinMixerIniSection("Mania", "Keys", "1"),
                            new SkinMixerIniSection("Mania", "Keys", "2"),
                            new SkinMixerIniSection("Mania", "Keys", "3"),
                            new SkinMixerIniSection("Mania", "Keys", "4"),
                            new SkinMixerIniSection("Mania", "Keys", "5"),
                            new SkinMixerIniSection("Mania", "Keys", "6"),
                            new SkinMixerIniSection("Mania", "Keys", "7"),
                            new SkinMixerIniSection("Mania", "Keys", "8"),
                            new SkinMixerIniSection("Mania", "Keys", "9"),
                        },
                    },
                    new SkinMixerIniProperty("General", "AnimationFramerate"),
                }
            },
            new SkinMixerGroup
            {
                Name = "Cursor",
                Children = new SkinMixerOption[]
                {
                    new SkinMixerGroup
                    {
                        Name = "Head",
                        Children = new SkinMixerOption[]
                        {
                            new SkinMixerFileOption("cursor", false),
    
                            new SkinMixerIniProperty("General", "CursorCentre"),
                            new SkinMixerIniProperty("General", "CursorExpand"),
                            new SkinMixerIniProperty("General", "CursorRotate"),
                        },
                    },
                    new SkinMixerGroup
                    {
                        Name = "Trail",
                        Children = new SkinMixerOption[]
                        {
                            new SkinMixerFileOption("cursortrail", false),
                            new SkinMixerFileOption("cursormiddle", false),
                            new SkinMixerFileOption("star2", false),
    
                            new SkinMixerIniProperty("General", "CursorTrailRotate"),
                        },
                    },
                    new SkinMixerFileOption("cursor-smoke", false),
                },
            },
            new SkinMixerGroup
            {
                Name = "Hitsounds",
                PreviewFileNames = new string[] { "normal-hitnormal", "soft-hitnormal", "drum-hitnormal", "combobreak" },
                Children = new SkinMixerOption[]
                {
                    new SkinMixerGroup
                    {
                        Name = "Normal",
                        PreviewFileNames = new string[] { "normal-hitnormal", "normal-hitclap", "normal-hitwhistle", "normal-hitfinish" },
                        Children = new SkinMixerOption[]
                        {
                            new SkinMixerFileOption("normal-hitclap", true, "Clap"),
                            new SkinMixerFileOption("normal-hitfinish", true, "Finish"),
                            new SkinMixerFileOption("normal-hitnormal", true, "Normal"),
                            new SkinMixerFileOption("normal-hitwhistle", true, "Whistle"),
                            new SkinMixerFileOption("normal-sliderslide", true, "Slider slide"),
                            new SkinMixerFileOption("normal-slidertick", true, "Slider tick"),
                            new SkinMixerFileOption("normal-sliderwhistle", true, "Slider whistle"),
                        },
                    },
                    new SkinMixerGroup
                    {
                        Name = "Soft",
                        PreviewFileNames = new string[] { "soft-hitnormal", "soft-hitclap", "soft-hitwhistle", "soft-hitfinish" },
                        Children = new SkinMixerOption[]
                        {
                            new SkinMixerFileOption("soft-hitclap", true, "Clap"),
                            new SkinMixerFileOption("soft-hitfinish", true, "Finish"),
                            new SkinMixerFileOption("soft-hitnormal", true, "Normal"),
                            new SkinMixerFileOption("soft-hitwhistle", true, "Whistle"),
                            new SkinMixerFileOption("soft-sliderslide", true, "Slider slide"),
                            new SkinMixerFileOption("soft-slidertick", true, "Slider tick"),
                            new SkinMixerFileOption("soft-sliderwhistle", true, "Slider whistle"),
                        },
                    },
                    new SkinMixerGroup
                    {
                        Name = "Drum",
                        PreviewFileNames = new string[] { "drum-hitnormal", "drum-hitclap", "drum-hitwhistle", "drum-hitfinish" },
                        Children = new SkinMixerOption[]
                        {
                            new SkinMixerFileOption("drum-hitclap", true, "Clap"),
                            new SkinMixerFileOption("drum-hitfinish", true, "Finish"),
                            new SkinMixerFileOption("drum-hitnormal", true, "Normal"),
                            new SkinMixerFileOption("drum-hitwhistle", true, "Whistle"),
                            new SkinMixerFileOption("drum-sliderslide", true, "Slider slide"),
                            new SkinMixerFileOption("drum-slidertick", true, "Slider tick"),
                            new SkinMixerFileOption("drum-sliderwhistle", true, "Slider whistle"),
                        },
                    },
                    new SkinMixerGroup
                    {
                        Name = "Spinner",
                        PreviewFileNames = new string[] { "spinnerspin", "spinnerbonus" },
                        Children = new SkinMixerOption[]
                        {
                            new SkinMixerFileOption("spinnerbonus", true, "Bonus"),
                            new SkinMixerFileOption("spinnerspin", true, "Spin"),
                        },
                    },
                    new SkinMixerGroup
                    {
                        Name = "Nightcore beats",
                        PreviewFileNames = new string[] { "nightcore-clap", "nightcore-kick", "nightcore-hat", "nightcore-finish" },
                        Children = new SkinMixerOption[]
                        {
                            new SkinMixerFileOption("nightcore-clap", true, "Clap"),
                            new SkinMixerFileOption("nightcore-finish", true, "Finish"),
                            new SkinMixerFileOption("nightcore-hat", true, "Hat"),
                            new SkinMixerFileOption("nightcore-kick", true, "Kick"),
                        },
                    },
                    new SkinMixerGroup
                    {
                        Name = "osu!taiko specific",
                        PreviewFileNames = new string[] { "taiko-normal-hitnormal", "taiko-soft-hitnormal", "taiko-drum-hitnormal" },
                        Children = new SkinMixerOption[]
                        {
                            new SkinMixerGroup
                            {
                                Name = "Normal",
                                PreviewFileNames = new string[] { "taiko-normal-hitnormal", "taiko-normal-hitclap", "taiko-normal-hitwhistle", "taiko-normal-hitfinish" },
                                Children = new SkinMixerOption[]
                                {
                                    new SkinMixerFileOption("taiko-normal-hitnormal", true, "Normal"),
                                    new SkinMixerFileOption("taiko-normal-hitclap", true, "Clap"),
                                    new SkinMixerFileOption("taiko-normal-hitfinish", true, "Finish"),
                                    new SkinMixerFileOption("taiko-normal-hitwhistle", true, "Whistle"),
                                },
                            },
                            new SkinMixerGroup
                            {
                                Name = "Soft",
                                PreviewFileNames = new string[] { "taiko-soft-hitnormal", "taiko-soft-hitclap", "taiko-soft-hitwhistle", "taiko-soft-hitfinish" },
                                Children = new SkinMixerOption[]
                                {
                                    new SkinMixerFileOption("taiko-soft-hitnormal", true, "Normal"),
                                    new SkinMixerFileOption("taiko-soft-hitclap", true, "Clap"),
                                    new SkinMixerFileOption("taiko-soft-hitfinish", true, "Finish"),
                                    new SkinMixerFileOption("taiko-soft-hitwhistle", true, "Whistle"),
                                },
                            },
                            new SkinMixerGroup
                            {
                                Name = "Drum",
                                PreviewFileNames = new string[] { "taiko-drum-hitnormal", "taiko-drum-hitclap", "taiko-drum-hitwhistle", "taiko-drum-hitfinish" },
                                Children = new SkinMixerOption[]
                                {
                                    new SkinMixerFileOption("taiko-drum-hitnormal", true, "Normal"),
                                    new SkinMixerFileOption("taiko-drum-hitclap", true, "Clap"),
                                    new SkinMixerFileOption("taiko-drum-hitfinish", true, "Finish"),
                                    new SkinMixerFileOption("taiko-drum-hitwhistle", true, "Whistle"),
                                },
                            },
                        },
                    },
                    new SkinMixerFileOption("combobreak", true, "Combo break"),
                    new SkinMixerFileOption("comboburst", true, "Combo burst"),
    
                    new SkinMixerIniProperty("General", "CustomComboBurstSounds"),
                    new SkinMixerIniProperty("General", "LayeredHitSounds"),
                    new SkinMixerIniProperty("General", "SpinnerFrequencyModulate"),
                },
            },
            new SkinMixerGroup
            {
                Name = "Menu Sounds",
                Children = new SkinMixerOption[]
                {
                    new SkinMixerGroup
                    {
                        Name = "Interface",
                        Children = new SkinMixerOption[]
                        {
                            new SkinMixerFileOption("welcome", true, "Welcome to osu!"),
                            new SkinMixerFileOption("seeya", true, "See ya next time!"),
                            new SkinMixerFileOption("menu-play-*", true, "Menu play button", allowedSuffixes: new[] { "hover", "click" }),
                            new SkinMixerFileOption("menu-freeplay-*", true, "Menu solo button", allowedSuffixes: new[] { "hover", "click" }),
                            new SkinMixerFileOption("menu-multiplayer-*", true, "Menu multi button", allowedSuffixes: new[] { "hover", "click" }),
                            new SkinMixerFileOption("menu-charts-*", true, "Menu charts button", allowedSuffixes: new[] { "hover", "click" }),
                            new SkinMixerFileOption("menu-edit-*", true, "Menu edit button", allowedSuffixes: new[] { "hover", "click" }),
                            new SkinMixerFileOption("menu-options-*", true, "Menu options button", allowedSuffixes: new[] { "hover", "click" }),
                            new SkinMixerFileOption("menu-exit-*", true, "Menu exit button", allowedSuffixes: new[] { "hover", "click" }),
                            new SkinMixerFileOption("menu-direct-*", true, "Menu osu!direct button", allowedSuffixes: new[] { "hover", "click" }),
                            new SkinMixerFileOption("menu-back-*", true, "Menu back button", allowedSuffixes: new[] { "hover", "click" }),
                            new SkinMixerFileOption("applause", true, "Applause"),
                            new SkinMixerFileOption("back-button-click", true, "Back button click"),
                            new SkinMixerFileOption("back-button-hover", true, "Back button hover"),
                            new SkinMixerFileOption("check-off", true, "Check off"),
                            new SkinMixerFileOption("check-on", true, "Check on"),
                            new SkinMixerFileOption("click-close", true, "Close tab"),
                            new SkinMixerFileOption("click-short-confirm", true, "Short confirm"),
                            new SkinMixerFileOption("click-short", true, "Short hover"),
                            new SkinMixerFileOption("heartbeat", true, "Menu cookie heartbeat"),
                            new SkinMixerFileOption("key-confirm", true, "Chat message send"),
                            new SkinMixerFileOption("key-movement", true, "Text cursor move"),
                            new SkinMixerFileOption("key-delete", true, "Backspace"),
                            new SkinMixerFileOption("key-press-*", true, "Typing sounds", allowedSuffixes: new[] { "1", "2", "3", "4" }),
                            new SkinMixerFileOption("match-*", true, "Multiplayer", allowedSuffixes: new[] { "confirm", "join", "leave", "ready", "notready", "start" }),
                            new SkinMixerFileOption("menuback", true, "Menu back button (alt)"),
                            new SkinMixerFileOption("menuclick", true, "Difficulty hover"),
                            new SkinMixerFileOption("menuhit", true, "Scene switch"),
                            new SkinMixerFileOption("metronomelow", true, "Metronome"),
                            new SkinMixerFileOption("multi-skipped", true, "Multiplayer skip"),
                            new SkinMixerFileOption("select-expand", true, "Expand"),
                            new SkinMixerFileOption("select-difficulty", true, "Difficulty select"),
                            new SkinMixerFileOption("shutter", true, "Screenshot"),
                            new SkinMixerFileOption("sliderbar", true, "Slider bar"),
                        },
                    },
                    new SkinMixerGroup
                    {
                        Name = "In-game",
                        Children = new SkinMixerOption[]
                        {
                            new SkinMixerGroup
                            {
                                Name = "Countdown",
                                PreviewFileNames = new string[] { "readys", "count3s", "count2s", "count1s", "gos" },
                                Children = new SkinMixerOption[]
                                {
                                    new SkinMixerFileOption("readys", true, "Ready?"),
                                    new SkinMixerFileOption("count3s", true, "3"),
                                    new SkinMixerFileOption("count2s", true, "2"),
                                    new SkinMixerFileOption("count1s", true, "1"),
                                    new SkinMixerFileOption("gos", true, "Go!"),
                                },
                            },
                            new SkinMixerGroup
                            {
                                Name = "Pause/fail screen",
                                Children = new SkinMixerOption[]
                                {
                                    new SkinMixerFileOption("failsound", true, "Fail sound"),
                                    new SkinMixerFileOption("pause-loop", true, "Pause music"),
                                    new SkinMixerFileOption("pause-hover", true, "Button hover"),
                                    new SkinMixerFileOption("pause-continue-*", true, "Continue button", allowedSuffixes: new[] { "hover", "click" }),
                                    new SkinMixerFileOption("pause-retry-*", true, "Retry button", allowedSuffixes: new[] { "hover", "click" }),
                                    new SkinMixerFileOption("pause-back-*", true, "Back button", allowedSuffixes: new[] { "hover", "click" }),
                                }
                            },
                            new SkinMixerGroup
                            {
                                Name = "Section pass/fail",
                                PreviewFileNames = new string[] { "sectionpass", "sectionfail" },
                                Children = new SkinMixerOption[]
                                {
                                    new SkinMixerFileOption("sectionfail", true, "Section fail"),
                                    new SkinMixerFileOption("sectionpass", true, "Section pass"),
                                },
                            }
                        },
                    },
                },
            },
        };
    }
}
