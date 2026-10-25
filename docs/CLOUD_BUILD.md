# Building Rigfall in the cloud (no Unity Editor on your computer)

Rigfall is a Unity 6 project, but you don't have to install the Unity Editor to build or play it. GitHub Actions builds it with [GameCI](https://game.ci) (a Docker image with Unity inside) and publishes the WebGL build to GitHub Pages.

Unity still requires a licence for every build, including cloud builds. A free **Unity Personal** licence is enough. Getting the licence file is the only step that needs something from Unity on your computer: **Unity Hub**, the small launcher, not the Editor.

## 1. Put the code on GitHub

1. Create an empty public repository called `rigfall` on GitHub.
2. Push this folder to it (`main` branch).
3. **Settings → Pages → Build and deployment → Source: GitHub Actions.**

The **CI** workflow runs straight away (security scan, data checks, Python and C# tests). The **Unity WebGL build and GitHub Pages** workflow also starts, sees that the Unity secrets aren't there yet, and stops with a notice. That's expected.

## 2. Get your Unity Personal licence file (one time)

These steps follow GameCI's activation guide (https://game.ci/docs/github/activation).

1. Create a free Unity account at unity.com if you don't have one.
2. Install **Unity Hub** (you can skip installing any Editor when it offers).
3. Sign in to Unity Hub with that account.
4. Unity Hub → **Preferences → Licenses → Add → Get a free personal license**, and finish the steps. GameCI warns that a licence can appear in the Hub without the file being written, so do this step even if the Hub already shows a licence.
5. Find the licence file. These folders can be hidden, so turn on "show hidden files":
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`

The licence isn't tied to an operating system, so a file from Windows or macOS works for the Linux build machine.

**Keep this file private.** Never commit it. `.gitignore` excludes `*.ulf`, and the security scanner fails the build if one is ever added.

You can uninstall Unity Hub afterwards if you like. If the licence stops working later (Unity Personal licences are periodically renewed), repeat this step and update the secret.

## 3. Add the three secrets

GitHub → your repository → **Settings → Secrets and variables → Actions → New repository secret**:

| Name | Value |
|---|---|
| `UNITY_LICENSE` | The whole contents of `Unity_lic.ulf` (open it in a text editor, copy everything) |
| `UNITY_EMAIL` | The email address of your Unity account |
| `UNITY_PASSWORD` | Your Unity account password |

GameCI uses these only inside the build step to activate Unity, and returns the licence seat when the build finishes. If your Unity account uses two-factor authentication and activation fails, check the GameCI docs for the current guidance.

If you have Unity Plus or Pro instead, GameCI uses a `UNITY_SERIAL` secret in place of `UNITY_LICENSE`. See the activation guide.

## 4. Build and publish

**Actions → Unity WebGL build and GitHub Pages → Run workflow.** It also runs on every push to `main`.

What happens:

1. The repository checks run first (security scan, data validation, model check). A failure stops the build.
2. GameCI starts the Unity 6000.0.31f1 WebGL image, opens the project, and calls `Rigfall.EditorTools.BuildScript.BuildWebGL`. That creates the empty bootstrap scene, sets the player options (gzip with decompression fallback, which GitHub Pages needs) and builds.
3. The build is uploaded as a Pages artifact and deployed.

The first build takes roughly 20 to 40 minutes because Unity imports every asset from scratch. The game then lives at `https://<your-user>.github.io/rigfall/`.

## Troubleshooting

| Symptom | Fix |
|---|---|
| "Unity build skipped" notice | One of the three secrets is missing or misspelled. |
| Activation or licence error | The `.ulf` is incomplete (copy the whole file), expired, or from a different account than `UNITY_EMAIL`. Make a fresh one in Unity Hub. |
| "No space left on device" | The workflow frees disk space before building. If it still runs out, re-run the job. |
| Page loads but stays on the Unity logo | Wait a little longer on the first visit (the browser decompresses the game), then hard-refresh. |
| Compile errors in the build log | The C# hasn't been compiled by a local Unity Editor yet (see the note below). Open an issue with the log, or paste the errors into your assistant. |

## A note on what's been verified

The C# game rules in `Assets/Rigfall/Scripts/Core` are covered by unit tests that CI runs with the .NET SDK. The Unity-side scripts (`Runtime/`, `Editor/`) can only be compiled by Unity itself, so the first cloud build is also their first compile. If it reports errors, they will be small API mismatches. Fix them, push, and the next build picks them up.

## Building on your own computer instead (optional)

If you ever do install Unity 6000.0.31f1 (with WebGL Build Support), open the folder in Unity Hub, then use the menu **Rigfall → Build WebGL** or press Play. The game needs no scene setup: `Bootstrap.cs` builds everything at start-up.
