# Maintaining VolxGames Unity Bridge

## Local development

For local package development in a Unity project, temporarily switch the dependency to a local path:

```json
{
  "dependencies": {
    "com.volxgames.unity.bridge": "file:/absolute/path/to/volxgames-unity-bridge"
  }
}
```

When the change is ready, switch consuming projects back to a Git tag release.

## Repo layout

- `Editor/`
  Unity editor bridge package code
- `Tools/unity-mcp-server/`
  Node MCP adapter for Codex, Cursor, and other MCP clients
- `.codex/config.toml.example`
  Example Codex project configuration
- `CHANGELOG.md`
  Release notes for tagged package versions

## Custom commands

Project-specific editor scripts can register extra commands without editing the package:

```csharp
using VolxGames.UnityBridge.Editor;
using UnityEditor;

[InitializeOnLoad]
public static class ProjectBridgeCommands
{
    static ProjectBridgeCommands()
    {
        UnityBridgeCommandRegistry.Register(
            "project_validate_prefabs",
            _ =>
            {
                AssetDatabase.Refresh();
                return new UnityBridgeCustomCommandResult
                {
                    ok = true,
                    message = "Prefab validation placeholder finished."
                };
            },
            "Run a project-specific prefab validation command.");
    }
}
```

## Release workflow

1. Update package code and docs in this repo.
2. Bump `package.json` version.
3. Append release notes in `CHANGELOG.md`.
4. Commit and tag the release, for example `v0.1.1`.
5. In consuming Unity projects, update the Git dependency tag in `Packages/manifest.json`.
