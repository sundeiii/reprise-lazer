// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Skinning;

namespace osu.Game.Screens.Play.HUD.Counters
{
    /// <summary>
    /// Reprise: what a play's pp counters show, live (as tosu gives its overlays): its pp so far, its pp if full combo'd (as tosu has it: its 100s and 50s
    /// so far kept, its misses and slider breaks and the rest of the map 300s), its 100s, 50s, misses and slider breaks, its grade, and how far into the map it is.
    /// </summary>
    public partial class LivePlayStats : Component
    {
        /// <summary>The play's pp so far (as the game's own pp counter has it).</summary>
        public readonly BindableInt PerformancePoints = new BindableInt();

        /// <summary>The play's pp if it were a full combo, as tosu has it: its 100s and 50s so far kept, its misses, slider breaks and the rest of the map 300s.</summary>
        public readonly BindableInt FullComboPerformancePoints = new BindableInt();

        public readonly BindableInt Oks = new BindableInt();
        public readonly BindableInt Mehs = new BindableInt();
        public readonly BindableInt Misses = new BindableInt();

        /// <summary>Combo breaks that weren't misses (a slider's tick, repeat or end let go of).</summary>
        public readonly BindableInt SliderBreaks = new BindableInt();

        public readonly Bindable<ScoreRank> Rank = new Bindable<ScoreRank>(ScoreRank.X);

        /// <summary>How far into the song the play is: its time over the song's whole length (tosu's <c>time.current / time.mp3</c>).</summary>
        public readonly BindableDouble SongProgress = new BindableDouble();

        /// <summary>How far into the map the play is: its time over its last note's end (tosu's <c>time.current / time.full</c>).</summary>
        public readonly BindableDouble MapProgress = new BindableDouble();

        /// <summary>Whether the first note's been reached.</summary>
        public readonly BindableBool Started = new BindableBool();

        [Resolved]
        private ScoreProcessor? scoreProcessor { get; set; }

        [Resolved]
        private GameplayState? gameplayState { get; set; }

        [Resolved]
        private IGameplayClock? gameplayClock { get; set; }

        [Resolved]
        private IFrameStableClock? frameStableClock { get; set; }

        [Resolved]
        private IBindable<WorkingBeatmap>? working { get; set; }

        private readonly CancellationTokenSource loadCancellation = new CancellationTokenSource();
        private List<TimedDifficultyAttributes>? timedAttributes;
        private PerformanceCalculator? performanceCalculator;
        private ScoreInfo? scoreInfo;
        private JudgementResult? lastJudgement;
        private double firstHitTime;
        private double lastHitTime;

        [BackgroundDependencyLoader]
        private void load(BeatmapDifficultyCache difficultyCache)
        {
            if (gameplayState == null)
                return;

            var mods = gameplayState.Mods.Select(m => m.DeepClone()).ToArray();
            performanceCalculator = gameplayState.Ruleset.CreatePerformanceCalculator();
            scoreInfo = new ScoreInfo(gameplayState.Score.ScoreInfo.BeatmapInfo, gameplayState.Score.ScoreInfo.Ruleset) { Mods = mods };
            (firstHitTime, lastHitTime) = BeatmapExtensions.CalculatePlayableBounds(gameplayState.Beatmap.HitObjects);

            // Reprise: with the approach rate override on, the map being played isn't the one the pp is worked out from.
            IWorkingBeatmap forPerformance = GameplayOverrides.ApproachRateEnabled && working?.Value != null
                ? working.Value
                : new GameplayWorkingBeatmap(gameplayState.Beatmap);
            difficultyCache.GetTimedDifficultyAttributesAsync(forPerformance, gameplayState.Ruleset, mods, loadCancellation.Token)
                           .ContinueWith(task => Schedule(() =>
                           {
                               timedAttributes = task.GetResultSafely();
                               if (lastJudgement != null)
                                   onJudgementChanged(lastJudgement);
                           }), TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (scoreProcessor == null)
                return;

            scoreProcessor.Rank.BindValueChanged(rank => Rank.Value = rank.NewValue, true);
            scoreProcessor.NewJudgement += onJudgementChanged;
            scoreProcessor.JudgementReverted += onJudgementChanged;

            if (gameplayState?.LastJudgementResult.Value != null)
                onJudgementChanged(gameplayState.LastJudgementResult.Value);
        }

        protected override void Update()
        {
            base.Update();

            IClock? clock = (IClock?)frameStableClock ?? gameplayClock;
            if (clock == null || lastHitTime <= 0)
                return;

            double time = clock.CurrentTime;
            double songLength = working?.Value?.Track.Length > 0 ? working.Value.Track.Length : lastHitTime;

            SongProgress.Value = Math.Clamp(time / songLength, 0, 1);
            MapProgress.Value = Math.Clamp(time / lastHitTime, 0, 1);
            Started.Value = time >= firstHitTime;
        }

        private void onJudgementChanged(JudgementResult judgement)
        {
            lastJudgement = judgement;

            if (scoreProcessor == null)
                return;

            var statistics = scoreProcessor.Statistics;
            Oks.Value = statistics.GetValueOrDefault(HitResult.Ok);
            Mehs.Value = statistics.GetValueOrDefault(HitResult.Meh);
            Misses.Value = statistics.GetValueOrDefault(HitResult.Miss);
            SliderBreaks.Value = statistics.Where(s => s.Key != HitResult.Miss && s.Key.BreaksCombo()).Sum(s => s.Value);

            if (timedAttributes == null || timedAttributes.Count == 0 || performanceCalculator == null || scoreInfo == null)
                return;

            scoreProcessor.PopulateScore(scoreInfo);
            PerformancePoints.Value = round(performanceCalculator.Calculate(scoreInfo, attributesAt(judgement.HitObject.GetEndTime())).Total);
            FullComboPerformancePoints.Value = round(performanceCalculator.Calculate(fullCombo(scoreInfo, timedAttributes[^1].Attributes), timedAttributes[^1].Attributes).Total);
        }

        // The play as a full combo, as tosu works it out (its calculateFcScore): the 100s and 50s (and the like) played so far kept as they
        // are, every miss and slider break a 300, every note still to come a 300, and every slider tick and end hit.
        private ScoreInfo fullCombo(ScoreInfo score, DifficultyAttributes attributes)
        {
            var maximum = score.MaximumStatistics;
            var played = score.Statistics;
            var result = new Dictionary<HitResult, int>(maximum);

            var basic = maximum.Where(s => s.Key.IsBasic() && s.Value > 0).Select(s => s.Key).ToList();
            HitResult best = basic.DefaultIfEmpty(HitResult.Great).Max();
            int totalNotes = basic.Sum(r => maximum[r]);

            int lesser = 0;

            foreach (var (type, count) in played.Where(s => s.Key.IsBasic() && s.Key.IsHit() && s.Key != best && s.Value > 0))
            {
                result[type] = count;
                lesser += count;
            }

            result[best] = Math.Max(0, totalNotes - lesser);

            foreach (var miss in result.Keys.Where(k => k.IsMiss()).ToList())
                result[miss] = 0;

            // Small ticks (as droplets) don't break combo: those missed stay missed.
            int smallTickMisses = played.GetValueOrDefault(HitResult.SmallTickMiss);

            if (smallTickMisses > 0 && maximum.ContainsKey(HitResult.SmallTickHit))
            {
                result[HitResult.SmallTickHit] = Math.Max(0, maximum[HitResult.SmallTickHit] - smallTickMisses);
                result[HitResult.SmallTickMiss] = smallTickMisses;
            }

            var fc = score.DeepClone();
            fc.Statistics = result;
            fc.MaximumStatistics = maximum;
            fc.MaxCombo = attributes.MaxCombo;

            if (scoreProcessor != null)
                fc.Accuracy = StandardisedScoreMigrationTools.ComputeAccuracy(result, maximum, scoreProcessor);

            return fc;
        }

        private DifficultyAttributes attributesAt(double time)
        {
            int index = timedAttributes!.BinarySearch(new TimedDifficultyAttributes(time, null!));
            if (index < 0)
                index = ~index - 1;

            return timedAttributes[Math.Clamp(index, 0, timedAttributes.Count - 1)].Attributes;
        }

        private static int round(double pp) => (int)Math.Round(pp, MidpointRounding.AwayFromZero);

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (scoreProcessor != null)
            {
                scoreProcessor.NewJudgement -= onJudgementChanged;
                scoreProcessor.JudgementReverted -= onJudgementChanged;
            }

            loadCancellation.Cancel();
        }

        private class GameplayWorkingBeatmap : WorkingBeatmap
        {
            private readonly IBeatmap gameplayBeatmap;

            public GameplayWorkingBeatmap(IBeatmap gameplayBeatmap)
                : base(gameplayBeatmap.BeatmapInfo, null)
            {
                this.gameplayBeatmap = gameplayBeatmap;
            }

            public override IBeatmap GetPlayableBeatmap(osu.Game.Rulesets.IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken cancellationToken) => gameplayBeatmap;

            protected override IBeatmap GetBeatmap() => gameplayBeatmap;

            public override Texture GetBackground() => throw new NotImplementedException();

            protected override Track GetBeatmapTrack() => throw new NotImplementedException();

            protected internal override ISkin GetSkin() => throw new NotImplementedException();

            public override Stream GetStream(string storagePath) => throw new NotImplementedException();
        }
    }
}
