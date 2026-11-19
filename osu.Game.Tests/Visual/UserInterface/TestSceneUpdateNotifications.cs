// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Updater;
using osuTK.Input;

namespace osu.Game.Tests.Visual.UserInterface
{
    /// <summary>
    /// Reprise: the update notifications as a player meets them, clicked with the mouse: the notes showing by themselves after an
    /// update, "see what's new", and "update ready" (notes then restart; restarting straight away without notes). The notes come in on
    /// another thread a moment later, as GitHub's do (which is what broke: a hidden overlay never ran what it scheduled then).
    /// </summary>
    public partial class TestSceneUpdateNotifications : OsuManualInputManagerTestScene
    {
        private NotificationOverlay notifications = null!;
        private TestWhatsNewOverlay whatsNew = null!;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            notifications = new NotificationOverlay { Anchor = Anchor.TopRight, Origin = Anchor.TopRight };
            whatsNew = new TestWhatsNewOverlay { Notes = "## What's new\n\n- something new" };

            // As the game provides them to the notifications.
            Child = new DependencyProvidingContainer
            {
                RelativeSizeAxes = Axes.Both,
                CachedDependencies = new (Type, object)[]
                {
                    (typeof(INotificationOverlay), notifications),
                    (typeof(WhatsNewOverlay), whatsNew),
                },
                Children = new Drawable[] { whatsNew, notifications },
            };
        });

        [Test]
        public void TestNotesShowByThemselvesAfterUpdate()
        {
            // OsuGame does this at the first start of a new version.
            AddStep("game started after an update", () => whatsNew.ShowFor("v2026.1119.3-lazer"));
            AddUntilStep("notes shown", () => whatsNew.State.Value == Visibility.Visible);
            AddAssert("restart button hidden", () => restartButton.Parent!.Alpha == 0);
        }

        [Test]
        public void TestClickSeeWhatsNew()
        {
            AddStep("post \"now running\"", () => notifications.Post(new UpdateCompleteNotification("v2026.1119.3-lazer")));
            clickNotification<UpdateCompleteNotification>();
            AddUntilStep("notes shown", () => whatsNew.State.Value == Visibility.Visible);
        }

        [Test]
        public void TestClickUpdateReady()
        {
            bool restarted = false;

            postUpdateReady(() => restarted = true);
            clickNotification<UpdateManager.UpdateReadyNotification>();

            AddUntilStep("notes shown", () => whatsNew.State.Value == Visibility.Visible);
            AddAssert("not restarted yet", () => !restarted);
            AddStep("click restart", () =>
            {
                InputManager.MoveMouseTo(restartButton);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("restarted", () => restarted);
        }

        [Test]
        public void TestClickUpdateReadyWithoutNotes()
        {
            bool restarted = false;

            AddStep("notes can't be had", () => whatsNew.Fails = true);
            postUpdateReady(() => restarted = true);
            clickNotification<UpdateManager.UpdateReadyNotification>();
            AddUntilStep("restarted straight away", () => restarted);
        }

        [Test]
        public void TestClickUpdateReadyWithEmptyNotes()
        {
            bool restarted = false;

            AddStep("notes empty", () => whatsNew.Notes = "**Full Changelog**: https://x");
            postUpdateReady(() => restarted = true);
            clickNotification<UpdateManager.UpdateReadyNotification>();
            AddUntilStep("restarted straight away", () => restarted);
        }

        private RoundedButton restartButton => whatsNew.ChildrenOfType<RoundedButton>().Single(b => b.Text.ToString() == "Restart to update");

        // As VelopackUpdateManager posts it: downloading, then completed.
        private void postUpdateReady(Action restart)
        {
            UpdateManager.UpdateDownloadProgressNotification progress = null!;

            AddStep("post download", () => notifications.Post(progress = new UpdateManager.UpdateDownloadProgressNotification(CancellationToken.None)
            {
                CompletionClickAction = UpdateManager.ShowNotesThenRestart(whatsNew, "v2026.1119.3-lazer", restart),
            }));
            AddStep("download done", () =>
            {
                progress.StartDownload();
                progress.State = ProgressNotificationState.Completed;
            });
        }

        private void clickNotification<T>() where T : Notification
        {
            AddStep("open notifications", () => notifications.Show());
            AddUntilStep("notification there", () => notifications.ChildrenOfType<T>().Any(n => n.IsPresent));
            AddWaitStep("let it settle", 5);
            AddStep("click it", () =>
            {
                var notification = notifications.ChildrenOfType<T>().First(n => n.IsPresent);
                InputManager.MoveMouseTo(notification);
                InputManager.Click(MouseButton.Left);
            });
        }

        private partial class TestWhatsNewOverlay : WhatsNewOverlay
        {
            public string Notes = string.Empty;
            public bool Fails;

            // From another thread a moment later, as GitHub's come.
            public int Fetches;

            protected override void FetchNotes(string version, Action<string?> onNotes, Action<Exception> onFailed)
            {
                Fetches++;

                string notes = Notes;
                bool fails = Fails;

                Task.Run(async () =>
                {
                    await Task.Delay(200).ConfigureAwait(false);

                    if (fails)
                        onFailed(new Exception("offline"));
                    else
                        onNotes(notes);
                });
            }
        }
    }
}
