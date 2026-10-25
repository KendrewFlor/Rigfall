"""Repository security scanner for Rigfall. Standard library only; runs in CI and locally.

    python3 tools/security_scan.py

Checks:
  1. Hidden text: bidi overrides, zero-width and tag characters (Trojan Source, hidden prompts).
  2. Prompt-injection bait aimed at AI coding agents, and HTML comments hidden in Markdown.
  3. AI agent config files that coding tools load silently (CLAUDE.md, AGENTS.md, .mcp.json, ...).
  4. Secrets: API keys, tokens, private keys and Unity licence files (.ulf/.alf).
  5. Supply chain: Unity packages only from Unity's registry (no git/file/scoped registries),
     no native plugins, no .jslib/.jspre JavaScript plugins, pinned GitHub Actions with
     least-privilege permissions and no pull_request_target.
  6. Game code hygiene: no network, process, reflection-emit or native-interop APIs in the
     C# game code; the pure Core layer never touches UnityEngine; the Python tools and
     Blender scripts never open network connections or spawn processes.
  7. AI co-author trailers in git history (in a git checkout).
"""
from __future__ import annotations

import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SELF = "tools/security_scan.py"
IGNORE_DIRS = {".git", "Library", "Temp", "Logs", "obj", "bin", "Build", "build", "Builds", "UserSettings", "__pycache__", ".venv"}
TEXT_EXT = {".cs", ".py", ".json", ".md", ".yml", ".yaml", ".txt", ".shader", ".cginc", ".hlsl", ".csproj", ".xml",
            ".asmdef", ".editorconfig", ".gitignore", ".gitattributes", ".sh", ".toml", ".cfg", ".ini", ""}
BINARY_OK = {".png": ("docs/",), ".wav": ("Assets/Rigfall/Resources/Audio/",), ".ttf": ("Assets/Rigfall/Resources/Fonts/",)}

findings: list[tuple[str, int, str, str]] = []


def fail(file: str, line: int, rule: str, detail: str) -> None:
    findings.append((file, line, rule, detail))


def list_files() -> list[str]:
    """In a git checkout scan what would be published (tracked + untracked-not-ignored)."""
    if (ROOT / ".git").exists():
        try:
            out = subprocess.run(["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"], cwd=ROOT,
                                 capture_output=True, check=True).stdout.decode("utf-8")
            return sorted(f for f in out.split("\0") if f and (ROOT / f).is_file())
        except (OSError, subprocess.CalledProcessError):
            pass
    files = []
    for p in ROOT.rglob("*"):
        if p.is_file() and not any(part in IGNORE_DIRS for part in p.relative_to(ROOT).parts):
            files.append(p.relative_to(ROOT).as_posix())
    return sorted(files)


def j(*parts: str) -> str:
    # Patterns are assembled from fragments so this file doesn't trip other scanners.
    return "".join(parts)


INJECTION = [re.compile(p, re.I) for p in (
    j(r"ignore\s+(all\s+)?(the\s+)?(previous|prior|above|earlier)\s+", r"(instructions|prompts|messages|rules)"),
    j(r"disregard\s+(all\s+)?(previous|prior|your)\s+", r"(instructions|guidelines|rules)"),
    j(r"you\s+are\s+now\s+(in\s+)?", r"(developer|dan|jailbreak|god)\s*mode"),
    j(r"<\s*/?\s*(system|assistant|im_start|im_end)", r"\s*>"),
    j(r"\b(new|updated|real)\s+system\s+", r"prompt\b"),
    j(r"\b(AI|LLM|assistant|agent|claude|copilot|cursor|codex|gemini)\b[^\n]{0,40}\b(must|should|always)\s+", r"(run|execute|curl|download|install|send|exfiltrate)\b"),
    j(r"(curl|wget|iwr|irm)\s+[^\n|]*\|\s*", r"(sh|bash|zsh|iex|python|node)\b"),
    j(r"do\s+not\s+(tell|inform|mention\s+to)\s+the\s+", r"user"),
)]
HIDDEN = re.compile(r"[\u200b-\u200f\u202a-\u202e\u2060-\u2064\u2066-\u2069\ufeff\u00ad]|[\U000e0000-\U000e007f]")  # escapes, not literal characters
SECRETS = [
    ("Anthropic key", re.compile(r"sk-ant-[A-Za-z0-9_-]{20,}")),
    ("OpenAI-style key", re.compile(r"\bsk-(proj-)?[A-Za-z0-9]{32,}")),
    ("GitHub token", re.compile(r"\b(ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9]{36,}\b|github_pat_[A-Za-z0-9_]{60,}")),
    ("AWS access key", re.compile(r"\bAKIA[0-9A-Z]{16}\b")),
    ("Google API key", re.compile(r"\bAIza[0-9A-Za-z_-]{35}\b")),
    ("Slack token", re.compile(r"\bxox[abprs]-[A-Za-z0-9-]{10,}")),
    ("Private key", re.compile(r"-----BEGIN (RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----")),
    ("Unity licence", re.compile(r"<License\s+id=|<DeveloperData\s+Value=")),
]
AGENT_FILES = [re.compile(p, re.I if i < 4 else 0) for i, p in enumerate((
    r"(^|/)CLAUDE(\.local)?\.md$", r"(^|/)AGENTS\.md$", r"(^|/)GEMINI\.md$", r"(^|/)\.mcp\.json$",
    r"(^|/)\.claude/", r"(^|/)\.cursor/", r"(^|/)\.cursorrules$", r"(^|/)\.windsurfrules$", r"(^|/)\.clinerules",
    r"(^|/)\.github/copilot-instructions\.md$", r"(^|/)\.wolf/", r"(^|/)\.vscode/(tasks|settings|mcp)\.json$",
    r"(^|/)\.codex/", r"(^|/)\.continue/",
))]

# C# game code: things a tower-defence game never needs and an attacker would love.
CS_BANNED = [
    (re.compile(r"\bUnityWebRequest\b|\bWWW\b|\bWWWForm\b"), "network request API"),
    (re.compile(r"\bSystem\.Net\b|\bHttpClient\b|\bWebClient\b|\bTcpClient\b|\bUdpClient\b|\bSocket\b"), "network API"),
    (re.compile(r"\bProcess\.Start\b|\bSystem\.Diagnostics\.Process\b"), "process spawning"),
    (re.compile(r"\[\s*DllImport\b|\bextern\s+static\b"), "native interop"),
    (re.compile(r"\bSystem\.Reflection\.Emit\b|\bAssembly\.Load"), "dynamic code loading"),
    (re.compile(r"\bApplication\.OpenURL\b|\bApplication\.ExternalEval\b|\bApplication\.ExternalCall\b"), "browser escape"),
    (re.compile(r"\bResources\.LoadAsync\b.*http|AssetBundle\.LoadFromWebRequest|\bAddressables\b"), "remote content"),
]
PY_BANNED = [
    (re.compile(r"^\s*(import|from)\s+(urllib|requests|http|socket|ftplib|smtplib|xmlrpc|httpx|aiohttp|paramiko)\b"), "network module"),
    (re.compile(r"^\s*(import|from)\s+(subprocess|pty|ctypes)\b|\bos\.(system|popen|exec\w*|spawn\w*)\s*\("), "process / native access"),
    (re.compile(r"\b(eval|exec)\s*\(|\bpickle\.loads?\b|\bmarshal\.loads\b|__import__\s*\("), "dynamic code execution"),
]
PY_PROCESS_ALLOWED = {SELF}  # the scanner itself runs `git` to read history


def scan_text(file: str, text: str) -> None:
    lines = text.split("\n")
    for n, line in enumerate(lines, 1):
        if HIDDEN.search(line):
            fail(file, n, "hidden-unicode", "Invisible or bidirectional control character (Trojan Source / hidden prompt).")
        if file == SELF:
            continue
        for rx in INJECTION:
            if rx.search(line):
                fail(file, n, "prompt-injection", f"Matches {rx.pattern[:50]}...")
        for name, rx in SECRETS:
            if rx.search(line):
                fail(file, n, "secret", name)
    if file.endswith(".md") and re.search(r"<!--[\s\S]*?-->", text):
        fail(file, 0, "hidden-markdown-comment", "HTML comment in Markdown: hidden from readers on GitHub, visible to agents.")

    if file.endswith(".cs"):
        code = re.sub(r"//[^\n]*", "", text)  # comments may mention what's banned
        for n, line in enumerate(code.split("\n"), 1):
            for rx, what in CS_BANNED:
                if rx.search(line):
                    fail(file, n, "unsafe-source", what)
            if re.search(r"[\"']https?://", line):
                fail(file, n, "unsafe-source", "remote URL in game code")
        if "/Scripts/Core/" in file and re.search(r"^\s*using\s+Unity", text, re.M):
            fail(file, 0, "architecture", "Rigfall.Core must stay plain C# (no UnityEngine), so it can be unit tested.")

    if file.endswith(".py") and file not in PY_PROCESS_ALLOWED:
        for n, line in enumerate(lines, 1):
            for rx, what in PY_BANNED:
                if rx.search(line):
                    fail(file, n, "unsafe-source", what)


def check_unity_packages(files: list[str]) -> None:
    manifest = ROOT / "Packages" / "manifest.json"
    if not manifest.exists():
        fail("Packages/manifest.json", 0, "supply-chain", "Missing package manifest.")
        return
    data = json.loads(manifest.read_text(encoding="utf-8"))
    if data.get("scopedRegistries"):
        fail("Packages/manifest.json", 0, "supply-chain", "Scoped registries pull packages from outside Unity's registry.")
    if data.get("testables"):
        fail("Packages/manifest.json", 0, "supply-chain", "'testables' is not used by this project.")
    for name, version in data.get("dependencies", {}).items():
        if not re.fullmatch(r"com\.unity\.[a-z0-9.-]+", name):
            fail("Packages/manifest.json", 0, "supply-chain", f"{name}: only Unity's own com.unity.* packages are allowed.")
        if not re.fullmatch(r"\d+\.\d+\.\d+(-[a-z0-9.]+)?", str(version)):
            fail("Packages/manifest.json", 0, "supply-chain", f"{name}: '{version}' must be an exact registry version (no git/file/URL).")
    for f in files:
        low = f.lower()
        if low.endswith((".dll", ".so", ".dylib", ".bundle", ".aar", ".jar", ".a", ".jslib", ".jspre", ".exe", ".bat", ".ps1")):
            fail(f, 0, "supply-chain", "Native/JavaScript plugin or executable. The game is built only from source in this repo.")
        if low.endswith((".ulf", ".alf")) or re.search(r"unity_v.*\.(ulf|alf)$", low):
            fail(f, 0, "secret", "Unity licence/activation file. Store it as the UNITY_LICENSE secret, never in the repo.")
        ext = Path(f).suffix.lower()
        if ext in BINARY_OK and not f.startswith(BINARY_OK[ext]):
            fail(f, 0, "supply-chain", f"{ext} files belong under {BINARY_OK[ext][0]}")


def check_workflows(files: list[str]) -> None:
    for f in files:
        if not re.match(r"^\.github/workflows/.+\.ya?ml$", f):
            continue
        text = (ROOT / f).read_text(encoding="utf-8")
        for n, line in enumerate(text.split("\n"), 1):
            m = re.match(r"^\s*-?\s*uses:\s*([^\s#]+)", line)
            if m and not m.group(1).startswith("./") and not re.search(r"@[0-9a-f]{40}$", m.group(1)):
                fail(f, n, "unpinned-action", f"{m.group(1)} must be pinned to a full commit SHA.")
        if "pull_request_target" in text:
            fail(f, 0, "workflow", "pull_request_target runs untrusted PR code with secrets.")
        if not re.search(r"^permissions:", text, re.M):
            fail(f, 0, "workflow", "Top-level 'permissions:' block required (least privilege).")
        if re.search(r"\$\{\{\s*github\.event\.(issue|pull_request|comment|review|head_commit)\.(title|body|message|head\.ref)", text):
            fail(f, 0, "workflow", "Untrusted event text interpolated into a workflow (script injection).")
        if re.search(r"secrets\.UNITY_[A-Z]+", text) and re.search(r"^\s*pull_request\s*:", text, re.M):
            fail(f, 0, "workflow", "Unity secrets must not be exposed to pull_request runs.")


def check_history() -> None:
    if not (ROOT / ".git").exists():
        return
    try:
        log = subprocess.run(["git", "log", "--format=%H%n%B%n--END--"], cwd=ROOT, capture_output=True, check=True).stdout.decode("utf-8", "replace")
    except (OSError, subprocess.CalledProcessError):
        return
    trailer = re.compile(r"^co-authored-by:.*(claude|anthropic|copilot|openai|chatgpt|cursor|gemini)", re.I | re.M)
    for block in log.split("--END--"):
        sha = block.strip().split("\n")[0] if block.strip() else ""
        if sha and trailer.search(block):
            fail(f"commit {sha[:10]}", 0, "ai-coauthor", "Commit carries an AI co-author trailer.")


def allowlisted_agent_files() -> set[str]:
    path = ROOT / ".agent-files-allowlist"
    if not path.exists():
        return set()
    return {s.strip() for s in path.read_text(encoding="utf-8").split("\n") if s.strip() and not s.strip().startswith("#")}


def main() -> int:
    files = list_files()
    allowed = allowlisted_agent_files()
    for f in files:
        if any(rx.search(f) for rx in AGENT_FILES) and f not in allowed:
            fail(f, 0, "agent-config", "AI agent instruction/config file: auto-loaded by coding agents, a prompt-injection "
                                       "vector. Review it and add it to .agent-files-allowlist, or delete it.")
        p = Path(f)
        if p.suffix.lower() in TEXT_EXT or p.name in ("LICENSE", "CODEOWNERS", "commit-msg"):
            try:
                text = (ROOT / f).read_text(encoding="utf-8")
            except UnicodeDecodeError:
                fail(f, 0, "encoding", "Text file is not valid UTF-8.")
                continue
            scan_text(f, text)
    check_unity_packages(files)
    check_workflows(files)
    check_history()

    if findings:
        print(f"Security scan found {len(findings)} issue(s):\n", file=sys.stderr)
        for file, line, rule, detail in findings:
            print(f"  x [{rule}] {file}{':' + str(line) if line else ''} - {detail}", file=sys.stderr)
        return 1
    print(f"Security scan passed ({len(files)} files checked).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
