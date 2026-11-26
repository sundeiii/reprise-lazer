// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Development;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Online;
using osu.Game.Online.API;

namespace osu.Game.Utils
{
    /// <summary>
    /// Reprise: sends the errors players hit to the server they play on (<c>POST /web/client-error</c>), where staff
    /// see them in the admin panel (Game errors). Only release builds, never on osu!'s own servers; each kind of error once a session, at most
    /// <see cref="max_reports_per_session"/>, in the background. No beatmaps, settings or files: the exception, the
    /// game's message, the version and the platform (and who, from the player's token, when signed in).
    /// <para>
    /// Crashes (the game giving up on an unhandled exception) are also written to <c>crashes/</c> in the game's storage,
    /// with the last lines of the log, before the game closes. They are sent right away when the server answers within a
    /// few seconds, and otherwise on the next launch; the newest <see cref="max_crash_files"/> files stay on disk either way.
    /// </para>
    /// </summary>
    public class GameErrorReporter : IDisposable
    {
        public const string CLIENT = @"lazer";

        private const int max_reports_per_session = 20;

        // Caught errors are noisier; they get their own cap so they can't use up the unhandled ones'.
        private const int max_handled_reports_per_session = 20;
        private const int max_stack_trace = 8 * 1024;

        // The server takes 32 KB a report: the stack trace (8 KB) and this much of the log fit with room to spare.
        private const int max_log_tail = 6 * 1024;
        private const int log_tail_lines = 200;
        private const int max_crash_files = 10;
        private static readonly TimeSpan crash_send_timeout = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan max_pending_age = TimeSpan.FromDays(14);

        private const string crash_directory = @"crashes";
        private const string pending_suffix = @".json";
        private const string sent_suffix = @".sent.json";

        private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        private readonly OsuGame game;
        private readonly Storage? storage;
        private readonly HashSet<string> sent = new HashSet<string>();
        private readonly HashSet<string> sentHandled = new HashSet<string>();
        private readonly Queue<string> logTail = new Queue<string>();
        private readonly bool allowed;
        private int crashed;

        /// <summary>A random id for this launch, so a crash and the errors before it can be told apart from other launches.</summary>
        public readonly string Session = Guid.NewGuid().ToString(@"N")[..12];

        public GameErrorReporter(OsuGame game, Storage? storage = null)
        {
            this.game = game;
            this.storage = storage;

            allowed = game.IsDeployedBuild
                      && !DebugUtils.IsNUnitRunning
                      && Environment.GetEnvironmentVariable("OSU_DISABLE_ERROR_REPORTING") != "1";

            if (allowed)
            {
                Logger.NewEntry += processLogEntry;
                // An exception no thread catches outside the game's own threads ends the process even when the game
                // would have let it go: it is a crash too.
                AppDomain.CurrentDomain.UnhandledException += onUnhandledException;
            }
        }

        private void onUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            if (args.IsTerminating && args.ExceptionObject is Exception exception)
                ReportCrash(exception);
        }

        /// <summary>
        /// Whether errors are sent now (a release build, not on osu!'s own servers, which have Sentry). The error notification
        /// only says it was reported when this is on.
        /// </summary>
        public bool Enabled => allowed && !game.CreateEndpoints().WebsiteUrl.EndsWith(@".ppy.sh", StringComparison.Ordinal);

        private void processLogEntry(LogEntry entry)
        {
            rememberLogLine(entry);

            var exception = entry.Exception;

            if (exception == null || exception is SentryOnlyDiagnosticsException || !Enabled || !SentryLogger.ShouldSubmitException(exception))
                return;

            string type = exception.GetType().FullName ?? exception.GetType().Name;

            // "error": an exception nothing caught, which the game let pass (or an unobserved task's). "handled": one
            // the game's own code caught and logged, and carried on.
            bool unhandled = entry.Message == @"An unhandled error has occurred." || entry.Message == @"An unobserved error has occurred.";
            var seen = unhandled ? sent : sentHandled;
            int cap = unhandled ? max_reports_per_session : max_handled_reports_per_session;

            lock (seen)
            {
                if (seen.Count >= cap || !seen.Add($"{type}\n{entry.Message}\n{exception.StackTrace?.Split('\n')[0]}"))
                    return;
            }

            var report = createReport(unhandled ? @"error" : @"handled", exception, entry.Message, null);
            string? token = game.Dependencies.Get<IAPIProvider>().AccessToken;

            Task.Run(async () =>
            {
                try
                {
                    await sendAsync(report, token, CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Not logged as an error: that would be reported in turn. The error itself is in the log already.
                }
            });
        }

        private void rememberLogLine(LogEntry entry)
        {
            string line = $"{DateTime.UtcNow:HH:mm:ss.fff} [{entry.Target?.ToString().ToLowerInvariant() ?? entry.LoggerName}] {entry.Level.ToString().ToLowerInvariant()}: {entry.Message}";
            if (line.Length > 1024)
                line = line[..1024];

            lock (logTail)
            {
                logTail.Enqueue(line);
                while (logTail.Count > log_tail_lines)
                    logTail.Dequeue();
            }
        }

        private string currentLogTail()
        {
            string tail;
            lock (logTail)
                tail = string.Join('\n', logTail);

            return tail.Length <= max_log_tail ? tail : tail[^max_log_tail..];
        }

        private ErrorReport createReport(string kind, Exception exception, string? context, string? logTailText)
        {
            string stackTrace = exception.ToString();
            if (stackTrace.Length > max_stack_trace)
                stackTrace = stackTrace[..max_stack_trace];

            return new ErrorReport
            {
                Client = CLIENT,
                Kind = kind,
                Version = game.Version,
                Platform = $"{RuntimeInfo.OS} {Environment.OSVersion.Version}",
                ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
                Message = exception.Message,
                StackTrace = stackTrace,
                Context = context,
                OccurredAt = DateTime.UtcNow.ToString(@"O"),
                Session = Session,
                LogTail = logTailText,
            };
        }

        /// <summary>
        /// The game is closing on <paramref name="exception"/> (called by <see cref="OsuGameBase"/> when it stops ignoring
        /// unhandled exceptions). Writes the crash file and gives the server a few seconds to take it. Never throws.
        /// </summary>
        public void ReportCrash(Exception exception)
        {
            if (!allowed || Interlocked.Exchange(ref crashed, 1) == 1)
                return;

            try
            {
                var report = createReport(@"crash", exception, @"The game closed on this error.", currentLogTail());
                string? path = writeCrashFile(report);

                if (!Enabled)
                    return;

                string? token = null;

                try
                {
                    token = game.Dependencies.Get<IAPIProvider>().AccessToken;
                }
                catch
                {
                    // the API may already be gone this late
                }

                // The game thread is going down anyway: waiting here is what lets the report out before the process ends.
                using var cts = new CancellationTokenSource(crash_send_timeout);
                var send = Task.Run(() => sendAsync(report, token, cts.Token));
                bool ok = send.Wait(crash_send_timeout + TimeSpan.FromMilliseconds(500)) && send.GetResultSafely();

                if (ok && path != null)
                    markSent(path);
            }
            catch
            {
                // a crash report must never make the crash worse
            }
        }

        /// <summary>Sends the crash files a previous launch couldn't (call once the server is known, after loading).</summary>
        public void SendPendingCrashes()
        {
            if (!Enabled || storage == null)
                return;

            string? token = game.Dependencies.Get<IAPIProvider>().AccessToken;

            Task.Run(async () =>
            {
                try
                {
                    // let the game finish starting (and the player sign in) first
                    await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                    token = game.Dependencies.Get<IAPIProvider>().AccessToken ?? token;

                    foreach (string path in crashFiles().Where(p => !p.EndsWith(sent_suffix, StringComparison.Ordinal)).Take(max_crash_files))
                    {
                        if (File.GetLastWriteTimeUtc(path) < DateTime.UtcNow - max_pending_age)
                        {
                            markSent(path); // too old to matter; kept for the player, not sent
                            continue;
                        }

                        var report = JsonConvert.DeserializeObject<ErrorReport>(await File.ReadAllTextAsync(path).ConfigureAwait(false));
                        if (report == null)
                            continue;

                        if (await sendAsync(report, token, CancellationToken.None).ConfigureAwait(false))
                            markSent(path);
                    }
                }
                catch
                {
                    // tried again next launch
                }
            });
        }

        private IEnumerable<string> crashFiles()
        {
            if (storage == null)
                return Array.Empty<string>();

            string dir = storage.GetFullPath(crash_directory);
            if (!Directory.Exists(dir))
                return Array.Empty<string>();

            return Directory.GetFiles(dir, @"*" + pending_suffix).OrderByDescending(File.GetLastWriteTimeUtc).ToArray();
        }

        private string? writeCrashFile(ErrorReport report)
        {
            if (storage == null)
                return null;

            try
            {
                string dir = storage.GetFullPath(crash_directory);
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Session}{pending_suffix}");
                File.WriteAllText(path, JsonConvert.SerializeObject(report, Formatting.Indented));

                foreach (string old in crashFiles().Skip(max_crash_files))
                    File.Delete(old);

                return path;
            }
            catch
            {
                return null;
            }
        }

        private static void markSent(string path)
        {
            try
            {
                if (!path.EndsWith(sent_suffix, StringComparison.Ordinal))
                    File.Move(path, path[..^pending_suffix.Length] + sent_suffix, true);
            }
            catch
            {
                // sent again next launch; the server counts it into the same row
            }
        }

        /// <summary>Whether the server answered for good (taken, or refused: a 400 or 429 is dropped, not retried).</summary>
        private async Task<bool> sendAsync(ErrorReport report, string? token, CancellationToken cancellationToken)
        {
            string url = $"{game.CreateEndpoints().WebsiteUrl}/web/client-error";

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonConvert.SerializeObject(report), Encoding.UTF8, @"application/json"),
            };
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue(@"Bearer", token);

            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return (int)response.StatusCode < 500;
        }

        public void Dispose()
        {
            if (allowed)
            {
                Logger.NewEntry -= processLogEntry;
                AppDomain.CurrentDomain.UnhandledException -= onUnhandledException;
            }
        }

        /// <summary>The body of <c>POST /web/client-error</c> (and a crash file).</summary>
        private class ErrorReport
        {
            [JsonProperty(@"client")]
            public string Client { get; set; } = CLIENT;

            [JsonProperty(@"kind")]
            public string Kind { get; set; } = @"error";

            [JsonProperty(@"version")]
            public string? Version { get; set; }

            [JsonProperty(@"platform")]
            public string? Platform { get; set; }

            [JsonProperty(@"exception_type")]
            public string? ExceptionType { get; set; }

            [JsonProperty(@"message")]
            public string? Message { get; set; }

            [JsonProperty(@"stack_trace")]
            public string? StackTrace { get; set; }

            [JsonProperty(@"context")]
            public string? Context { get; set; }

            [JsonProperty(@"occurred_at")]
            public string? OccurredAt { get; set; }

            [JsonProperty(@"session")]
            public string? Session { get; set; }

            [JsonProperty(@"log_tail", NullValueHandling = NullValueHandling.Ignore)]
            public string? LogTail { get; set; }
        }
    }
}
