# Threat model

## What we protect

1. **People who play the game**: the WebGL build must not run code from anywhere but this repository, contact other servers, or open other pages.
2. **People who clone the repository**: cloning, opening it in Unity or an AI coding tool, or running its scripts must not execute anything unexpected.
3. **The maintainer's secrets**: the Unity licence and account in the repository secrets, and the GitHub token used by workflows.
4. **Players' saved progress** in their own browser.

## Assets and trust boundaries

| Boundary | Trusted side | Untrusted side |
|---|---|---|
| Repository | Reviewed commits on `main` | Pull requests from forks, issue and PR text |
| Build | This repository's source, Unity's own packages at pinned versions, GameCI's builder at a pinned commit | Anything downloaded at build time beyond those |
| Browser | The deployed build | Saved data in IndexedDB (a player, an extension or another tab may edit it) |
| AI tooling | The maintainer's own local configuration | Text inside the repository that an agent might read as instructions |

## Threats and responses

### Supply chain

- **Package substitution or typosquatting.** The manifest may only contain `com.unity.*` packages at exact versions from Unity's registry; the scanner enforces it.
- **Malicious plugin in Assets.** Native and JavaScript plugin file types are rejected wherever they appear. The Unity project has no `Plugins` folder.
- **A compromised GitHub Action or builder image.** Actions are pinned to commit SHAs, so a moved tag can't change what runs. GameCI's builder downloads the Unity Editor Docker image (`unityci/editor`) for the version in `ProjectSettings/ProjectVersion.txt`; that image is a residual trust in GameCI and Docker Hub. The build job only holds the Unity secrets and a read-only token; publishing happens in a separate job with only `pages: write` and `id-token: write`.
- **Python tooling.** The tools that CI runs use only the Python standard library. Regenerating audio and previews needs numpy and Pillow, installed by the developer on their own machine, not in CI.

### Secrets

- The Unity licence is a secret, not a file. `*.ulf` is gitignored, and the scanner rejects licence files and licence XML.
- The Unity build workflow never runs for pull requests (`on: push` to `main` and `workflow_dispatch` only), so code from a fork can't run with the secrets. The scanner fails if a workflow that uses Unity secrets gains a `pull_request` trigger.

### Game code

- **Network and escape hatches.** The scanner bans network APIs, `Application.OpenURL`, process spawning, native interop and dynamic code loading in C#. Rigfall needs none of them.
- **Save tampering.** A modified save can at worst give a player unlocks in their own browser. It can't crash the game: loads are size-limited, wrapped in exception handling and sanitised field by field. Building paths are re-checked against the upgrade rules; unknown ids are dropped.
- **Data files.** Game data is JSON parsed with `JsonUtility` into plain classes; nothing in the data is executed. `tools/validate_data.py` cross-checks every reference.

### Repository content and AI agents

- **Prompt injection.** Coding agents read files automatically. The repository contains no agent configuration (`CLAUDE.md`, `AGENTS.md`, `.mcp.json`, `.claude/`, `.cursor/` ...); they're gitignored and the scanner fails if one is committed. It also rejects hidden or bidirectional Unicode (Trojan Source), HTML comments in Markdown (invisible on GitHub, visible to agents) and common injection phrases.
- **Contributor attribution.** A commit hook removes AI co-author trailers, and the scanner fails if any reach the history.

## Out of scope / accepted risks

- A player cheating in their own single-player game by editing their save.
- Trust in Unity itself, its package registry, GameCI's Docker images and GitHub's infrastructure.
- Browser extensions that inject into any page.
