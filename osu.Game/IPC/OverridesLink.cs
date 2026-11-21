// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Configuration;

namespace osu.Game.IPC
{
    /// <summary>
    /// Hosts the local pipe a companion app connects to, and applies the setup it sends to <see cref="GameplayOverrides"/>.
    /// Changes wait while a play holds the state; everything is turned off when the companion goes away.
    /// </summary>
    public partial class OverridesLink : Component
    {
        public const string PIPE_NAME = "reprise-lazer-overrides";

        private const int protocol_version = 1;

        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly object sync = new object();

        private Setup? pending;
        private bool toldHeld;
        private StreamWriter? writer;

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Task.Run(() => run(cancellation.Token));
        }

        protected override void Update()
        {
            base.Update();

            Setup next;

            lock (sync)
            {
                if (pending == null || GameplayOverrides.Held)
                    return;

                next = pending;
                pending = null;
            }

            apply(next);

            lock (sync)
            {
                if (toldHeld)
                {
                    toldHeld = false;
                    send(new { type = "ack" });
                }
            }
        }

        private static void apply(Setup s)
        {
            GameplayOverrides.Relax = s.Relax;
            GameplayOverrides.RelaxOffset = Math.Clamp(s.RelaxOffset, -12, 12);
            GameplayOverrides.RelaxJitter = Math.Clamp(s.RelaxJitter, 0, 10);
            GameplayOverrides.RemoveHidden = s.RemoveHidden;
            GameplayOverrides.TimewarpRate.Value = s.TimewarpRate;
            GameplayOverrides.TimewarpEnabled = s.TimewarpEnabled;
            GameplayOverrides.ApproachRate = Math.Clamp(s.ApproachRate, -1, 11);
            GameplayOverrides.ApproachRateEnabled = s.ApproachRateEnabled;
            GameplayOverrides.AimAssist = s.AimAssist;
            GameplayOverrides.AimCorrectionStrength.Value = s.AimCorrectionStrength;
            GameplayOverrides.AimCorrectionRelative = s.AimCorrectionRelative;
        }

        private async Task run(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await using var pipe = new NamedPipeServerStream(PIPE_NAME, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                    await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
                    Logger.Log("Overrides companion connected");
                    await session(pipe, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
                {
                    Logger.Log($"Overrides companion link: {e.Message}");
                }
                finally
                {
                    lock (sync)
                    {
                        if (writer != null)
                        {
                            writer = null;
                            pending = Setup.OFF;
                            toldHeld = false;
                            Logger.Log("Overrides companion disconnected, overrides off");
                        }
                    }
                }

                try
                {
                    await Task.Delay(500, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private async Task session(NamedPipeServerStream pipe, CancellationToken token)
        {
            var encoding = new UTF8Encoding(false);
            using var reader = new StreamReader(pipe, encoding, false, 4096, true);
            var sessionWriter = new StreamWriter(pipe, encoding, 4096, true) { AutoFlush = true, NewLine = "\n" };

            using (var handshakeTimeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                handshakeTimeout.CancelAfter(TimeSpan.FromSeconds(5));

                string? line = await reader.ReadLineAsync(handshakeTimeout.Token).ConfigureAwait(false);
                if (line == null)
                    return;

                using var hello = JsonDocument.Parse(line);
                var root = hello.RootElement;

                if (str(root, "type") != "hello" || str(root, "name") != "reprise-overrides")
                    return;

                if (num(root, "protocol", 0) != protocol_version)
                {
                    await sessionWriter.WriteLineAsync(JsonSerializer.Serialize(new { type = "error", reason = $"Reprise Lazer speaks protocol {protocol_version}" }))
                                       .ConfigureAwait(false);
                    return;
                }
            }

            lock (sync)
            {
                writer = sessionWriter;
                send(new { type = "welcome", protocol = protocol_version, name = "reprise-lazer", version = game.Version });
            }

            while (await reader.ReadLineAsync(token).ConfigureAwait(false) is string line)
            {
                using var message = JsonDocument.Parse(line);
                var root = message.RootElement;

                if (str(root, "type") != "settings" || !root.TryGetProperty("settings", out var settings) || settings.ValueKind != JsonValueKind.Object)
                    continue;

                var setup = Setup.From(settings);

                lock (sync)
                {
                    pending = setup;

                    if (GameplayOverrides.Held)
                    {
                        toldHeld = true;
                        send(new { type = "held" });
                    }
                    else
                        send(new { type = "ack" });
                }
            }
        }

        private void send(object message)
        {
            try
            {
                writer?.WriteLine(JsonSerializer.Serialize(message));
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException)
            {
                writer = null;
            }
        }

        private static string? str(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static double num(JsonElement e, string name, double fallback) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : fallback;

        private static bool flag(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

        private sealed record Setup(
            bool Relax, double RelaxOffset, double RelaxJitter,
            bool RemoveHidden,
            bool TimewarpEnabled, double TimewarpRate,
            bool ApproachRateEnabled, double ApproachRate,
            bool AimAssist, int AimCorrectionStrength, bool AimCorrectionRelative)
        {
            public static readonly Setup OFF = new Setup(false, 0, 0, false, false, 1, false, 9, false, 30, false);

            public static Setup From(JsonElement s) => new Setup(
                flag(s, "relax"), num(s, "relax_offset", 0), num(s, "relax_jitter", 0),
                flag(s, "remove_hidden"),
                flag(s, "timewarp_enabled"), num(s, "timewarp_rate", 1),
                flag(s, "approach_rate_enabled"), num(s, "approach_rate", 9),
                flag(s, "aim_assist"), (int)Math.Round(num(s, "aim_correction_strength", 30)), flag(s, "aim_correction_relative"));
        }

        protected override void Dispose(bool isDisposing)
        {
            cancellation.Cancel();
            base.Dispose(isDisposing);
        }
    }
}
