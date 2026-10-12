// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Sentry;

namespace osu.Desktop
{
    public static class SentryConfig
    {
        private static IDisposable? sentry;

        public static bool IsEnabled => sentry != null;

        public static void Initialise()
        {
            string? dsn = Environment.GetEnvironmentVariable("RINARI_SENTRY_DSN");

            if (string.IsNullOrWhiteSpace(dsn))
                dsn = getBuildTimeDsn();

            if (string.IsNullOrWhiteSpace(dsn))
                return;

            sentry = SentrySdk.Init(options =>
            {
                options.Dsn = dsn;

#if DEBUG
                options.Environment = "development";
                options.Debug = true;
#else
                options.Environment = "production";
#endif

                options.Release = getRelease();
                options.SendDefaultPii = false;

                // Keep this off for now. We only want crash/error reports first.
                options.TracesSampleRate = 0.0;

                options.SetBeforeSend((sentryEvent, hint) =>
                {
                    // Avoid sending obviously sensitive machine/server info.
                    sentryEvent.ServerName = null;
                    return sentryEvent;
                });
            });

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                    CaptureException(ex, "AppDomain.UnhandledException");
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                CaptureException(args.Exception, "TaskScheduler.UnobservedTaskException");
                args.SetObserved();
            };
        }

        public static void CaptureException(Exception exception, string source)
        {
            if (!IsEnabled)
                return;

            SentrySdk.ConfigureScope(scope =>
            {
                scope.SetTag("source", source);
                scope.SetTag("app", "reprise-lazer");
            });

            SentrySdk.CaptureException(exception);
        }

        public static void Shutdown()
        {
            if (sentry == null)
                return;

            SentrySdk.FlushAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();

            sentry.Dispose();
            sentry = null;
        }

        private static string? getBuildTimeDsn()
        {
            return typeof(SentryConfig).Assembly
                                       .GetCustomAttributes<AssemblyMetadataAttribute>()
                                       .FirstOrDefault(a => a.Key == "RinariSentryDsn")
                                       ?.Value;
        }

        private static string getRelease()
        {
            Version? version = typeof(SentryConfig).Assembly.GetName().Version;

            return version == null
                ? "reprise-lazer@unknown"
                : $"reprise-lazer@{version}";
        }
    }
}