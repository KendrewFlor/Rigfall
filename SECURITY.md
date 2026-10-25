# Security policy

## Reporting a vulnerability

Please use GitHub's **private vulnerability reporting** (Security tab → "Report a vulnerability"). Don't open a public issue. You'll get an acknowledgement within 7 days.

## What Rigfall is and isn't

Rigfall is a single-player Unity game published as a static WebGL build on GitHub Pages. It has **no server, no accounts, no network features, no payments and no third-party code**. Progress is saved in the browser (Unity's PlayerPrefs, stored in IndexedDB).

## Protections in this repository

| Threat | Control |
|---|---|
| Malicious or compromised package | Only Unity's own packages (`com.unity.ai.navigation`, `com.unity.ugui` and built-in modules), pinned to exact versions. The scanner rejects anything else: other package names, git/file/URL sources and scoped registries. |
| Native or JavaScript plugins in the build | None. The scanner rejects `.dll`, `.so`, `.dylib`, `.jslib`, `.jspre`, `.aar`, `.jar` and executables anywhere in the repo. Everything in the game is built from the C# and data in this repository. |
| Tampered art or audio | Models, music and sound effects are generated from Python source in the repo; CI checks the committed meshes match their recipes. Binary files are only allowed in three folders (fonts, audio, doc images). |
| Network calls hidden in game code | The scanner bans `UnityWebRequest`, `WWW`, `System.Net`, sockets, `HttpClient`, `Application.OpenURL`, remote URLs, process spawning, `DllImport` and dynamic assembly loading in C#, and network, subprocess and `eval`/`exec`/`pickle` use in the Python tools. |
| Compromised GitHub Action | Every action is pinned to a full commit SHA; Dependabot proposes reviewed bumps. Workflows use a read-only token by default and `persist-credentials: false`, and never use `pull_request_target`. |
| Leaked Unity licence | The licence only lives in repository secrets. `.ulf`/`.alf` files are gitignored and rejected by the scanner, as is licence XML content. The Unity build workflow runs only on pushes to `main` and manual dispatch, never for pull requests, so fork PRs can't reach the secrets. |
| Tampered save data | Save files are untrusted input: size-capped, parsed with `JsonUtility`, then sanitised (unknown ids dropped, numbers clamped, building paths re-validated against the rules) before use. |
| Prompt injection against AI coding agents | No agent instruction files are committed. The scanner fails on agent config files (`CLAUDE.md`, `AGENTS.md`, `.mcp.json`, `.claude/` ...), hidden or bidirectional Unicode, HTML comments in Markdown and common injection phrases. |
| Leaked secrets | The scanner checks for common key formats (Anthropic, OpenAI, GitHub, AWS, Google, Slack, private keys, Unity licences). |
| AI tools listed as contributors | `.githooks/commit-msg` strips AI co-author trailers; the scanner fails if one is in history. |

Run every check locally with `python3 tools/security_scan.py` (Python 3.10+, nothing to install). Details: [docs/THREAT_MODEL.md](docs/THREAT_MODEL.md).

## Recommended GitHub settings for the maintainer

These live in repository settings, not in code, so they have to be switched on by hand:

1. Turn on **two-factor authentication** for your GitHub account.
2. Settings → Code security: turn on **secret scanning**, **push protection**, **Dependabot alerts** and **private vulnerability reporting**.
3. Settings → Branches: protect `main`: require pull requests, require the `CI / check` status, block force pushes.
4. Settings → Actions → General: set workflow permissions to **read repository contents**, and require approval for workflows from outside contributors.
5. Settings → Environments → `github-pages`: limit deployments to the `main` branch.
6. Settings → Pages: source **GitHub Actions**.
