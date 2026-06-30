# Changelog

All notable changes to `com.volxgames.unity.bridge` should be documented in this file.

## [0.1.3] - 2026-06-30

- Add multi-Unity instance discovery so MCP clients can target a running bridge by `UNITY_BRIDGE_PROJECT_PATH` instead of sharing one fixed localhost port.
- Fall back from port `48761` to the next available bridge port while staying below Fresnel's reserved `48861` port.
- Write local bridge instance records with project path, process id, bound URL, Unity version, and heartbeat data for safer MCP adapter discovery.
- Fail closed when multiple live Unity bridge instances are running and no explicit project path or URL is configured.
- Harden MCP discovery against stale records, non-loopback registry URLs, Fresnel runtime/bridge URLs, and hanging localhost probes.
- Fix timeout responses so listener threads no longer call Unity main-thread-only APIs such as `EditorApplication.isUpdating`.
- Cancel timed-out pending main-thread jobs and commands so they do not run later after the client has already received a timeout.
- Add `lastAssetRefreshAtUtc` to state and refresh responses so clients do not confuse an AssetDatabase refresh with an assembly reload.
- Add Node discovery tests covering explicit URLs, project-path targeting, stale records, multi-instance ambiguity, Fresnel rejection, and path normalization.

## [0.1.2] - 2026-06-23

- Return explicit `busy` responses for main-thread timeouts during compilation, refresh, and reload-heavy editor states.
- Include current compilation summary in timed-out command responses to make fallback handling clearer for MCP clients.
- Add `unity_inspector_selection`, `unity_console_selection`, and `unity_current_context` tools for richer live editor context reads.
- Move client config examples under `examples/` and document Unity version compatibility more clearly for package users.

## [0.1.1] - 2026-06-22

- Simplified the Unity menu to a single `Tools/Unity Bridge/Settings` entry.
- Added missing Unity `.meta` files for package assets and tooling files.

## [0.1.0] - 2026-06-22

- Initial standalone package release.
- Unity Editor localhost bridge with runtime, hierarchy, asset, build, package, test, and settings tooling.
- Node MCP adapter for Codex and Cursor.
- Risky mutation confirmation guardrails.
- Auto-restart after reload and read retry across brief bridge restarts.
- `play_and_focus_game` workflow helper.
