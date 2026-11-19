// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays.Settings.Sections;

namespace osu.Game.Tests.Visual.Settings
{
    /// <summary>
    /// Reprise: the skin settings' "Import skins from osu!lazer" list.
    /// </summary>
    public partial class TestSceneImportLazerSkins : OsuManualInputManagerTestScene
    {
        [Test]
        public void TestOpen()
        {
            ImportLazerSkinsButton button = null!;

            AddStep("create button", () => Child = new PopoverContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = button = new ImportLazerSkinsButton
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Width = 400,
                },
            });

            AddStep("open", () => button.TriggerClick());
            AddUntilStep("list says what it found", () =>
            {
                var popover = this.ChildrenOfType<ImportLazerSkinsPopover>().SingleOrDefault();
                var status = popover?.ChildrenOfType<OsuTextFlowContainer>().FirstOrDefault();
                string text = string.Concat(status?.ChildrenOfType<SpriteText>().Select(s => s.Text.ToString()) ?? []);
                return text.Length > 0 && !text.StartsWith("Reading", System.StringComparison.Ordinal);
            });
        }
    }
}
