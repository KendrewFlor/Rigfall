# Contributing

Thanks for helping! A few ground rules keep this repository safe to clone and easy to review.

## Setup

You need Python 3.10+ and, for the C# rule tests, the .NET 8 SDK. The Unity Editor is optional: the game is built in the cloud (see [docs/CLOUD_BUILD.md](docs/CLOUD_BUILD.md)).

```sh
git config core.hooksPath .githooks          # strips AI co-author trailers from your commits
python3 tools/security_scan.py
python3 tools/validate_data.py
python3 -m unittest discover -s tests/python
dotnet run --project tests/CoreTests
```

All of these must pass before you open a pull request (CI runs the same commands).

## Where things live

| You want to change | Edit | Then run |
|---|---|---|
| Balance, enemies, perks, mutators, weapons, buildings, achievements | `Assets/Rigfall/Resources/Data/*.json` | `tools/validate_data.py` |
| A field's layout | `tools/make_maps.py` | `python3 tools/make_maps.py` |
| A model | `blender/rigfall_models.py` | `python3 blender/build_models.py`, then `tools/render_previews.py` to look |
| Music or a sound effect | `tools/synth_audio.py` | `python3 tools/synth_audio.py --only <name>` |
| Game rules (waves, economy, wheel, unlocks) | `Assets/Rigfall/Scripts/Core` | add a test in `tests/CoreTests/CoreRulesTests.cs` |
| Behaviour in the world, UI | `Assets/Rigfall/Scripts/Runtime` | build in Unity or through the cloud build |

## Rules

- **Only Unity's own packages**, pinned to exact versions. No Asset Store packages, git packages, scoped registries or plugins (`.dll`, `.jslib` ...).
- **No network access** in the game, and no subprocesses or network in the Python tools. The scanner enforces both.
- **Core stays plain C#.** `Assets/Rigfall/Scripts/Core` must not use `UnityEngine`, so the rules stay testable.
- **Generated files are regenerated, not hand-edited**: `maps.json`, `Resources/Models/*.json`, `Resources/Audio/*.wav`. CI checks maps and models against their scripts.
- **No AI agent config files** in commits (`CLAUDE.md`, `AGENTS.md`, `.mcp.json`, `.claude/` ...). Keep them local; they're gitignored. See [docs/AI_TOOLING.md](docs/AI_TOOLING.md).
- **Never commit a Unity licence file** (`*.ulf`). It belongs in the `UNITY_LICENSE` repository secret.
