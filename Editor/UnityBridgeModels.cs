using System;
using System.Collections.Generic;

namespace VolxGames.UnityBridge.Editor
{
    [Serializable]
    internal sealed class UnityBridgeHealth
    {
        public bool ok = true;
        public string projectName;
        public string projectPath;
        public string url;
        public int port;
        public int processId;
        public bool running;
        public string unityVersion;
        public string startedAtUtc;
    }

    [Serializable]
    internal sealed class UnityBridgeProjectSummary
    {
        public string projectName;
        public string projectPath;
        public string unityVersion;
        public string companyName;
        public string productName;
        public string activeScene;
        public List<string> tags = new List<string>();
        public List<string> layers = new List<string>();
        public List<UnityBridgeBuildSceneRef> buildScenes = new List<UnityBridgeBuildSceneRef>();
    }

    [Serializable]
    internal sealed class UnityBridgeBuildSummary
    {
        public string activeBuildTarget;
        public string selectedBuildTargetGroup;
        public string activePlatformName;
        public bool developmentBuild;
        public bool connectProfiler;
        public bool allowDebugging;
        public bool buildAppBundle;
        public string locationPathName;
        public bool buildInProgress;
        public string currentBuildStartedAtUtc;
        public string lastBuildFinishedAtUtc;
        public string lastBuildResult;
        public string lastBuildOutputPath;
    }

    [Serializable]
    internal sealed class UnityBridgeBuildReportSummary
    {
        public bool hasReport;
        public bool succeeded;
        public string result;
        public string message;
        public string buildTarget;
        public string buildTargetGroup;
        public string outputPath;
        public string startedAtUtc;
        public string finishedAtUtc;
        public double durationSeconds;
        public ulong totalSizeBytes;
        public int totalErrors;
        public int totalWarnings;
        public string options;
        public List<string> scenes = new List<string>();
    }

    [Serializable]
    internal sealed class UnityBridgeBuildPlayerRequest
    {
        public string buildTarget;
        public string locationPathName;
        public List<string> scenes = new List<string>();
        public bool development;
        public bool connectProfiler;
        public bool allowDebugging;
        public bool buildAppBundle;
        public bool autoRunPlayer;
        public bool buildScriptsOnly;
        public bool showBuiltPlayer;
        public bool strictMode;
        public bool acceptExternalModificationsToPlayer;
        public bool compressWithLz4;
        public bool compressWithLz4HC;
    }

    [Serializable]
    internal sealed class UnityBridgeBuildSettingsRequest
    {
        public bool setDevelopmentBuild;
        public bool developmentBuild;
        public bool setConnectProfiler;
        public bool connectProfiler;
        public bool setAllowDebugging;
        public bool allowDebugging;
        public bool setBuildAppBundle;
        public bool buildAppBundle;
        public string locationPathName;
    }

    [Serializable]
    internal sealed class UnityBridgeSwitchBuildTargetRequest
    {
        public string buildTarget;
        public string buildTargetGroup;
    }

    [Serializable]
    internal sealed class UnityBridgePlayerSettingsSummary
    {
        public string activeBuildTarget;
        public string activeBuildTargetGroup;
        public string namedBuildTarget;
        public string applicationIdentifier;
        public string scriptingBackend;
        public string apiCompatibilityLevel;
        public string managedStrippingLevel;
        public bool incrementalIl2CppBuild;
        public bool captureStartupLogs;
        public string scriptingDefineSymbols;
        public List<string> scriptingDefineSymbolList = new List<string>();
    }

    [Serializable]
    internal sealed class UnityBridgePlayerSettingsRequest
    {
        public string buildTarget;
        public string buildTargetGroup;
        public bool setApplicationIdentifier;
        public string applicationIdentifier;
        public bool setScriptingBackend;
        public string scriptingBackend;
        public bool setApiCompatibilityLevel;
        public string apiCompatibilityLevel;
        public bool setManagedStrippingLevel;
        public string managedStrippingLevel;
        public bool setIncrementalIl2CppBuild;
        public bool incrementalIl2CppBuild;
        public bool setCaptureStartupLogs;
        public bool captureStartupLogs;
        public bool setScriptingDefineSymbols;
        public string scriptingDefineSymbols;
        public List<string> scriptingDefineSymbolList = new List<string>();
    }

    [Serializable]
    internal sealed class UnityBridgePhysicsSettingsSummary
    {
        public UnityBridgePhysics3DSettingsSummary physics3D = new UnityBridgePhysics3DSettingsSummary();
        public UnityBridgePhysics2DSettingsSummary physics2D = new UnityBridgePhysics2DSettingsSummary();
        public string message;
    }

    [Serializable]
    internal sealed class UnityBridgePhysics3DSettingsSummary
    {
        public UnityBridgeVector3Value gravity;
        public float bounceThreshold;
        public float defaultMaxDepenetrationVelocity;
        public float sleepThreshold;
        public float defaultContactOffset;
        public int defaultSolverIterations;
        public int defaultSolverVelocityIterations;
        public bool queriesHitBackfaces;
        public bool queriesHitTriggers;
        public bool enableAdaptiveForce;
        public string simulationMode;
        public bool autoSyncTransforms;
        public bool reuseCollisionCallbacks;
        public bool invokeCollisionCallbacks;
        public string contactPairsMode;
        public string broadphaseType;
        public string frictionType;
        public bool enableEnhancedDeterminism;
        public bool improvedPatchFriction;
        public bool generateOnTriggerStayEvents;
        public string solverType;
        public float defaultMaxAngularSpeed;
        public float fastMotionThreshold;
        public bool incrementalStaticBroadphase;
    }

    [Serializable]
    internal sealed class UnityBridgePhysics2DSettingsSummary
    {
        public UnityBridgeVector2Value gravity;
        public int velocityIterations;
        public int positionIterations;
        public float bounceThreshold;
        public float maxLinearCorrection;
        public float maxAngularCorrection;
        public float maxTranslationSpeed;
        public float maxRotationSpeed;
        public float baumgarteScale;
        public float baumgarteTimeOfImpactScale;
        public float timeToSleep;
        public float linearSleepTolerance;
        public float angularSleepTolerance;
        public float defaultContactOffset;
        public string simulationMode;
        public int maxSubStepCount;
        public int minSubStepFPS;
        public bool useSubStepping;
        public bool useSubStepContacts;
        public bool queriesHitTriggers;
        public bool queriesStartInColliders;
        public bool callbacksOnDisable;
        public bool reuseCollisionCallbacks;
        public bool autoSyncTransforms;
    }

    [Serializable]
    internal sealed class UnityBridgePhysicsSettingsRequest
    {
        public UnityBridgePhysics3DSettingsRequest physics3D = new UnityBridgePhysics3DSettingsRequest();
        public UnityBridgePhysics2DSettingsRequest physics2D = new UnityBridgePhysics2DSettingsRequest();
    }

    [Serializable]
    internal sealed class UnityBridgeProjectTagLayerSummary
    {
        public List<string> tags = new List<string>();
        public List<UnityBridgeNamedIndexValue> layers = new List<UnityBridgeNamedIndexValue>();
        public List<UnityBridgeSortingLayerValue> sortingLayers = new List<UnityBridgeSortingLayerValue>();
        public List<UnityBridgeNamedIndexValue> renderingLayers = new List<UnityBridgeNamedIndexValue>();
        public string message;
    }

    [Serializable]
    internal sealed class UnityBridgeProjectTagLayerRequest
    {
        public List<string> addTags = new List<string>();
        public List<string> removeTags = new List<string>();
        public List<UnityBridgeNamedIndexValue> setLayers = new List<UnityBridgeNamedIndexValue>();
        public List<string> addSortingLayers = new List<string>();
        public List<UnityBridgeSortingLayerRenameRequest> renameSortingLayers = new List<UnityBridgeSortingLayerRenameRequest>();
        public List<string> removeSortingLayers = new List<string>();
        public List<UnityBridgeNamedIndexValue> setRenderingLayers = new List<UnityBridgeNamedIndexValue>();
    }

    [Serializable]
    internal sealed class UnityBridgeQualitySettingsSummary
    {
        public int currentQualityIndex;
        public string currentQualityName;
        public List<UnityBridgeQualityLevelSummary> qualityLevels = new List<UnityBridgeQualityLevelSummary>();
        public List<UnityBridgePlatformQualityValue> perPlatformDefaults = new List<UnityBridgePlatformQualityValue>();
        public string message;
    }

    [Serializable]
    internal sealed class UnityBridgeQualityLevelSummary
    {
        public int index;
        public string name;
        public int pixelLightCount;
        public int shadows;
        public int shadowResolution;
        public int shadowProjection;
        public int shadowCascades;
        public float shadowDistance;
        public int globalTextureMipmapLimit;
        public int anisotropicTextures;
        public int antiAliasing;
        public int vSyncCount;
        public float lodBias;
        public int maximumLODLevel;
        public bool streamingMipmapsActive;
        public float streamingMipmapsMemoryBudget;
        public string customRenderPipelineGuid;
    }

    [Serializable]
    internal sealed class UnityBridgePlatformQualityValue
    {
        public string platform;
        public int qualityIndex;
    }

    [Serializable]
    internal sealed class UnityBridgeQualitySettingsRequest
    {
        public bool setCurrentQualityIndex;
        public int currentQualityIndex;
        public List<UnityBridgeQualityLevelRequest> qualityLevels = new List<UnityBridgeQualityLevelRequest>();
        public List<UnityBridgePlatformQualityValue> perPlatformDefaults = new List<UnityBridgePlatformQualityValue>();
    }

    [Serializable]
    internal sealed class UnityBridgeAudioSettingsSummary
    {
        public float volume;
        public float rolloffScale;
        public float dopplerFactor;
        public int defaultSpeakerMode;
        public int sampleRate;
        public int dspBufferSize;
        public int virtualVoiceCount;
        public int realVoiceCount;
        public string spatializerPlugin;
        public string ambisonicDecoderPlugin;
        public bool disableAudio;
        public bool virtualizeEffects;
        public int requestedDspBufferSize;
        public string message;
    }

    [Serializable]
    internal sealed class UnityBridgeAudioSettingsRequest
    {
        public bool setVolume;
        public float volume;
        public bool setRolloffScale;
        public float rolloffScale;
        public bool setDopplerFactor;
        public float dopplerFactor;
        public bool setDefaultSpeakerMode;
        public int defaultSpeakerMode;
        public bool setSampleRate;
        public int sampleRate;
        public bool setDspBufferSize;
        public int dspBufferSize;
        public bool setVirtualVoiceCount;
        public int virtualVoiceCount;
        public bool setRealVoiceCount;
        public int realVoiceCount;
        public bool setSpatializerPlugin;
        public string spatializerPlugin;
        public bool setAmbisonicDecoderPlugin;
        public string ambisonicDecoderPlugin;
        public bool setDisableAudio;
        public bool disableAudio;
        public bool setVirtualizeEffects;
        public bool virtualizeEffects;
        public bool setRequestedDspBufferSize;
        public int requestedDspBufferSize;
    }

    [Serializable]
    internal sealed class UnityBridgeTimeSettingsSummary
    {
        public float fixedTimestep;
        public float maximumAllowedTimestep;
        public float timeScale;
        public float maximumParticleTimestep;
        public string message;
    }

    [Serializable]
    internal sealed class UnityBridgeTimeSettingsRequest
    {
        public bool setFixedTimestep;
        public float fixedTimestep;
        public bool setMaximumAllowedTimestep;
        public float maximumAllowedTimestep;
        public bool setTimeScale;
        public float timeScale;
        public bool setMaximumParticleTimestep;
        public float maximumParticleTimestep;
    }

    [Serializable]
    internal sealed class UnityBridgeQualityLevelRequest
    {
        public int index = -1;
        public bool setName;
        public string name;
        public bool setPixelLightCount;
        public int pixelLightCount;
        public bool setShadows;
        public int shadows;
        public bool setShadowResolution;
        public int shadowResolution;
        public bool setShadowProjection;
        public int shadowProjection;
        public bool setShadowCascades;
        public int shadowCascades;
        public bool setShadowDistance;
        public float shadowDistance;
        public bool setGlobalTextureMipmapLimit;
        public int globalTextureMipmapLimit;
        public bool setAnisotropicTextures;
        public int anisotropicTextures;
        public bool setAntiAliasing;
        public int antiAliasing;
        public bool setVSyncCount;
        public int vSyncCount;
        public bool setLodBias;
        public float lodBias;
        public bool setMaximumLODLevel;
        public int maximumLODLevel;
        public bool setStreamingMipmapsActive;
        public bool streamingMipmapsActive;
        public bool setStreamingMipmapsMemoryBudget;
        public float streamingMipmapsMemoryBudget;
    }

    [Serializable]
    internal sealed class UnityBridgeNamedIndexValue
    {
        public int index;
        public string name;
    }

    [Serializable]
    internal sealed class UnityBridgeSortingLayerValue
    {
        public int index;
        public string name;
        public int uniqueId;
        public bool locked;
    }

    [Serializable]
    internal sealed class UnityBridgeSortingLayerRenameRequest
    {
        public string name;
        public int uniqueId = -1;
        public string newName;
    }

    [Serializable]
    internal sealed class UnityBridgePhysics3DSettingsRequest
    {
        public bool setGravity;
        public UnityBridgeVector3Value gravity;
        public bool setBounceThreshold;
        public float bounceThreshold;
        public bool setDefaultMaxDepenetrationVelocity;
        public float defaultMaxDepenetrationVelocity;
        public bool setSleepThreshold;
        public float sleepThreshold;
        public bool setDefaultContactOffset;
        public float defaultContactOffset;
        public bool setDefaultSolverIterations;
        public int defaultSolverIterations;
        public bool setDefaultSolverVelocityIterations;
        public int defaultSolverVelocityIterations;
        public bool setQueriesHitBackfaces;
        public bool queriesHitBackfaces;
        public bool setQueriesHitTriggers;
        public bool queriesHitTriggers;
        public bool setEnableAdaptiveForce;
        public bool enableAdaptiveForce;
        public bool setSimulationMode;
        public string simulationMode;
        public bool setAutoSyncTransforms;
        public bool autoSyncTransforms;
        public bool setReuseCollisionCallbacks;
        public bool reuseCollisionCallbacks;
        public bool setInvokeCollisionCallbacks;
        public bool invokeCollisionCallbacks;
        public bool setContactPairsMode;
        public string contactPairsMode;
        public bool setBroadphaseType;
        public string broadphaseType;
        public bool setFrictionType;
        public string frictionType;
        public bool setEnableEnhancedDeterminism;
        public bool enableEnhancedDeterminism;
        public bool setImprovedPatchFriction;
        public bool improvedPatchFriction;
        public bool setGenerateOnTriggerStayEvents;
        public bool generateOnTriggerStayEvents;
        public bool setSolverType;
        public string solverType;
        public bool setDefaultMaxAngularSpeed;
        public float defaultMaxAngularSpeed;
        public bool setFastMotionThreshold;
        public float fastMotionThreshold;
        public bool setIncrementalStaticBroadphase;
        public bool incrementalStaticBroadphase;
    }

    [Serializable]
    internal sealed class UnityBridgePhysics2DSettingsRequest
    {
        public bool setGravity;
        public UnityBridgeVector2Value gravity;
        public bool setVelocityIterations;
        public int velocityIterations;
        public bool setPositionIterations;
        public int positionIterations;
        public bool setBounceThreshold;
        public float bounceThreshold;
        public bool setMaxLinearCorrection;
        public float maxLinearCorrection;
        public bool setMaxAngularCorrection;
        public float maxAngularCorrection;
        public bool setMaxTranslationSpeed;
        public float maxTranslationSpeed;
        public bool setMaxRotationSpeed;
        public float maxRotationSpeed;
        public bool setBaumgarteScale;
        public float baumgarteScale;
        public bool setBaumgarteTimeOfImpactScale;
        public float baumgarteTimeOfImpactScale;
        public bool setTimeToSleep;
        public float timeToSleep;
        public bool setLinearSleepTolerance;
        public float linearSleepTolerance;
        public bool setAngularSleepTolerance;
        public float angularSleepTolerance;
        public bool setDefaultContactOffset;
        public float defaultContactOffset;
        public bool setSimulationMode;
        public string simulationMode;
        public bool setMaxSubStepCount;
        public int maxSubStepCount;
        public bool setMinSubStepFPS;
        public int minSubStepFPS;
        public bool setUseSubStepping;
        public bool useSubStepping;
        public bool setUseSubStepContacts;
        public bool useSubStepContacts;
        public bool setQueriesHitTriggers;
        public bool queriesHitTriggers;
        public bool setQueriesStartInColliders;
        public bool queriesStartInColliders;
        public bool setCallbacksOnDisable;
        public bool callbacksOnDisable;
        public bool setReuseCollisionCallbacks;
        public bool reuseCollisionCallbacks;
        public bool setAutoSyncTransforms;
        public bool autoSyncTransforms;
    }

    [Serializable]
    internal sealed class UnityBridgeTestReportSummary
    {
        public bool hasReport;
        public bool running;
        public string runId;
        public string testMode;
        public string message;
        public string resultState;
        public string startedAtUtc;
        public string finishedAtUtc;
        public double durationSeconds;
        public int totalCount;
        public int passCount;
        public int failCount;
        public int skipCount;
        public int inconclusiveCount;
        public int assertCount;
        public int startedCount;
        public int finishedCount;
        public string resultFilePath;
        public List<string> testNames = new List<string>();
        public List<string> groupNames = new List<string>();
        public List<string> categoryNames = new List<string>();
        public List<string> assemblyNames = new List<string>();
    }

    [Serializable]
    internal sealed class UnityBridgeCompilationSummary
    {
        public bool hasReport;
        public bool running;
        public bool succeeded;
        public string compilationId;
        public string message;
        public string startedAtUtc;
        public string finishedAtUtc;
        public double durationSeconds;
        public int assemblyCount;
        public int completedAssemblyCount;
        public int totalErrors;
        public int totalWarnings;
        public List<string> assemblyNames = new List<string>();
        public List<string> completedAssemblyNames = new List<string>();
    }

    [Serializable]
    internal sealed class UnityBridgeRunTestsRequest
    {
        public string testMode;
        public bool runSynchronously;
        public string resultFilePath;
        public List<string> testNames = new List<string>();
        public List<string> groupNames = new List<string>();
        public List<string> categoryNames = new List<string>();
        public List<string> assemblyNames = new List<string>();
    }

    [Serializable]
    internal sealed class UnityBridgePackageOperationSummary
    {
        public bool hasOperation;
        public bool running;
        public bool succeeded;
        public string operationId;
        public string action;
        public string status;
        public string message;
        public int errorCode;
        public string errorMessage;
        public string startedAtUtc;
        public string finishedAtUtc;
        public List<string> packageIds = new List<string>();
        public List<string> removePackageIds = new List<string>();
        public List<UnityBridgePackageInfo> packages = new List<UnityBridgePackageInfo>();
    }

    [Serializable]
    internal sealed class UnityBridgePackageOperationRequest
    {
        public string packageId;
        public List<string> packageIds = new List<string>();
        public List<string> removePackageIds = new List<string>();
    }

    [Serializable]
    internal sealed class UnityBridgeState
    {
        public string projectName;
        public string projectPath;
        public string unityVersion;
        public bool isPlaying;
        public bool isPaused;
        public bool isCompiling;
        public bool isUpdating;
        public bool isApplicationActive;
        public string activeScene;
        public int selectionCount;
        public List<UnityBridgeObjectRef> selection = new List<UnityBridgeObjectRef>();
        public List<UnityBridgeSceneRef> openScenes = new List<UnityBridgeSceneRef>();
        public string lastAssetRefreshAtUtc;
        public string lastReloadAtUtc;
    }

    [Serializable]
    internal sealed class UnityBridgeRuntimeSummary
    {
        public bool isPlaying;
        public bool isPaused;
        public bool inPlayMode;
        public int frameCount;
        public float timeScale;
        public float fixedDeltaTime;
        public float maximumDeltaTime;
        public int targetFrameRate;
        public float realtimeSinceStartup;
        public float unscaledTime;
        public float timeSinceLevelLoad;
        public string activeScene;
        public int loadedSceneCount;
        public int cameraCount;
        public int activeCameraCount;
        public int audioListenerCount;
        public int animatorCount;
        public int rigidbodyCount;
        public int rigidbody2DCount;
    }

    [Serializable]
    internal sealed class UnityBridgeRuntimeObjectSnapshot
    {
        public UnityBridgeObjectRef reference;
        public string hierarchyPath;
        public string scenePath;
        public bool activeSelf;
        public bool activeInHierarchy;
        public string tag;
        public int layer;
        public int childCount;
        public int componentCount;
    }

    [Serializable]
    internal sealed class UnityBridgeObjectRef
    {
        public string name;
        public string path;
        public string type;
        public int instanceId;
        public bool persistent;
    }

    [Serializable]
    internal sealed class UnityBridgeLogEntry
    {
        public string level;
        public string message;
        public string stackTrace;
        public string timestampUtc;
    }

    [Serializable]
    internal sealed class UnityBridgeEventEntry
    {
        public string type;
        public string message;
        public string context;
        public string timestampUtc;
    }

    [Serializable]
    internal sealed class UnityBridgeLogQueryRequest
    {
        public string query;
        public string level;
        public int limit = 100;
    }

    [Serializable]
    internal sealed class UnityBridgeEventQueryRequest
    {
        public string query;
        public string type;
        public int limit = 100;
    }

    [Serializable]
    internal sealed class UnityBridgeSceneRef
    {
        public string name;
        public string path;
        public bool loaded;
        public bool dirty;
        public bool active;
    }

    [Serializable]
    internal sealed class UnityBridgeBuildSceneRef
    {
        public string path;
        public bool enabled;
        public int buildIndex;
    }

    [Serializable]
    internal sealed class UnityBridgeBuildScenesRequest
    {
        public List<UnityBridgeBuildSceneRef> scenes = new List<UnityBridgeBuildSceneRef>();
    }

    [Serializable]
    internal sealed class UnityBridgeBuildSceneMutationRequest
    {
        public string path;
        public bool enabled = true;
        public int buildIndex = -1;
    }

    [Serializable]
    internal sealed class UnityBridgeSceneStats
    {
        public string scenePath;
        public int rootCount;
        public int gameObjectCount;
        public int componentCount;
        public int inactiveCount;
    }

    [Serializable]
    internal sealed class UnityBridgePrefabStageInfo
    {
        public bool isOpen;
        public string assetPath;
        public string prefabContentsRootName;
        public string scenePath;
    }

    [Serializable]
    internal sealed class UnityBridgeEditorWindowInfo
    {
        public string title;
        public string type;
        public bool focused;
        public bool hasFocus;
    }

    [Serializable]
    internal sealed class UnityBridgeInspectorSelectionSummary
    {
        public bool hasInspectorWindow;
        public string windowTitle;
        public string windowType;
        public bool focused;
        public bool hasFocus;
        public bool isLocked;
        public string selectedPropertyPath;
        public int selectionCount;
        public List<UnityBridgeObjectRef> selection = new List<UnityBridgeObjectRef>();
        public UnityBridgeObjectDetails activeObject;
        public string message;
    }

    [Serializable]
    internal sealed class UnityBridgeConsoleSelectionSummary
    {
        public bool hasConsoleWindow;
        public string windowTitle;
        public string windowType;
        public bool focused;
        public bool hasFocus;
        public int selectedEntryIndex = -1;
        public UnityBridgeLogEntry selectedEntry;
        public string message;
    }

    [Serializable]
    internal sealed class UnityBridgeCurrentContextSummary
    {
        public UnityBridgeState state;
        public UnityBridgeEditorWindowInfo focusedWindow;
        public UnityBridgeInspectorSelectionSummary inspector;
        public UnityBridgeConsoleSelectionSummary console;
        public UnityBridgeObjectDetails activeObject;
        public UnityBridgePrefabStageInfo prefabStage;
    }

    [Serializable]
    internal sealed class UnityBridgePackageInfo
    {
        public string name;
        public string displayName;
        public string version;
        public string source;
        public string resolvedPath;
        public bool isDirectDependency;
    }

    [Serializable]
    internal sealed class UnityBridgeHierarchyScene
    {
        public string name;
        public string path;
        public bool active;
        public List<UnityBridgeHierarchyNode> roots = new List<UnityBridgeHierarchyNode>();
    }

    [Serializable]
    internal sealed class UnityBridgeHierarchyNode
    {
        public string name;
        public string path;
        public bool activeSelf;
        public bool activeInHierarchy;
        public string tag;
        public int layer;
        public List<string> componentTypes = new List<string>();
        public List<UnityBridgeHierarchyNode> children = new List<UnityBridgeHierarchyNode>();
    }

    [Serializable]
    internal sealed class UnityBridgeObjectDetails
    {
        public UnityBridgeObjectRef reference;
        public string assetPath;
        public string hierarchyPath;
        public string scenePath;
        public string parentPath;
        public int childCount;
        public bool persistent;
        public UnityBridgeTransformSnapshot transform;
        public List<UnityBridgeComponentDetails> components = new List<UnityBridgeComponentDetails>();
        public List<UnityBridgeSerializedPropertyDetails> serializedProperties = new List<UnityBridgeSerializedPropertyDetails>();
    }

    [Serializable]
    internal sealed class UnityBridgeComponentDetails
    {
        public string type;
        public string name;
        public int index;
        public List<UnityBridgeSerializedPropertyDetails> serializedProperties = new List<UnityBridgeSerializedPropertyDetails>();
    }

    [Serializable]
    internal sealed class UnityBridgeTransformSnapshot
    {
        public UnityBridgeVector3Value localPosition;
        public UnityBridgeVector3Value localEulerAngles;
        public UnityBridgeVector3Value localScale;
        public UnityBridgeVector3Value worldPosition;
    }

    [Serializable]
    internal sealed class UnityBridgeSerializedPropertyDetails
    {
        public string path;
        public string displayName;
        public string propertyType;
        public int depth;
        public bool editable;
        public string value;
    }

    [Serializable]
    internal sealed class UnityBridgeVector2Value
    {
        public float x;
        public float y;
    }

    [Serializable]
    internal sealed class UnityBridgeVector3Value
    {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    internal sealed class UnityBridgeVector4Value
    {
        public float x;
        public float y;
        public float z;
        public float w;
    }

    [Serializable]
    internal sealed class UnityBridgeColorValue
    {
        public float r;
        public float g;
        public float b;
        public float a = 1f;
    }

    [Serializable]
    internal sealed class UnityBridgeAssetSearchRequest
    {
        public string query;
        public string type;
    }

    [Serializable]
    internal sealed class UnityBridgeAssetSearchResult
    {
        public string query;
        public string type;
        public List<UnityBridgeObjectRef> assets = new List<UnityBridgeObjectRef>();
    }

    [Serializable]
    internal sealed class UnityBridgeAssetDependencyResult
    {
        public string path;
        public bool recursive;
        public List<UnityBridgeObjectRef> dependencies = new List<UnityBridgeObjectRef>();
    }

    [Serializable]
    internal sealed class UnityBridgeHierarchySearchRequest
    {
        public string query;
        public string scenePath;
        public string tag;
        public string componentType;
        public bool includeInactive;
    }

    [Serializable]
    internal sealed class UnityBridgeHierarchySearchResult
    {
        public string query;
        public string scenePath;
        public string tag;
        public string componentType;
        public bool includeInactive;
        public List<UnityBridgeObjectRef> objects = new List<UnityBridgeObjectRef>();
    }

    [Serializable]
    internal sealed class UnityBridgeRuntimeQueryRequest
    {
        public string query;
        public string scenePath;
        public string tag;
        public string componentType;
        public bool includeInactive;
        public int limit = 100;
    }

    [Serializable]
    internal sealed class UnityBridgeRuntimeQueryResult
    {
        public string query;
        public string scenePath;
        public string tag;
        public string componentType;
        public bool includeInactive;
        public int limit;
        public List<UnityBridgeRuntimeObjectSnapshot> objects = new List<UnityBridgeRuntimeObjectSnapshot>();
    }

    [Serializable]
    public sealed class UnityBridgeCustomCommandResult
    {
        public bool ok = true;
        public string message;
    }

    [Serializable]
    internal sealed class UnityBridgeCommandDescriptor
    {
        public string name;
        public string description;
        public string argumentHint;
        public string source;
    }

    [Serializable]
    internal sealed class UnityBridgeCommandRequest
    {
        public string name;
        public string argument;
        public string confirm;
    }

    [Serializable]
    internal sealed class UnityBridgePathRequest
    {
        public string path;
    }

    [Serializable]
    internal sealed class UnityBridgeAssetDependencyRequest
    {
        public string path;
        public bool recursive = true;
    }

    [Serializable]
    internal sealed class UnityBridgeAssetPathRequest
    {
        public string path;
        public string guid;
    }

    [Serializable]
    internal sealed class UnityBridgeAssetPathResult
    {
        public string path;
        public string guid;
        public bool exists;
    }

    [Serializable]
    internal sealed class UnityBridgeWindowFocusRequest
    {
        public string type;
        public string title;
    }

    [Serializable]
    internal sealed class UnityBridgeCommandResponse
    {
        public bool ok;
        public bool busy;
        public string message;
        public UnityBridgeState state;
        public UnityBridgeCompilationSummary compilation;
    }

    [Serializable]
    internal sealed class UnityBridgeBusyResponse
    {
        public bool ok;
        public bool busy;
        public string message;
        public UnityBridgeState state;
        public UnityBridgeCompilationSummary compilation;
    }

    [Serializable]
    internal sealed class UnityBridgeCreateGameObjectRequest
    {
        public string name;
        public string parentPath;
        public string scenePath;
        public string primitiveType;
        public string tag;
        public int layer = -1;
        public bool active = true;
    }

    [Serializable]
    internal sealed class UnityBridgeInstantiatePrefabRequest
    {
        public string assetPath;
        public string parentPath;
        public string scenePath;
        public string name;
        public bool active = true;
    }

    [Serializable]
    internal sealed class UnityBridgeHierarchyPathRequest
    {
        public string path;
    }

    [Serializable]
    internal sealed class UnityBridgeRenameHierarchyObjectRequest
    {
        public string path;
        public string name;
    }

    [Serializable]
    internal sealed class UnityBridgeSetActiveRequest
    {
        public string path;
        public bool active;
    }

    [Serializable]
    internal sealed class UnityBridgeSetParentRequest
    {
        public string path;
        public string parentPath;
        public bool worldPositionStays = true;
    }

    [Serializable]
    internal sealed class UnityBridgeTransformMutationRequest
    {
        public string path;
        public bool setLocalPosition;
        public bool setLocalEulerAngles;
        public bool setLocalScale;
        public UnityBridgeVector3Value localPosition;
        public UnityBridgeVector3Value localEulerAngles;
        public UnityBridgeVector3Value localScale;
    }

    [Serializable]
    internal sealed class UnityBridgeSetHierarchyMetadataRequest
    {
        public string path;
        public string name;
        public string tag;
        public int layer = -1;
        public bool setStaticFlags;
        public bool staticFlags;
    }

    [Serializable]
    internal sealed class UnityBridgeAddComponentRequest
    {
        public string path;
        public string componentType;
    }

    [Serializable]
    internal sealed class UnityBridgeRemoveComponentRequest
    {
        public string path;
        public string componentType;
        public int componentIndex = -1;
    }

    [Serializable]
    internal sealed class UnityBridgeSetComponentPropertyRequest
    {
        public string path;
        public string componentType;
        public int componentIndex = -1;
        public string propertyPath;
        public string valueType;
        public bool boolValue;
        public int intValue;
        public float floatValue;
        public string stringValue;
        public string objectReferencePath;
        public UnityBridgeVector2Value vector2Value;
        public UnityBridgeVector3Value vector3Value;
        public UnityBridgeVector4Value vector4Value;
        public UnityBridgeColorValue colorValue;
    }

    [Serializable]
    internal sealed class UnityBridgeSetComponentEnabledRequest
    {
        public string path;
        public string componentType;
        public int componentIndex = -1;
        public bool enabled = true;
    }

    [Serializable]
    internal sealed class UnityBridgeInvokeComponentMethodRequest
    {
        public string path;
        public string componentType;
        public int componentIndex = -1;
        public string methodName;
        public string argumentType;
        public bool boolValue;
        public int intValue;
        public float floatValue;
        public string stringValue;
    }

    [Serializable]
    internal sealed class UnityBridgeInvokeStaticMethodRequest
    {
        public string typeName;
        public string methodName;
        public string argumentType;
        public bool boolValue;
        public int intValue;
        public float floatValue;
        public string stringValue;
    }

    [Serializable]
    internal sealed class UnityBridgeCaptureWindowRequest
    {
        public string type;
        public string title;
        public string outputPath;
        public int padding = 0;
    }

    [Serializable]
    internal sealed class UnityBridgePlayerPrefsValueRequest
    {
        public string key;
        public string valueType;
        public bool boolValue;
        public int intValue;
        public float floatValue;
        public string stringValue;
    }

    [Serializable]
    internal sealed class UnityBridgePlayerPrefsValueResponse
    {
        public bool hasKey;
        public string key;
        public string valueType;
        public bool boolValue;
        public int intValue;
        public float floatValue;
        public string stringValue;
        public string message;
    }

    [Serializable]
    internal sealed class UnityBridgeTextureImporterSummary
    {
        public string assetPath;
        public bool found;
        public string textureType;
        public string spriteImportMode;
        public bool isReadable;
        public bool mipmapEnabled;
        public bool alphaIsTransparency;
        public string filterMode;
        public string wrapMode;
        public int maxTextureSize;
        public string textureCompression;
        public string message;
    }

    [Serializable]
    internal sealed class UnityBridgeTextureImporterRequest
    {
        public string path;
        public bool setTextureType;
        public string textureType;
        public bool setSpriteImportMode;
        public string spriteImportMode;
        public bool setIsReadable;
        public bool isReadable;
        public bool setMipmapEnabled;
        public bool mipmapEnabled;
        public bool setAlphaIsTransparency;
        public bool alphaIsTransparency;
        public bool setFilterMode;
        public string filterMode;
        public bool setWrapMode;
        public string wrapMode;
        public bool setMaxTextureSize;
        public int maxTextureSize;
        public bool setTextureCompression;
        public string textureCompression;
    }

    [Serializable]
    internal sealed class UnityBridgeFloatValueRequest
    {
        public float value;
    }

    [Serializable]
    internal sealed class UnityBridgeIntValueRequest
    {
        public int value;
    }

    [Serializable]
    internal sealed class UnityBridgeWindowKeyEventRequest
    {
        public string type;
        public string title;
        public string keyCode;
        public char character;
        public bool shift;
        public bool control;
        public bool alt;
        public bool command;
        public string eventType;
    }

    [Serializable]
    internal sealed class UnityBridgeWindowMouseEventRequest
    {
        public string type;
        public string title;
        public string eventType;
        public float x;
        public float y;
        public int button;
        public int clickCount = 1;
        public UnityBridgeVector2Value delta;
        public bool shift;
        public bool control;
        public bool alt;
        public bool command;
    }

    [Serializable]
    internal sealed class UnityBridgeAssetCreateFolderRequest
    {
        public string parentFolder;
        public string folderName;
    }

    [Serializable]
    internal sealed class UnityBridgeAssetPathPairRequest
    {
        public string fromPath;
        public string toPath;
    }

    [Serializable]
    internal sealed class UnityBridgeRenameAssetRequest
    {
        public string path;
        public string name;
    }
}
