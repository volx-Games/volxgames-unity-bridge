#!/usr/bin/env node

const fs = require("fs");
const os = require("os");
const path = require("path");

const { stdin, stdout, stderr, env } = process;

const explicitBridgeBaseUrl = env.UNITY_BRIDGE_URL ? normalizeBaseUrl(env.UNITY_BRIDGE_URL) : "";
const bridgeProjectPath = env.UNITY_BRIDGE_PROJECT_PATH ? normalizePath(env.UNITY_BRIDGE_PROJECT_PATH) : "";
let resolvedBridgeBaseUrl = "";
const serverInfo = {
  name: "unity-mcp-server",
  version: "0.1.3",
};
const BridgeReadRetryCount = 6;
const BridgeReadRetryBaseDelayMs = 400;
const BridgeDiscoveryProbeTimeoutMs = 750;
const RISKY_CONFIRMATION_TEXT = "I understand this will modify the Unity project.";
const riskyCommandNames = new Set([
  "switch_build_target",
  "set_build_scenes",
  "remove_build_scene",
  "remove_package",
  "execute_menu_item",
  "delete_hierarchy_object",
  "rename_hierarchy_object",
  "remove_component",
  "playerprefs_delete_key",
  "playerprefs_delete_all",
  "delete_asset",
  "move_asset",
  "rename_asset",
]);
const riskyToolNames = new Set(Array.from(riskyCommandNames, (name) => `unity_${name}`));

let inputBuffer = "";

stdin.setEncoding("utf8");
stdin.on("data", (chunk) => {
  inputBuffer += chunk;
  flushLines();
});

stdin.on("end", () => {
  flushLines(true);
});

function flushLines(flushRemainder = false) {
  while (true) {
    const newlineIndex = inputBuffer.indexOf("\n");
    if (newlineIndex === -1) {
      if (flushRemainder && inputBuffer.trim()) {
        void handleMessage(inputBuffer.trim());
        inputBuffer = "";
      }

      return;
    }

    const line = inputBuffer.slice(0, newlineIndex).trim();
    inputBuffer = inputBuffer.slice(newlineIndex + 1);
    if (!line) {
      continue;
    }

    void handleMessage(line);
  }
}

async function handleMessage(line) {
  let message;
  try {
    message = JSON.parse(line);
  } catch (error) {
    writeError(null, -32700, `Invalid JSON: ${error.message}`);
    return;
  }

  if (message.method === "initialize") {
    writeResult(message.id, {
      protocolVersion: "2024-11-05",
      capabilities: {
        tools: {},
      },
      serverInfo,
    });
    return;
  }

  if (message.method === "notifications/initialized") {
    return;
  }

  if (message.method === "tools/list") {
    writeResult(message.id, {
      tools: [
        tool("unity_health", "Read Unity bridge health information.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_state", "Read high-level Unity editor state.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_project", "Read Unity project metadata including tags, layers, and build scenes.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_runtime", "Read runtime and play-mode telemetry such as frame count, time scale, and target frame rate.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_query_runtime_objects", "Query live runtime GameObjects by name, path, scene, tag, or component type.", {
          type: "object",
          properties: {
            query: { type: "string" },
            scenePath: { type: "string" },
            tag: { type: "string" },
            componentType: { type: "string" },
            includeInactive: { type: "boolean" },
            limit: { type: "number" },
          },
          additionalProperties: false,
        }),
        tool("unity_inspect_runtime_object", "Read detailed runtime information for a GameObject by hierarchy path.", {
          type: "object",
          properties: {
            path: { type: "string" },
          },
          required: ["path"],
          additionalProperties: false,
        }),
        tool("unity_build", "Read Unity build target and build setting summary.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_player_settings", "Read active-target Unity player settings such as bundle id and scripting define symbols.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_audio_settings", "Read project audio settings from AudioManager.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_time_settings", "Read project time settings from TimeManager.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_quality_settings", "Read project quality settings and quality levels.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_project_tags_layers", "Read project tags, user layers, sorting layers, and rendering layers.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_physics_settings", "Read global Unity 3D and 2D physics project settings.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_set_player_settings", "Update active-target Unity player settings such as bundle id and scripting define symbols.", {
          type: "object",
          properties: {
            buildTarget: { type: "string" },
            buildTargetGroup: { type: "string" },
            setApplicationIdentifier: { type: "boolean" },
            applicationIdentifier: { type: "string" },
            setScriptingBackend: { type: "boolean" },
            scriptingBackend: { type: "string" },
            setApiCompatibilityLevel: { type: "boolean" },
            apiCompatibilityLevel: { type: "string" },
            setManagedStrippingLevel: { type: "boolean" },
            managedStrippingLevel: { type: "string" },
            setIncrementalIl2CppBuild: { type: "boolean" },
            incrementalIl2CppBuild: { type: "boolean" },
            setCaptureStartupLogs: { type: "boolean" },
            captureStartupLogs: { type: "boolean" },
            setScriptingDefineSymbols: { type: "boolean" },
            scriptingDefineSymbols: { type: "string" },
            scriptingDefineSymbolList: {
              type: "array",
              items: { type: "string" }
            }
          },
          additionalProperties: false,
        }),
        tool("unity_set_audio_settings", "Update project audio settings from AudioManager.", {
          type: "object",
          properties: {
            setVolume: { type: "boolean" },
            volume: { type: "number" },
            setRolloffScale: { type: "boolean" },
            rolloffScale: { type: "number" },
            setDopplerFactor: { type: "boolean" },
            dopplerFactor: { type: "number" },
            setDefaultSpeakerMode: { type: "boolean" },
            defaultSpeakerMode: { type: "number" },
            setSampleRate: { type: "boolean" },
            sampleRate: { type: "number" },
            setDspBufferSize: { type: "boolean" },
            dspBufferSize: { type: "number" },
            setVirtualVoiceCount: { type: "boolean" },
            virtualVoiceCount: { type: "number" },
            setRealVoiceCount: { type: "boolean" },
            realVoiceCount: { type: "number" },
            setSpatializerPlugin: { type: "boolean" },
            spatializerPlugin: { type: "string" },
            setAmbisonicDecoderPlugin: { type: "boolean" },
            ambisonicDecoderPlugin: { type: "string" },
            setDisableAudio: { type: "boolean" },
            disableAudio: { type: "boolean" },
            setVirtualizeEffects: { type: "boolean" },
            virtualizeEffects: { type: "boolean" },
            setRequestedDspBufferSize: { type: "boolean" },
            requestedDspBufferSize: { type: "number" },
          },
          additionalProperties: false,
        }),
        tool("unity_set_time_settings", "Update project time settings from TimeManager.", {
          type: "object",
          properties: {
            setFixedTimestep: { type: "boolean" },
            fixedTimestep: { type: "number" },
            setMaximumAllowedTimestep: { type: "boolean" },
            maximumAllowedTimestep: { type: "number" },
            setTimeScale: { type: "boolean" },
            timeScale: { type: "number" },
            setMaximumParticleTimestep: { type: "boolean" },
            maximumParticleTimestep: { type: "number" },
          },
          additionalProperties: false,
        }),
        tool("unity_set_quality_settings", "Update project quality settings and per-level values.", {
          type: "object",
          properties: {
            setCurrentQualityIndex: { type: "boolean" },
            currentQualityIndex: { type: "number" },
            qualityLevels: {
              type: "array",
              items: {
                type: "object",
                properties: {
                  index: { type: "number" },
                  setName: { type: "boolean" },
                  name: { type: "string" },
                  setPixelLightCount: { type: "boolean" },
                  pixelLightCount: { type: "number" },
                  setShadows: { type: "boolean" },
                  shadows: { type: "number" },
                  setShadowResolution: { type: "boolean" },
                  shadowResolution: { type: "number" },
                  setShadowProjection: { type: "boolean" },
                  shadowProjection: { type: "number" },
                  setShadowCascades: { type: "boolean" },
                  shadowCascades: { type: "number" },
                  setShadowDistance: { type: "boolean" },
                  shadowDistance: { type: "number" },
                  setGlobalTextureMipmapLimit: { type: "boolean" },
                  globalTextureMipmapLimit: { type: "number" },
                  setAnisotropicTextures: { type: "boolean" },
                  anisotropicTextures: { type: "number" },
                  setAntiAliasing: { type: "boolean" },
                  antiAliasing: { type: "number" },
                  setVSyncCount: { type: "boolean" },
                  vSyncCount: { type: "number" },
                  setLodBias: { type: "boolean" },
                  lodBias: { type: "number" },
                  setMaximumLODLevel: { type: "boolean" },
                  maximumLODLevel: { type: "number" },
                  setStreamingMipmapsActive: { type: "boolean" },
                  streamingMipmapsActive: { type: "boolean" },
                  setStreamingMipmapsMemoryBudget: { type: "boolean" },
                  streamingMipmapsMemoryBudget: { type: "number" },
                },
                required: ["index"],
                additionalProperties: false,
              },
            },
            perPlatformDefaults: {
              type: "array",
              items: {
                type: "object",
                properties: {
                  platform: { type: "string" },
                  qualityIndex: { type: "number" },
                },
                required: ["platform", "qualityIndex"],
                additionalProperties: false,
              },
            },
          },
          additionalProperties: false,
        }),
        tool("unity_set_project_tags_layers", "Update project tags, user layers, sorting layers, and rendering layers.", {
          type: "object",
          properties: {
            addTags: {
              type: "array",
              items: { type: "string" },
            },
            removeTags: {
              type: "array",
              items: { type: "string" },
            },
            setLayers: {
              type: "array",
              items: {
                type: "object",
                properties: {
                  index: { type: "number" },
                  name: { type: "string" },
                },
                required: ["index"],
                additionalProperties: false,
              },
            },
            addSortingLayers: {
              type: "array",
              items: { type: "string" },
            },
            renameSortingLayers: {
              type: "array",
              items: {
                type: "object",
                properties: {
                  name: { type: "string" },
                  uniqueId: { type: "number" },
                  newName: { type: "string" },
                },
                required: ["newName"],
                additionalProperties: false,
              },
            },
            removeSortingLayers: {
              type: "array",
              items: { type: "string" },
            },
            setRenderingLayers: {
              type: "array",
              items: {
                type: "object",
                properties: {
                  index: { type: "number" },
                  name: { type: "string" },
                },
                required: ["index"],
                additionalProperties: false,
              },
            },
          },
          additionalProperties: false,
        }),
        tool("unity_set_physics_settings", "Update global Unity 3D and 2D physics project settings.", {
          type: "object",
          properties: {
            physics3D: {
              type: "object",
              properties: {
                setGravity: { type: "boolean" },
                gravity: {
                  type: "object",
                  properties: {
                    x: { type: "number" },
                    y: { type: "number" },
                    z: { type: "number" },
                  },
                  additionalProperties: false,
                },
                setBounceThreshold: { type: "boolean" },
                bounceThreshold: { type: "number" },
                setDefaultMaxDepenetrationVelocity: { type: "boolean" },
                defaultMaxDepenetrationVelocity: { type: "number" },
                setSleepThreshold: { type: "boolean" },
                sleepThreshold: { type: "number" },
                setDefaultContactOffset: { type: "boolean" },
                defaultContactOffset: { type: "number" },
                setDefaultSolverIterations: { type: "boolean" },
                defaultSolverIterations: { type: "number" },
                setDefaultSolverVelocityIterations: { type: "boolean" },
                defaultSolverVelocityIterations: { type: "number" },
                setQueriesHitBackfaces: { type: "boolean" },
                queriesHitBackfaces: { type: "boolean" },
                setQueriesHitTriggers: { type: "boolean" },
                queriesHitTriggers: { type: "boolean" },
                setEnableAdaptiveForce: { type: "boolean" },
                enableAdaptiveForce: { type: "boolean" },
                setSimulationMode: { type: "boolean" },
                simulationMode: { type: "string" },
                setAutoSyncTransforms: { type: "boolean" },
                autoSyncTransforms: { type: "boolean" },
                setReuseCollisionCallbacks: { type: "boolean" },
                reuseCollisionCallbacks: { type: "boolean" },
                setInvokeCollisionCallbacks: { type: "boolean" },
                invokeCollisionCallbacks: { type: "boolean" },
                setContactPairsMode: { type: "boolean" },
                contactPairsMode: { type: "string" },
                setBroadphaseType: { type: "boolean" },
                broadphaseType: { type: "string" },
                setFrictionType: { type: "boolean" },
                frictionType: { type: "string" },
                setEnableEnhancedDeterminism: { type: "boolean" },
                enableEnhancedDeterminism: { type: "boolean" },
                setImprovedPatchFriction: { type: "boolean" },
                improvedPatchFriction: { type: "boolean" },
                setGenerateOnTriggerStayEvents: { type: "boolean" },
                generateOnTriggerStayEvents: { type: "boolean" },
                setSolverType: { type: "boolean" },
                solverType: { type: "string" },
                setDefaultMaxAngularSpeed: { type: "boolean" },
                defaultMaxAngularSpeed: { type: "number" },
                setFastMotionThreshold: { type: "boolean" },
                fastMotionThreshold: { type: "number" },
                setIncrementalStaticBroadphase: { type: "boolean" },
                incrementalStaticBroadphase: { type: "boolean" },
              },
              additionalProperties: false,
            },
            physics2D: {
              type: "object",
              properties: {
                setGravity: { type: "boolean" },
                gravity: {
                  type: "object",
                  properties: {
                    x: { type: "number" },
                    y: { type: "number" },
                  },
                  additionalProperties: false,
                },
                setVelocityIterations: { type: "boolean" },
                velocityIterations: { type: "number" },
                setPositionIterations: { type: "boolean" },
                positionIterations: { type: "number" },
                setBounceThreshold: { type: "boolean" },
                bounceThreshold: { type: "number" },
                setMaxLinearCorrection: { type: "boolean" },
                maxLinearCorrection: { type: "number" },
                setMaxAngularCorrection: { type: "boolean" },
                maxAngularCorrection: { type: "number" },
                setMaxTranslationSpeed: { type: "boolean" },
                maxTranslationSpeed: { type: "number" },
                setMaxRotationSpeed: { type: "boolean" },
                maxRotationSpeed: { type: "number" },
                setBaumgarteScale: { type: "boolean" },
                baumgarteScale: { type: "number" },
                setBaumgarteTimeOfImpactScale: { type: "boolean" },
                baumgarteTimeOfImpactScale: { type: "number" },
                setTimeToSleep: { type: "boolean" },
                timeToSleep: { type: "number" },
                setLinearSleepTolerance: { type: "boolean" },
                linearSleepTolerance: { type: "number" },
                setAngularSleepTolerance: { type: "boolean" },
                angularSleepTolerance: { type: "number" },
                setDefaultContactOffset: { type: "boolean" },
                defaultContactOffset: { type: "number" },
                setSimulationMode: { type: "boolean" },
                simulationMode: { type: "string" },
                setMaxSubStepCount: { type: "boolean" },
                maxSubStepCount: { type: "number" },
                setMinSubStepFPS: { type: "boolean" },
                minSubStepFPS: { type: "number" },
                setUseSubStepping: { type: "boolean" },
                useSubStepping: { type: "boolean" },
                setUseSubStepContacts: { type: "boolean" },
                useSubStepContacts: { type: "boolean" },
                setQueriesHitTriggers: { type: "boolean" },
                queriesHitTriggers: { type: "boolean" },
                setQueriesStartInColliders: { type: "boolean" },
                queriesStartInColliders: { type: "boolean" },
                setCallbacksOnDisable: { type: "boolean" },
                callbacksOnDisable: { type: "boolean" },
                setReuseCollisionCallbacks: { type: "boolean" },
                reuseCollisionCallbacks: { type: "boolean" },
                setAutoSyncTransforms: { type: "boolean" },
                autoSyncTransforms: { type: "boolean" },
              },
              additionalProperties: false,
            },
          },
          additionalProperties: false,
        }),
        tool("unity_set_build_settings", "Update active-target Unity build flags and build output location.", {
          type: "object",
          properties: {
            setDevelopmentBuild: { type: "boolean" },
            developmentBuild: { type: "boolean" },
            setConnectProfiler: { type: "boolean" },
            connectProfiler: { type: "boolean" },
            setAllowDebugging: { type: "boolean" },
            allowDebugging: { type: "boolean" },
            setBuildAppBundle: { type: "boolean" },
            buildAppBundle: { type: "boolean" },
            locationPathName: { type: "string" }
          },
          additionalProperties: false,
        }),
        riskyTool("unity_switch_build_target", "Switch the active Unity build target.", {
          type: "object",
          properties: {
            buildTarget: { type: "string" },
            buildTargetGroup: { type: "string" }
          },
          required: ["buildTarget"],
          additionalProperties: false,
        }),
        tool("unity_build_scenes", "Read the Unity build scene list.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_last_build_report", "Read the most recent Unity player build report captured by the bridge.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_build_player", "Run a Unity player build and return its report summary.", {
          type: "object",
          properties: {
            buildTarget: { type: "string" },
            locationPathName: { type: "string" },
            scenes: {
              type: "array",
              items: { type: "string" },
            },
            development: { type: "boolean" },
            connectProfiler: { type: "boolean" },
            allowDebugging: { type: "boolean" },
            buildAppBundle: { type: "boolean" },
            autoRunPlayer: { type: "boolean" },
            buildScriptsOnly: { type: "boolean" },
            showBuiltPlayer: { type: "boolean" },
            strictMode: { type: "boolean" },
            acceptExternalModificationsToPlayer: { type: "boolean" },
            compressWithLz4: { type: "boolean" },
            compressWithLz4HC: { type: "boolean" },
          },
          additionalProperties: false,
        }),
        riskyTool("unity_set_build_scenes", "Replace the Unity build scene list.", {
          type: "object",
          properties: {
            scenes: {
              type: "array",
              items: {
                type: "object",
                properties: {
                  path: { type: "string" },
                  enabled: { type: "boolean" }
                },
                required: ["path"],
                additionalProperties: false
              }
            }
          },
          required: ["scenes"],
          additionalProperties: false,
        }),
        tool("unity_add_build_scene", "Add or move a scene in the Unity build scene list.", {
          type: "object",
          properties: {
            path: { type: "string" },
            enabled: { type: "boolean" },
            buildIndex: { type: "number" }
          },
          required: ["path"],
          additionalProperties: false,
        }),
        riskyTool("unity_remove_build_scene", "Remove a scene from the Unity build scene list.", {
          type: "object",
          properties: {
            path: { type: "string" }
          },
          required: ["path"],
          additionalProperties: false,
        }),
        tool("unity_tests", "Read the current or most recent Unity test run summary.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_last_test_report", "Read the most recent completed Unity test report.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_run_tests", "Start a Unity test run through the Test Runner API.", {
          type: "object",
          properties: {
            testMode: { type: "string" },
            runSynchronously: { type: "boolean" },
            resultFilePath: { type: "string" },
            testNames: {
              type: "array",
              items: { type: "string" },
            },
            groupNames: {
              type: "array",
              items: { type: "string" },
            },
            categoryNames: {
              type: "array",
              items: { type: "string" },
            },
            assemblyNames: {
              type: "array",
              items: { type: "string" },
            },
          },
          additionalProperties: false,
        }),
        tool("unity_compilation", "Read the current or most recent Unity compilation summary.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_last_compilation", "Read the most recent completed Unity compilation summary.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_editor_windows", "List currently loaded editor windows.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_inspector_selection", "Read the current Inspector window state, active object, and selected property path.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_console_selection", "Read the current Console window state and selected log entry.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_current_context", "Read the current Unity editor context, including state, focused window, inspector, console, and active object.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_packages", "List registered Unity packages for the project.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_package_operations", "Read the current or most recent Unity package manager operation.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_last_package_operation", "Read the most recent completed Unity package manager operation.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_add_package", "Add a Unity package by name, version, git URL, or file path.", {
          type: "object",
          properties: {
            packageId: { type: "string" },
          },
          required: ["packageId"],
          additionalProperties: false,
        }),
        riskyTool("unity_remove_package", "Remove a direct dependency Unity package by name.", {
          type: "object",
          properties: {
            packageId: { type: "string" },
          },
          required: ["packageId"],
          additionalProperties: false,
        }),
        tool("unity_embed_package", "Embed an installed Unity package into the Packages directory.", {
          type: "object",
          properties: {
            packageId: { type: "string" },
          },
          required: ["packageId"],
          additionalProperties: false,
        }),
        tool("unity_resolve_packages", "Resolve pending Unity package manifest changes.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_scene_stats", "Read scene-level object and component counts for loaded scenes.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_prefab_stage", "Read the currently open prefab stage, if any.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_selection", "Read current Unity selection.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_active_object", "Read details for the active Unity selection.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_scenes", "List scene assets in the project.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_commands", "List available built-in and custom Unity bridge commands.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_hierarchy", "Read the loaded scene hierarchy snapshot.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_inspect_hierarchy_object", "Read details for a GameObject by hierarchy path.", {
          type: "object",
          properties: {
            path: {
              type: "string",
              description: "Hierarchy path like Root/Child/Subchild.",
            },
          },
          required: ["path"],
          additionalProperties: false,
        }),
        tool("unity_inspect_asset", "Read details for an asset by Unity asset path.", {
          type: "object",
          properties: {
            path: {
              type: "string",
              description: "Unity asset path like Assets/Scenes/GameScene.unity.",
            },
          },
          required: ["path"],
          additionalProperties: false,
        }),
        tool("unity_asset_dependencies", "Read asset dependencies for a Unity asset path.", {
          type: "object",
          properties: {
            path: {
              type: "string",
              description: "Unity asset path like Assets/Scenes/GameScene.unity.",
            },
            recursive: {
              type: "boolean",
              description: "Include transitive dependencies when true.",
            },
          },
          required: ["path"],
          additionalProperties: false,
        }),
        tool("unity_resolve_asset", "Resolve a Unity asset path to GUID or a GUID to asset path.", {
          type: "object",
          properties: {
            path: {
              type: "string",
              description: "Optional Unity asset path.",
            },
            guid: {
              type: "string",
              description: "Optional Unity asset GUID.",
            },
          },
          additionalProperties: false,
        }),
        tool("unity_search_assets", "Search project assets by query and optional type.", {
          type: "object",
          properties: {
            query: {
              type: "string",
              description: "AssetDatabase search string, for example Player or l:Label.",
            },
            type: {
              type: "string",
              description: "Optional Unity asset type, for example Scene, Prefab, Material.",
            },
          },
          additionalProperties: false,
        }),
        tool("unity_search_hierarchy", "Search loaded hierarchy objects by name, path, tag, or component type.", {
          type: "object",
          properties: {
            query: {
              type: "string",
              description: "Optional name or hierarchy path substring.",
            },
            scenePath: {
              type: "string",
              description: "Optional scene path filter like Assets/Scenes/GameScene.unity.",
            },
            tag: {
              type: "string",
              description: "Optional exact Unity tag filter.",
            },
            componentType: {
              type: "string",
              description: "Optional component type filter like SpriteRenderer or Rigidbody2D.",
            },
            includeInactive: {
              type: "boolean",
              description: "Include inactive objects when true.",
            },
          },
          additionalProperties: false,
        }),
        tool("unity_logs", "Read recent Unity console entries captured by the bridge.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_query_logs", "Query recent Unity logs by text, level, and limit.", {
          type: "object",
          properties: {
            query: {
              type: "string",
              description: "Optional text filter applied to message and stack trace.",
            },
            level: {
              type: "string",
              description: "Optional log level filter such as Log, Warning, Error, Exception.",
            },
            limit: {
              type: "number",
              description: "Maximum number of entries to return.",
            },
          },
          additionalProperties: false,
        }),
        tool("unity_events", "Read recent Unity bridge event history.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_query_events", "Query recent Unity bridge events by text, type, and limit.", {
          type: "object",
          properties: {
            query: {
              type: "string",
              description: "Optional text filter applied to event message and context.",
            },
            type: {
              type: "string",
              description: "Optional event type filter such as selection, hierarchy, playmode, compilation.",
            },
            limit: {
              type: "number",
              description: "Maximum number of entries to return.",
            },
          },
          additionalProperties: false,
        }),
        tool("unity_play", "Enter play mode.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_play_and_focus_game", "Enter play mode and focus the Unity Game view.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_pause", "Pause the editor.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_resume", "Resume the editor from pause.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_step", "Advance one frame while paused in play mode.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_stop", "Stop play mode.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_set_time_scale", "Set Unity Time.timeScale.", {
          type: "object",
          properties: {
            value: { type: "number" },
          },
          required: ["value"],
          additionalProperties: false,
        }),
        tool("unity_set_target_frame_rate", "Set Application.targetFrameRate.", {
          type: "object",
          properties: {
            value: { type: "number" },
          },
          required: ["value"],
          additionalProperties: false,
        }),
        tool("unity_refresh_assets", "Refresh the AssetDatabase.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_save_assets", "Save dirty assets.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_save_open_scenes", "Save currently open scenes.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_save_scene", "Save a specific open scene by path.", {
          type: "object",
          properties: {
            path: {
              type: "string",
              description: "Path of an already open scene.",
            },
          },
          required: ["path"],
          additionalProperties: false,
        }),
        tool("unity_close_scene", "Close a specific open scene by path.", {
          type: "object",
          properties: {
            path: {
              type: "string",
              description: "Path of an already open scene.",
            },
          },
          required: ["path"],
          additionalProperties: false,
        }),
        tool("unity_clear_console", "Clear the Unity console.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_open_scene", "Open a scene by Unity asset path.", {
          type: "object",
          properties: {
            path: {
              type: "string",
              description: "Unity scene path like Assets/Scenes/GameScene.unity.",
            },
          },
          required: ["path"],
          additionalProperties: false,
        }),
        tool("unity_open_scene_additive", "Open a scene additively by Unity asset path.", {
          type: "object",
          properties: {
            path: {
              type: "string",
              description: "Unity scene path like Assets/Scenes/GameScene.unity.",
            },
          },
          required: ["path"],
          additionalProperties: false,
        }),
        tool("unity_set_active_scene", "Set an already open scene as the active scene.", {
          type: "object",
          properties: {
            path: {
              type: "string",
              description: "Path of an already open scene.",
            },
          },
          required: ["path"],
          additionalProperties: false,
        }),
        tool("unity_open_prefab_stage", "Open a prefab asset in Prefab Mode.", {
          type: "object",
          properties: {
            path: {
              type: "string",
              description: "Unity prefab asset path like Assets/Prefabs/Foo.prefab.",
            },
          },
          required: ["path"],
          additionalProperties: false,
        }),
        tool("unity_close_prefab_stage", "Return from Prefab Mode to the main stage.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_open_asset", "Open an asset using the Unity editor.", {
          type: "object",
          properties: {
            path: {
              type: "string",
              description: "Unity asset path like Assets/Textures/Foo.png.",
            },
          },
          required: ["path"],
          additionalProperties: false,
        }),
        tool("unity_frame_selected", "Frame the current selection in the active Scene view.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_focus_project_window", "Focus the Unity Project window.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_focus_scene_view", "Focus the Unity Scene view.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_focus_window", "Focus an editor window by type or title.", {
          type: "object",
          properties: {
            type: {
              type: "string",
              description: "Optional window type name such as SceneView or UnityEditor.ProjectBrowser.",
            },
            title: {
              type: "string",
              description: "Optional exact window title such as Console or Project.",
            },
          },
          additionalProperties: false,
        }),
        tool("unity_select_asset", "Select an asset by Unity asset path.", {
          type: "object",
          properties: {
            path: {
              type: "string",
              description: "Unity asset path like Assets/Prefabs/Foo.prefab.",
            },
          },
          required: ["path"],
          additionalProperties: false,
        }),
        tool("unity_ping_asset", "Ping an asset in the Unity editor.", {
          type: "object",
          properties: {
            path: {
              type: "string",
              description: "Unity asset path like Assets/Prefabs/Foo.prefab.",
            },
          },
          required: ["path"],
          additionalProperties: false,
        }),
        tool("unity_reveal_asset", "Reveal an asset in Finder.", {
          type: "object",
          properties: {
            path: {
              type: "string",
              description: "Unity asset path like Assets/Prefabs/Foo.prefab.",
            },
          },
          required: ["path"],
          additionalProperties: false,
        }),
        tool("unity_select_hierarchy_object", "Select a GameObject by hierarchy path.", {
          type: "object",
          properties: {
            path: {
              type: "string",
              description: "Hierarchy path like Root/Child/Subchild.",
            },
          },
          required: ["path"],
          additionalProperties: false,
        }),
        riskyTool("unity_execute_menu_item", "Execute a Unity menu item.", {
          type: "object",
          properties: {
            path: {
              type: "string",
              description: "Unity menu item path like File/Save.",
            },
          },
          required: ["path"],
          additionalProperties: false,
        }),
        tool("unity_create_game_object", "Create a GameObject or primitive in the loaded hierarchy.", {
          type: "object",
          properties: {
            name: { type: "string" },
            parentPath: { type: "string" },
            scenePath: { type: "string" },
            primitiveType: { type: "string" },
            tag: { type: "string" },
            layer: { type: "number" },
            active: { type: "boolean" },
          },
          required: ["name"],
          additionalProperties: false,
        }),
        tool("unity_instantiate_prefab", "Instantiate a prefab asset into the loaded hierarchy.", {
          type: "object",
          properties: {
            assetPath: { type: "string" },
            parentPath: { type: "string" },
            scenePath: { type: "string" },
            name: { type: "string" },
            active: { type: "boolean" },
          },
          required: ["assetPath"],
          additionalProperties: false,
        }),
        tool("unity_duplicate_hierarchy_object", "Duplicate a hierarchy object.", {
          type: "object",
          properties: {
            path: { type: "string" },
          },
          required: ["path"],
          additionalProperties: false,
        }),
        riskyTool("unity_delete_hierarchy_object", "Delete a hierarchy object.", {
          type: "object",
          properties: {
            path: { type: "string" },
          },
          required: ["path"],
          additionalProperties: false,
        }),
        riskyTool("unity_rename_hierarchy_object", "Rename a hierarchy object.", {
          type: "object",
          properties: {
            path: { type: "string" },
            name: { type: "string" },
          },
          required: ["path", "name"],
          additionalProperties: false,
        }),
        tool("unity_set_hierarchy_object_active", "Set a hierarchy object's active state.", {
          type: "object",
          properties: {
            path: { type: "string" },
            active: { type: "boolean" },
          },
          required: ["path", "active"],
          additionalProperties: false,
        }),
        tool("unity_set_hierarchy_metadata", "Update hierarchy name, tag, layer, or static state.", {
          type: "object",
          properties: {
            path: { type: "string" },
            name: { type: "string" },
            tag: { type: "string" },
            layer: { type: "number" },
            setStaticFlags: { type: "boolean" },
            staticFlags: { type: "boolean" },
          },
          required: ["path"],
          additionalProperties: false,
        }),
        tool("unity_set_hierarchy_parent", "Reparent a hierarchy object.", {
          type: "object",
          properties: {
            path: { type: "string" },
            parentPath: { type: "string" },
            worldPositionStays: { type: "boolean" },
          },
          required: ["path"],
          additionalProperties: false,
        }),
        tool("unity_set_transform", "Set local transform values on a hierarchy object.", {
          type: "object",
          properties: {
            path: { type: "string" },
            setLocalPosition: { type: "boolean" },
            setLocalEulerAngles: { type: "boolean" },
            setLocalScale: { type: "boolean" },
            localPosition: vector3Schema(),
            localEulerAngles: vector3Schema(),
            localScale: vector3Schema(),
          },
          required: ["path"],
          additionalProperties: false,
        }),
        tool("unity_add_component", "Add a component to a hierarchy object.", {
          type: "object",
          properties: {
            path: { type: "string" },
            componentType: { type: "string" },
          },
          required: ["path", "componentType"],
          additionalProperties: false,
        }),
        riskyTool("unity_remove_component", "Remove a component from a hierarchy object.", {
          type: "object",
          properties: {
            path: { type: "string" },
            componentType: { type: "string" },
            componentIndex: { type: "number" },
          },
          required: ["path", "componentType"],
          additionalProperties: false,
        }),
        tool("unity_set_component_enabled", "Enable or disable a component.", {
          type: "object",
          properties: {
            path: { type: "string" },
            componentType: { type: "string" },
            componentIndex: { type: "number" },
            enabled: { type: "boolean" },
          },
          required: ["path", "componentType", "enabled"],
          additionalProperties: false,
        }),
        tool("unity_set_component_property", "Write a serialized component property.", {
          type: "object",
          properties: {
            path: { type: "string" },
            componentType: { type: "string" },
            componentIndex: { type: "number" },
            propertyPath: { type: "string" },
            valueType: { type: "string" },
            boolValue: { type: "boolean" },
            intValue: { type: "number" },
            floatValue: { type: "number" },
            stringValue: { type: "string" },
            objectReferencePath: { type: "string" },
            vector2Value: vector2Schema(),
            vector3Value: vector3Schema(),
            vector4Value: vector4Schema(),
            colorValue: colorSchema(),
          },
          required: ["path", "componentType", "propertyPath", "valueType"],
          additionalProperties: false,
        }),
        tool("unity_invoke_component_method", "Invoke a component method with zero or one primitive argument.", {
          type: "object",
          properties: {
            path: { type: "string" },
            componentType: { type: "string" },
            componentIndex: { type: "number" },
            methodName: { type: "string" },
            argumentType: { type: "string" },
            boolValue: { type: "boolean" },
            intValue: { type: "number" },
            floatValue: { type: "number" },
            stringValue: { type: "string" },
          },
          required: ["path", "componentType", "methodName"],
          additionalProperties: false,
        }),
        tool("unity_invoke_static_method", "Invoke a static method with zero or one primitive argument.", {
          type: "object",
          properties: {
            typeName: { type: "string" },
            methodName: { type: "string" },
            argumentType: { type: "string" },
            boolValue: { type: "boolean" },
            intValue: { type: "number" },
            floatValue: { type: "number" },
            stringValue: { type: "string" },
          },
          required: ["typeName", "methodName"],
          additionalProperties: false,
        }),
        tool("unity_capture_scene_view", "Capture the active Scene view to a PNG file.", {
          type: "object",
          properties: {
            outputPath: { type: "string" },
            padding: { type: "number" },
          },
          additionalProperties: false,
        }),
        tool("unity_capture_game_view", "Capture the Game view to a PNG file.", {
          type: "object",
          properties: {
            outputPath: { type: "string" },
            padding: { type: "number" },
          },
          additionalProperties: false,
        }),
        tool("unity_capture_window", "Capture an editor window by type or title to a PNG file.", {
          type: "object",
          properties: {
            type: { type: "string" },
            title: { type: "string" },
            outputPath: { type: "string" },
            padding: { type: "number" },
          },
          additionalProperties: false,
        }),
        tool("unity_send_window_key_event", "Send a keyboard event to an editor window such as Game or Scene.", {
          type: "object",
          properties: {
            type: { type: "string" },
            title: { type: "string" },
            keyCode: { type: "string" },
            character: { type: "string" },
            shift: { type: "boolean" },
            control: { type: "boolean" },
            alt: { type: "boolean" },
            command: { type: "boolean" },
            eventType: { type: "string" },
          },
          required: ["keyCode"],
          additionalProperties: false,
        }),
        tool("unity_send_window_mouse_event", "Send a mouse event to an editor window such as Game or Scene.", {
          type: "object",
          properties: {
            type: { type: "string" },
            title: { type: "string" },
            eventType: { type: "string" },
            x: { type: "number" },
            y: { type: "number" },
            button: { type: "number" },
            clickCount: { type: "number" },
            delta: vector2Schema(),
            shift: { type: "boolean" },
            control: { type: "boolean" },
            alt: { type: "boolean" },
            command: { type: "boolean" },
          },
          additionalProperties: false,
        }),
        tool("unity_playerprefs_get", "Read a PlayerPrefs key and optionally decode it as a typed value.", {
          type: "object",
          properties: {
            key: { type: "string" },
            valueType: { type: "string" },
          },
          required: ["key"],
          additionalProperties: false,
        }),
        tool("unity_playerprefs_set", "Set a PlayerPrefs key.", {
          type: "object",
          properties: {
            key: { type: "string" },
            valueType: { type: "string" },
            boolValue: { type: "boolean" },
            intValue: { type: "number" },
            floatValue: { type: "number" },
            stringValue: { type: "string" },
          },
          required: ["key", "valueType"],
          additionalProperties: false,
        }),
        riskyTool("unity_playerprefs_delete_key", "Delete a PlayerPrefs key.", {
          type: "object",
          properties: {
            key: { type: "string" },
          },
          required: ["key"],
          additionalProperties: false,
        }),
        riskyTool("unity_playerprefs_delete_all", "Delete all PlayerPrefs keys.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_create_folder", "Create a folder in Assets.", {
          type: "object",
          properties: {
            parentFolder: { type: "string" },
            folderName: { type: "string" },
          },
          required: ["parentFolder", "folderName"],
          additionalProperties: false,
        }),
        riskyTool("unity_delete_asset", "Delete an asset or folder by Unity path.", {
          type: "object",
          properties: {
            path: { type: "string" },
          },
          required: ["path"],
          additionalProperties: false,
        }),
        riskyTool("unity_move_asset", "Move an asset to a new Unity path.", {
          type: "object",
          properties: {
            fromPath: { type: "string" },
            toPath: { type: "string" },
          },
          required: ["fromPath", "toPath"],
          additionalProperties: false,
        }),
        riskyTool("unity_rename_asset", "Rename an asset in place.", {
          type: "object",
          properties: {
            path: { type: "string" },
            name: { type: "string" },
          },
          required: ["path", "name"],
          additionalProperties: false,
        }),
        tool("unity_duplicate_asset", "Duplicate an asset to a new path.", {
          type: "object",
          properties: {
            fromPath: { type: "string" },
            toPath: { type: "string" },
          },
          required: ["fromPath", "toPath"],
          additionalProperties: false,
        }),
        tool("unity_texture_importer", "Read texture importer settings for a texture asset.", {
          type: "object",
          properties: {
            path: { type: "string" },
          },
          required: ["path"],
          additionalProperties: false,
        }),
        tool("unity_set_texture_importer", "Update texture importer settings for a texture asset.", {
          type: "object",
          properties: {
            path: { type: "string" },
            setTextureType: { type: "boolean" },
            textureType: { type: "string" },
            setSpriteImportMode: { type: "boolean" },
            spriteImportMode: { type: "string" },
            setIsReadable: { type: "boolean" },
            isReadable: { type: "boolean" },
            setMipmapEnabled: { type: "boolean" },
            mipmapEnabled: { type: "boolean" },
            setAlphaIsTransparency: { type: "boolean" },
            alphaIsTransparency: { type: "boolean" },
            setFilterMode: { type: "boolean" },
            filterMode: { type: "string" },
            setWrapMode: { type: "boolean" },
            wrapMode: { type: "string" },
            setMaxTextureSize: { type: "boolean" },
            maxTextureSize: { type: "number" },
            setTextureCompression: { type: "boolean" },
            textureCompression: { type: "string" },
          },
          required: ["path"],
          additionalProperties: false,
        }),
        tool("unity_undo", "Undo the last editor operation.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_redo", "Redo the last undone editor operation.", {
          type: "object",
          properties: {},
          additionalProperties: false,
        }),
        tool("unity_command", "Execute a safe command through the Unity bridge.", {
          type: "object",
          properties: {
            name: {
              type: "string",
              description: "Command name such as play, pause, resume, stop, refresh_assets, save_assets, save_open_scenes, open_scene, select_asset, select_hierarchy_object, execute_menu_item.",
            },
            argument: {
              type: "string",
              description: "Optional argument used by some commands, such as the Unity menu path for execute_menu_item.",
            },
            confirm: {
              type: "string",
              description: `Required for risky commands. Exact value: ${RISKY_CONFIRMATION_TEXT}`,
            },
          },
          required: ["name"],
          additionalProperties: false,
        }),
      ],
    });
    return;
  }

  if (message.method === "tools/call") {
    const params = message.params || {};
    const toolName = params.name;
    const argumentsObject = params.arguments || {};

    try {
      const result = await callTool(toolName, argumentsObject);
      writeResult(message.id, {
        content: [
          {
            type: "text",
            text: JSON.stringify(result, null, 2),
          },
        ],
      });
    } catch (error) {
      writeError(message.id, -32000, error.message);
    }

    return;
  }

  writeError(message.id, -32601, `Method not found: ${message.method}`);
}

async function callTool(toolName, args) {
  switch (toolName) {
    case "unity_health":
      return requestJson("/health");
    case "unity_state":
      return requestJson("/state");
    case "unity_project":
      return requestJson("/project");
    case "unity_runtime":
      return requestJson("/runtime");
    case "unity_query_runtime_objects":
      return requestJson("/runtime/query", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          query: args.query || "",
          scenePath: args.scenePath || "",
          tag: args.tag || "",
          componentType: args.componentType || "",
          includeInactive: Boolean(args.includeInactive),
          limit: typeof args.limit === "number" ? Math.trunc(args.limit) : 100,
        }),
      });
    case "unity_inspect_runtime_object":
      return requestJson("/runtime/inspect", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          path: args.path,
        }),
      });
    case "unity_build":
      return requestJson("/build");
    case "unity_player_settings":
      return requestJson("/player-settings");
    case "unity_audio_settings":
      return requestJson("/audio-settings");
    case "unity_time_settings":
      return requestJson("/time-settings");
    case "unity_quality_settings":
      return requestJson("/quality-settings");
    case "unity_project_tags_layers":
      return requestJson("/project-tags-layers");
    case "unity_physics_settings":
      return requestJson("/physics-settings");
    case "unity_set_player_settings":
      return requestJson("/player-settings", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          buildTarget: args.buildTarget || "",
          buildTargetGroup: args.buildTargetGroup || "",
          setApplicationIdentifier: Boolean(args.setApplicationIdentifier),
          applicationIdentifier: args.applicationIdentifier || "",
          setScriptingBackend: Boolean(args.setScriptingBackend),
          scriptingBackend: args.scriptingBackend || "",
          setApiCompatibilityLevel: Boolean(args.setApiCompatibilityLevel),
          apiCompatibilityLevel: args.apiCompatibilityLevel || "",
          setManagedStrippingLevel: Boolean(args.setManagedStrippingLevel),
          managedStrippingLevel: args.managedStrippingLevel || "",
          setIncrementalIl2CppBuild: Boolean(args.setIncrementalIl2CppBuild),
          incrementalIl2CppBuild: Boolean(args.incrementalIl2CppBuild),
          setCaptureStartupLogs: Boolean(args.setCaptureStartupLogs),
          captureStartupLogs: Boolean(args.captureStartupLogs),
          setScriptingDefineSymbols: Boolean(args.setScriptingDefineSymbols),
          scriptingDefineSymbols: args.scriptingDefineSymbols || "",
          scriptingDefineSymbolList: Array.isArray(args.scriptingDefineSymbolList)
            ? args.scriptingDefineSymbolList.filter((value) => typeof value === "string" && value.trim())
            : [],
        }),
      });
    case "unity_set_project_tags_layers":
      return requestJson("/project-tags-layers", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          addTags: Array.isArray(args.addTags) ? args.addTags.filter((value) => typeof value === "string" && value.trim()) : [],
          removeTags: Array.isArray(args.removeTags) ? args.removeTags.filter((value) => typeof value === "string" && value.trim()) : [],
          setLayers: Array.isArray(args.setLayers)
            ? args.setLayers
                .filter((value) => value && typeof value.index === "number")
                .map((value) => ({
                  index: Math.trunc(value.index),
                  name: typeof value.name === "string" ? value.name : "",
                }))
            : [],
          addSortingLayers: Array.isArray(args.addSortingLayers) ? args.addSortingLayers.filter((value) => typeof value === "string" && value.trim()) : [],
          renameSortingLayers: Array.isArray(args.renameSortingLayers)
            ? args.renameSortingLayers
                .filter((value) => value && typeof value.newName === "string" && value.newName.trim())
                .map((value) => ({
                  name: typeof value.name === "string" ? value.name : "",
                  uniqueId: typeof value.uniqueId === "number" ? Math.trunc(value.uniqueId) : -1,
                  newName: value.newName,
                }))
            : [],
          removeSortingLayers: Array.isArray(args.removeSortingLayers) ? args.removeSortingLayers.filter((value) => typeof value === "string" && value.trim()) : [],
          setRenderingLayers: Array.isArray(args.setRenderingLayers)
            ? args.setRenderingLayers
                .filter((value) => value && typeof value.index === "number")
                .map((value) => ({
                  index: Math.trunc(value.index),
                  name: typeof value.name === "string" ? value.name : "",
                }))
            : [],
        }),
      });
    case "unity_set_quality_settings":
      return requestJson("/quality-settings", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          setCurrentQualityIndex: Boolean(args.setCurrentQualityIndex),
          currentQualityIndex: typeof args.currentQualityIndex === "number" ? Math.trunc(args.currentQualityIndex) : 0,
          qualityLevels: Array.isArray(args.qualityLevels)
            ? args.qualityLevels
                .filter((value) => value && typeof value.index === "number")
                .map((value) => ({
                  index: Math.trunc(value.index),
                  setName: Boolean(value.setName),
                  name: value.name || "",
                  setPixelLightCount: Boolean(value.setPixelLightCount),
                  pixelLightCount: typeof value.pixelLightCount === "number" ? Math.trunc(value.pixelLightCount) : 0,
                  setShadows: Boolean(value.setShadows),
                  shadows: typeof value.shadows === "number" ? Math.trunc(value.shadows) : 0,
                  setShadowResolution: Boolean(value.setShadowResolution),
                  shadowResolution: typeof value.shadowResolution === "number" ? Math.trunc(value.shadowResolution) : 0,
                  setShadowProjection: Boolean(value.setShadowProjection),
                  shadowProjection: typeof value.shadowProjection === "number" ? Math.trunc(value.shadowProjection) : 0,
                  setShadowCascades: Boolean(value.setShadowCascades),
                  shadowCascades: typeof value.shadowCascades === "number" ? Math.trunc(value.shadowCascades) : 0,
                  setShadowDistance: Boolean(value.setShadowDistance),
                  shadowDistance: typeof value.shadowDistance === "number" ? value.shadowDistance : 0,
                  setGlobalTextureMipmapLimit: Boolean(value.setGlobalTextureMipmapLimit),
                  globalTextureMipmapLimit: typeof value.globalTextureMipmapLimit === "number" ? Math.trunc(value.globalTextureMipmapLimit) : 0,
                  setAnisotropicTextures: Boolean(value.setAnisotropicTextures),
                  anisotropicTextures: typeof value.anisotropicTextures === "number" ? Math.trunc(value.anisotropicTextures) : 0,
                  setAntiAliasing: Boolean(value.setAntiAliasing),
                  antiAliasing: typeof value.antiAliasing === "number" ? Math.trunc(value.antiAliasing) : 0,
                  setVSyncCount: Boolean(value.setVSyncCount),
                  vSyncCount: typeof value.vSyncCount === "number" ? Math.trunc(value.vSyncCount) : 0,
                  setLodBias: Boolean(value.setLodBias),
                  lodBias: typeof value.lodBias === "number" ? value.lodBias : 0,
                  setMaximumLODLevel: Boolean(value.setMaximumLODLevel),
                  maximumLODLevel: typeof value.maximumLODLevel === "number" ? Math.trunc(value.maximumLODLevel) : 0,
                  setStreamingMipmapsActive: Boolean(value.setStreamingMipmapsActive),
                  streamingMipmapsActive: Boolean(value.streamingMipmapsActive),
                  setStreamingMipmapsMemoryBudget: Boolean(value.setStreamingMipmapsMemoryBudget),
                  streamingMipmapsMemoryBudget: typeof value.streamingMipmapsMemoryBudget === "number" ? value.streamingMipmapsMemoryBudget : 0,
                }))
            : [],
          perPlatformDefaults: Array.isArray(args.perPlatformDefaults)
            ? args.perPlatformDefaults
                .filter((value) => value && typeof value.platform === "string" && typeof value.qualityIndex === "number")
                .map((value) => ({
                  platform: value.platform,
                  qualityIndex: Math.trunc(value.qualityIndex),
                }))
            : [],
        }),
      });
    case "unity_set_audio_settings":
      return requestJson("/audio-settings", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          setVolume: Boolean(args.setVolume),
          volume: typeof args.volume === "number" ? args.volume : 0,
          setRolloffScale: Boolean(args.setRolloffScale),
          rolloffScale: typeof args.rolloffScale === "number" ? args.rolloffScale : 0,
          setDopplerFactor: Boolean(args.setDopplerFactor),
          dopplerFactor: typeof args.dopplerFactor === "number" ? args.dopplerFactor : 0,
          setDefaultSpeakerMode: Boolean(args.setDefaultSpeakerMode),
          defaultSpeakerMode: typeof args.defaultSpeakerMode === "number" ? Math.trunc(args.defaultSpeakerMode) : 0,
          setSampleRate: Boolean(args.setSampleRate),
          sampleRate: typeof args.sampleRate === "number" ? Math.trunc(args.sampleRate) : 0,
          setDspBufferSize: Boolean(args.setDspBufferSize),
          dspBufferSize: typeof args.dspBufferSize === "number" ? Math.trunc(args.dspBufferSize) : 0,
          setVirtualVoiceCount: Boolean(args.setVirtualVoiceCount),
          virtualVoiceCount: typeof args.virtualVoiceCount === "number" ? Math.trunc(args.virtualVoiceCount) : 0,
          setRealVoiceCount: Boolean(args.setRealVoiceCount),
          realVoiceCount: typeof args.realVoiceCount === "number" ? Math.trunc(args.realVoiceCount) : 0,
          setSpatializerPlugin: Boolean(args.setSpatializerPlugin),
          spatializerPlugin: args.spatializerPlugin || "",
          setAmbisonicDecoderPlugin: Boolean(args.setAmbisonicDecoderPlugin),
          ambisonicDecoderPlugin: args.ambisonicDecoderPlugin || "",
          setDisableAudio: Boolean(args.setDisableAudio),
          disableAudio: Boolean(args.disableAudio),
          setVirtualizeEffects: Boolean(args.setVirtualizeEffects),
          virtualizeEffects: Boolean(args.virtualizeEffects),
          setRequestedDspBufferSize: Boolean(args.setRequestedDspBufferSize),
          requestedDspBufferSize: typeof args.requestedDspBufferSize === "number" ? Math.trunc(args.requestedDspBufferSize) : 0,
        }),
      });
    case "unity_set_time_settings":
      return requestJson("/time-settings", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          setFixedTimestep: Boolean(args.setFixedTimestep),
          fixedTimestep: typeof args.fixedTimestep === "number" ? args.fixedTimestep : 0,
          setMaximumAllowedTimestep: Boolean(args.setMaximumAllowedTimestep),
          maximumAllowedTimestep: typeof args.maximumAllowedTimestep === "number" ? args.maximumAllowedTimestep : 0,
          setTimeScale: Boolean(args.setTimeScale),
          timeScale: typeof args.timeScale === "number" ? args.timeScale : 0,
          setMaximumParticleTimestep: Boolean(args.setMaximumParticleTimestep),
          maximumParticleTimestep: typeof args.maximumParticleTimestep === "number" ? args.maximumParticleTimestep : 0,
        }),
      });
    case "unity_set_physics_settings":
      return requestJson("/physics-settings", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          physics3D: args.physics3D || {},
          physics2D: args.physics2D || {},
        }),
      });
    case "unity_set_build_settings":
      return requestJson("/build/settings", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          setDevelopmentBuild: Boolean(args.setDevelopmentBuild),
          developmentBuild: Boolean(args.developmentBuild),
          setConnectProfiler: Boolean(args.setConnectProfiler),
          connectProfiler: Boolean(args.connectProfiler),
          setAllowDebugging: Boolean(args.setAllowDebugging),
          allowDebugging: Boolean(args.allowDebugging),
          setBuildAppBundle: Boolean(args.setBuildAppBundle),
          buildAppBundle: Boolean(args.buildAppBundle),
          locationPathName: args.locationPathName || "",
        }),
      });
    case "unity_switch_build_target":
      requireRiskyConfirmation(toolName, args.confirm);
      return requestJson("/build/target", {
        method: "POST",
        headers: {
          "content-type": "application/json",
          "x-unity-bridge-confirm": args.confirm,
        },
        body: JSON.stringify({
          buildTarget: args.buildTarget,
          buildTargetGroup: args.buildTargetGroup || "",
        }),
      });
    case "unity_build_scenes":
      return requestJson("/build/scenes");
    case "unity_last_build_report":
      return requestJson("/build/last");
    case "unity_build_player":
      return requestJson("/build/player", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          buildTarget: args.buildTarget || "",
          locationPathName: args.locationPathName || "",
          scenes: Array.isArray(args.scenes) ? args.scenes.filter((scene) => typeof scene === "string" && scene.trim()) : [],
          development: Boolean(args.development),
          connectProfiler: Boolean(args.connectProfiler),
          allowDebugging: Boolean(args.allowDebugging),
          buildAppBundle: Boolean(args.buildAppBundle),
          autoRunPlayer: Boolean(args.autoRunPlayer),
          buildScriptsOnly: Boolean(args.buildScriptsOnly),
          showBuiltPlayer: Boolean(args.showBuiltPlayer),
          strictMode: Boolean(args.strictMode),
          acceptExternalModificationsToPlayer: Boolean(args.acceptExternalModificationsToPlayer),
          compressWithLz4: Boolean(args.compressWithLz4),
          compressWithLz4HC: Boolean(args.compressWithLz4HC),
        }),
      });
    case "unity_set_build_scenes":
      requireRiskyConfirmation(toolName, args.confirm);
      return requestJson("/build/scenes", {
        method: "POST",
        headers: {
          "content-type": "application/json",
          "x-unity-bridge-confirm": args.confirm,
        },
        body: JSON.stringify({
          scenes: Array.isArray(args.scenes)
            ? args.scenes
                .filter((scene) => scene && typeof scene.path === "string" && scene.path.trim())
                .map((scene) => ({
                  path: scene.path,
                  enabled: scene.enabled !== undefined ? Boolean(scene.enabled) : true,
                }))
            : [],
        }),
      });
    case "unity_add_build_scene":
      return requestJson("/build/scenes/add", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          path: args.path,
          enabled: args.enabled !== undefined ? Boolean(args.enabled) : true,
          buildIndex: typeof args.buildIndex === "number" ? Math.trunc(args.buildIndex) : -1,
        }),
      });
    case "unity_remove_build_scene":
      requireRiskyConfirmation(toolName, args.confirm);
      return requestJson("/build/scenes/remove", {
        method: "POST",
        headers: {
          "content-type": "application/json",
          "x-unity-bridge-confirm": args.confirm,
        },
        body: JSON.stringify({
          path: args.path,
        }),
      });
    case "unity_tests":
      return requestJson("/tests");
    case "unity_last_test_report":
      return requestJson("/tests/last");
    case "unity_run_tests":
      return requestJson("/tests/run", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          testMode: args.testMode || "",
          runSynchronously: Boolean(args.runSynchronously),
          resultFilePath: args.resultFilePath || "",
          testNames: Array.isArray(args.testNames) ? args.testNames.filter((value) => typeof value === "string" && value.trim()) : [],
          groupNames: Array.isArray(args.groupNames) ? args.groupNames.filter((value) => typeof value === "string" && value.trim()) : [],
          categoryNames: Array.isArray(args.categoryNames) ? args.categoryNames.filter((value) => typeof value === "string" && value.trim()) : [],
          assemblyNames: Array.isArray(args.assemblyNames) ? args.assemblyNames.filter((value) => typeof value === "string" && value.trim()) : [],
        }),
      });
    case "unity_compilation":
      return requestJson("/compilation");
    case "unity_last_compilation":
      return requestJson("/compilation/last");
    case "unity_editor_windows":
      return requestJson("/editor/windows");
    case "unity_inspector_selection":
      return requestJson("/inspector/selection");
    case "unity_console_selection":
      return requestJson("/console/selection");
    case "unity_current_context":
      return requestJson("/context/current");
    case "unity_packages":
      return requestJson("/packages");
    case "unity_package_operations":
      return requestJson("/packages/operations");
    case "unity_last_package_operation":
      return requestJson("/packages/operations/last");
    case "unity_add_package":
      return requestJson("/packages/add", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          packageId: args.packageId,
        }),
      });
    case "unity_remove_package":
      requireRiskyConfirmation(toolName, args.confirm);
      return requestJson("/packages/remove", {
        method: "POST",
        headers: {
          "content-type": "application/json",
          "x-unity-bridge-confirm": args.confirm,
        },
        body: JSON.stringify({
          packageId: args.packageId,
        }),
      });
    case "unity_embed_package":
      return requestJson("/packages/embed", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          packageId: args.packageId,
        }),
      });
    case "unity_resolve_packages":
      return requestJson("/packages/resolve", {
        method: "POST",
      });
    case "unity_scene_stats":
      return requestJson("/scene-stats");
    case "unity_prefab_stage":
      return requestJson("/prefab-stage");
    case "unity_selection":
      return requestJson("/selection");
    case "unity_active_object":
      return requestJson("/active-object");
    case "unity_scenes":
      return requestJson("/scenes");
    case "unity_commands":
      return requestJson("/commands");
    case "unity_hierarchy":
      return requestJson("/hierarchy");
    case "unity_inspect_hierarchy_object":
      return requestJson("/inspect/hierarchy", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          path: args.path,
        }),
      });
    case "unity_inspect_asset":
      return requestJson("/inspect/asset", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          path: args.path,
        }),
      });
    case "unity_asset_dependencies":
      return requestJson("/asset/dependencies", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          path: args.path,
          recursive: args.recursive !== false,
        }),
      });
    case "unity_resolve_asset":
      return requestJson("/asset/resolve", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          path: args.path || "",
          guid: args.guid || "",
        }),
      });
    case "unity_search_assets":
      return requestJson("/search/assets", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          query: args.query || "",
          type: args.type || "",
        }),
      });
    case "unity_search_hierarchy":
      return requestJson("/search/hierarchy", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          query: args.query || "",
          scenePath: args.scenePath || "",
          tag: args.tag || "",
          componentType: args.componentType || "",
          includeInactive: Boolean(args.includeInactive),
        }),
      });
    case "unity_logs":
      return requestJson("/logs");
    case "unity_query_logs":
      return requestJson("/logs/query", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          query: args.query || "",
          level: args.level || "",
          limit: typeof args.limit === "number" ? args.limit : 100,
        }),
      });
    case "unity_events":
      return requestJson("/events");
    case "unity_query_events":
      return requestJson("/events/query", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          query: args.query || "",
          type: args.type || "",
          limit: typeof args.limit === "number" ? args.limit : 100,
        }),
      });
    case "unity_play":
      return commandRequest("play");
    case "unity_play_and_focus_game":
      return commandRequest("play_and_focus_game");
    case "unity_pause":
      return commandRequest("pause");
    case "unity_resume":
      return commandRequest("resume");
    case "unity_step":
      return commandRequest("step");
    case "unity_stop":
      return commandRequest("stop");
    case "unity_set_time_scale":
      return commandRequest("set_time_scale", JSON.stringify({
        value: typeof args.value === "number" ? args.value : 1,
      }));
    case "unity_set_target_frame_rate":
      return commandRequest("set_target_frame_rate", JSON.stringify({
        value: typeof args.value === "number" ? Math.trunc(args.value) : -1,
      }));
    case "unity_refresh_assets":
      return commandRequest("refresh_assets");
    case "unity_save_assets":
      return commandRequest("save_assets");
    case "unity_save_open_scenes":
      return commandRequest("save_open_scenes");
    case "unity_save_scene":
      return commandRequest("save_scene", args.path);
    case "unity_close_scene":
      return commandRequest("close_scene", args.path);
    case "unity_clear_console":
      return commandRequest("clear_console");
    case "unity_open_scene":
      return commandRequest("open_scene", args.path);
    case "unity_open_scene_additive":
      return commandRequest("open_scene_additive", args.path);
    case "unity_set_active_scene":
      return commandRequest("set_active_scene", args.path);
    case "unity_open_prefab_stage":
      return commandRequest("open_prefab_stage", args.path);
    case "unity_close_prefab_stage":
      return commandRequest("close_prefab_stage");
    case "unity_open_asset":
      return commandRequest("open_asset", args.path);
    case "unity_frame_selected":
      return commandRequest("frame_selected");
    case "unity_focus_project_window":
      return commandRequest("focus_project_window");
    case "unity_focus_scene_view":
      return commandRequest("focus_scene_view");
    case "unity_focus_window":
      return commandRequest("focus_window", JSON.stringify({
        type: args.type || "",
        title: args.title || "",
      }));
    case "unity_select_asset":
      return commandRequest("select_asset", args.path);
    case "unity_ping_asset":
      return commandRequest("ping_asset", args.path);
    case "unity_reveal_asset":
      return commandRequest("reveal_asset", args.path);
    case "unity_select_hierarchy_object":
      return commandRequest("select_hierarchy_object", args.path);
    case "unity_execute_menu_item":
      requireRiskyConfirmation(toolName, args.confirm);
      return commandRequest("execute_menu_item", args.path, args.confirm);
    case "unity_create_game_object":
      return commandRequest("create_game_object", JSON.stringify({
        name: args.name,
        parentPath: args.parentPath || "",
        scenePath: args.scenePath || "",
        primitiveType: args.primitiveType || "",
        tag: args.tag || "",
        layer: typeof args.layer === "number" ? args.layer : -1,
        active: args.active !== false,
      }));
    case "unity_instantiate_prefab":
      return commandRequest("instantiate_prefab", JSON.stringify({
        assetPath: args.assetPath,
        parentPath: args.parentPath || "",
        scenePath: args.scenePath || "",
        name: args.name || "",
        active: args.active !== false,
      }));
    case "unity_duplicate_hierarchy_object":
      return commandRequest("duplicate_hierarchy_object", JSON.stringify({
        path: args.path,
      }));
    case "unity_delete_hierarchy_object":
      requireRiskyConfirmation(toolName, args.confirm);
      return commandRequest("delete_hierarchy_object", JSON.stringify({
        path: args.path,
      }), args.confirm);
    case "unity_rename_hierarchy_object":
      requireRiskyConfirmation(toolName, args.confirm);
      return commandRequest("rename_hierarchy_object", JSON.stringify({
        path: args.path,
        name: args.name,
      }), args.confirm);
    case "unity_set_hierarchy_object_active":
      return commandRequest("set_hierarchy_object_active", JSON.stringify({
        path: args.path,
        active: Boolean(args.active),
      }));
    case "unity_set_hierarchy_metadata":
      return commandRequest("set_hierarchy_metadata", JSON.stringify({
        path: args.path,
        name: args.name || "",
        tag: args.tag || "",
        layer: typeof args.layer === "number" ? args.layer : -1,
        setStaticFlags: Boolean(args.setStaticFlags),
        staticFlags: Boolean(args.staticFlags),
      }));
    case "unity_set_hierarchy_parent":
      return commandRequest("set_hierarchy_parent", JSON.stringify({
        path: args.path,
        parentPath: args.parentPath || "",
        worldPositionStays: args.worldPositionStays !== false,
      }));
    case "unity_set_transform":
      return commandRequest("set_transform", JSON.stringify({
        path: args.path,
        setLocalPosition: Boolean(args.setLocalPosition),
        setLocalEulerAngles: Boolean(args.setLocalEulerAngles),
        setLocalScale: Boolean(args.setLocalScale),
        localPosition: args.localPosition || null,
        localEulerAngles: args.localEulerAngles || null,
        localScale: args.localScale || null,
      }));
    case "unity_add_component":
      return commandRequest("add_component", JSON.stringify({
        path: args.path,
        componentType: args.componentType,
      }));
    case "unity_remove_component":
      requireRiskyConfirmation(toolName, args.confirm);
      return commandRequest("remove_component", JSON.stringify({
        path: args.path,
        componentType: args.componentType,
        componentIndex: typeof args.componentIndex === "number" ? args.componentIndex : -1,
      }), args.confirm);
    case "unity_set_component_enabled":
      return commandRequest("set_component_enabled", JSON.stringify({
        path: args.path,
        componentType: args.componentType,
        componentIndex: typeof args.componentIndex === "number" ? args.componentIndex : -1,
        enabled: Boolean(args.enabled),
      }));
    case "unity_set_component_property":
      return commandRequest("set_component_property", JSON.stringify({
        path: args.path,
        componentType: args.componentType,
        componentIndex: typeof args.componentIndex === "number" ? args.componentIndex : -1,
        propertyPath: args.propertyPath,
        valueType: args.valueType,
        boolValue: Boolean(args.boolValue),
        intValue: typeof args.intValue === "number" ? Math.trunc(args.intValue) : 0,
        floatValue: typeof args.floatValue === "number" ? args.floatValue : 0,
        stringValue: args.stringValue || "",
        objectReferencePath: args.objectReferencePath || "",
        vector2Value: args.vector2Value || null,
        vector3Value: args.vector3Value || null,
        vector4Value: args.vector4Value || null,
        colorValue: args.colorValue || null,
      }));
    case "unity_invoke_component_method":
      return commandRequest("invoke_component_method", JSON.stringify({
        path: args.path,
        componentType: args.componentType,
        componentIndex: typeof args.componentIndex === "number" ? args.componentIndex : -1,
        methodName: args.methodName,
        argumentType: args.argumentType || "",
        boolValue: Boolean(args.boolValue),
        intValue: typeof args.intValue === "number" ? Math.trunc(args.intValue) : 0,
        floatValue: typeof args.floatValue === "number" ? args.floatValue : 0,
        stringValue: args.stringValue || "",
      }));
    case "unity_invoke_static_method":
      return commandRequest("invoke_static_method", JSON.stringify({
        typeName: args.typeName,
        methodName: args.methodName,
        argumentType: args.argumentType || "",
        boolValue: Boolean(args.boolValue),
        intValue: typeof args.intValue === "number" ? Math.trunc(args.intValue) : 0,
        floatValue: typeof args.floatValue === "number" ? args.floatValue : 0,
        stringValue: args.stringValue || "",
      }));
    case "unity_capture_scene_view":
      return commandRequest("capture_scene_view", JSON.stringify({
        outputPath: args.outputPath || "",
        padding: typeof args.padding === "number" ? Math.trunc(args.padding) : 0,
      }));
    case "unity_capture_game_view":
      return commandRequest("capture_game_view", JSON.stringify({
        outputPath: args.outputPath || "",
        padding: typeof args.padding === "number" ? Math.trunc(args.padding) : 0,
      }));
    case "unity_capture_window":
      return commandRequest("capture_window", JSON.stringify({
        type: args.type || "",
        title: args.title || "",
        outputPath: args.outputPath || "",
        padding: typeof args.padding === "number" ? Math.trunc(args.padding) : 0,
      }));
    case "unity_send_window_key_event":
      return commandRequest("send_window_key_event", JSON.stringify({
        type: args.type || "",
        title: args.title || "",
        keyCode: args.keyCode,
        character: typeof args.character === "string" && args.character.length > 0 ? args.character[0] : "\u0000",
        shift: Boolean(args.shift),
        control: Boolean(args.control),
        alt: Boolean(args.alt),
        command: Boolean(args.command),
        eventType: args.eventType || "",
      }));
    case "unity_send_window_mouse_event":
      return commandRequest("send_window_mouse_event", JSON.stringify({
        type: args.type || "",
        title: args.title || "",
        eventType: args.eventType || "",
        x: typeof args.x === "number" ? args.x : 0,
        y: typeof args.y === "number" ? args.y : 0,
        button: typeof args.button === "number" ? Math.trunc(args.button) : 0,
        clickCount: typeof args.clickCount === "number" ? Math.trunc(args.clickCount) : 1,
        delta: args.delta || null,
        shift: Boolean(args.shift),
        control: Boolean(args.control),
        alt: Boolean(args.alt),
        command: Boolean(args.command),
      }));
    case "unity_playerprefs_get":
      return requestJson("/playerprefs/get", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          key: args.key,
          valueType: args.valueType || "",
        }),
      });
    case "unity_playerprefs_set":
      return commandRequest("playerprefs_set", JSON.stringify({
        key: args.key,
        valueType: args.valueType,
        boolValue: Boolean(args.boolValue),
        intValue: typeof args.intValue === "number" ? Math.trunc(args.intValue) : 0,
        floatValue: typeof args.floatValue === "number" ? args.floatValue : 0,
        stringValue: args.stringValue || "",
      }));
    case "unity_playerprefs_delete_key":
      requireRiskyConfirmation(toolName, args.confirm);
      return commandRequest("playerprefs_delete_key", JSON.stringify({
        key: args.key,
      }), args.confirm);
    case "unity_playerprefs_delete_all":
      requireRiskyConfirmation(toolName, args.confirm);
      return commandRequest("playerprefs_delete_all", "", args.confirm);
    case "unity_create_folder":
      return commandRequest("create_folder", JSON.stringify({
        parentFolder: args.parentFolder,
        folderName: args.folderName,
      }));
    case "unity_delete_asset":
      requireRiskyConfirmation(toolName, args.confirm);
      return commandRequest("delete_asset", JSON.stringify({
        path: args.path,
      }), args.confirm);
    case "unity_move_asset":
      requireRiskyConfirmation(toolName, args.confirm);
      return commandRequest("move_asset", JSON.stringify({
        fromPath: args.fromPath,
        toPath: args.toPath,
      }), args.confirm);
    case "unity_rename_asset":
      requireRiskyConfirmation(toolName, args.confirm);
      return commandRequest("rename_asset", JSON.stringify({
        path: args.path,
        name: args.name,
      }), args.confirm);
    case "unity_duplicate_asset":
      return commandRequest("duplicate_asset", JSON.stringify({
        fromPath: args.fromPath,
        toPath: args.toPath,
      }));
    case "unity_texture_importer":
      return requestJson("/importers/texture", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          path: args.path,
        }),
      });
    case "unity_set_texture_importer":
      return requestJson("/importers/texture/set", {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          path: args.path,
          setTextureType: Boolean(args.setTextureType),
          textureType: args.textureType || "",
          setSpriteImportMode: Boolean(args.setSpriteImportMode),
          spriteImportMode: args.spriteImportMode || "",
          setIsReadable: Boolean(args.setIsReadable),
          isReadable: Boolean(args.isReadable),
          setMipmapEnabled: Boolean(args.setMipmapEnabled),
          mipmapEnabled: Boolean(args.mipmapEnabled),
          setAlphaIsTransparency: Boolean(args.setAlphaIsTransparency),
          alphaIsTransparency: Boolean(args.alphaIsTransparency),
          setFilterMode: Boolean(args.setFilterMode),
          filterMode: args.filterMode || "",
          setWrapMode: Boolean(args.setWrapMode),
          wrapMode: args.wrapMode || "",
          setMaxTextureSize: Boolean(args.setMaxTextureSize),
          maxTextureSize: typeof args.maxTextureSize === "number" ? Math.trunc(args.maxTextureSize) : 0,
          setTextureCompression: Boolean(args.setTextureCompression),
          textureCompression: args.textureCompression || "",
        }),
      });
    case "unity_undo":
      return commandRequest("undo");
    case "unity_redo":
      return commandRequest("redo");
    case "unity_command":
      if (riskyCommandNames.has(args.name)) {
        requireRiskyConfirmation(toolName, args.confirm);
      }

      return commandRequest(args.name, args.argument || "", args.confirm || "");
    default:
      throw new Error(`Unknown tool: ${toolName}`);
  }
}

async function commandRequest(name, argument = "", confirm = "") {
  return requestJson("/command", {
    method: "POST",
    headers: {
      "content-type": "application/json",
    },
    body: JSON.stringify({
      name,
      argument,
      confirm,
    }),
  });
}

async function requestJson(path, options = {}) {
  const method = (options.method || "GET").toUpperCase();
  const shouldRetry = method === "GET";
  let attempt = 0;

  while (true) {
    try {
      const bridgeBaseUrl = await resolveBridgeBaseUrl();
      const response = await fetch(`${bridgeBaseUrl}${path}`, options);
      const text = await response.text();

      let data;
      try {
        data = text ? JSON.parse(text) : {};
      } catch (error) {
        throw new Error(`Bridge returned non-JSON response (${response.status}): ${text}`);
      }

      if (!response.ok) {
        const message = data && typeof data.message === "string" ? data.message : `Bridge request failed with ${response.status}`;
        throw new Error(message);
      }

      if (path === "/health") {
        validateUnityMcpHealth(data, bridgeBaseUrl);
      }

      return data;
    } catch (error) {
      if (!shouldRetry || !isRetryableBridgeError(error) || attempt >= BridgeReadRetryCount) {
        if (!explicitBridgeBaseUrl) {
          resolvedBridgeBaseUrl = "";
        }
        throw error;
      }

      await sleep(BridgeReadRetryBaseDelayMs * (attempt + 1));
      attempt += 1;
    }
  }
}

async function resolveBridgeBaseUrl() {
  if (explicitBridgeBaseUrl) {
    return explicitBridgeBaseUrl;
  }

  if (resolvedBridgeBaseUrl) {
    return resolvedBridgeBaseUrl;
  }

  const instances = await readLiveBridgeInstances();
  let matches = instances;

  if (bridgeProjectPath) {
    matches = instances.filter((instance) => normalizePath(instance.projectPath || "") === bridgeProjectPath);
    if (matches.length === 0) {
      throw new Error(`No live Unity Bridge instance found for UNITY_BRIDGE_PROJECT_PATH=${bridgeProjectPath}.`);
    }
  }

  if (matches.length === 0) {
    throw new Error(
      "No live Unity Bridge instance found. Start Unity Bridge in the target project, or set UNITY_BRIDGE_URL."
    );
  }

  if (matches.length > 1) {
    const details = matches
      .map((instance) => `${instance.projectName || "Unity project"} at ${instance.projectPath || "unknown path"} (${instance.url})`)
      .join("; ");
    throw new Error(
      `Multiple Unity Bridge instances are running. Set UNITY_BRIDGE_PROJECT_PATH or UNITY_BRIDGE_URL. Found: ${details}`
    );
  }

  const selectedBridgeBaseUrl = normalizeBaseUrl(matches[0].url);
  if (bridgeProjectPath) {
    resolvedBridgeBaseUrl = selectedBridgeBaseUrl;
  }

  return selectedBridgeBaseUrl;
}

async function readLiveBridgeInstances() {
  const registryDir = bridgeRegistryDirectory();
  let entries;
  try {
    entries = fs.readdirSync(registryDir, { withFileTypes: true });
  } catch (error) {
    if (error && error.code === "ENOENT") {
      return [];
    }

    throw error;
  }

  const instances = [];
  for (const entry of entries) {
    if (!entry.isFile() || !entry.name.endsWith(".json")) {
      continue;
    }

    const filePath = path.join(registryDir, entry.name);
    let instance;
    try {
      instance = JSON.parse(fs.readFileSync(filePath, "utf8"));
    } catch {
      continue;
    }

    if (!instance || typeof instance.url !== "string" || !isLoopbackHttpUrl(instance.url)) {
      continue;
    }

    const liveInstance = await readLiveBridgeHealth(instance.url);
    if (liveInstance) {
      instances.push({
        ...instance,
        ...liveInstance,
        url: normalizeBaseUrl(instance.url),
      });
    }
  }

  return instances;
}

async function readLiveBridgeHealth(url) {
  try {
    const response = await fetchWithTimeout(`${normalizeBaseUrl(url)}/health`, BridgeDiscoveryProbeTimeoutMs);
    if (!response.ok) {
      return null;
    }

    const data = await response.json();
    validateUnityMcpHealth(data, normalizeBaseUrl(url));
    if (!data || data.ok !== true || data.running !== true) {
      return null;
    }

    return data;
  } catch {
    return null;
  }
}

async function fetchWithTimeout(url, timeoutMs) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), timeoutMs);
  try {
    return await fetch(url, { signal: controller.signal });
  } finally {
    clearTimeout(timeout);
  }
}

function bridgeRegistryDirectory() {
  if (process.platform === "darwin") {
    return path.join(os.homedir(), "Library", "Application Support", "VolxGames", "UnityBridge", "instances");
  }

  if (process.platform === "win32") {
    return path.join(env.APPDATA || path.join(os.homedir(), "AppData", "Roaming"), "VolxGames", "UnityBridge", "instances");
  }

  return path.join(env.XDG_CONFIG_HOME || path.join(os.homedir(), ".config"), "VolxGames", "UnityBridge", "instances");
}

function normalizeBaseUrl(url) {
  return String(url || "").replace(/\/+$/, "");
}

function normalizePath(value) {
  if (!value) {
    return "";
  }

  const normalized = path.resolve(String(value)).replace(/[\\/]+$/, "");
  return process.platform === "darwin" || process.platform === "win32"
    ? normalized.toLowerCase()
    : normalized;
}

function isLoopbackHttpUrl(value) {
  let parsed;
  try {
    parsed = new URL(value);
  } catch {
    return false;
  }

  const hostname = parsed.hostname.toLowerCase();
  return (
    (parsed.protocol === "http:" || parsed.protocol === "https:") &&
    (hostname === "localhost" || hostname === "127.0.0.1" || hostname === "::1" || hostname === "[::1]")
  );
}

function validateUnityMcpHealth(data, bridgeBaseUrl) {
  const service = data && typeof data.service === "string" ? data.service : "";
  if (service === "fresnel-runtime") {
    throw new Error(
      `UNITY_BRIDGE_URL points to the Fresnel Python runtime (${bridgeBaseUrl}), not the Unity MCP bridge.`
    );
  }

  if (service === "fresnel-unity-bridge") {
    throw new Error(
      `UNITY_BRIDGE_URL points to the Fresnel private bridge (${bridgeBaseUrl}), not the Unity MCP bridge.`
    );
  }

  return data;
}

function tool(name, description, inputSchema) {
  return { name, description, inputSchema };
}

function riskyTool(name, description, inputSchema) {
  return tool(
    name,
    `${description} Requires confirm="${RISKY_CONFIRMATION_TEXT}".`,
    withConfirmationSchema(inputSchema),
  );
}

function withConfirmationSchema(inputSchema) {
  return {
    ...inputSchema,
    properties: {
      ...(inputSchema.properties || {}),
      confirm: {
        type: "string",
        description: `Exact confirmation text: ${RISKY_CONFIRMATION_TEXT}`,
      },
    },
    required: [...(inputSchema.required || []), "confirm"],
  };
}

function requireRiskyConfirmation(toolName, confirm) {
  if (confirm === RISKY_CONFIRMATION_TEXT) {
    return;
  }

  throw new Error(
    `${toolName} requires confirm="${RISKY_CONFIRMATION_TEXT}" to avoid accidental destructive project changes.`,
  );
}

function isRetryableBridgeError(error) {
  if (!(error instanceof Error)) {
    return false;
  }

  return /fetch failed|ECONNREFUSED|EPIPE|socket hang up|networkerror/i.test(error.message);
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function vector2Schema() {
  return {
    type: "object",
    properties: {
      x: { type: "number" },
      y: { type: "number" },
    },
    required: ["x", "y"],
    additionalProperties: false,
  };
}

function vector3Schema() {
  return {
    type: "object",
    properties: {
      x: { type: "number" },
      y: { type: "number" },
      z: { type: "number" },
    },
    required: ["x", "y", "z"],
    additionalProperties: false,
  };
}

function vector4Schema() {
  return {
    type: "object",
    properties: {
      x: { type: "number" },
      y: { type: "number" },
      z: { type: "number" },
      w: { type: "number" },
    },
    required: ["x", "y", "z", "w"],
    additionalProperties: false,
  };
}

function colorSchema() {
  return {
    type: "object",
    properties: {
      r: { type: "number" },
      g: { type: "number" },
      b: { type: "number" },
      a: { type: "number" },
    },
    required: ["r", "g", "b", "a"],
    additionalProperties: false,
  };
}

function writeResult(id, result) {
  stdout.write(`${JSON.stringify({ jsonrpc: "2.0", id, result })}\n`);
}

function writeError(id, code, message) {
  stderr.write(`[unity-mcp-server] ${message}\n`);
  stdout.write(`${JSON.stringify({ jsonrpc: "2.0", id, error: { code, message } })}\n`);
}
