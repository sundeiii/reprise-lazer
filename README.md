<p align="center">
  <img width="500" alt="osu! logo" src="assets/lazer.png">
</p>

# Reprise Lazer

The osu!(lazer) client for [reprise.pw](https://osu.reprise.pw), an unofficial osu! private server. It's
[ppy/osu](https://github.com/ppy/osu) with reprise's changes on top, and it is not affiliated with ppy Pty Ltd.

## Download

| [Windows 10+ (x64)](https://github.com/sundeiii/reprise-lazer/releases/latest/download/Reprise-Lazer-Setup.exe) | macOS 12+ ([Intel](https://github.com/sundeiii/reprise-lazer/releases/latest/download/Reprise-Lazer-macOS-Intel.zip), [Apple Silicon](https://github.com/sundeiii/reprise-lazer/releases/latest/download/Reprise-Lazer-macOS-Apple-Silicon.zip)) | [Linux (x64)](https://github.com/sundeiii/reprise-lazer/releases/latest/download/Reprise-Lazer.AppImage) |
| --- | --- | --- |

Windows without installing: the [portable zip](https://github.com/sundeiii/reprise-lazer/releases/latest/download/Reprise-Lazer-win-x64-portable.zip).
These links always give the latest release; every release is on the [releases page](https://github.com/sundeiii/reprise-lazer/releases).
Once installed, the game keeps itself up to date.

Reprise Lazer is its own app next to osu!(lazer): it keeps its files in its own folder (`osu-reprise`), so it doesn't
touch an existing lazer install.

## What's different from osu!(lazer)

- **Plays on reprise.pw**: the website, the game server and multiplayer are reprise's.
- **Relax and Autopilot modes**: a VN / RX / AP picker in the toolbar. With Relax or Autopilot on, the leaderboards
  (song select, in game, results) and your rank and pp are that mode's, as on the website. Relax and Autopilot are
  ranked and score in full (1x), and the star rating and pp are what the play would be worth without them.
- **Ranked mods**: Double Time and Nightcore at any speed, and Difficulty Adjust, are ranked.
- **osu!stable's look, as a setting**: with a legacy skin, the results screen, song select, the main menu and parts of
  the HUD can look like osu!stable's (Settings > Skin).
- **Session stats**: your plays this session (accuracy, unstable rate, hit errors) on the results screen, with an
  attempt counter.
- **osu-web's beatmap listing and beatmap page** layouts.
- No gameplay assistance of any kind.

Crash reports go to reprise's own Sentry. Problems or ideas: [open an issue](https://github.com/sundeiii/reprise-lazer/issues).

## Developing a custom ruleset

osu! is designed to allow user-created gameplay variations, called "rulesets". Building one of these allows a developer to harness the power of the osu! beatmap library, game engine, and general UX for a new style of gameplay. To get started working on a ruleset, we have some templates available [here](https://github.com/ppy/osu/tree/master/Templates).

You can see some examples of custom rulesets by visiting the [custom ruleset directory](https://github.com/ppy/osu/discussions/13096).

## Developing osu!

### Prerequisites

Please make sure you have the following prerequisites:

- A desktop platform with the correct [.NET SDK](https://dotnet.microsoft.com/download) installed.

When working with the codebase, we recommend using an IDE with intelligent code completion and syntax highlighting, such as the latest version of [Visual Studio](https://visualstudio.microsoft.com/vs/), [JetBrains Rider](https://www.jetbrains.com/rider/), or [Visual Studio Code](https://code.visualstudio.com/) with the [EditorConfig](https://marketplace.visualstudio.com/items?itemName=EditorConfig.EditorConfig) and [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) plugin installed.

### Downloading the source code

Clone the repository:

```shell
git clone https://github.com/sundeiii/reprise-lazer
cd reprise-lazer
```

To update the source code to the latest commit, run the following command inside the `osu` directory:

```shell
git pull
```

### Building

#### From an IDE

You should load the solution via one of the platform-specific `.slnf` files, rather than the main `.sln`. This will reduce dependencies and hide platforms that you don't care about. Valid `.slnf` files are:

- `osu.Desktop.slnf` (most common)
- `osu.Android.slnf`
- `osu.iOS.slnf`

Run configurations for the recommended IDEs (listed above) are included. You should use the provided Build/Run functionality of your IDE to get things going. When testing or building new components, it's highly encouraged you use the `osu! (Tests)` project/configuration. More information on this is provided [below](#contributing).

To build for mobile platforms, you will likely need to run `sudo dotnet workload restore` if you haven't done so previously. This will install Android/iOS tooling required to complete the build.

#### From CLI

You can also build and run *osu!* from the command-line with a single command:

```shell
dotnet run --project osu.Desktop
```

When running locally to do any kind of performance testing, make sure to add `-c Release` to the build command, as the overhead of running with the default `Debug` configuration can be large (especially when testing with local framework modifications as below).

If the build fails, try to restore NuGet packages with `dotnet restore`.

### Testing with resource/framework modifications

Sometimes it may be necessary to cross-test changes in [osu-resources](https://github.com/ppy/osu-resources) or [osu-framework](https://github.com/ppy/osu-framework). This can be quickly achieved using included commands:

Windows:

```ps
UseLocalFramework.ps1
UseLocalResources.ps1
```

macOS / Linux:

```ps
UseLocalFramework.sh
UseLocalResources.sh
```

Note that these commands assume you have the relevant project(s) checked out in adjacent directories:

```
|- osu            // this repository
|- osu-framework
|- osu-resources
```

### Code analysis

Before committing your code, please run a code formatter. This can be achieved by running `dotnet format` in the command line, or using the `Format code` command in your IDE.

We have adopted some cross-platform, compiler integrated analyzers. They can provide warnings when you are editing, building inside IDE or from command line, as-if they are provided by the compiler itself.

JetBrains ReSharper InspectCode is also used for wider rule sets. You can run it from PowerShell with `.\InspectCode.ps1`. Alternatively, you can install ReSharper or use Rider to get inline support in your IDE of choice.

## Contributing

When it comes to contributing to the project, the two main things you can do to help out are reporting issues and submitting pull requests. Please refer to the [contributing guidelines](CONTRIBUTING.md) to understand how to help in the most effective way possible.

If you wish to help with localisation efforts, head over to [crowdin](https://crowdin.com/project/osu-web).

Our team believes in **human contributions**. Any contribution – be it an issue report or a pull request – which is created by, documented by, or aided by AI/LLM usage will typically be **closed and locked without further discussion**.

## Licence

*osu!*'s code and framework are licensed under the [MIT licence](https://opensource.org/licenses/MIT). Please see [the licence file](LICENCE) for more information. [tl;dr](https://tldrlegal.com/license/mit-license) you can do whatever you want as long as you include the original copyright and license notice in any copy of the software/source.

Code modified or created by GooGuTeam remains under the MIT License.

Please note that this *does not cover* the usage of the "osu!" or "ppy" branding in any software, resources, advertising or promotion, as this is protected by trademark law.

Please also note that game resources are covered by a separate licence. Please see the [ppy/osu-resources](https://github.com/ppy/osu-resources) repository for clarifications.
