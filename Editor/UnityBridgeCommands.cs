using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VolxGames.UnityBridge.Editor
{
    internal static class UnityBridgeCommands
    {
        private const string RiskyActionConfirmation = "I understand this will modify the Unity project.";

        public static List<UnityBridgeCommandDescriptor> ListAvailableCommands()
        {
            var commands = new List<UnityBridgeCommandDescriptor>
            {
                BuiltIn("play", "Enter play mode."),
                BuiltIn("play_and_focus_game", "Enter play mode and focus the Game view."),
                BuiltIn("pause", "Pause the editor."),
                BuiltIn("resume", "Resume the editor."),
                BuiltIn("step", "Advance one frame while paused in play mode."),
                BuiltIn("stop", "Stop play mode."),
                BuiltIn("set_time_scale", "Set Unity Time.timeScale.", "{\"value\":1.0}"),
                BuiltIn("set_target_frame_rate", "Set Application.targetFrameRate.", "{\"value\":60}"),
                BuiltIn("build_player", "Run a Unity player build and store the last build report.", "{\"buildTarget\":\"StandaloneOSX\",\"locationPathName\":\"Builds/StandaloneOSX/App.app\"}"),
                BuiltIn("set_build_settings", "Update active-target build flags and output location.", "{\"setDevelopmentBuild\":true,\"developmentBuild\":true}"),
                BuiltIn("switch_build_target", "Switch the active Unity build target.", "{\"buildTarget\":\"iOS\",\"buildTargetGroup\":\"iPhone\"}"),
                BuiltIn("set_player_settings", "Update active-target player settings such as bundle id, backend, compatibility, stripping, startup logs, and define symbols.", "{\"setCaptureStartupLogs\":true,\"captureStartupLogs\":true}"),
                BuiltIn("set_project_tags_layers", "Update project tags, user layers, sorting layers, and rendering layers.", "{\"addTags\":[\"BridgeTempTag\"],\"setLayers\":[{\"index\":8,\"name\":\"BridgeLayer\"}]}"),
                BuiltIn("set_quality_settings", "Update project quality settings and per-level values.", "{\"qualityLevels\":[{\"index\":0,\"setLodBias\":true,\"lodBias\":1.25}]}"),
                BuiltIn("set_audio_settings", "Update project audio settings.", "{\"setVolume\":true,\"volume\":0.8}"),
                BuiltIn("set_time_settings", "Update project time settings.", "{\"setFixedTimestep\":true,\"fixedTimestep\":0.0166667}"),
                BuiltIn("run_tests", "Run Unity edit mode and/or play mode tests.", "{\"testMode\":\"EditMode\",\"assemblyNames\":[\"MyProject.Editor.Tests\"]}"),
                BuiltIn("set_build_scenes", "Replace the Unity build scene list.", "{\"scenes\":[{\"path\":\"Assets/Scenes/GameScene.unity\",\"enabled\":true}]}"),
                BuiltIn("add_build_scene", "Add or move a scene in Unity build settings.", "{\"path\":\"Assets/Scenes/GameScene.unity\",\"enabled\":true,\"buildIndex\":0}"),
                BuiltIn("remove_build_scene", "Remove a scene from Unity build settings.", "{\"path\":\"Assets/Scenes/GameScene.unity\"}"),
                BuiltIn("add_package", "Add a Unity package by name, version, git URL, or local path.", "{\"packageId\":\"com.unity.inputsystem\"}"),
                BuiltIn("remove_package", "Remove a direct dependency Unity package by name.", "{\"packageId\":\"com.unity.inputsystem\"}"),
                BuiltIn("embed_package", "Embed an installed Unity package into Packages/ for local editing.", "{\"packageId\":\"com.unity.inputsystem\"}"),
                BuiltIn("resolve_packages", "Resolve pending package manifest changes."),
                BuiltIn("refresh_assets", "Refresh the AssetDatabase."),
                BuiltIn("compilation_status", "Read current or most recent Unity compilation status."),
                BuiltIn("last_compilation", "Read the most recent finished Unity compilation report."),
                BuiltIn("save_assets", "Save dirty assets."),
                BuiltIn("save_open_scenes", "Save open scenes."),
                BuiltIn("save_scene", "Save a specific open scene.", "Assets/Scenes/GameScene.unity"),
                BuiltIn("close_scene", "Close a specific open scene.", "Assets/Scenes/GameScene.unity"),
                BuiltIn("clear_console", "Clear the Unity console."),
                BuiltIn("open_scene", "Open a scene asset.", "Assets/Scenes/GameScene.unity"),
                BuiltIn("open_scene_additive", "Open a scene asset additively.", "Assets/Scenes/GameScene.unity"),
                BuiltIn("set_active_scene", "Set an already open scene as the active scene.", "Assets/Scenes/GameScene.unity"),
                BuiltIn("open_prefab_stage", "Open a prefab asset in Prefab Mode.", "Assets/Prefabs/Foo.prefab"),
                BuiltIn("close_prefab_stage", "Return from Prefab Mode to the main stage."),
                BuiltIn("open_asset", "Open an asset using the Unity editor.", "Assets/Path/To.asset"),
                BuiltIn("frame_selected", "Frame the current selection in the last active SceneView."),
                BuiltIn("focus_project_window", "Focus the Project window."),
                BuiltIn("focus_scene_view", "Focus the Scene view."),
                BuiltIn("focus_window", "Focus an editor window by type or title.", "{\"type\":\"SceneView\"}"),
                BuiltIn("select_asset", "Select an asset.", "Assets/Path/To.asset"),
                BuiltIn("ping_asset", "Ping an asset in the editor.", "Assets/Path/To.asset"),
                BuiltIn("reveal_asset", "Reveal an asset in Finder.", "Assets/Path/To.asset"),
                BuiltIn("select_hierarchy_object", "Select a hierarchy object.", "Root/Child"),
                BuiltIn("execute_menu_item", "Execute a Unity menu item.", "File/Save"),
                BuiltIn("create_game_object", "Create a GameObject or primitive.", "{\"name\":\"BridgeObject\",\"parentPath\":\"Root\"}"),
                BuiltIn("instantiate_prefab", "Instantiate a prefab asset into the hierarchy.", "{\"assetPath\":\"Assets/Prefabs/Foo.prefab\",\"scenePath\":\"Assets/Scenes/GameScene.unity\"}"),
                BuiltIn("duplicate_hierarchy_object", "Duplicate a hierarchy object.", "{\"path\":\"Root/Child\"}"),
                BuiltIn("delete_hierarchy_object", "Delete a hierarchy object.", "{\"path\":\"Root/Child\"}"),
                BuiltIn("rename_hierarchy_object", "Rename a hierarchy object.", "{\"path\":\"Root/Child\",\"name\":\"Renamed\"}"),
                BuiltIn("set_hierarchy_object_active", "Toggle a hierarchy object active state.", "{\"path\":\"Root/Child\",\"active\":false}"),
                BuiltIn("set_hierarchy_metadata", "Update name, tag, layer, or static state on a hierarchy object.", "{\"path\":\"Root/Child\",\"tag\":\"Untagged\",\"layer\":0}"),
                BuiltIn("set_hierarchy_parent", "Reparent a hierarchy object.", "{\"path\":\"Root/Child\",\"parentPath\":\"OtherRoot\"}"),
                BuiltIn("set_transform", "Write local position/rotation/scale.", "{\"path\":\"Root/Child\",\"setLocalPosition\":true,\"localPosition\":{\"x\":0,\"y\":1,\"z\":0}}"),
                BuiltIn("add_component", "Add a component to a hierarchy object.", "{\"path\":\"Root/Child\",\"componentType\":\"BoxCollider2D\"}"),
                BuiltIn("remove_component", "Remove a component from a hierarchy object.", "{\"path\":\"Root/Child\",\"componentType\":\"BoxCollider2D\"}"),
                BuiltIn("set_component_enabled", "Enable or disable a component.", "{\"path\":\"Root/Child\",\"componentType\":\"MonoBehaviour\",\"enabled\":false}"),
                BuiltIn("set_component_property", "Write a serialized component property.", "{\"path\":\"Root/Child\",\"componentType\":\"SpriteRenderer\",\"propertyPath\":\"m_Color\",\"valueType\":\"color\"}"),
                BuiltIn("invoke_component_method", "Invoke a component method with zero or one primitive argument.", "{\"path\":\"Root/Child\",\"componentType\":\"Animator\",\"methodName\":\"Rebind\"}"),
                BuiltIn("invoke_static_method", "Invoke a static method with zero or one primitive argument.", "{\"typeName\":\"UnityEditor.EditorApplication\",\"methodName\":\"Beep\"}"),
                BuiltIn("capture_scene_view", "Capture the active Scene view to a PNG file.", "{\"outputPath\":\"/tmp/scene.png\"}"),
                BuiltIn("capture_game_view", "Capture the Game view to a PNG file.", "{\"outputPath\":\"/tmp/game.png\"}"),
                BuiltIn("capture_window", "Capture an editor window by type or title to a PNG file.", "{\"type\":\"SceneView\",\"outputPath\":\"/tmp/window.png\"}"),
                BuiltIn("send_window_key_event", "Send a keyboard event to an editor window.", "{\"title\":\"Game\",\"keyCode\":\"Space\",\"eventType\":\"KeyDown\"}"),
                BuiltIn("send_window_mouse_event", "Send a mouse event to an editor window.", "{\"title\":\"Game\",\"eventType\":\"MouseDown\",\"x\":100,\"y\":100,\"button\":0}"),
                BuiltIn("playerprefs_get", "Read a PlayerPrefs key, optionally with a typed valueType.", "{\"key\":\"foo\",\"valueType\":\"string\"}"),
                BuiltIn("playerprefs_set", "Set a PlayerPrefs key.", "{\"key\":\"foo\",\"valueType\":\"string\",\"stringValue\":\"bar\"}"),
                BuiltIn("playerprefs_delete_key", "Delete a PlayerPrefs key.", "{\"key\":\"foo\"}"),
                BuiltIn("playerprefs_delete_all", "Delete all PlayerPrefs keys."),
                BuiltIn("create_folder", "Create a project folder asset.", "{\"parentFolder\":\"Assets\",\"folderName\":\"BridgeGenerated\"}"),
                BuiltIn("delete_asset", "Delete a project asset.", "{\"path\":\"Assets/Foo.asset\"}"),
                BuiltIn("move_asset", "Move or rename an asset path.", "{\"fromPath\":\"Assets/Foo.asset\",\"toPath\":\"Assets/Bar/Foo.asset\"}"),
                BuiltIn("rename_asset", "Rename an asset in place.", "{\"path\":\"Assets/Foo.asset\",\"name\":\"Bar\"}"),
                BuiltIn("duplicate_asset", "Duplicate an asset to a new path.", "{\"fromPath\":\"Assets/Foo.asset\",\"toPath\":\"Assets/Foo Copy.asset\"}"),
                BuiltIn("set_texture_importer", "Update texture importer settings for an asset.", "{\"path\":\"Assets/Textures/Moon_256x256.png\",\"setIsReadable\":true,\"isReadable\":true}"),
                BuiltIn("undo", "Undo the last editor operation."),
                BuiltIn("redo", "Redo the last undone editor operation.")
            };

            commands.AddRange(UnityBridgeCommandRegistry.ListCustomCommands());
            return commands;
        }

        public static UnityBridgeCommandResponse Execute(UnityBridgeCommandRequest request, string lastReloadAtUtc)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.name))
            {
                return Fail("Command name is required.", lastReloadAtUtc);
            }

            if (RequiresConfirmation(request.name) && !HasRequiredConfirmation(request.confirm))
            {
                return Fail($"Command '{request.name}' requires confirm=\"{RiskyActionConfirmation}\".", lastReloadAtUtc);
            }

            try
            {
                switch (request.name)
                {
                    case "play":
                        EditorApplication.isPlaying = true;
                        return Ok("Entered play mode.", lastReloadAtUtc);
                    case "play_and_focus_game":
                        EditorApplication.isPlaying = true;
                        EditorApplication.delayCall += FocusGameViewWindow;
                        return Ok("Entered play mode and scheduled Game view focus.", lastReloadAtUtc);
                    case "pause":
                        EditorApplication.isPaused = true;
                        return Ok("Paused editor.", lastReloadAtUtc);
                    case "resume":
                        EditorApplication.isPaused = false;
                        return Ok("Resumed editor.", lastReloadAtUtc);
                    case "step":
                        EditorApplication.Step();
                        return Ok("Stepped one frame.", lastReloadAtUtc);
                    case "stop":
                        EditorApplication.isPlaying = false;
                        return Ok("Stopped play mode.", lastReloadAtUtc);
                    case "set_time_scale":
                        return UnityBridgeMutations.SetTimeScale(request.argument, lastReloadAtUtc);
                    case "set_target_frame_rate":
                        return UnityBridgeMutations.SetTargetFrameRate(request.argument, lastReloadAtUtc);
                    case "build_player":
                        return UnityBridgeMutations.BuildPlayer(request.argument, lastReloadAtUtc);
                    case "set_build_settings":
                        return UnityBridgeMutations.SetBuildSettings(request.argument, lastReloadAtUtc);
                    case "switch_build_target":
                        return UnityBridgeMutations.SwitchBuildTarget(request.argument, lastReloadAtUtc);
                    case "set_player_settings":
                        return UnityBridgeMutations.SetPlayerSettings(request.argument, lastReloadAtUtc);
                    case "set_project_tags_layers":
                        var projectTagLayerResponse = UnityBridgeMutations.SetProjectTagLayers(request.argument);
                        return Ok(projectTagLayerResponse.message, lastReloadAtUtc);
                    case "set_quality_settings":
                        var qualitySettingsResponse = UnityBridgeMutations.SetQualitySettings(request.argument);
                        return Ok(qualitySettingsResponse.message, lastReloadAtUtc);
                    case "set_audio_settings":
                        var audioSettingsResponse = UnityBridgeMutations.SetAudioSettings(request.argument);
                        return Ok(audioSettingsResponse.message, lastReloadAtUtc);
                    case "set_time_settings":
                        var timeSettingsResponse = UnityBridgeMutations.SetTimeSettings(request.argument);
                        return Ok(timeSettingsResponse.message, lastReloadAtUtc);
                    case "run_tests":
                        return UnityBridgeMutations.RunTests(request.argument, lastReloadAtUtc);
                    case "set_build_scenes":
                        return UnityBridgeMutations.SetBuildScenes(request.argument, lastReloadAtUtc);
                    case "add_build_scene":
                        return UnityBridgeMutations.AddBuildScene(request.argument, lastReloadAtUtc);
                    case "remove_build_scene":
                        return UnityBridgeMutations.RemoveBuildScene(request.argument, lastReloadAtUtc);
                    case "add_package":
                        return UnityBridgeMutations.AddPackage(request.argument, lastReloadAtUtc);
                    case "remove_package":
                        return UnityBridgeMutations.RemovePackage(request.argument, lastReloadAtUtc);
                    case "embed_package":
                        return UnityBridgeMutations.EmbedPackage(request.argument, lastReloadAtUtc);
                    case "resolve_packages":
                        return UnityBridgeMutations.ResolvePackages(lastReloadAtUtc);
                    case "refresh_assets":
                        var refreshedAtUtc = UnityBridgeStateBuilder.MarkAssetRefresh();
                        AssetDatabase.Refresh();
                        return Ok(EditorApplication.isCompiling
                            ? $"Refreshed AssetDatabase at {refreshedAtUtc}. Compilation is still running; call compilation_status until running=false before trusting logs."
                            : $"Refreshed AssetDatabase at {refreshedAtUtc}. No compilation is currently running; lastReloadAtUtc only changes when Unity performs an assembly reload.",
                            lastReloadAtUtc);
                    case "compilation_status":
                        return Ok(UnityBridgeJson.SerializeObject(UnityBridgeCompilationTracker.GetCurrentOrLastReport()), lastReloadAtUtc);
                    case "last_compilation":
                        return Ok(UnityBridgeJson.SerializeObject(UnityBridgeCompilationTracker.GetLastReport()), lastReloadAtUtc);
                    case "save_assets":
                        AssetDatabase.SaveAssets();
                        return Ok("Saved assets.", lastReloadAtUtc);
                    case "save_open_scenes":
                        EditorSceneManager.SaveOpenScenes();
                        return Ok("Saved open scenes.", lastReloadAtUtc);
                    case "save_scene":
                        if (string.IsNullOrWhiteSpace(request.argument))
                        {
                            return Fail("Command argument is required for save_scene.", lastReloadAtUtc);
                        }

                        var sceneToSave = SceneManager.GetSceneByPath(request.argument);
                        if (!sceneToSave.IsValid() || !sceneToSave.isLoaded)
                        {
                            return Fail($"Open scene was not found: {request.argument}", lastReloadAtUtc);
                        }

                        EditorSceneManager.SaveScene(sceneToSave);
                        return Ok($"Saved scene: {request.argument}", lastReloadAtUtc);
                    case "close_scene":
                        if (string.IsNullOrWhiteSpace(request.argument))
                        {
                            return Fail("Command argument is required for close_scene.", lastReloadAtUtc);
                        }

                        var sceneToClose = SceneManager.GetSceneByPath(request.argument);
                        if (!sceneToClose.IsValid() || !sceneToClose.isLoaded)
                        {
                            return Fail($"Open scene was not found: {request.argument}", lastReloadAtUtc);
                        }

                        if (!EditorSceneManager.CloseScene(sceneToClose, true))
                        {
                            return Fail($"Scene could not be closed: {request.argument}", lastReloadAtUtc);
                        }

                        return Ok($"Closed scene: {request.argument}", lastReloadAtUtc);
                    case "clear_console":
                        ClearConsole();
                        return Ok("Cleared console.", lastReloadAtUtc);
                    case "open_scene":
                        if (string.IsNullOrWhiteSpace(request.argument))
                        {
                            return Fail("Command argument is required for open_scene.", lastReloadAtUtc);
                        }

                        var openedScene = EditorSceneManager.OpenScene(request.argument);
                        return Ok($"Opened scene: {openedScene.path}", lastReloadAtUtc);
                    case "open_scene_additive":
                        if (string.IsNullOrWhiteSpace(request.argument))
                        {
                            return Fail("Command argument is required for open_scene_additive.", lastReloadAtUtc);
                        }

                        var additiveScene = EditorSceneManager.OpenScene(request.argument, OpenSceneMode.Additive);
                        return Ok($"Opened scene additively: {additiveScene.path}", lastReloadAtUtc);
                    case "set_active_scene":
                        if (string.IsNullOrWhiteSpace(request.argument))
                        {
                            return Fail("Command argument is required for set_active_scene.", lastReloadAtUtc);
                        }

                        var targetScene = SceneManager.GetSceneByPath(request.argument);
                        if (!targetScene.IsValid() || !targetScene.isLoaded)
                        {
                            return Fail($"Open scene was not found: {request.argument}", lastReloadAtUtc);
                        }

                        SceneManager.SetActiveScene(targetScene);
                        return Ok($"Set active scene: {request.argument}", lastReloadAtUtc);
                    case "open_prefab_stage":
                        if (string.IsNullOrWhiteSpace(request.argument))
                        {
                            return Fail("Command argument is required for open_prefab_stage.", lastReloadAtUtc);
                        }

                        var prefabStage = PrefabStageUtility.OpenPrefab(request.argument);
                        return Ok($"Opened prefab stage: {prefabStage.assetPath}", lastReloadAtUtc);
                    case "close_prefab_stage":
                        StageUtility.GoToMainStage();
                        return Ok("Returned to main stage.", lastReloadAtUtc);
                    case "open_asset":
                        if (string.IsNullOrWhiteSpace(request.argument))
                        {
                            return Fail("Command argument is required for open_asset.", lastReloadAtUtc);
                        }

                        var openAsset = AssetDatabase.LoadMainAssetAtPath(request.argument);
                        if (openAsset == null)
                        {
                            return Fail($"Asset was not found: {request.argument}", lastReloadAtUtc);
                        }

                        AssetDatabase.OpenAsset(openAsset);
                        return Ok($"Opened asset: {request.argument}", lastReloadAtUtc);
                    case "frame_selected":
                        if (SceneView.lastActiveSceneView == null)
                        {
                            return Fail("No active SceneView is available.", lastReloadAtUtc);
                        }

                        SceneView.lastActiveSceneView.FrameSelected();
                        return Ok("Framed current selection.", lastReloadAtUtc);
                    case "focus_project_window":
                        EditorUtility.FocusProjectWindow();
                        return Ok("Focused Project window.", lastReloadAtUtc);
                    case "focus_scene_view":
                        SceneView.FocusWindowIfItsOpen(typeof(SceneView));
                        return Ok("Focused Scene view.", lastReloadAtUtc);
                    case "focus_window":
                        var focusRequest = JsonUtility.FromJson<UnityBridgeWindowFocusRequest>(request.argument ?? string.Empty);
                        if (focusRequest == null || (string.IsNullOrWhiteSpace(focusRequest.type) && string.IsNullOrWhiteSpace(focusRequest.title)))
                        {
                            return Fail("focus_window requires JSON with type and/or title.", lastReloadAtUtc);
                        }

                        var editorWindow = UnityBridgeStateBuilder.FindEditorWindow(focusRequest.type, focusRequest.title);
                        if (editorWindow == null)
                        {
                            return Fail("Matching editor window was not found.", lastReloadAtUtc);
                        }

                        editorWindow.Focus();
                        return Ok($"Focused editor window: {editorWindow.GetType().FullName}", lastReloadAtUtc);
                    case "select_asset":
                        if (string.IsNullOrWhiteSpace(request.argument))
                        {
                            return Fail("Command argument is required for select_asset.", lastReloadAtUtc);
                        }

                        var asset = AssetDatabase.LoadMainAssetAtPath(request.argument);
                        if (asset == null)
                        {
                            return Fail($"Asset was not found: {request.argument}", lastReloadAtUtc);
                        }

                        Selection.activeObject = asset;
                        EditorGUIUtility.PingObject(asset);
                        return Ok($"Selected asset: {request.argument}", lastReloadAtUtc);
                    case "ping_asset":
                        if (string.IsNullOrWhiteSpace(request.argument))
                        {
                            return Fail("Command argument is required for ping_asset.", lastReloadAtUtc);
                        }

                        var pingAsset = AssetDatabase.LoadMainAssetAtPath(request.argument);
                        if (pingAsset == null)
                        {
                            return Fail($"Asset was not found: {request.argument}", lastReloadAtUtc);
                        }

                        EditorGUIUtility.PingObject(pingAsset);
                        return Ok($"Pinged asset: {request.argument}", lastReloadAtUtc);
                    case "reveal_asset":
                        if (string.IsNullOrWhiteSpace(request.argument))
                        {
                            return Fail("Command argument is required for reveal_asset.", lastReloadAtUtc);
                        }

                        var revealAsset = AssetDatabase.LoadMainAssetAtPath(request.argument);
                        if (revealAsset == null)
                        {
                            return Fail($"Asset was not found: {request.argument}", lastReloadAtUtc);
                        }

                        EditorUtility.RevealInFinder(request.argument);
                        return Ok($"Revealed asset in Finder: {request.argument}", lastReloadAtUtc);
                    case "select_hierarchy_object":
                        if (string.IsNullOrWhiteSpace(request.argument))
                        {
                            return Fail("Command argument is required for select_hierarchy_object.", lastReloadAtUtc);
                        }

                        var gameObject = UnityBridgeStateBuilder.FindGameObjectByHierarchyPath(request.argument);
                        if (gameObject == null)
                        {
                            return Fail($"Hierarchy object was not found: {request.argument}", lastReloadAtUtc);
                        }

                        Selection.activeGameObject = gameObject;
                        EditorGUIUtility.PingObject(gameObject);
                        return Ok($"Selected hierarchy object: {request.argument}", lastReloadAtUtc);
                    case "execute_menu_item":
                        if (string.IsNullOrWhiteSpace(request.argument))
                        {
                            return Fail("Command argument is required for execute_menu_item.", lastReloadAtUtc);
                        }

                        if (!EditorApplication.ExecuteMenuItem(request.argument))
                        {
                            return Fail($"Menu item was not found or could not execute: {request.argument}", lastReloadAtUtc);
                        }

                        return Ok($"Executed menu item: {request.argument}", lastReloadAtUtc);
                    case "create_game_object":
                        return UnityBridgeMutations.CreateGameObject(request.argument, lastReloadAtUtc);
                    case "instantiate_prefab":
                        return UnityBridgeMutations.InstantiatePrefab(request.argument, lastReloadAtUtc);
                    case "duplicate_hierarchy_object":
                        return UnityBridgeMutations.DuplicateHierarchyObject(request.argument, lastReloadAtUtc);
                    case "delete_hierarchy_object":
                        return UnityBridgeMutations.DeleteHierarchyObject(request.argument, lastReloadAtUtc);
                    case "rename_hierarchy_object":
                        return UnityBridgeMutations.RenameHierarchyObject(request.argument, lastReloadAtUtc);
                    case "set_hierarchy_object_active":
                        return UnityBridgeMutations.SetHierarchyObjectActive(request.argument, lastReloadAtUtc);
                    case "set_hierarchy_metadata":
                        return UnityBridgeMutations.SetHierarchyMetadata(request.argument, lastReloadAtUtc);
                    case "set_hierarchy_parent":
                        return UnityBridgeMutations.SetHierarchyParent(request.argument, lastReloadAtUtc);
                    case "set_transform":
                        return UnityBridgeMutations.SetTransform(request.argument, lastReloadAtUtc);
                    case "add_component":
                        return UnityBridgeMutations.AddComponent(request.argument, lastReloadAtUtc);
                    case "remove_component":
                        return UnityBridgeMutations.RemoveComponent(request.argument, lastReloadAtUtc);
                    case "set_component_enabled":
                        return UnityBridgeMutations.SetComponentEnabled(request.argument, lastReloadAtUtc);
                    case "set_component_property":
                        return UnityBridgeMutations.SetComponentProperty(request.argument, lastReloadAtUtc);
                    case "invoke_component_method":
                        return UnityBridgeMutations.InvokeComponentMethod(request.argument, lastReloadAtUtc);
                    case "invoke_static_method":
                        return UnityBridgeMutations.InvokeStaticMethod(request.argument, lastReloadAtUtc);
                    case "capture_scene_view":
                        return UnityBridgeMutations.CaptureSceneView(request.argument, lastReloadAtUtc);
                    case "capture_game_view":
                        return UnityBridgeMutations.CaptureGameView(request.argument, lastReloadAtUtc);
                    case "capture_window":
                        return UnityBridgeMutations.CaptureWindow(request.argument, lastReloadAtUtc);
                    case "send_window_key_event":
                        return UnityBridgeMutations.SendWindowKeyEvent(request.argument, lastReloadAtUtc);
                    case "send_window_mouse_event":
                        return UnityBridgeMutations.SendWindowMouseEvent(request.argument, lastReloadAtUtc);
                    case "playerprefs_get":
                        var getResponse = UnityBridgeMutations.PlayerPrefsGet(request.argument);
                        return getResponse.hasKey
                            ? Ok(getResponse.message + " Key: " + getResponse.key, lastReloadAtUtc)
                            : Fail(getResponse.message, lastReloadAtUtc);
                    case "playerprefs_set":
                        return UnityBridgeMutations.PlayerPrefsSet(request.argument, lastReloadAtUtc);
                    case "playerprefs_delete_key":
                        return UnityBridgeMutations.PlayerPrefsDeleteKey(request.argument, lastReloadAtUtc);
                    case "playerprefs_delete_all":
                        return UnityBridgeMutations.PlayerPrefsDeleteAll(lastReloadAtUtc);
                    case "create_folder":
                        return UnityBridgeMutations.CreateFolder(request.argument, lastReloadAtUtc);
                    case "delete_asset":
                        return UnityBridgeMutations.DeleteAsset(request.argument, lastReloadAtUtc);
                    case "move_asset":
                        return UnityBridgeMutations.MoveAsset(request.argument, lastReloadAtUtc);
                    case "rename_asset":
                        return UnityBridgeMutations.RenameAsset(request.argument, lastReloadAtUtc);
                    case "duplicate_asset":
                        return UnityBridgeMutations.DuplicateAsset(request.argument, lastReloadAtUtc);
                    case "set_texture_importer":
                        var textureImporterResponse = UnityBridgeMutations.SetTextureImporter(request.argument);
                        return textureImporterResponse.found
                            ? Ok(textureImporterResponse.message, lastReloadAtUtc)
                            : Fail(textureImporterResponse.message, lastReloadAtUtc);
                    case "undo":
                        Undo.PerformUndo();
                        return Ok("Performed Undo.", lastReloadAtUtc);
                    case "redo":
                        Undo.PerformRedo();
                        return Ok("Performed Redo.", lastReloadAtUtc);
                    default:
                        UnityBridgeCustomCommandResult customResult;
                        if (UnityBridgeCommandRegistry.TryExecute(request.name, request.argument, out customResult))
                        {
                            if (customResult == null)
                            {
                                return Ok($"Executed custom command: {request.name}", lastReloadAtUtc);
                            }

                            return customResult.ok
                                ? Ok(customResult.message ?? $"Executed custom command: {request.name}", lastReloadAtUtc)
                                : Fail(customResult.message ?? $"Custom command failed: {request.name}", lastReloadAtUtc);
                        }

                        return Fail($"Unknown command: {request.name}", lastReloadAtUtc);
                }
            }
            catch (Exception exception)
            {
                return Fail(exception.Message, lastReloadAtUtc);
            }
        }

        private static UnityBridgeCommandDescriptor BuiltIn(string name, string description, string argumentHint = "")
        {
            return new UnityBridgeCommandDescriptor
            {
                name = name,
                description = description,
                argumentHint = argumentHint,
                source = "builtin"
            };
        }

        private static void ClearConsole()
        {
            var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor.dll");
            if (logEntriesType == null)
            {
                throw new InvalidOperationException("UnityEditor.LogEntries type was not found.");
            }

            var clearMethod = logEntriesType.GetMethod("Clear", BindingFlags.Public | BindingFlags.Static);
            if (clearMethod == null)
            {
                throw new InvalidOperationException("UnityEditor.LogEntries.Clear was not found.");
            }

            clearMethod.Invoke(null, null);
        }

        private static void FocusGameViewWindow()
        {
            var gameWindow = UnityBridgeStateBuilder.FindEditorWindow("UnityEditor.GameView", "Game");
            if (gameWindow == null)
            {
                return;
            }

            gameWindow.Show();
            gameWindow.Focus();
        }

        private static UnityBridgeCommandResponse Ok(string message, string lastReloadAtUtc)
        {
            return new UnityBridgeCommandResponse
            {
                ok = true,
                message = message,
                state = UnityBridgeStateBuilder.BuildState(lastReloadAtUtc)
            };
        }

        private static UnityBridgeCommandResponse Fail(string message, string lastReloadAtUtc)
        {
            return new UnityBridgeCommandResponse
            {
                ok = false,
                message = message,
                state = UnityBridgeStateBuilder.BuildState(lastReloadAtUtc)
            };
        }

        private static bool RequiresConfirmation(string commandName)
        {
            switch (commandName)
            {
                case "switch_build_target":
                case "set_build_scenes":
                case "remove_build_scene":
                case "remove_package":
                case "execute_menu_item":
                case "delete_hierarchy_object":
                case "rename_hierarchy_object":
                case "remove_component":
                case "playerprefs_delete_key":
                case "playerprefs_delete_all":
                case "delete_asset":
                case "move_asset":
                case "rename_asset":
                    return true;
                default:
                    return false;
            }
        }

        private static bool HasRequiredConfirmation(string confirm)
        {
            return string.Equals(confirm, RiskyActionConfirmation, StringComparison.Ordinal);
        }
    }
}
