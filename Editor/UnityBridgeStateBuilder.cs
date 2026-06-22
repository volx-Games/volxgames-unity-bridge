using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.Build;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

namespace VolxGames.UnityBridge.Editor
{
    internal static class UnityBridgeStateBuilder
    {
        public static UnityBridgeHealth BuildHealth(int port, bool running, string startedAtUtc)
        {
            return new UnityBridgeHealth
            {
                projectName = Application.productName,
                projectPath = Application.dataPath.Replace("/Assets", string.Empty),
                port = port,
                running = running,
                unityVersion = Application.unityVersion,
                startedAtUtc = startedAtUtc
            };
        }

        public static UnityBridgeState BuildState(string lastReloadAtUtc)
        {
            var state = new UnityBridgeState
            {
                projectName = Application.productName,
                projectPath = Application.dataPath.Replace("/Assets", string.Empty),
                unityVersion = Application.unityVersion,
                isPlaying = EditorApplication.isPlaying,
                isPaused = EditorApplication.isPaused,
                isCompiling = EditorApplication.isCompiling,
                isUpdating = EditorApplication.isUpdating,
                isApplicationActive = InternalEditorUtility.isApplicationActive,
                activeScene = SceneManager.GetActiveScene().path,
                lastReloadAtUtc = lastReloadAtUtc
            };

            foreach (var selectedObject in Selection.objects)
            {
                state.selection.Add(ToObjectRef(selectedObject));
            }

            state.selectionCount = state.selection.Count;

            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                state.openScenes.Add(new UnityBridgeSceneRef
                {
                    name = scene.name,
                    path = scene.path,
                    loaded = scene.isLoaded,
                    dirty = scene.isDirty,
                    active = scene == SceneManager.GetActiveScene()
                });
            }

            return state;
        }

        public static UnityBridgeProjectSummary BuildProjectSummary()
        {
            var summary = new UnityBridgeProjectSummary
            {
                projectName = Application.productName,
                projectPath = Application.dataPath.Replace("/Assets", string.Empty),
                unityVersion = Application.unityVersion,
                companyName = Application.companyName,
                productName = Application.productName,
                activeScene = SceneManager.GetActiveScene().path
            };

            foreach (var tag in InternalEditorUtility.tags)
            {
                summary.tags.Add(tag);
            }

            var layers = InternalEditorUtility.layers;
            foreach (var layer in layers)
            {
                summary.layers.Add(layer);
            }

            var buildScenes = EditorBuildSettings.scenes;
            for (var index = 0; index < buildScenes.Length; index++)
            {
                var scene = buildScenes[index];
                summary.buildScenes.Add(new UnityBridgeBuildSceneRef
                {
                    path = scene.path,
                    enabled = scene.enabled,
                    buildIndex = index
                });
            }

            return summary;
        }

        public static UnityBridgeRuntimeSummary BuildRuntimeSummary()
        {
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var audioListeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var animators = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var rigidbodies = UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var rigidbodies2D = UnityEngine.Object.FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            return new UnityBridgeRuntimeSummary
            {
                isPlaying = EditorApplication.isPlaying,
                isPaused = EditorApplication.isPaused,
                inPlayMode = EditorApplication.isPlayingOrWillChangePlaymode,
                frameCount = Time.frameCount,
                timeScale = Time.timeScale,
                fixedDeltaTime = Time.fixedDeltaTime,
                maximumDeltaTime = Time.maximumDeltaTime,
                targetFrameRate = Application.targetFrameRate,
                realtimeSinceStartup = Time.realtimeSinceStartup,
                unscaledTime = Time.unscaledTime,
                timeSinceLevelLoad = Time.timeSinceLevelLoad,
                activeScene = SceneManager.GetActiveScene().path,
                loadedSceneCount = SceneManager.sceneCount,
                cameraCount = cameras.Length,
                activeCameraCount = cameras.Count(camera => camera != null && camera.isActiveAndEnabled),
                audioListenerCount = audioListeners.Length,
                animatorCount = animators.Length,
                rigidbodyCount = rigidbodies.Length,
                rigidbody2DCount = rigidbodies2D.Length
            };
        }

        public static UnityBridgeBuildSummary BuildBuildSummary()
        {
            var lastBuildReport = UnityBridgeBuildTracker.GetLastBuildReport();
            return new UnityBridgeBuildSummary
            {
                activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                selectedBuildTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget).ToString(),
                activePlatformName = EditorUserBuildSettings.activeBuildTarget.ToString(),
                developmentBuild = EditorUserBuildSettings.development,
                connectProfiler = EditorUserBuildSettings.connectProfiler,
                allowDebugging = EditorUserBuildSettings.allowDebugging,
                buildAppBundle = EditorUserBuildSettings.buildAppBundle,
                locationPathName = EditorUserBuildSettings.GetBuildLocation(EditorUserBuildSettings.activeBuildTarget),
                buildInProgress = UnityBridgeBuildTracker.IsBuildRunning,
                currentBuildStartedAtUtc = UnityBridgeBuildTracker.CurrentBuildStartedAtUtc,
                lastBuildFinishedAtUtc = lastBuildReport.finishedAtUtc,
                lastBuildResult = lastBuildReport.result,
                lastBuildOutputPath = lastBuildReport.outputPath
            };
        }

        public static UnityBridgePlayerSettingsSummary BuildPlayerSettingsSummary()
        {
            var activeTarget = EditorUserBuildSettings.activeBuildTarget;
            var activeGroup = BuildPipeline.GetBuildTargetGroup(activeTarget);
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(activeGroup);
            var defineSymbols = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget) ?? string.Empty;

            var summary = new UnityBridgePlayerSettingsSummary
            {
                activeBuildTarget = activeTarget.ToString(),
                activeBuildTargetGroup = activeGroup.ToString(),
                namedBuildTarget = namedBuildTarget.TargetName,
                applicationIdentifier = PlayerSettings.GetApplicationIdentifier(namedBuildTarget),
                scriptingBackend = PlayerSettings.GetScriptingBackend(namedBuildTarget).ToString(),
                apiCompatibilityLevel = PlayerSettings.GetApiCompatibilityLevel(namedBuildTarget).ToString(),
                managedStrippingLevel = PlayerSettings.GetManagedStrippingLevel(namedBuildTarget).ToString(),
                incrementalIl2CppBuild = PlayerSettings.GetIncrementalIl2CppBuild(namedBuildTarget),
                captureStartupLogs = PlayerSettings.GetCaptureStartupLogs(namedBuildTarget),
                scriptingDefineSymbols = defineSymbols
            };

            foreach (var symbol in defineSymbols.Split(';'))
            {
                var trimmed = symbol.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    summary.scriptingDefineSymbolList.Add(trimmed);
                }
            }

            return summary;
        }

        public static UnityBridgePhysicsSettingsSummary BuildPhysicsSettingsSummary()
        {
            var physics3DObject = LoadProjectSettingsAsset("ProjectSettings/DynamicsManager.asset");
            var physics2DObject = LoadProjectSettingsAsset("ProjectSettings/Physics2DSettings.asset");
            if (physics3DObject == null || physics2DObject == null)
            {
                return new UnityBridgePhysicsSettingsSummary
                {
                    message = "Physics project settings assets were not found."
                };
            }

            var physics3DSerialized = new SerializedObject(physics3DObject);
            var physics2DSerialized = new SerializedObject(physics2DObject);
            return new UnityBridgePhysicsSettingsSummary
            {
                physics3D = BuildPhysics3DSettingsSummary(physics3DSerialized),
                physics2D = BuildPhysics2DSettingsSummary(physics2DSerialized),
                message = "Read global physics settings."
            };
        }

        public static UnityBridgeProjectTagLayerSummary BuildProjectTagLayerSummary()
        {
            var tagManager = LoadProjectSettingsAsset("ProjectSettings/TagManager.asset");
            if (tagManager == null)
            {
                return new UnityBridgeProjectTagLayerSummary
                {
                    message = "TagManager project settings asset was not found."
                };
            }

            var serializedObject = new SerializedObject(tagManager);
            var summary = new UnityBridgeProjectTagLayerSummary
            {
                message = "Read project tags, layers, and sorting layers."
            };

            var tagsProperty = serializedObject.FindProperty("tags");
            for (var index = 0; index < tagsProperty.arraySize; index++)
            {
                var value = tagsProperty.GetArrayElementAtIndex(index).stringValue;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    summary.tags.Add(value);
                }
            }

            var layersProperty = serializedObject.FindProperty("layers");
            for (var index = 0; index < layersProperty.arraySize; index++)
            {
                summary.layers.Add(new UnityBridgeNamedIndexValue
                {
                    index = index,
                    name = layersProperty.GetArrayElementAtIndex(index).stringValue ?? string.Empty
                });
            }

            var sortingLayersProperty = serializedObject.FindProperty("m_SortingLayers");
            for (var index = 0; index < sortingLayersProperty.arraySize; index++)
            {
                var element = sortingLayersProperty.GetArrayElementAtIndex(index);
                summary.sortingLayers.Add(new UnityBridgeSortingLayerValue
                {
                    index = index,
                    name = element.FindPropertyRelative("name").stringValue,
                    uniqueId = element.FindPropertyRelative("uniqueID").intValue,
                    locked = element.FindPropertyRelative("locked").boolValue
                });
            }

            var renderingLayersProperty = serializedObject.FindProperty("m_RenderingLayers");
            if (renderingLayersProperty != null)
            {
                for (var index = 0; index < renderingLayersProperty.arraySize; index++)
                {
                    summary.renderingLayers.Add(new UnityBridgeNamedIndexValue
                    {
                        index = index,
                        name = renderingLayersProperty.GetArrayElementAtIndex(index).stringValue ?? string.Empty
                    });
                }
            }

            return summary;
        }

        public static UnityBridgeQualitySettingsSummary BuildQualitySettingsSummary()
        {
            var qualitySettings = LoadProjectSettingsAsset("ProjectSettings/QualitySettings.asset");
            if (qualitySettings == null)
            {
                return new UnityBridgeQualitySettingsSummary
                {
                    message = "QualitySettings project settings asset was not found."
                };
            }

            var serializedObject = new SerializedObject(qualitySettings);
            var summary = new UnityBridgeQualitySettingsSummary
            {
                currentQualityIndex = serializedObject.FindProperty("m_CurrentQuality").intValue,
                message = "Read project quality settings."
            };

            var qualityLevelsProperty = serializedObject.FindProperty("m_QualitySettings");
            for (var index = 0; index < qualityLevelsProperty.arraySize; index++)
            {
                var level = qualityLevelsProperty.GetArrayElementAtIndex(index);
                var qualityLevel = new UnityBridgeQualityLevelSummary
                {
                    index = index,
                    name = level.FindPropertyRelative("name").stringValue,
                    pixelLightCount = level.FindPropertyRelative("pixelLightCount").intValue,
                    shadows = level.FindPropertyRelative("shadows").intValue,
                    shadowResolution = level.FindPropertyRelative("shadowResolution").intValue,
                    shadowProjection = level.FindPropertyRelative("shadowProjection").intValue,
                    shadowCascades = level.FindPropertyRelative("shadowCascades").intValue,
                    shadowDistance = level.FindPropertyRelative("shadowDistance").floatValue,
                    globalTextureMipmapLimit = level.FindPropertyRelative("globalTextureMipmapLimit").intValue,
                    anisotropicTextures = level.FindPropertyRelative("anisotropicTextures").intValue,
                    antiAliasing = level.FindPropertyRelative("antiAliasing").intValue,
                    vSyncCount = level.FindPropertyRelative("vSyncCount").intValue,
                    lodBias = level.FindPropertyRelative("lodBias").floatValue,
                    maximumLODLevel = level.FindPropertyRelative("maximumLODLevel").intValue,
                    streamingMipmapsActive = level.FindPropertyRelative("streamingMipmapsActive").boolValue,
                    streamingMipmapsMemoryBudget = level.FindPropertyRelative("streamingMipmapsMemoryBudget").floatValue,
                    customRenderPipelineGuid = level.FindPropertyRelative("customRenderPipeline").objectReferenceValue != null
                        ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(level.FindPropertyRelative("customRenderPipeline").objectReferenceValue))
                        : string.Empty
                };
                summary.qualityLevels.Add(qualityLevel);
            }

            if (summary.currentQualityIndex >= 0 && summary.currentQualityIndex < summary.qualityLevels.Count)
            {
                summary.currentQualityName = summary.qualityLevels[summary.currentQualityIndex].name;
            }

            var platformDefaultsProperty = serializedObject.FindProperty("m_PerPlatformDefaultQuality");
            if (platformDefaultsProperty != null)
            {
                for (var index = 0; index < platformDefaultsProperty.arraySize; index++)
                {
                    var entry = platformDefaultsProperty.GetArrayElementAtIndex(index);
                    summary.perPlatformDefaults.Add(new UnityBridgePlatformQualityValue
                    {
                        platform = entry.displayName,
                        qualityIndex = entry.intValue
                    });
                }
            }

            return summary;
        }

        public static UnityBridgeAudioSettingsSummary BuildAudioSettingsSummary()
        {
            var audioManager = LoadProjectSettingsAsset("ProjectSettings/AudioManager.asset");
            if (audioManager == null)
            {
                return new UnityBridgeAudioSettingsSummary
                {
                    message = "AudioManager project settings asset was not found."
                };
            }

            var serializedObject = new SerializedObject(audioManager);
            return new UnityBridgeAudioSettingsSummary
            {
                volume = serializedObject.FindProperty("m_Volume").floatValue,
                rolloffScale = serializedObject.FindProperty("Rolloff Scale").floatValue,
                dopplerFactor = serializedObject.FindProperty("Doppler Factor").floatValue,
                defaultSpeakerMode = serializedObject.FindProperty("Default Speaker Mode").intValue,
                sampleRate = serializedObject.FindProperty("m_SampleRate").intValue,
                dspBufferSize = serializedObject.FindProperty("m_DSPBufferSize").intValue,
                virtualVoiceCount = serializedObject.FindProperty("m_VirtualVoiceCount").intValue,
                realVoiceCount = serializedObject.FindProperty("m_RealVoiceCount").intValue,
                spatializerPlugin = serializedObject.FindProperty("m_SpatializerPlugin").stringValue,
                ambisonicDecoderPlugin = serializedObject.FindProperty("m_AmbisonicDecoderPlugin").stringValue,
                disableAudio = serializedObject.FindProperty("m_DisableAudio").boolValue,
                virtualizeEffects = serializedObject.FindProperty("m_VirtualizeEffects").boolValue,
                requestedDspBufferSize = serializedObject.FindProperty("m_RequestedDSPBufferSize").intValue,
                message = "Read project audio settings."
            };
        }

        public static UnityBridgeTimeSettingsSummary BuildTimeSettingsSummary()
        {
            var timeManagerPath = GetProjectSettingsPath("TimeManager.asset");
            if (!System.IO.File.Exists(timeManagerPath))
            {
                return new UnityBridgeTimeSettingsSummary
                {
                    message = "TimeManager project settings asset was not found."
                };
            }

            var values = ReadTimeManagerValues(timeManagerPath);
            return new UnityBridgeTimeSettingsSummary
            {
                fixedTimestep = values.fixedTimestep,
                maximumAllowedTimestep = values.maximumAllowedTimestep,
                timeScale = values.timeScale,
                maximumParticleTimestep = values.maximumParticleTimestep,
                message = "Read project time settings."
            };
        }

        public static UnityBridgeTextureImporterSummary BuildTextureImporterSummary(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return new UnityBridgeTextureImporterSummary
                {
                    assetPath = assetPath,
                    found = false,
                    message = "Texture asset path is required."
                };
            }

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return new UnityBridgeTextureImporterSummary
                {
                    assetPath = assetPath,
                    found = false,
                    message = "TextureImporter was not found for asset."
                };
            }

            return new UnityBridgeTextureImporterSummary
            {
                assetPath = assetPath,
                found = true,
                textureType = importer.textureType.ToString(),
                spriteImportMode = importer.spriteImportMode.ToString(),
                isReadable = importer.isReadable,
                mipmapEnabled = importer.mipmapEnabled,
                alphaIsTransparency = importer.alphaIsTransparency,
                filterMode = importer.filterMode.ToString(),
                wrapMode = importer.wrapMode.ToString(),
                maxTextureSize = importer.maxTextureSize,
                textureCompression = importer.textureCompression.ToString(),
                message = "Read texture importer settings."
            };
        }

        public static List<UnityBridgeSceneStats> BuildSceneStats()
        {
            var stats = new List<UnityBridgeSceneStats>();
            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                var roots = scene.GetRootGameObjects();
                var sceneStats = new UnityBridgeSceneStats
                {
                    scenePath = scene.path,
                    rootCount = roots.Length
                };

                foreach (var root in roots)
                {
                    AccumulateSceneStats(root.transform, sceneStats);
                }

                stats.Add(sceneStats);
            }

            return stats;
        }

        public static UnityBridgePrefabStageInfo BuildPrefabStageInfo()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
            {
                return new UnityBridgePrefabStageInfo
                {
                    isOpen = false
                };
            }

            return new UnityBridgePrefabStageInfo
            {
                isOpen = true,
                assetPath = stage.assetPath,
                prefabContentsRootName = stage.prefabContentsRoot != null ? stage.prefabContentsRoot.name : string.Empty,
                scenePath = stage.scene.path
            };
        }

        public static List<UnityBridgeEditorWindowInfo> BuildEditorWindows()
        {
            var windows = new List<UnityBridgeEditorWindowInfo>();
            var editorWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            foreach (var window in editorWindows)
            {
                if (window == null)
                {
                    continue;
                }

                windows.Add(new UnityBridgeEditorWindowInfo
                {
                    title = window.titleContent != null ? window.titleContent.text : string.Empty,
                    type = window.GetType().FullName,
                    focused = EditorWindow.focusedWindow == window,
                    hasFocus = window.hasFocus
                });
            }

            windows.Sort((left, right) => string.CompareOrdinal(left.title, right.title));
            return windows;
        }

        public static EditorWindow FindEditorWindow(string typeName, string title)
        {
            var editorWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            foreach (var window in editorWindows)
            {
                if (window == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(typeName))
                {
                    var fullName = window.GetType().FullName ?? string.Empty;
                    var shortName = window.GetType().Name ?? string.Empty;
                    if (string.Equals(fullName, typeName, System.StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(shortName, typeName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return window;
                    }
                }

                if (!string.IsNullOrWhiteSpace(title))
                {
                    var windowTitle = window.titleContent != null ? window.titleContent.text : string.Empty;
                    if (string.Equals(windowTitle, title, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return window;
                    }
                }
            }

            return null;
        }

        public static List<UnityBridgePackageInfo> BuildPackages()
        {
            var packages = new List<UnityBridgePackageInfo>();
            var allPackages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            foreach (var package in allPackages)
            {
                packages.Add(new UnityBridgePackageInfo
                {
                    name = package.name,
                    displayName = package.displayName,
                    version = package.version,
                    source = package.source.ToString(),
                    resolvedPath = package.resolvedPath,
                    isDirectDependency = package.isDirectDependency
                });
            }

            packages.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            return packages;
        }

        public static List<UnityBridgeObjectRef> BuildSelection()
        {
            var selection = new List<UnityBridgeObjectRef>();
            foreach (var selectedObject in Selection.objects)
            {
                selection.Add(ToObjectRef(selectedObject));
            }

            return selection;
        }

        public static List<UnityBridgeHierarchyScene> BuildHierarchy()
        {
            var hierarchy = new List<UnityBridgeHierarchyScene>();
            var activeScene = SceneManager.GetActiveScene();

            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                var hierarchyScene = new UnityBridgeHierarchyScene
                {
                    name = scene.name,
                    path = scene.path,
                    active = scene == activeScene
                };

                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    hierarchyScene.roots.Add(BuildHierarchyNode(root, root.name));
                }

                hierarchy.Add(hierarchyScene);
            }

            return hierarchy;
        }

        public static GameObject FindGameObjectByHierarchyPath(string hierarchyPath)
        {
            if (string.IsNullOrWhiteSpace(hierarchyPath))
            {
                return null;
            }

            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    var match = FindGameObjectRecursive(root.transform, hierarchyPath, root.name);
                    if (match != null)
                    {
                        return match.gameObject;
                    }
                }
            }

            return null;
        }

        public static UnityBridgeObjectDetails BuildActiveObjectDetails()
        {
            return BuildObjectDetails(Selection.activeObject);
        }

        public static UnityBridgeObjectDetails BuildHierarchyObjectDetails(string hierarchyPath)
        {
            var gameObject = FindGameObjectByHierarchyPath(hierarchyPath);
            return BuildObjectDetails(gameObject);
        }

        public static UnityBridgeObjectDetails BuildAssetDetails(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            return BuildObjectDetails(asset);
        }

        public static List<UnityBridgeObjectRef> BuildSceneAssets()
        {
            return SearchAssets("t:Scene");
        }

        public static UnityBridgeAssetSearchResult SearchAssetsResult(string query, string type)
        {
            var result = new UnityBridgeAssetSearchResult
            {
                query = query,
                type = type
            };

            var searchQuery = string.IsNullOrWhiteSpace(type)
                ? query
                : string.IsNullOrWhiteSpace(query) ? "t:" + type : query + " t:" + type;

            foreach (var asset in SearchAssets(searchQuery))
            {
                result.assets.Add(asset);
            }

            return result;
        }

        public static UnityBridgeHierarchySearchResult SearchHierarchy(UnityBridgeHierarchySearchRequest request)
        {
            request = request ?? new UnityBridgeHierarchySearchRequest();

            var result = new UnityBridgeHierarchySearchResult
            {
                query = request.query,
                scenePath = request.scenePath,
                tag = request.tag,
                componentType = request.componentType,
                includeInactive = request.includeInactive
            };

            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!string.IsNullOrWhiteSpace(request.scenePath) && scene.path != request.scenePath)
                {
                    continue;
                }

                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    SearchHierarchyRecursive(root.transform, root.name, request, result.objects);
                }
            }

            return result;
        }

        public static UnityBridgeRuntimeQueryResult QueryRuntimeObjects(UnityBridgeRuntimeQueryRequest request)
        {
            request = request ?? new UnityBridgeRuntimeQueryRequest();

            var result = new UnityBridgeRuntimeQueryResult
            {
                query = request.query,
                scenePath = request.scenePath,
                tag = request.tag,
                componentType = request.componentType,
                includeInactive = request.includeInactive,
                limit = request.limit > 0 ? request.limit : 100
            };

            var gameObjects = UnityEngine.Object.FindObjectsByType<GameObject>(
                request.includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (var gameObject in gameObjects)
            {
                if (gameObject == null || !MatchesRuntimeQuery(gameObject, request))
                {
                    continue;
                }

                result.objects.Add(BuildRuntimeObjectSnapshot(gameObject));
                if (result.objects.Count >= result.limit)
                {
                    break;
                }
            }

            return result;
        }

        public static UnityBridgeObjectDetails BuildRuntimeObjectDetails(string hierarchyPath)
        {
            var gameObject = FindGameObjectByHierarchyPath(hierarchyPath);
            return BuildObjectDetails(gameObject);
        }

        public static UnityBridgeAssetDependencyResult BuildAssetDependencies(string assetPath, bool recursive)
        {
            var result = new UnityBridgeAssetDependencyResult
            {
                path = assetPath,
                recursive = recursive
            };

            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return result;
            }

            var dependencies = AssetDatabase.GetDependencies(assetPath, recursive);
            foreach (var dependencyPath in dependencies)
            {
                if (dependencyPath == assetPath)
                {
                    continue;
                }

                var asset = AssetDatabase.LoadMainAssetAtPath(dependencyPath);
                if (asset != null)
                {
                    result.dependencies.Add(ToObjectRef(asset));
                }
            }

            return result;
        }

        public static UnityBridgeAssetPathResult ResolveAssetPath(string path, string guid)
        {
            var result = new UnityBridgeAssetPathResult();

            if (!string.IsNullOrWhiteSpace(path))
            {
                result.path = path;
                result.guid = AssetDatabase.AssetPathToGUID(path);
                result.exists = !string.IsNullOrWhiteSpace(result.guid);
                return result;
            }

            if (!string.IsNullOrWhiteSpace(guid))
            {
                result.guid = guid;
                result.path = AssetDatabase.GUIDToAssetPath(guid);
                result.exists = !string.IsNullOrWhiteSpace(result.path);
                return result;
            }

            return result;
        }

        private static UnityBridgeObjectRef ToObjectRef(Object unityObject)
        {
            var assetPath = AssetDatabase.GetAssetPath(unityObject);
            return new UnityBridgeObjectRef
            {
                name = unityObject.name,
                path = string.IsNullOrWhiteSpace(assetPath) ? BuildHierarchyPath(unityObject) : assetPath,
                type = unityObject.GetType().FullName,
                instanceId = unityObject.GetInstanceID(),
                persistent = EditorUtility.IsPersistent(unityObject)
            };
        }

        public static string BuildHierarchyPath(Object unityObject)
        {
            var gameObject = unityObject as GameObject;
            if (gameObject == null)
            {
                return unityObject.name;
            }

            var parts = new Stack<string>();
            var current = gameObject.transform;
            while (current != null)
            {
                parts.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", parts);
        }

        private static UnityBridgeObjectDetails BuildObjectDetails(Object unityObject)
        {
            if (unityObject == null)
            {
                return null;
            }

            var assetPath = AssetDatabase.GetAssetPath(unityObject);
            var details = new UnityBridgeObjectDetails
            {
                reference = ToObjectRef(unityObject),
                assetPath = assetPath,
                hierarchyPath = string.IsNullOrWhiteSpace(assetPath) ? BuildHierarchyPath(unityObject) : null,
                persistent = EditorUtility.IsPersistent(unityObject)
            };

            var gameObject = unityObject as GameObject;
            if (gameObject != null)
            {
                details.scenePath = gameObject.scene.path;
                details.parentPath = gameObject.transform.parent != null ? BuildHierarchyPath(gameObject.transform.parent.gameObject) : null;
                details.childCount = gameObject.transform.childCount;
                details.transform = BuildTransformSnapshot(gameObject.transform);

                var components = gameObject.GetComponents<Component>();
                for (var componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    var component = components[componentIndex];
                    details.components.Add(new UnityBridgeComponentDetails
                    {
                        type = component == null ? "MissingComponent" : component.GetType().FullName,
                        name = component == null ? "MissingComponent" : component.GetType().Name,
                        index = componentIndex,
                        serializedProperties = component == null ? new List<UnityBridgeSerializedPropertyDetails>() : BuildSerializedProperties(component)
                    });
                }

                details.serializedProperties = BuildSerializedProperties(gameObject);

                return details;
            }

            var componentObject = unityObject as Component;
            if (componentObject != null)
            {
                details.scenePath = componentObject.gameObject.scene.path;
                details.parentPath = componentObject.transform.parent != null ? BuildHierarchyPath(componentObject.transform.parent.gameObject) : null;
                details.childCount = componentObject.transform.childCount;
                details.transform = BuildTransformSnapshot(componentObject.transform);
                details.components.Add(new UnityBridgeComponentDetails
                {
                    type = componentObject.GetType().FullName,
                    name = componentObject.GetType().Name,
                    index = 0,
                    serializedProperties = BuildSerializedProperties(componentObject)
                });
                details.serializedProperties = BuildSerializedProperties(componentObject);
            }

            return details;
        }

        private static List<UnityBridgeObjectRef> SearchAssets(string searchQuery)
        {
            var results = new List<UnityBridgeObjectRef>();
            var guids = AssetDatabase.FindAssets(searchQuery);
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (asset != null)
                {
                    results.Add(ToObjectRef(asset));
                }
            }

            return results;
        }

        private static void SearchHierarchyRecursive(Transform current, string currentPath, UnityBridgeHierarchySearchRequest request, List<UnityBridgeObjectRef> results)
        {
            var gameObject = current.gameObject;
            if ((request.includeInactive || gameObject.activeInHierarchy) && MatchesHierarchySearch(gameObject, currentPath, request))
            {
                results.Add(ToObjectRef(gameObject));
            }

            for (var childIndex = 0; childIndex < current.childCount; childIndex++)
            {
                var child = current.GetChild(childIndex);
                SearchHierarchyRecursive(child, currentPath + "/" + child.name, request, results);
            }
        }

        private static bool MatchesRuntimeQuery(GameObject gameObject, UnityBridgeRuntimeQueryRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.scenePath) && gameObject.scene.path != request.scenePath)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(request.query))
            {
                var hierarchyPath = BuildHierarchyPath(gameObject);
                var matchesName = gameObject.name.IndexOf(request.query, System.StringComparison.OrdinalIgnoreCase) >= 0;
                var matchesPath = hierarchyPath.IndexOf(request.query, System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!matchesName && !matchesPath)
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(request.tag) && gameObject.tag != request.tag)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(request.componentType))
            {
                var components = gameObject.GetComponents<Component>();
                var matchedComponent = false;
                foreach (var component in components)
                {
                    if (component == null)
                    {
                        continue;
                    }

                    var fullName = component.GetType().FullName ?? string.Empty;
                    var shortName = component.GetType().Name ?? string.Empty;
                    if (fullName.IndexOf(request.componentType, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        shortName.IndexOf(request.componentType, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchedComponent = true;
                        break;
                    }
                }

                if (!matchedComponent)
                {
                    return false;
                }
            }

            return request.includeInactive || gameObject.activeInHierarchy;
        }

        private static UnityBridgeRuntimeObjectSnapshot BuildRuntimeObjectSnapshot(GameObject gameObject)
        {
            return new UnityBridgeRuntimeObjectSnapshot
            {
                reference = ToObjectRef(gameObject),
                hierarchyPath = BuildHierarchyPath(gameObject),
                scenePath = gameObject.scene.path,
                activeSelf = gameObject.activeSelf,
                activeInHierarchy = gameObject.activeInHierarchy,
                tag = gameObject.tag,
                layer = gameObject.layer,
                childCount = gameObject.transform.childCount,
                componentCount = gameObject.GetComponents<Component>().Length
            };
        }

        private static void AccumulateSceneStats(Transform current, UnityBridgeSceneStats stats)
        {
            stats.gameObjectCount += 1;
            if (!current.gameObject.activeInHierarchy)
            {
                stats.inactiveCount += 1;
            }

            stats.componentCount += current.gameObject.GetComponents<Component>().Length;

            for (var childIndex = 0; childIndex < current.childCount; childIndex++)
            {
                AccumulateSceneStats(current.GetChild(childIndex), stats);
            }
        }

        private static bool MatchesHierarchySearch(GameObject gameObject, string currentPath, UnityBridgeHierarchySearchRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.query))
            {
                var matchesName = gameObject.name.IndexOf(request.query, System.StringComparison.OrdinalIgnoreCase) >= 0;
                var matchesPath = currentPath.IndexOf(request.query, System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!matchesName && !matchesPath)
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(request.tag) && gameObject.tag != request.tag)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(request.componentType))
            {
                var components = gameObject.GetComponents<Component>();
                var matchedComponent = false;
                foreach (var component in components)
                {
                    if (component == null)
                    {
                        continue;
                    }

                    var fullName = component.GetType().FullName ?? string.Empty;
                    var shortName = component.GetType().Name ?? string.Empty;
                    if (fullName.IndexOf(request.componentType, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        shortName.IndexOf(request.componentType, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchedComponent = true;
                        break;
                    }
                }

                if (!matchedComponent)
                {
                    return false;
                }
            }

            return true;
        }

        private static UnityBridgeHierarchyNode BuildHierarchyNode(GameObject gameObject, string hierarchyPath)
        {
            var node = new UnityBridgeHierarchyNode
            {
                name = gameObject.name,
                path = hierarchyPath,
                activeSelf = gameObject.activeSelf,
                activeInHierarchy = gameObject.activeInHierarchy,
                tag = gameObject.tag,
                layer = gameObject.layer
            };

            var components = gameObject.GetComponents<Component>();
            foreach (var component in components)
            {
                node.componentTypes.Add(component == null ? "MissingComponent" : component.GetType().FullName);
            }

            var transform = gameObject.transform;
            for (var childIndex = 0; childIndex < transform.childCount; childIndex++)
            {
                var child = transform.GetChild(childIndex);
                node.children.Add(BuildHierarchyNode(child.gameObject, hierarchyPath + "/" + child.name));
            }

            return node;
        }

        private static Transform FindGameObjectRecursive(Transform current, string targetPath, string currentPath)
        {
            if (currentPath == targetPath)
            {
                return current;
            }

            for (var childIndex = 0; childIndex < current.childCount; childIndex++)
            {
                var child = current.GetChild(childIndex);
                var childPath = currentPath + "/" + child.name;
                var match = FindGameObjectRecursive(child, targetPath, childPath);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static UnityBridgeTransformSnapshot BuildTransformSnapshot(Transform transform)
        {
            if (transform == null)
            {
                return null;
            }

            return new UnityBridgeTransformSnapshot
            {
                localPosition = ToVector3Value(transform.localPosition),
                localEulerAngles = ToVector3Value(transform.localEulerAngles),
                localScale = ToVector3Value(transform.localScale),
                worldPosition = ToVector3Value(transform.position)
            };
        }

        private static UnityBridgeVector3Value ToVector3Value(Vector3 value)
        {
            return new UnityBridgeVector3Value
            {
                x = value.x,
                y = value.y,
                z = value.z
            };
        }

        private static UnityBridgeVector2Value ToVector2Value(Vector2 value)
        {
            return new UnityBridgeVector2Value
            {
                x = value.x,
                y = value.y
            };
        }

        private static UnityBridgePhysics3DSettingsSummary BuildPhysics3DSettingsSummary(SerializedObject serializedObject)
        {
            return new UnityBridgePhysics3DSettingsSummary
            {
                gravity = ToVector3Value(serializedObject.FindProperty("m_Gravity").vector3Value),
                bounceThreshold = serializedObject.FindProperty("m_BounceThreshold").floatValue,
                defaultMaxDepenetrationVelocity = serializedObject.FindProperty("m_DefaultMaxDepenetrationVelocity").floatValue,
                sleepThreshold = serializedObject.FindProperty("m_SleepThreshold").floatValue,
                defaultContactOffset = serializedObject.FindProperty("m_DefaultContactOffset").floatValue,
                defaultSolverIterations = serializedObject.FindProperty("m_DefaultSolverIterations").intValue,
                defaultSolverVelocityIterations = serializedObject.FindProperty("m_DefaultSolverVelocityIterations").intValue,
                queriesHitBackfaces = serializedObject.FindProperty("m_QueriesHitBackfaces").boolValue,
                queriesHitTriggers = serializedObject.FindProperty("m_QueriesHitTriggers").boolValue,
                enableAdaptiveForce = serializedObject.FindProperty("m_EnableAdaptiveForce").boolValue,
                simulationMode = ((SimulationMode)serializedObject.FindProperty("m_SimulationMode").intValue).ToString(),
                autoSyncTransforms = serializedObject.FindProperty("m_AutoSyncTransforms").boolValue,
                reuseCollisionCallbacks = serializedObject.FindProperty("m_ReuseCollisionCallbacks").boolValue,
                invokeCollisionCallbacks = serializedObject.FindProperty("m_InvokeCollisionCallbacks").boolValue,
                contactPairsMode = ReadSerializedEnumValue(serializedObject.FindProperty("m_ContactPairsMode")),
                broadphaseType = ReadSerializedEnumValue(serializedObject.FindProperty("m_BroadphaseType")),
                frictionType = ReadSerializedEnumValue(serializedObject.FindProperty("m_FrictionType")),
                enableEnhancedDeterminism = serializedObject.FindProperty("m_EnableEnhancedDeterminism").boolValue,
                improvedPatchFriction = serializedObject.FindProperty("m_ImprovedPatchFriction").boolValue,
                generateOnTriggerStayEvents = serializedObject.FindProperty("m_GenerateOnTriggerStayEvents").boolValue,
                solverType = ReadSerializedEnumValue(serializedObject.FindProperty("m_SolverType")),
                defaultMaxAngularSpeed = serializedObject.FindProperty("m_DefaultMaxAngularSpeed").floatValue,
                fastMotionThreshold = serializedObject.FindProperty("m_FastMotionThreshold").floatValue,
                incrementalStaticBroadphase = serializedObject.FindProperty("m_IncrementalStaticBroadphase").boolValue
            };
        }

        private static UnityBridgePhysics2DSettingsSummary BuildPhysics2DSettingsSummary(SerializedObject serializedObject)
        {
            return new UnityBridgePhysics2DSettingsSummary
            {
                gravity = ToVector2Value(serializedObject.FindProperty("m_Gravity").vector2Value),
                velocityIterations = serializedObject.FindProperty("m_VelocityIterations").intValue,
                positionIterations = serializedObject.FindProperty("m_PositionIterations").intValue,
                bounceThreshold = serializedObject.FindProperty("m_BounceThreshold").floatValue,
                maxLinearCorrection = serializedObject.FindProperty("m_MaxLinearCorrection").floatValue,
                maxAngularCorrection = serializedObject.FindProperty("m_MaxAngularCorrection").floatValue,
                maxTranslationSpeed = serializedObject.FindProperty("m_MaxTranslationSpeed").floatValue,
                maxRotationSpeed = serializedObject.FindProperty("m_MaxRotationSpeed").floatValue,
                baumgarteScale = serializedObject.FindProperty("m_BaumgarteScale").floatValue,
                baumgarteTimeOfImpactScale = serializedObject.FindProperty("m_BaumgarteTimeOfImpactScale").floatValue,
                timeToSleep = serializedObject.FindProperty("m_TimeToSleep").floatValue,
                linearSleepTolerance = serializedObject.FindProperty("m_LinearSleepTolerance").floatValue,
                angularSleepTolerance = serializedObject.FindProperty("m_AngularSleepTolerance").floatValue,
                defaultContactOffset = serializedObject.FindProperty("m_DefaultContactOffset").floatValue,
                simulationMode = ((SimulationMode2D)serializedObject.FindProperty("m_SimulationMode").intValue).ToString(),
                maxSubStepCount = serializedObject.FindProperty("m_MaxSubStepCount").intValue,
                minSubStepFPS = serializedObject.FindProperty("m_MinSubStepFPS").intValue,
                useSubStepping = serializedObject.FindProperty("m_UseSubStepping").boolValue,
                useSubStepContacts = serializedObject.FindProperty("m_UseSubStepContacts").boolValue,
                queriesHitTriggers = serializedObject.FindProperty("m_QueriesHitTriggers").boolValue,
                queriesStartInColliders = serializedObject.FindProperty("m_QueriesStartInColliders").boolValue,
                callbacksOnDisable = serializedObject.FindProperty("m_CallbacksOnDisable").boolValue,
                reuseCollisionCallbacks = serializedObject.FindProperty("m_ReuseCollisionCallbacks").boolValue,
                autoSyncTransforms = serializedObject.FindProperty("m_AutoSyncTransforms").boolValue
            };
        }

        private static Object LoadProjectSettingsAsset(string assetPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(assetPath).FirstOrDefault(asset => asset != null);
        }

        private static string ReadSerializedEnumValue(SerializedProperty property)
        {
            if (property == null)
            {
                return string.Empty;
            }

            if (property.propertyType == SerializedPropertyType.Enum &&
                property.enumDisplayNames != null &&
                property.enumValueIndex >= 0 &&
                property.enumValueIndex < property.enumDisplayNames.Length)
            {
                return property.enumDisplayNames[property.enumValueIndex];
            }

            return property.intValue.ToString(CultureInfo.InvariantCulture);
        }

        private static (float fixedTimestep, float maximumAllowedTimestep, float timeScale, float maximumParticleTimestep) ReadTimeManagerValues(string path)
        {
            var result = (fixedTimestep: 0f, maximumAllowedTimestep: 0f, timeScale: 0f, maximumParticleTimestep: 0f);
            var lines = System.IO.File.ReadAllLines(path);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (line.StartsWith("  Fixed Timestep:", System.StringComparison.Ordinal))
                {
                    if (TryParseSerializedRational(lines, index + 1, out var fixedTimestep))
                    {
                        result.fixedTimestep = fixedTimestep;
                    }
                }
                else if (TryParseProjectSettingsFloat(line, "  Maximum Allowed Timestep: ", out var maximumAllowedTimestep))
                {
                    result.maximumAllowedTimestep = maximumAllowedTimestep;
                }
                else if (TryParseProjectSettingsFloat(line, "  m_TimeScale: ", out var timeScale))
                {
                    result.timeScale = timeScale;
                }
                else if (TryParseProjectSettingsFloat(line, "  Maximum Particle Timestep: ", out var maximumParticleTimestep))
                {
                    result.maximumParticleTimestep = maximumParticleTimestep;
                }
            }

            return result;
        }

        private static bool TryParseSerializedRational(string[] lines, int startIndex, out float value)
        {
            value = 0f;
            long count = 0;
            long denominator = 0;
            long numerator = 0;

            for (var index = startIndex; index < System.Math.Min(startIndex + 4, lines.Length); index++)
            {
                var line = lines[index];
                if (TryParseProjectSettingsLong(line, "    m_Count: ", out var parsedCount))
                {
                    count = parsedCount;
                }
                else if (TryParseProjectSettingsLong(line, "      m_Denominator: ", out var parsedDenominator))
                {
                    denominator = parsedDenominator;
                }
                else if (TryParseProjectSettingsLong(line, "      m_Numerator: ", out var parsedNumerator))
                {
                    numerator = parsedNumerator;
                }
            }

            if (numerator == 0)
            {
                return false;
            }

            value = (float)(count * (double)denominator / numerator);
            return true;
        }

        private static bool TryParseProjectSettingsFloat(string line, string prefix, out float value)
        {
            value = 0f;
            if (!line.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                return false;
            }

            return float.TryParse(line.Substring(prefix.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParseProjectSettingsLong(string line, string prefix, out long value)
        {
            value = 0L;
            if (!line.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                return false;
            }

            return long.TryParse(line.Substring(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static string GetProjectSettingsPath(string fileName)
        {
            return System.IO.Path.Combine(Application.dataPath, "..", "ProjectSettings", fileName);
        }

        private static List<UnityBridgeSerializedPropertyDetails> BuildSerializedProperties(Object unityObject)
        {
            var properties = new List<UnityBridgeSerializedPropertyDetails>();
            if (unityObject == null)
            {
                return properties;
            }

            var serializedObject = new SerializedObject(unityObject);
            var iterator = serializedObject.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                properties.Add(new UnityBridgeSerializedPropertyDetails
                {
                    path = iterator.propertyPath,
                    displayName = iterator.displayName,
                    propertyType = iterator.propertyType.ToString(),
                    depth = iterator.depth,
                    editable = iterator.editable,
                    value = ReadSerializedPropertyValue(iterator)
                });
                enterChildren = false;
            }

            return properties;
        }

        private static string ReadSerializedPropertyValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    return property.boolValue ? "true" : "false";
                case SerializedPropertyType.Integer:
                    return property.intValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Float:
                    return property.floatValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.String:
                    return property.stringValue;
                case SerializedPropertyType.Enum:
                    return property.enumDisplayNames != null &&
                           property.enumValueIndex >= 0 &&
                           property.enumValueIndex < property.enumDisplayNames.Length
                        ? property.enumDisplayNames[property.enumValueIndex]
                        : property.enumValueIndex.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue != null
                        ? AssetDatabase.GetAssetPath(property.objectReferenceValue) ?? property.objectReferenceValue.name
                        : string.Empty;
                case SerializedPropertyType.Vector2:
                    return property.vector2Value.ToString();
                case SerializedPropertyType.Vector3:
                    return property.vector3Value.ToString();
                case SerializedPropertyType.Vector4:
                    return property.vector4Value.ToString();
                case SerializedPropertyType.Color:
                    return property.colorValue.ToString();
                case SerializedPropertyType.LayerMask:
                    return property.intValue.ToString(CultureInfo.InvariantCulture);
                default:
                    return property.hasVisibleChildren ? "<complex>" : string.Empty;
            }
        }
    }
}
