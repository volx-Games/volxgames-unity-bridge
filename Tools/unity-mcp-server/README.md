# Unity MCP Server

This server exposes MCP tools backed by the reusable Unity editor bridge in the repo root package `com.volxgames.unity.bridge`.

It is not Codex-specific. Any MCP client that can launch a stdio server can use this adapter, including Cursor.

Risky mutation tools require:

```text
confirm = "I understand this will modify the Unity project."
```

This is enforced for destructive or high-impact actions such as deleting or moving assets, removing packages, deleting PlayerPrefs, removing components, deleting hierarchy objects, replacing build scenes, or switching the build target.

## Tools

- `unity_health`
- `unity_state`
- `unity_project`
- `unity_runtime`
- `unity_query_runtime_objects`
- `unity_inspect_runtime_object`
- `unity_build`
- `unity_player_settings`
- `unity_audio_settings`
- `unity_time_settings`
- `unity_quality_settings`
- `unity_project_tags_layers`
- `unity_physics_settings`
- `unity_set_player_settings`
- `unity_set_audio_settings`
- `unity_set_time_settings`
- `unity_set_quality_settings`
- `unity_set_project_tags_layers`
- `unity_set_physics_settings`
- `unity_set_build_settings`
- `unity_switch_build_target`
- `unity_build_scenes`
- `unity_last_build_report`
- `unity_build_player`
- `unity_set_build_scenes`
- `unity_add_build_scene`
- `unity_remove_build_scene`
- `unity_tests`
- `unity_last_test_report`
- `unity_run_tests`
- `unity_compilation`
- `unity_last_compilation`
- `unity_editor_windows`
- `unity_packages`
- `unity_package_operations`
- `unity_last_package_operation`
- `unity_add_package`
- `unity_remove_package`
- `unity_embed_package`
- `unity_resolve_packages`
- `unity_scene_stats`
- `unity_prefab_stage`
- `unity_selection`
- `unity_active_object`
- `unity_scenes`
- `unity_commands`
- `unity_hierarchy`
- `unity_inspect_hierarchy_object`
- `unity_inspect_asset`
- `unity_asset_dependencies`
- `unity_resolve_asset`
- `unity_search_assets`
- `unity_search_hierarchy`
- `unity_logs`
- `unity_query_logs`
- `unity_events`
- `unity_query_events`
- `unity_play`
- `unity_play_and_focus_game`
- `unity_pause`
- `unity_resume`
- `unity_step`
- `unity_stop`
- `unity_set_time_scale`
- `unity_set_target_frame_rate`
- `unity_refresh_assets`
- `unity_save_assets`
- `unity_save_open_scenes`
- `unity_save_scene`
- `unity_close_scene`
- `unity_clear_console`
- `unity_open_scene`
- `unity_open_scene_additive`
- `unity_set_active_scene`
- `unity_open_prefab_stage`
- `unity_close_prefab_stage`
- `unity_open_asset`
- `unity_frame_selected`
- `unity_focus_project_window`
- `unity_focus_scene_view`
- `unity_focus_window`
- `unity_select_asset`
- `unity_ping_asset`
- `unity_reveal_asset`
- `unity_select_hierarchy_object`
- `unity_execute_menu_item`
- `unity_create_game_object`
- `unity_instantiate_prefab`
- `unity_duplicate_hierarchy_object`
- `unity_delete_hierarchy_object`
- `unity_rename_hierarchy_object`
- `unity_set_hierarchy_object_active`
- `unity_set_hierarchy_metadata`
- `unity_set_hierarchy_parent`
- `unity_set_transform`
- `unity_add_component`
- `unity_remove_component`
- `unity_set_component_enabled`
- `unity_set_component_property`
- `unity_invoke_component_method`
- `unity_invoke_static_method`
- `unity_capture_scene_view`
- `unity_capture_game_view`
- `unity_capture_window`
- `unity_send_window_key_event`
- `unity_send_window_mouse_event`
- `unity_playerprefs_set`
- `unity_playerprefs_get`
- `unity_playerprefs_delete_key`
- `unity_playerprefs_delete_all`
- `unity_create_folder`
- `unity_delete_asset`
- `unity_move_asset`
- `unity_rename_asset`
- `unity_duplicate_asset`
- `unity_texture_importer`
- `unity_set_texture_importer`
- `unity_undo`
- `unity_redo`
- `unity_command`

## Usage

```bash
UNITY_BRIDGE_URL=http://127.0.0.1:48761 node ./Tools/unity-mcp-server/server.js
```

The Unity package must be loaded in the editor and the bridge must be running.

## Cursor setup

Example Cursor config:

```json
{
  "mcpServers": {
    "unity-bridge": {
      "command": "node",
      "args": [
        "${workspaceFolder}/Tools/unity-mcp-server/server.js"
      ],
      "env": {
        "UNITY_BRIDGE_URL": "http://127.0.0.1:48761"
      }
    }
  }
}
```

The same example is checked in at `Tools/unity-mcp-server/examples/cursor.mcp.json`.

To generate a one-click Cursor deeplink for the current checkout:

```bash
node ./Tools/unity-mcp-server/scripts/generate-cursor-install-link.js
```

## Codex setup

Codex uses MCP servers from `~/.codex/config.toml` or a project-scoped `.codex/config.toml`.

Quick add with the Codex CLI:

```bash
codex mcp add unity-bridge --env UNITY_BRIDGE_URL=http://127.0.0.1:48761 -- node ./Tools/unity-mcp-server/server.js
```

Verify it:

```bash
codex mcp list
```

Helper script:

```bash
bash ./Tools/unity-mcp-server/scripts/setup-codex-mcp.sh
```

Example project config:

```toml
[mcp_servers.unity-bridge]
command = "node"
args = ["/absolute/path/to/volxgames-unity-bridge/Tools/unity-mcp-server/server.js"]
cwd = "/absolute/path/to/volxgames-unity-bridge"

[mcp_servers.unity-bridge.env]
UNITY_BRIDGE_URL = "http://127.0.0.1:48761"
```

An example file is checked in at `.codex/config.toml.example`.

## Direct streaming endpoints

The Unity bridge also exposes direct SSE streams over HTTP for live observers:

- `GET /stream/logs`
- `GET /stream/events`
- `GET /stream/runtime`

Example:

```bash
curl -N http://127.0.0.1:48761/stream/runtime
```
