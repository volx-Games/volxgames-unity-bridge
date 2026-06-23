# VolxGames Unity Bridge

This package runs a localhost Unity Editor bridge that exposes project state, runtime state, logs, and safe mutation commands to an external MCP adapter.

Licensed under MIT. See `LICENSE.md`.

## Install

Add the package to a Unity project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.volxgames.unity.bridge": "https://github.com/volx-Games/volxgames-unity-bridge.git#v0.1.1"
  }
}
```

When a new release is published, update the tag in `Packages/manifest.json` and let Unity resolve the package again.

## Compatibility

- Targets Unity 6 (`6000.0+`)
- Tested on `6000.3.8f1`
- Older Unity versions are not officially supported yet

## Menu

- `Tools/Unity Bridge/Start`
- `Tools/Unity Bridge/Stop`
- `Tools/Unity Bridge/Settings`

## HTTP endpoints

- `GET /health`
- `GET /state`
- `GET /project`
- `GET /runtime`
- `POST /runtime/query`
- `POST /runtime/inspect`
- `GET /stream/logs`
- `GET /stream/events`
- `GET /stream/runtime`
- `GET /build`
- `GET /player-settings`
- `GET /audio-settings`
- `GET /time-settings`
- `GET /quality-settings`
- `GET /project-tags-layers`
- `GET /physics-settings`
- `GET /build/last`
- `POST /build/player`
- `POST /player-settings`
- `POST /audio-settings`
- `POST /time-settings`
- `POST /quality-settings`
- `POST /project-tags-layers`
- `POST /physics-settings`
- `POST /playerprefs/get`
- `POST /importers/texture`
- `POST /importers/texture/set`
- `POST /build/settings`
- `POST /build/target`
- `GET /build/scenes`
- `POST /build/scenes`
- `POST /build/scenes/add`
- `POST /build/scenes/remove`
- `GET /tests`
- `GET /tests/last`
- `POST /tests/run`
- `GET /compilation`
- `GET /compilation/last`
- `GET /packages`
- `GET /packages/operations`
- `GET /packages/operations/last`
- `POST /packages/add`
- `POST /packages/remove`
- `POST /packages/embed`
- `POST /packages/resolve`
- `GET /editor/windows`
- `GET /scene-stats`
- `GET /prefab-stage`
- `GET /selection`
- `GET /active-object`
- `GET /scenes`
- `GET /commands`
- `GET /hierarchy`
- `POST /inspect/hierarchy`
- `POST /inspect/asset`
- `POST /asset/dependencies`
- `POST /asset/resolve`
- `POST /search/assets`
- `POST /search/hierarchy`
- `GET /logs`
- `POST /logs/query`
- `GET /events`
- `POST /events/query`
- `POST /command`

## Commands

- `play`
- `play_and_focus_game`
- `pause`
- `resume`
- `step`
- `stop`
- `set_time_scale`
- `set_target_frame_rate`
- `build_player`
- `set_player_settings`
- `set_audio_settings`
- `set_time_settings`
- `set_quality_settings`
- `set_project_tags_layers`
- `set_texture_importer`
- `playerprefs_get`
- `set_build_settings`
- `switch_build_target`
- `run_tests`
- `set_build_scenes`
- `add_build_scene`
- `remove_build_scene`
- `add_package`
- `remove_package`
- `embed_package`
- `resolve_packages`
- `refresh_assets`
- `save_assets`
- `save_open_scenes`
- `save_scene`
- `close_scene`
- `clear_console`
- `open_scene`
- `open_scene_additive`
- `set_active_scene`
- `open_prefab_stage`
- `close_prefab_stage`
- `open_asset`
- `frame_selected`
- `focus_project_window`
- `focus_scene_view`
- `focus_window`
- `select_asset`
- `ping_asset`
- `reveal_asset`
- `select_hierarchy_object`
- `execute_menu_item`
- `create_game_object`
- `instantiate_prefab`
- `duplicate_hierarchy_object`
- `delete_hierarchy_object`
- `rename_hierarchy_object`
- `set_hierarchy_object_active`
- `set_hierarchy_metadata`
- `set_hierarchy_parent`
- `set_transform`
- `add_component`
- `remove_component`
- `set_component_enabled`
- `set_component_property`
- `invoke_component_method`
- `invoke_static_method`
- `capture_scene_view`
- `capture_game_view`
- `capture_window`
- `send_window_key_event`
- `send_window_mouse_event`
- `playerprefs_set`
- `playerprefs_delete_key`
- `playerprefs_delete_all`
- `create_folder`
- `delete_asset`
- `move_asset`
- `rename_asset`
- `duplicate_asset`
- `undo`
- `redo`

## Safety

Risky mutation commands require:

```text
confirm = "I understand this will modify the Unity project."
```

This is enforced for destructive or high-impact actions such as deleting or moving assets, removing packages, deleting PlayerPrefs, removing components, deleting hierarchy objects, replacing build scenes, or switching the build target.
