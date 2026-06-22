using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VolxGames.UnityBridge.Editor
{
    internal static class UnityBridgeMutations
    {
        public static UnityBridgeCommandResponse CreateGameObject(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeCreateGameObjectRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.name))
            {
                return Fail("create_game_object requires JSON with name.", lastReloadAtUtc);
            }

            GameObject gameObject;
            if (!string.IsNullOrWhiteSpace(request.primitiveType))
            {
                PrimitiveType primitiveType;
                if (!Enum.TryParse(request.primitiveType, true, out primitiveType))
                {
                    return Fail("Unknown primitive type: " + request.primitiveType, lastReloadAtUtc);
                }

                gameObject = GameObject.CreatePrimitive(primitiveType);
                gameObject.name = request.name;
            }
            else
            {
                gameObject = new GameObject(request.name);
            }

            Undo.RegisterCreatedObjectUndo(gameObject, "UnityBridge Create GameObject");

            if (!string.IsNullOrWhiteSpace(request.parentPath))
            {
                var parent = UnityBridgeStateBuilder.FindGameObjectByHierarchyPath(request.parentPath);
                if (parent == null)
                {
                    Undo.DestroyObjectImmediate(gameObject);
                    return Fail("Parent hierarchy object was not found: " + request.parentPath, lastReloadAtUtc);
                }

                Undo.SetTransformParent(gameObject.transform, parent.transform, "UnityBridge Set Parent");
            }
            else if (!string.IsNullOrWhiteSpace(request.scenePath))
            {
                var scene = SceneManager.GetSceneByPath(request.scenePath);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    Undo.DestroyObjectImmediate(gameObject);
                    return Fail("Open scene was not found: " + request.scenePath, lastReloadAtUtc);
                }

                SceneManager.MoveGameObjectToScene(gameObject, scene);
            }

            if (!string.IsNullOrWhiteSpace(request.tag))
            {
                gameObject.tag = request.tag;
            }

            if (request.layer >= 0)
            {
                gameObject.layer = request.layer;
            }

            gameObject.SetActive(request.active);
            MarkSceneDirty(gameObject);
            Selection.activeGameObject = gameObject;
            return Ok("Created GameObject: " + UnityBridgeStateBuilder.BuildHierarchyPath(gameObject), lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse DuplicateHierarchyObject(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeHierarchyPathRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.path))
            {
                return Fail("duplicate_hierarchy_object requires JSON with path.", lastReloadAtUtc);
            }

            var source = UnityBridgeStateBuilder.FindGameObjectByHierarchyPath(request.path);
            if (source == null)
            {
                return Fail("Hierarchy object was not found: " + request.path, lastReloadAtUtc);
            }

            var duplicate = UnityEngine.Object.Instantiate(source, source.transform.parent);
            duplicate.name = source.name + " Copy";
            duplicate.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
            Undo.RegisterCreatedObjectUndo(duplicate, "UnityBridge Duplicate GameObject");
            MarkSceneDirty(duplicate);
            Selection.activeGameObject = duplicate;
            return Ok("Duplicated GameObject: " + UnityBridgeStateBuilder.BuildHierarchyPath(duplicate), lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse InstantiatePrefab(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeInstantiatePrefabRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.assetPath))
            {
                return Fail("instantiate_prefab requires JSON with assetPath.", lastReloadAtUtc);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(request.assetPath);
            if (prefab == null)
            {
                return Fail("Prefab asset was not found: " + request.assetPath, lastReloadAtUtc);
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                return Fail("Prefab could not be instantiated: " + request.assetPath, lastReloadAtUtc);
            }

            Undo.RegisterCreatedObjectUndo(instance, "UnityBridge Instantiate Prefab");
            PlaceGameObject(instance, request.parentPath, request.scenePath, lastReloadAtUtc, out var placementError);
            if (!string.IsNullOrWhiteSpace(placementError))
            {
                Undo.DestroyObjectImmediate(instance);
                return Fail(placementError, lastReloadAtUtc);
            }

            if (!string.IsNullOrWhiteSpace(request.name))
            {
                instance.name = request.name;
            }

            instance.SetActive(request.active);
            MarkSceneDirty(instance);
            Selection.activeGameObject = instance;
            return Ok("Instantiated prefab: " + UnityBridgeStateBuilder.BuildHierarchyPath(instance), lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse DeleteHierarchyObject(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeHierarchyPathRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.path))
            {
                return Fail("delete_hierarchy_object requires JSON with path.", lastReloadAtUtc);
            }

            var gameObject = UnityBridgeStateBuilder.FindGameObjectByHierarchyPath(request.path);
            if (gameObject == null)
            {
                return Fail("Hierarchy object was not found: " + request.path, lastReloadAtUtc);
            }

            var scene = gameObject.scene;
            Undo.DestroyObjectImmediate(gameObject);
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            return Ok("Deleted GameObject: " + request.path, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse RenameHierarchyObject(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeRenameHierarchyObjectRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.path) || string.IsNullOrWhiteSpace(request.name))
            {
                return Fail("rename_hierarchy_object requires JSON with path and name.", lastReloadAtUtc);
            }

            var gameObject = UnityBridgeStateBuilder.FindGameObjectByHierarchyPath(request.path);
            if (gameObject == null)
            {
                return Fail("Hierarchy object was not found: " + request.path, lastReloadAtUtc);
            }

            Undo.RecordObject(gameObject, "UnityBridge Rename GameObject");
            gameObject.name = request.name;
            EditorUtility.SetDirty(gameObject);
            MarkSceneDirty(gameObject);
            return Ok("Renamed GameObject to: " + request.name, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse SetHierarchyObjectActive(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeSetActiveRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.path))
            {
                return Fail("set_hierarchy_object_active requires JSON with path and active.", lastReloadAtUtc);
            }

            var gameObject = UnityBridgeStateBuilder.FindGameObjectByHierarchyPath(request.path);
            if (gameObject == null)
            {
                return Fail("Hierarchy object was not found: " + request.path, lastReloadAtUtc);
            }

            Undo.RecordObject(gameObject, "UnityBridge Set Active");
            gameObject.SetActive(request.active);
            EditorUtility.SetDirty(gameObject);
            MarkSceneDirty(gameObject);
            return Ok("Set active state for: " + request.path, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse SetHierarchyParent(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeSetParentRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.path))
            {
                return Fail("set_hierarchy_parent requires JSON with path.", lastReloadAtUtc);
            }

            var gameObject = UnityBridgeStateBuilder.FindGameObjectByHierarchyPath(request.path);
            if (gameObject == null)
            {
                return Fail("Hierarchy object was not found: " + request.path, lastReloadAtUtc);
            }

            Transform parentTransform = null;
            if (!string.IsNullOrWhiteSpace(request.parentPath))
            {
                var parent = UnityBridgeStateBuilder.FindGameObjectByHierarchyPath(request.parentPath);
                if (parent == null)
                {
                    return Fail("Parent hierarchy object was not found: " + request.parentPath, lastReloadAtUtc);
                }

                parentTransform = parent.transform;
            }

            Undo.SetTransformParent(gameObject.transform, parentTransform, "UnityBridge Set Parent");
            if (parentTransform == null)
            {
                var activeScene = SceneManager.GetActiveScene();
                if (activeScene.IsValid() && activeScene.isLoaded)
                {
                    SceneManager.MoveGameObjectToScene(gameObject, activeScene);
                }
            }

            if (!request.worldPositionStays)
            {
                gameObject.transform.localPosition = Vector3.zero;
                gameObject.transform.localRotation = Quaternion.identity;
            }

            MarkSceneDirty(gameObject);
            return Ok("Updated parent for: " + request.path, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse SetTransform(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeTransformMutationRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.path))
            {
                return Fail("set_transform requires JSON with path.", lastReloadAtUtc);
            }

            var gameObject = UnityBridgeStateBuilder.FindGameObjectByHierarchyPath(request.path);
            if (gameObject == null)
            {
                return Fail("Hierarchy object was not found: " + request.path, lastReloadAtUtc);
            }

            var transform = gameObject.transform;
            Undo.RecordObject(transform, "UnityBridge Set Transform");

            if (request.setLocalPosition && request.localPosition != null)
            {
                transform.localPosition = ToVector3(request.localPosition);
            }

            if (request.setLocalEulerAngles && request.localEulerAngles != null)
            {
                transform.localEulerAngles = ToVector3(request.localEulerAngles);
            }

            if (request.setLocalScale && request.localScale != null)
            {
                transform.localScale = ToVector3(request.localScale);
            }

            EditorUtility.SetDirty(transform);
            MarkSceneDirty(gameObject);
            return Ok("Updated transform for: " + request.path, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse SetHierarchyMetadata(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeSetHierarchyMetadataRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.path))
            {
                return Fail("set_hierarchy_metadata requires JSON with path.", lastReloadAtUtc);
            }

            var gameObject = UnityBridgeStateBuilder.FindGameObjectByHierarchyPath(request.path);
            if (gameObject == null)
            {
                return Fail("Hierarchy object was not found: " + request.path, lastReloadAtUtc);
            }

            Undo.RecordObject(gameObject, "UnityBridge Set Hierarchy Metadata");

            if (!string.IsNullOrWhiteSpace(request.name))
            {
                gameObject.name = request.name;
            }

            if (!string.IsNullOrWhiteSpace(request.tag))
            {
                gameObject.tag = request.tag;
            }

            if (request.layer >= 0)
            {
                gameObject.layer = request.layer;
            }

            if (request.setStaticFlags)
            {
                gameObject.isStatic = request.staticFlags;
            }

            EditorUtility.SetDirty(gameObject);
            MarkSceneDirty(gameObject);
            return Ok("Updated hierarchy metadata for: " + request.path, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse AddComponent(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeAddComponentRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.path) || string.IsNullOrWhiteSpace(request.componentType))
            {
                return Fail("add_component requires JSON with path and componentType.", lastReloadAtUtc);
            }

            var gameObject = UnityBridgeStateBuilder.FindGameObjectByHierarchyPath(request.path);
            if (gameObject == null)
            {
                return Fail("Hierarchy object was not found: " + request.path, lastReloadAtUtc);
            }

            var componentType = ResolveComponentType(request.componentType);
            if (componentType == null)
            {
                return Fail("Component type was not found: " + request.componentType, lastReloadAtUtc);
            }

            Undo.AddComponent(gameObject, componentType);
            MarkSceneDirty(gameObject);
            return Ok("Added component " + componentType.FullName + " to " + request.path, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse RemoveComponent(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeRemoveComponentRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.path) || string.IsNullOrWhiteSpace(request.componentType))
            {
                return Fail("remove_component requires JSON with path and componentType.", lastReloadAtUtc);
            }

            var gameObject = UnityBridgeStateBuilder.FindGameObjectByHierarchyPath(request.path);
            if (gameObject == null)
            {
                return Fail("Hierarchy object was not found: " + request.path, lastReloadAtUtc);
            }

            var component = FindComponent(gameObject, request.componentType, request.componentIndex);
            if (component == null)
            {
                return Fail("Component was not found: " + request.componentType, lastReloadAtUtc);
            }

            if (component is Transform)
            {
                return Fail("Transform cannot be removed.", lastReloadAtUtc);
            }

            Undo.DestroyObjectImmediate(component);
            MarkSceneDirty(gameObject);
            return Ok("Removed component from " + request.path, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse SetComponentProperty(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeSetComponentPropertyRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.path) || string.IsNullOrWhiteSpace(request.componentType) || string.IsNullOrWhiteSpace(request.propertyPath) || string.IsNullOrWhiteSpace(request.valueType))
            {
                return Fail("set_component_property requires path, componentType, propertyPath, and valueType.", lastReloadAtUtc);
            }

            var gameObject = UnityBridgeStateBuilder.FindGameObjectByHierarchyPath(request.path);
            if (gameObject == null)
            {
                return Fail("Hierarchy object was not found: " + request.path, lastReloadAtUtc);
            }

            var component = FindComponent(gameObject, request.componentType, request.componentIndex);
            if (component == null)
            {
                return Fail("Component was not found: " + request.componentType, lastReloadAtUtc);
            }

            return SetSerializedProperty(component, request.propertyPath, request, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse SetComponentEnabled(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeSetComponentEnabledRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.path) || string.IsNullOrWhiteSpace(request.componentType))
            {
                return Fail("set_component_enabled requires JSON with path and componentType.", lastReloadAtUtc);
            }

            var gameObject = UnityBridgeStateBuilder.FindGameObjectByHierarchyPath(request.path);
            if (gameObject == null)
            {
                return Fail("Hierarchy object was not found: " + request.path, lastReloadAtUtc);
            }

            var component = FindComponent(gameObject, request.componentType, request.componentIndex);
            if (component == null)
            {
                return Fail("Component was not found: " + request.componentType, lastReloadAtUtc);
            }

            var behaviour = component as Behaviour;
            if (behaviour != null)
            {
                Undo.RecordObject(behaviour, "UnityBridge Set Component Enabled");
                behaviour.enabled = request.enabled;
                EditorUtility.SetDirty(behaviour);
                MarkSceneDirty(gameObject);
                return Ok("Updated component enabled state for: " + request.path, lastReloadAtUtc);
            }

            return SetSerializedProperty(component, "m_Enabled", new UnityBridgeSetComponentPropertyRequest
            {
                valueType = "bool",
                boolValue = request.enabled
            }, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse InvokeComponentMethod(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeInvokeComponentMethodRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.path) || string.IsNullOrWhiteSpace(request.componentType) || string.IsNullOrWhiteSpace(request.methodName))
            {
                return Fail("invoke_component_method requires path, componentType, and methodName.", lastReloadAtUtc);
            }

            var gameObject = UnityBridgeStateBuilder.FindGameObjectByHierarchyPath(request.path);
            if (gameObject == null)
            {
                return Fail("Hierarchy object was not found: " + request.path, lastReloadAtUtc);
            }

            var component = FindComponent(gameObject, request.componentType, request.componentIndex);
            if (component == null)
            {
                return Fail("Component was not found: " + request.componentType, lastReloadAtUtc);
            }

            object[] arguments;
            Type[] argumentTypes;
            BuildMethodArguments(request, out arguments, out argumentTypes);

            var method = component.GetType().GetMethod(
                request.methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                argumentTypes,
                null);

            if (method == null)
            {
                return Fail("Method was not found on component: " + request.methodName, lastReloadAtUtc);
            }

            var result = method.Invoke(component, arguments);
            MarkSceneDirty(gameObject);
            return Ok("Invoked method " + request.methodName + (result != null ? " -> " + result : string.Empty), lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse InvokeStaticMethod(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeInvokeStaticMethodRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.typeName) || string.IsNullOrWhiteSpace(request.methodName))
            {
                return Fail("invoke_static_method requires typeName and methodName.", lastReloadAtUtc);
            }

            var targetType = ResolveType(request.typeName);
            if (targetType == null)
            {
                return Fail("Static target type was not found: " + request.typeName, lastReloadAtUtc);
            }

            object[] arguments;
            Type[] argumentTypes;
            BuildMethodArguments(request.argumentType, request.boolValue, request.intValue, request.floatValue, request.stringValue, out arguments, out argumentTypes);

            var method = targetType.GetMethod(
                request.methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                argumentTypes,
                null);

            if (method == null)
            {
                return Fail("Static method was not found: " + request.methodName, lastReloadAtUtc);
            }

            var result = method.Invoke(null, arguments);
            return Ok("Invoked static method " + request.methodName + (result != null ? " -> " + result : string.Empty), lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse CaptureSceneView(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeCaptureWindowRequest>(argument ?? string.Empty) ?? new UnityBridgeCaptureWindowRequest();
            request.type = string.IsNullOrWhiteSpace(request.type) ? "SceneView" : request.type;
            return CaptureWindow(request, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse CaptureGameView(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeCaptureWindowRequest>(argument ?? string.Empty) ?? new UnityBridgeCaptureWindowRequest();
            request.type = string.IsNullOrWhiteSpace(request.type) ? "UnityEditor.GameView" : request.type;
            if (string.IsNullOrWhiteSpace(request.title))
            {
                request.title = "Game";
            }

            return CaptureWindow(request, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse CaptureWindow(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeCaptureWindowRequest>(argument ?? string.Empty);
            if (request == null)
            {
                return Fail("capture_window requires JSON arguments.", lastReloadAtUtc);
            }

            return CaptureWindow(request, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse PlayerPrefsSet(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgePlayerPrefsValueRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.key) || string.IsNullOrWhiteSpace(request.valueType))
            {
                return Fail("playerprefs_set requires key and valueType.", lastReloadAtUtc);
            }

            switch (request.valueType)
            {
                case "bool":
                    PlayerPrefs.SetInt(request.key, request.boolValue ? 1 : 0);
                    break;
                case "int":
                    PlayerPrefs.SetInt(request.key, request.intValue);
                    break;
                case "float":
                    PlayerPrefs.SetFloat(request.key, request.floatValue);
                    break;
                case "string":
                    PlayerPrefs.SetString(request.key, request.stringValue ?? string.Empty);
                    break;
                default:
                    return Fail("Unsupported PlayerPrefs valueType: " + request.valueType, lastReloadAtUtc);
            }

            PlayerPrefs.Save();
            if (!PlayerPrefs.HasKey(request.key))
            {
                return Fail("PlayerPrefs write did not persist key: " + request.key, lastReloadAtUtc);
            }

            switch (request.valueType)
            {
                case "bool":
                    if ((PlayerPrefs.GetInt(request.key) != 0) != request.boolValue)
                    {
                        return Fail("PlayerPrefs bool write verification failed for key: " + request.key, lastReloadAtUtc);
                    }

                    break;
                case "int":
                    if (PlayerPrefs.GetInt(request.key) != request.intValue)
                    {
                        return Fail("PlayerPrefs int write verification failed for key: " + request.key, lastReloadAtUtc);
                    }

                    break;
                case "float":
                    if (Mathf.Abs(PlayerPrefs.GetFloat(request.key) - request.floatValue) > 0.0001f)
                    {
                        return Fail("PlayerPrefs float write verification failed for key: " + request.key, lastReloadAtUtc);
                    }

                    break;
                case "string":
                    if (!string.Equals(PlayerPrefs.GetString(request.key), request.stringValue ?? string.Empty, StringComparison.Ordinal))
                    {
                        return Fail("PlayerPrefs string write verification failed for key: " + request.key, lastReloadAtUtc);
                    }

                    break;
            }

            return Ok("Updated PlayerPrefs key: " + request.key, lastReloadAtUtc);
        }

        public static UnityBridgePlayerPrefsValueResponse PlayerPrefsGet(string argument)
        {
            var request = JsonUtility.FromJson<UnityBridgePlayerPrefsValueRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.key))
            {
                return new UnityBridgePlayerPrefsValueResponse
                {
                    hasKey = false,
                    key = string.Empty,
                    valueType = string.Empty,
                    message = "playerprefs_get requires key."
                };
            }

            var response = new UnityBridgePlayerPrefsValueResponse
            {
                hasKey = PlayerPrefs.HasKey(request.key),
                key = request.key,
                valueType = request.valueType ?? string.Empty
            };

            if (!response.hasKey)
            {
                response.message = "PlayerPrefs key was not found.";
                return response;
            }

            switch (request.valueType)
            {
                case "":
                case null:
                    response.message = "Key exists. Provide valueType for a typed read.";
                    return response;
                case "bool":
                    response.boolValue = PlayerPrefs.GetInt(request.key) != 0;
                    response.message = "Read PlayerPrefs bool value.";
                    return response;
                case "int":
                    response.intValue = PlayerPrefs.GetInt(request.key);
                    response.message = "Read PlayerPrefs int value.";
                    return response;
                case "float":
                    response.floatValue = PlayerPrefs.GetFloat(request.key);
                    response.message = "Read PlayerPrefs float value.";
                    return response;
                case "string":
                    response.stringValue = PlayerPrefs.GetString(request.key);
                    response.message = "Read PlayerPrefs string value.";
                    return response;
                default:
                    response.message = "Unsupported PlayerPrefs valueType: " + request.valueType;
                    return response;
            }
        }

        public static UnityBridgeCommandResponse PlayerPrefsDeleteKey(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgePlayerPrefsValueRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.key))
            {
                return Fail("playerprefs_delete_key requires key.", lastReloadAtUtc);
            }

            PlayerPrefs.DeleteKey(request.key);
            PlayerPrefs.Save();
            if (PlayerPrefs.HasKey(request.key))
            {
                return Fail("PlayerPrefs delete verification failed for key: " + request.key, lastReloadAtUtc);
            }

            return Ok("Deleted PlayerPrefs key: " + request.key, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse PlayerPrefsDeleteAll(string lastReloadAtUtc)
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            return Ok("Deleted all PlayerPrefs keys.", lastReloadAtUtc);
        }

        public static UnityBridgeTextureImporterSummary SetTextureImporter(string argument)
        {
            var request = JsonUtility.FromJson<UnityBridgeTextureImporterRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.path))
            {
                return new UnityBridgeTextureImporterSummary
                {
                    assetPath = request != null ? request.path : string.Empty,
                    found = false,
                    message = "set_texture_importer requires path."
                };
            }

            var importer = AssetImporter.GetAtPath(request.path) as TextureImporter;
            if (importer == null)
            {
                return ErrorTextureImporterSummary(request.path, "TextureImporter was not found for asset.");
            }

            if (request.setTextureType)
            {
                TextureImporterType textureType;
                if (!Enum.TryParse(request.textureType, true, out textureType))
                {
                    return ErrorTextureImporterSummary(request.path, "Unknown textureType: " + request.textureType);
                }

                importer.textureType = textureType;
            }

            if (request.setSpriteImportMode)
            {
                SpriteImportMode spriteImportMode;
                if (!Enum.TryParse(request.spriteImportMode, true, out spriteImportMode))
                {
                    return ErrorTextureImporterSummary(request.path, "Unknown spriteImportMode: " + request.spriteImportMode);
                }

                importer.spriteImportMode = spriteImportMode;
            }

            if (request.setIsReadable)
            {
                importer.isReadable = request.isReadable;
            }

            if (request.setMipmapEnabled)
            {
                importer.mipmapEnabled = request.mipmapEnabled;
            }

            if (request.setAlphaIsTransparency)
            {
                importer.alphaIsTransparency = request.alphaIsTransparency;
            }

            if (request.setFilterMode)
            {
                FilterMode filterMode;
                if (!Enum.TryParse(request.filterMode, true, out filterMode))
                {
                    return ErrorTextureImporterSummary(request.path, "Unknown filterMode: " + request.filterMode);
                }

                importer.filterMode = filterMode;
            }

            if (request.setWrapMode)
            {
                TextureWrapMode wrapMode;
                if (!Enum.TryParse(request.wrapMode, true, out wrapMode))
                {
                    return ErrorTextureImporterSummary(request.path, "Unknown wrapMode: " + request.wrapMode);
                }

                importer.wrapMode = wrapMode;
            }

            if (request.setMaxTextureSize)
            {
                importer.maxTextureSize = request.maxTextureSize;
            }

            if (request.setTextureCompression)
            {
                TextureImporterCompression textureCompression;
                if (!Enum.TryParse(request.textureCompression, true, out textureCompression))
                {
                    return ErrorTextureImporterSummary(request.path, "Unknown textureCompression: " + request.textureCompression);
                }

                importer.textureCompression = textureCompression;
            }

            importer.SaveAndReimport();
            return UnityBridgeStateBuilder.BuildTextureImporterSummary(request.path);
        }

        public static UnityBridgePhysicsSettingsSummary SetPhysicsSettings(string argument)
        {
            var request = JsonUtility.FromJson<UnityBridgePhysicsSettingsRequest>(argument ?? string.Empty);
            if (request == null)
            {
                return new UnityBridgePhysicsSettingsSummary
                {
                    message = "Physics settings payload was invalid."
                };
            }

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

            ApplyPhysics3DSettings(physics3DSerialized, request.physics3D ?? new UnityBridgePhysics3DSettingsRequest());
            ApplyPhysics2DSettings(physics2DSerialized, request.physics2D ?? new UnityBridgePhysics2DSettingsRequest());

            SaveProjectSettingsAsset(physics3DObject, physics3DSerialized);
            SaveProjectSettingsAsset(physics2DObject, physics2DSerialized);

            var summary = UnityBridgeStateBuilder.BuildPhysicsSettingsSummary();
            summary.message = "Updated global physics settings.";
            return summary;
        }

        public static UnityBridgeProjectTagLayerSummary SetProjectTagLayers(string argument)
        {
            var request = JsonUtility.FromJson<UnityBridgeProjectTagLayerRequest>(argument ?? string.Empty) ?? new UnityBridgeProjectTagLayerRequest();
            var tagManager = LoadProjectSettingsAsset("ProjectSettings/TagManager.asset");
            if (tagManager == null)
            {
                return new UnityBridgeProjectTagLayerSummary
                {
                    message = "TagManager project settings asset was not found."
                };
            }

            var serializedObject = new SerializedObject(tagManager);
            var tagsProperty = serializedObject.FindProperty("tags");
            var layersProperty = serializedObject.FindProperty("layers");
            var sortingLayersProperty = serializedObject.FindProperty("m_SortingLayers");
            var renderingLayersProperty = serializedObject.FindProperty("m_RenderingLayers");

            foreach (var tag in request.addTags ?? Enumerable.Empty<string>())
            {
                AddUniqueStringArrayValue(tagsProperty, tag);
            }

            foreach (var tag in request.removeTags ?? Enumerable.Empty<string>())
            {
                RemoveStringArrayValue(tagsProperty, tag);
            }

            foreach (var layer in request.setLayers ?? Enumerable.Empty<UnityBridgeNamedIndexValue>())
            {
                if (layer == null || layer.index < 0 || layer.index >= layersProperty.arraySize)
                {
                    continue;
                }

                layersProperty.GetArrayElementAtIndex(layer.index).stringValue = layer.name ?? string.Empty;
            }

            foreach (var sortingLayerName in request.addSortingLayers ?? Enumerable.Empty<string>())
            {
                AddSortingLayer(sortingLayersProperty, sortingLayerName);
            }

            foreach (var renameRequest in request.renameSortingLayers ?? Enumerable.Empty<UnityBridgeSortingLayerRenameRequest>())
            {
                RenameSortingLayer(sortingLayersProperty, renameRequest);
            }

            foreach (var sortingLayerName in request.removeSortingLayers ?? Enumerable.Empty<string>())
            {
                RemoveSortingLayer(sortingLayersProperty, sortingLayerName);
            }

            if (renderingLayersProperty != null)
            {
                foreach (var renderingLayer in request.setRenderingLayers ?? Enumerable.Empty<UnityBridgeNamedIndexValue>())
                {
                    if (renderingLayer == null || renderingLayer.index < 0 || renderingLayer.index >= renderingLayersProperty.arraySize)
                    {
                        continue;
                    }

                    renderingLayersProperty.GetArrayElementAtIndex(renderingLayer.index).stringValue = renderingLayer.name ?? string.Empty;
                }
            }

            SaveProjectSettingsAsset(tagManager, serializedObject);

            var summary = UnityBridgeStateBuilder.BuildProjectTagLayerSummary();
            summary.message = "Updated project tags, layers, and sorting layers.";
            return summary;
        }

        public static UnityBridgeQualitySettingsSummary SetQualitySettings(string argument)
        {
            var request = JsonUtility.FromJson<UnityBridgeQualitySettingsRequest>(argument ?? string.Empty) ?? new UnityBridgeQualitySettingsRequest();
            var qualitySettings = LoadProjectSettingsAsset("ProjectSettings/QualitySettings.asset");
            if (qualitySettings == null)
            {
                return new UnityBridgeQualitySettingsSummary
                {
                    message = "QualitySettings project settings asset was not found."
                };
            }

            var serializedObject = new SerializedObject(qualitySettings);
            var currentQualityProperty = serializedObject.FindProperty("m_CurrentQuality");
            var qualityLevelsProperty = serializedObject.FindProperty("m_QualitySettings");
            var platformDefaultsProperty = serializedObject.FindProperty("m_PerPlatformDefaultQuality");

            if (request.setCurrentQualityIndex &&
                request.currentQualityIndex >= 0 &&
                request.currentQualityIndex < qualityLevelsProperty.arraySize)
            {
                currentQualityProperty.intValue = request.currentQualityIndex;
            }

            foreach (var levelRequest in request.qualityLevels ?? Enumerable.Empty<UnityBridgeQualityLevelRequest>())
            {
                if (levelRequest == null || levelRequest.index < 0 || levelRequest.index >= qualityLevelsProperty.arraySize)
                {
                    continue;
                }

                var level = qualityLevelsProperty.GetArrayElementAtIndex(levelRequest.index);
                SetIfRequested(level.FindPropertyRelative("name"), levelRequest.setName, levelRequest.name);
                SetIfRequested(level.FindPropertyRelative("pixelLightCount"), levelRequest.setPixelLightCount, levelRequest.pixelLightCount);
                SetIfRequested(level.FindPropertyRelative("shadows"), levelRequest.setShadows, levelRequest.shadows);
                SetIfRequested(level.FindPropertyRelative("shadowResolution"), levelRequest.setShadowResolution, levelRequest.shadowResolution);
                SetIfRequested(level.FindPropertyRelative("shadowProjection"), levelRequest.setShadowProjection, levelRequest.shadowProjection);
                SetIfRequested(level.FindPropertyRelative("shadowCascades"), levelRequest.setShadowCascades, levelRequest.shadowCascades);
                SetIfRequested(level.FindPropertyRelative("shadowDistance"), levelRequest.setShadowDistance, levelRequest.shadowDistance);
                SetIfRequested(level.FindPropertyRelative("globalTextureMipmapLimit"), levelRequest.setGlobalTextureMipmapLimit, levelRequest.globalTextureMipmapLimit);
                SetIfRequested(level.FindPropertyRelative("anisotropicTextures"), levelRequest.setAnisotropicTextures, levelRequest.anisotropicTextures);
                SetIfRequested(level.FindPropertyRelative("antiAliasing"), levelRequest.setAntiAliasing, levelRequest.antiAliasing);
                SetIfRequested(level.FindPropertyRelative("vSyncCount"), levelRequest.setVSyncCount, levelRequest.vSyncCount);
                SetIfRequested(level.FindPropertyRelative("lodBias"), levelRequest.setLodBias, levelRequest.lodBias);
                SetIfRequested(level.FindPropertyRelative("maximumLODLevel"), levelRequest.setMaximumLODLevel, levelRequest.maximumLODLevel);
                SetIfRequested(level.FindPropertyRelative("streamingMipmapsActive"), levelRequest.setStreamingMipmapsActive, levelRequest.streamingMipmapsActive);
                SetIfRequested(level.FindPropertyRelative("streamingMipmapsMemoryBudget"), levelRequest.setStreamingMipmapsMemoryBudget, levelRequest.streamingMipmapsMemoryBudget);
            }

            if (platformDefaultsProperty != null)
            {
                foreach (var platformDefault in request.perPlatformDefaults ?? Enumerable.Empty<UnityBridgePlatformQualityValue>())
                {
                    if (platformDefault == null || string.IsNullOrWhiteSpace(platformDefault.platform))
                    {
                        continue;
                    }

                    var entry = FindPerPlatformQualityEntry(platformDefaultsProperty, platformDefault.platform);
                    if (entry != null)
                    {
                        entry.intValue = platformDefault.qualityIndex;
                    }
                }
            }

            SaveProjectSettingsAsset(qualitySettings, serializedObject);
            var summary = UnityBridgeStateBuilder.BuildQualitySettingsSummary();
            summary.message = "Updated project quality settings.";
            return summary;
        }

        public static UnityBridgeAudioSettingsSummary SetAudioSettings(string argument)
        {
            var request = JsonUtility.FromJson<UnityBridgeAudioSettingsRequest>(argument ?? string.Empty) ?? new UnityBridgeAudioSettingsRequest();
            var audioManager = LoadProjectSettingsAsset("ProjectSettings/AudioManager.asset");
            if (audioManager == null)
            {
                return new UnityBridgeAudioSettingsSummary
                {
                    message = "AudioManager project settings asset was not found."
                };
            }

            var serializedObject = new SerializedObject(audioManager);
            SetIfRequested(serializedObject.FindProperty("m_Volume"), request.setVolume, request.volume);
            SetIfRequested(serializedObject.FindProperty("Rolloff Scale"), request.setRolloffScale, request.rolloffScale);
            SetIfRequested(serializedObject.FindProperty("Doppler Factor"), request.setDopplerFactor, request.dopplerFactor);
            SetIfRequested(serializedObject.FindProperty("Default Speaker Mode"), request.setDefaultSpeakerMode, request.defaultSpeakerMode);
            SetIfRequested(serializedObject.FindProperty("m_SampleRate"), request.setSampleRate, request.sampleRate);
            SetIfRequested(serializedObject.FindProperty("m_DSPBufferSize"), request.setDspBufferSize, request.dspBufferSize);
            SetIfRequested(serializedObject.FindProperty("m_VirtualVoiceCount"), request.setVirtualVoiceCount, request.virtualVoiceCount);
            SetIfRequested(serializedObject.FindProperty("m_RealVoiceCount"), request.setRealVoiceCount, request.realVoiceCount);
            SetIfRequested(serializedObject.FindProperty("m_SpatializerPlugin"), request.setSpatializerPlugin, request.spatializerPlugin);
            SetIfRequested(serializedObject.FindProperty("m_AmbisonicDecoderPlugin"), request.setAmbisonicDecoderPlugin, request.ambisonicDecoderPlugin);
            SetIfRequested(serializedObject.FindProperty("m_DisableAudio"), request.setDisableAudio, request.disableAudio);
            SetIfRequested(serializedObject.FindProperty("m_VirtualizeEffects"), request.setVirtualizeEffects, request.virtualizeEffects);
            SetIfRequested(serializedObject.FindProperty("m_RequestedDSPBufferSize"), request.setRequestedDspBufferSize, request.requestedDspBufferSize);

            SaveProjectSettingsAsset(audioManager, serializedObject);
            var summary = UnityBridgeStateBuilder.BuildAudioSettingsSummary();
            summary.message = "Updated project audio settings.";
            return summary;
        }

        public static UnityBridgeTimeSettingsSummary SetTimeSettings(string argument)
        {
            var request = JsonUtility.FromJson<UnityBridgeTimeSettingsRequest>(argument ?? string.Empty) ?? new UnityBridgeTimeSettingsRequest();
            var timeManagerPath = GetProjectSettingsPath("TimeManager.asset");
            if (!File.Exists(timeManagerPath))
            {
                return new UnityBridgeTimeSettingsSummary
                {
                    message = "TimeManager project settings asset was not found."
                };
            }

            var lines = File.ReadAllLines(timeManagerPath).ToList();
            ReplaceSerializedRational(lines, "  Fixed Timestep:", request.setFixedTimestep, request.fixedTimestep);
            ReplaceProjectSettingsFloat(lines, "  Maximum Allowed Timestep: ", request.setMaximumAllowedTimestep, request.maximumAllowedTimestep);
            ReplaceProjectSettingsFloat(lines, "  m_TimeScale: ", request.setTimeScale, request.timeScale);
            ReplaceProjectSettingsFloat(lines, "  Maximum Particle Timestep: ", request.setMaximumParticleTimestep, request.maximumParticleTimestep);
            File.WriteAllLines(timeManagerPath, lines);
            AssetDatabase.Refresh();
            var summary = UnityBridgeStateBuilder.BuildTimeSettingsSummary();
            summary.message = "Updated project time settings.";
            return summary;
        }

        public static UnityBridgeCommandResponse SetTimeScale(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeFloatValueRequest>(argument ?? string.Empty);
            if (request == null)
            {
                return Fail("set_time_scale requires a JSON payload with value.", lastReloadAtUtc);
            }

            Time.timeScale = request.value;
            return Ok("Updated Time.timeScale to: " + request.value, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse SetTargetFrameRate(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeIntValueRequest>(argument ?? string.Empty);
            if (request == null)
            {
                return Fail("set_target_frame_rate requires a JSON payload with value.", lastReloadAtUtc);
            }

            Application.targetFrameRate = request.value;
            return Ok("Updated Application.targetFrameRate to: " + request.value, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse BuildPlayer(string argument, string lastReloadAtUtc)
        {
            var report = BuildPlayerReport(argument);
            var message = report.succeeded
                ? "Build succeeded: " + report.outputPath
                : "Build " + (string.IsNullOrWhiteSpace(report.result) ? "failed" : report.result) +
                  (string.IsNullOrWhiteSpace(report.message) ? string.Empty : " - " + report.message);

            return report.succeeded ? Ok(message, lastReloadAtUtc) : Fail(message, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse RunTests(string argument, string lastReloadAtUtc)
        {
            var report = UnityBridgeTestRunner.RunTests(argument);
            if (report.running)
            {
                return Ok("Test run started: " + report.testMode, lastReloadAtUtc);
            }

            var success = string.Equals(report.resultState, "Passed", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(report.resultState, "Success", StringComparison.OrdinalIgnoreCase);
            var message = string.IsNullOrWhiteSpace(report.message)
                ? "Test run finished with result " + report.resultState
                : "Test run " + report.resultState + " - " + report.message;
            return success ? Ok(message, lastReloadAtUtc) : Fail(message, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse AddPackage(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgePackageOperationRequest>(argument ?? string.Empty);
            var packageId = request != null && !string.IsNullOrWhiteSpace(request.packageId)
                ? request.packageId
                : request != null && request.packageIds != null
                    ? request.packageIds.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                    : null;
            if (string.IsNullOrWhiteSpace(packageId))
            {
                return Fail("add_package requires JSON with packageId.", lastReloadAtUtc);
            }

            var summary = UnityBridgePackageTracker.StartOperation("add", Client.Add(packageId), new[] { packageId }, null);
            return summary.running
                ? Ok("Started package add: " + packageId, lastReloadAtUtc)
                : Fail(summary.message, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse RemovePackage(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgePackageOperationRequest>(argument ?? string.Empty);
            var packageId = request != null && !string.IsNullOrWhiteSpace(request.packageId)
                ? request.packageId
                : request != null && request.packageIds != null
                    ? request.packageIds.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                    : null;
            if (string.IsNullOrWhiteSpace(packageId))
            {
                return Fail("remove_package requires JSON with packageId.", lastReloadAtUtc);
            }

            var summary = UnityBridgePackageTracker.StartOperation("remove", Client.Remove(packageId), null, new[] { packageId });
            return summary.running
                ? Ok("Started package removal: " + packageId, lastReloadAtUtc)
                : Fail(summary.message, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse EmbedPackage(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgePackageOperationRequest>(argument ?? string.Empty);
            var packageId = request != null && !string.IsNullOrWhiteSpace(request.packageId)
                ? request.packageId
                : request != null && request.packageIds != null
                    ? request.packageIds.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                    : null;
            if (string.IsNullOrWhiteSpace(packageId))
            {
                return Fail("embed_package requires JSON with packageId.", lastReloadAtUtc);
            }

            var summary = UnityBridgePackageTracker.StartOperation("embed", Client.Embed(packageId), new[] { packageId }, null);
            return summary.running
                ? Ok("Started package embed: " + packageId, lastReloadAtUtc)
                : Fail(summary.message, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse ResolvePackages(string lastReloadAtUtc)
        {
            Client.Resolve();
            var summary = UnityBridgePackageTracker.CompleteImmediateOperation("resolve", true, "Triggered Unity package resolve.", null, null);
            return Ok(summary.message, lastReloadAtUtc);
        }

        public static UnityBridgeBuildReportSummary BuildPlayerReport(string argument)
        {
            var request = JsonUtility.FromJson<UnityBridgeBuildPlayerRequest>(argument ?? string.Empty) ?? new UnityBridgeBuildPlayerRequest();
            var startedAtUtc = DateTime.UtcNow.ToString("O");

            BuildTarget target;
            string targetError;
            if (!TryResolveBuildTarget(request.buildTarget, out target, out targetError))
            {
                var invalidTargetReport = CreateBuildValidationFailure(targetError, startedAtUtc, request.locationPathName, request.buildTarget, request.scenes);
                UnityBridgeBuildTracker.SetLastBuildReport(invalidTargetReport);
                return invalidTargetReport;
            }

            var scenes = ResolveBuildScenes(request.scenes);
            if (scenes.Count == 0)
            {
                var noScenesReport = CreateBuildValidationFailure("No build scenes were provided and no enabled EditorBuildSettings scenes were found.", startedAtUtc, request.locationPathName, target.ToString(), request.scenes);
                UnityBridgeBuildTracker.SetLastBuildReport(noScenesReport);
                return noScenesReport;
            }

            var locationPathName = ResolveBuildLocation(request.locationPathName, target, request.buildAppBundle);
            var options = BuildOptions.None;
            if (request.development)
            {
                options |= BuildOptions.Development;
            }

            if (request.connectProfiler)
            {
                options |= BuildOptions.ConnectWithProfiler;
            }

            if (request.allowDebugging)
            {
                options |= BuildOptions.AllowDebugging;
            }

            if (request.autoRunPlayer)
            {
                options |= BuildOptions.AutoRunPlayer;
            }

            if (request.buildScriptsOnly)
            {
                options |= BuildOptions.BuildScriptsOnly;
            }

            if (request.showBuiltPlayer)
            {
                options |= BuildOptions.ShowBuiltPlayer;
            }

            if (request.strictMode)
            {
                options |= BuildOptions.StrictMode;
            }

            if (request.acceptExternalModificationsToPlayer)
            {
                options |= BuildOptions.AcceptExternalModificationsToPlayer;
            }

            if (request.compressWithLz4HC)
            {
                options |= BuildOptions.CompressWithLz4HC;
            }
            else if (request.compressWithLz4)
            {
                options |= BuildOptions.CompressWithLz4;
            }

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = locationPathName,
                target = target,
                options = options
            };

            UnityBridgeBuildTracker.MarkBuildStarted(startedAtUtc);
            Debug.Log("[UnityBridge] Starting build for " + target + " -> " + locationPathName);
            var originalBuildAppBundle = EditorUserBuildSettings.buildAppBundle;

            try
            {
                if (request.buildAppBundle)
                {
                    EditorUserBuildSettings.buildAppBundle = true;
                }

                var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                var buildReport = CreateBuildReportSummary(report, buildPlayerOptions, startedAtUtc);
                UnityBridgeBuildTracker.SetLastBuildReport(buildReport);
                Debug.Log("[UnityBridge] Build finished with result " + buildReport.result + " -> " + buildReport.outputPath);
                return buildReport;
            }
            catch (Exception exception)
            {
                var failedReport = CreateBuildExceptionReport(exception, buildPlayerOptions, startedAtUtc);
                UnityBridgeBuildTracker.SetLastBuildReport(failedReport);
                Debug.LogError("[UnityBridge] Build failed: " + exception);
                return failedReport;
            }
            finally
            {
                if (request.buildAppBundle)
                {
                    EditorUserBuildSettings.buildAppBundle = originalBuildAppBundle;
                }
            }
        }

        public static UnityBridgeCommandResponse SendWindowKeyEvent(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeWindowKeyEventRequest>(argument ?? string.Empty);
            if (request == null || (string.IsNullOrWhiteSpace(request.type) && string.IsNullOrWhiteSpace(request.title)) || string.IsNullOrWhiteSpace(request.keyCode))
            {
                return Fail("send_window_key_event requires type/title and keyCode.", lastReloadAtUtc);
            }

            var editorWindow = UnityBridgeStateBuilder.FindEditorWindow(request.type, request.title);
            if (editorWindow == null)
            {
                return Fail("Matching editor window was not found.", lastReloadAtUtc);
            }

            KeyCode keyCode;
            if (!Enum.TryParse(request.keyCode, true, out keyCode))
            {
                return Fail("Unknown keyCode: " + request.keyCode, lastReloadAtUtc);
            }

            var eventType = ParseEventType(request.eventType, EventType.KeyDown);
            var unityEvent = new Event
            {
                type = eventType,
                keyCode = keyCode,
                character = request.character,
                modifiers = BuildModifiers(request.shift, request.control, request.alt, request.command)
            };

            editorWindow.Focus();
            editorWindow.SendEvent(unityEvent);
            return Ok("Sent key event " + eventType + " to " + ResolveWindowLabel(editorWindow), lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse SendWindowMouseEvent(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeWindowMouseEventRequest>(argument ?? string.Empty);
            if (request == null || (string.IsNullOrWhiteSpace(request.type) && string.IsNullOrWhiteSpace(request.title)))
            {
                return Fail("send_window_mouse_event requires type/title.", lastReloadAtUtc);
            }

            var editorWindow = UnityBridgeStateBuilder.FindEditorWindow(request.type, request.title);
            if (editorWindow == null)
            {
                return Fail("Matching editor window was not found.", lastReloadAtUtc);
            }

            var eventType = ParseEventType(request.eventType, EventType.MouseDown);
            var unityEvent = new Event
            {
                type = eventType,
                mousePosition = new Vector2(request.x, request.y),
                button = request.button,
                clickCount = request.clickCount <= 0 ? 1 : request.clickCount,
                delta = request.delta == null ? Vector2.zero : new Vector2(request.delta.x, request.delta.y),
                modifiers = BuildModifiers(request.shift, request.control, request.alt, request.command)
            };

            editorWindow.Focus();
            editorWindow.SendEvent(unityEvent);
            return Ok("Sent mouse event " + eventType + " to " + ResolveWindowLabel(editorWindow), lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse CreateFolder(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeAssetCreateFolderRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.parentFolder) || string.IsNullOrWhiteSpace(request.folderName))
            {
                return Fail("create_folder requires JSON with parentFolder and folderName.", lastReloadAtUtc);
            }

            var guid = AssetDatabase.CreateFolder(request.parentFolder, request.folderName);
            if (string.IsNullOrWhiteSpace(guid))
            {
                return Fail("Folder could not be created.", lastReloadAtUtc);
            }

            return Ok("Created folder: " + AssetDatabase.GUIDToAssetPath(guid), lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse DeleteAsset(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgePathRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.path))
            {
                return Fail("delete_asset requires JSON with path.", lastReloadAtUtc);
            }

            if (!AssetDatabase.DeleteAsset(request.path))
            {
                return Fail("Asset could not be deleted: " + request.path, lastReloadAtUtc);
            }

            return Ok("Deleted asset: " + request.path, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse MoveAsset(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeAssetPathPairRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.fromPath) || string.IsNullOrWhiteSpace(request.toPath))
            {
                return Fail("move_asset requires JSON with fromPath and toPath.", lastReloadAtUtc);
            }

            var error = AssetDatabase.MoveAsset(request.fromPath, request.toPath);
            if (!string.IsNullOrWhiteSpace(error))
            {
                return Fail(error, lastReloadAtUtc);
            }

            return Ok("Moved asset to: " + request.toPath, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse RenameAsset(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeRenameAssetRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.path) || string.IsNullOrWhiteSpace(request.name))
            {
                return Fail("rename_asset requires JSON with path and name.", lastReloadAtUtc);
            }

            var error = AssetDatabase.RenameAsset(request.path, request.name);
            if (!string.IsNullOrWhiteSpace(error))
            {
                return Fail(error, lastReloadAtUtc);
            }

            var directory = System.IO.Path.GetDirectoryName(request.path)?.Replace("\\", "/") ?? "Assets";
            return Ok("Renamed asset to: " + directory + "/" + request.name, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse DuplicateAsset(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeAssetPathPairRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.fromPath) || string.IsNullOrWhiteSpace(request.toPath))
            {
                return Fail("duplicate_asset requires JSON with fromPath and toPath.", lastReloadAtUtc);
            }

            if (!AssetDatabase.CopyAsset(request.fromPath, request.toPath))
            {
                return Fail("Asset could not be duplicated.", lastReloadAtUtc);
            }

            return Ok("Duplicated asset to: " + request.toPath, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse SetBuildSettings(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeBuildSettingsRequest>(argument ?? string.Empty);
            if (request == null)
            {
                return Fail("set_build_settings requires JSON input.", lastReloadAtUtc);
            }

            if (request.setDevelopmentBuild)
            {
                EditorUserBuildSettings.development = request.developmentBuild;
            }

            if (request.setConnectProfiler)
            {
                EditorUserBuildSettings.connectProfiler = request.connectProfiler;
            }

            if (request.setAllowDebugging)
            {
                EditorUserBuildSettings.allowDebugging = request.allowDebugging;
            }

            if (request.setBuildAppBundle)
            {
                EditorUserBuildSettings.buildAppBundle = request.buildAppBundle;
            }

            if (!string.IsNullOrWhiteSpace(request.locationPathName))
            {
                EditorUserBuildSettings.SetBuildLocation(EditorUserBuildSettings.activeBuildTarget, request.locationPathName);
            }

            return Ok("Updated build settings for target: " + EditorUserBuildSettings.activeBuildTarget, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse SwitchBuildTarget(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeSwitchBuildTargetRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.buildTarget))
            {
                return Fail("switch_build_target requires JSON with buildTarget.", lastReloadAtUtc);
            }

            BuildTarget target;
            if (!Enum.TryParse(request.buildTarget, true, out target))
            {
                return Fail("Unknown buildTarget: " + request.buildTarget, lastReloadAtUtc);
            }

            BuildTargetGroup group;
            if (!string.IsNullOrWhiteSpace(request.buildTargetGroup))
            {
                if (!Enum.TryParse(request.buildTargetGroup, true, out group))
                {
                    return Fail("Unknown buildTargetGroup: " + request.buildTargetGroup, lastReloadAtUtc);
                }
            }
            else
            {
                group = BuildPipeline.GetBuildTargetGroup(target);
            }

            var switched = EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
            if (!switched)
            {
                return Fail("Unity refused to switch active build target to " + target + ".", lastReloadAtUtc);
            }

            return Ok("Switched active build target to: " + target, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse SetPlayerSettings(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgePlayerSettingsRequest>(argument ?? string.Empty);
            if (request == null)
            {
                return Fail("set_player_settings requires JSON input.", lastReloadAtUtc);
            }

            BuildTargetGroup buildTargetGroup;
            NamedBuildTarget namedBuildTarget;
            string error;
            if (!TryResolveNamedBuildTarget(request.buildTarget, request.buildTargetGroup, out buildTargetGroup, out namedBuildTarget, out error))
            {
                return Fail(error, lastReloadAtUtc);
            }

            if (request.setApplicationIdentifier)
            {
                if (string.IsNullOrWhiteSpace(request.applicationIdentifier))
                {
                    return Fail("applicationIdentifier cannot be empty when setApplicationIdentifier is true.", lastReloadAtUtc);
                }

                PlayerSettings.SetApplicationIdentifier(namedBuildTarget, request.applicationIdentifier);
            }

            if (request.setScriptingBackend)
            {
                ScriptingImplementation scriptingBackend;
                if (!Enum.TryParse(request.scriptingBackend, true, out scriptingBackend))
                {
                    return Fail("Unknown scriptingBackend: " + request.scriptingBackend, lastReloadAtUtc);
                }

                PlayerSettings.SetScriptingBackend(namedBuildTarget, scriptingBackend);
            }

            if (request.setApiCompatibilityLevel)
            {
                ApiCompatibilityLevel apiCompatibilityLevel;
                if (!Enum.TryParse(request.apiCompatibilityLevel, true, out apiCompatibilityLevel))
                {
                    return Fail("Unknown apiCompatibilityLevel: " + request.apiCompatibilityLevel, lastReloadAtUtc);
                }

                PlayerSettings.SetApiCompatibilityLevel(namedBuildTarget, apiCompatibilityLevel);
            }

            if (request.setManagedStrippingLevel)
            {
                ManagedStrippingLevel managedStrippingLevel;
                if (!Enum.TryParse(request.managedStrippingLevel, true, out managedStrippingLevel))
                {
                    return Fail("Unknown managedStrippingLevel: " + request.managedStrippingLevel, lastReloadAtUtc);
                }

                PlayerSettings.SetManagedStrippingLevel(namedBuildTarget, managedStrippingLevel);
            }

            if (request.setIncrementalIl2CppBuild)
            {
                PlayerSettings.SetIncrementalIl2CppBuild(namedBuildTarget, request.incrementalIl2CppBuild);
            }

            if (request.setCaptureStartupLogs)
            {
                PlayerSettings.SetCaptureStartupLogs(namedBuildTarget, request.captureStartupLogs);
            }

            if (request.setScriptingDefineSymbols)
            {
                var defineSymbols = request.scriptingDefineSymbols ?? string.Empty;
                if ((string.IsNullOrWhiteSpace(defineSymbols) || defineSymbols.Trim().Length == 0) &&
                    request.scriptingDefineSymbolList != null &&
                    request.scriptingDefineSymbolList.Count > 0)
                {
                    defineSymbols = string.Join(";", request.scriptingDefineSymbolList
                        .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
                        .Select(symbol => symbol.Trim()));
                }

                PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, defineSymbols);
            }

            return Ok("Updated player settings for " + namedBuildTarget.TargetName + ".", lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse SetBuildScenes(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeBuildScenesRequest>(argument ?? string.Empty);
            if (request == null || request.scenes == null)
            {
                return Fail("set_build_scenes requires JSON with scenes.", lastReloadAtUtc);
            }

            var buildScenes = new List<EditorBuildSettingsScene>();
            for (var index = 0; index < request.scenes.Count; index++)
            {
                var scene = request.scenes[index];
                if (scene == null || string.IsNullOrWhiteSpace(scene.path))
                {
                    return Fail("Each build scene requires a non-empty path.", lastReloadAtUtc);
                }

                if (!IsSceneAssetPath(scene.path))
                {
                    return Fail("Scene asset was not found: " + scene.path, lastReloadAtUtc);
                }

                buildScenes.Add(new EditorBuildSettingsScene(scene.path, scene.enabled));
            }

            EditorBuildSettings.scenes = buildScenes.ToArray();
            return Ok("Replaced build scenes. Count: " + buildScenes.Count, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse AddBuildScene(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgeBuildSceneMutationRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.path))
            {
                return Fail("add_build_scene requires JSON with path.", lastReloadAtUtc);
            }

            if (!IsSceneAssetPath(request.path))
            {
                return Fail("Scene asset was not found: " + request.path, lastReloadAtUtc);
            }

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes ?? Array.Empty<EditorBuildSettingsScene>());
            var existingIndex = scenes.FindIndex(scene => string.Equals(scene.path, request.path, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                scenes.RemoveAt(existingIndex);
            }

            var targetIndex = request.buildIndex;
            if (targetIndex < 0 || targetIndex > scenes.Count)
            {
                targetIndex = scenes.Count;
            }

            scenes.Insert(targetIndex, new EditorBuildSettingsScene(request.path, request.enabled));
            EditorBuildSettings.scenes = scenes.ToArray();

            return Ok("Added build scene at index " + targetIndex + ": " + request.path, lastReloadAtUtc);
        }

        public static UnityBridgeCommandResponse RemoveBuildScene(string argument, string lastReloadAtUtc)
        {
            var request = JsonUtility.FromJson<UnityBridgePathRequest>(argument ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.path))
            {
                return Fail("remove_build_scene requires JSON with path.", lastReloadAtUtc);
            }

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes ?? Array.Empty<EditorBuildSettingsScene>());
            var removedCount = scenes.RemoveAll(scene => string.Equals(scene.path, request.path, StringComparison.OrdinalIgnoreCase));
            if (removedCount <= 0)
            {
                return Fail("Build scene was not found: " + request.path, lastReloadAtUtc);
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            return Ok("Removed build scene: " + request.path, lastReloadAtUtc);
        }

        private static UnityBridgeBuildReportSummary CreateBuildReportSummary(BuildReport report, BuildPlayerOptions options, string startedAtUtc)
        {
            var finishedAtUtc = DateTime.UtcNow.ToString("O");
            if (report == null)
            {
                return new UnityBridgeBuildReportSummary
                {
                    hasReport = false,
                    succeeded = false,
                    result = "Unknown",
                    message = "BuildPipeline.BuildPlayer returned no report.",
                    buildTarget = options.target.ToString(),
                    buildTargetGroup = BuildPipeline.GetBuildTargetGroup(options.target).ToString(),
                    outputPath = options.locationPathName,
                    startedAtUtc = startedAtUtc,
                    finishedAtUtc = finishedAtUtc,
                    options = options.options.ToString(),
                    scenes = new List<string>(options.scenes ?? Array.Empty<string>())
                };
            }

            var summary = report.summary;
            return new UnityBridgeBuildReportSummary
            {
                hasReport = true,
                succeeded = summary.result == BuildResult.Succeeded,
                result = summary.result.ToString(),
                message = summary.result == BuildResult.Succeeded ? "Build completed successfully." : "Build completed with a non-success result.",
                buildTarget = summary.platform.ToString(),
                buildTargetGroup = BuildPipeline.GetBuildTargetGroup(summary.platform).ToString(),
                outputPath = string.IsNullOrWhiteSpace(summary.outputPath) ? options.locationPathName : summary.outputPath,
                startedAtUtc = startedAtUtc,
                finishedAtUtc = finishedAtUtc,
                durationSeconds = summary.totalTime.TotalSeconds,
                totalSizeBytes = summary.totalSize,
                totalErrors = summary.totalErrors,
                totalWarnings = summary.totalWarnings,
                options = options.options.ToString(),
                scenes = new List<string>(options.scenes ?? Array.Empty<string>())
            };
        }

        private static UnityBridgeBuildReportSummary CreateBuildExceptionReport(Exception exception, BuildPlayerOptions options, string startedAtUtc)
        {
            return new UnityBridgeBuildReportSummary
            {
                hasReport = false,
                succeeded = false,
                result = "Exception",
                message = exception.Message,
                buildTarget = options.target.ToString(),
                buildTargetGroup = BuildPipeline.GetBuildTargetGroup(options.target).ToString(),
                outputPath = options.locationPathName,
                startedAtUtc = startedAtUtc,
                finishedAtUtc = DateTime.UtcNow.ToString("O"),
                options = options.options.ToString(),
                scenes = new List<string>(options.scenes ?? Array.Empty<string>())
            };
        }

        private static UnityBridgeBuildReportSummary CreateBuildValidationFailure(string message, string startedAtUtc, string locationPathName, string buildTarget, List<string> requestedScenes)
        {
            return new UnityBridgeBuildReportSummary
            {
                hasReport = false,
                succeeded = false,
                result = "InvalidRequest",
                message = message,
                buildTarget = buildTarget ?? string.Empty,
                buildTargetGroup = string.Empty,
                outputPath = locationPathName ?? string.Empty,
                startedAtUtc = startedAtUtc,
                finishedAtUtc = DateTime.UtcNow.ToString("O"),
                options = BuildOptions.None.ToString(),
                scenes = requestedScenes != null ? new List<string>(requestedScenes) : new List<string>()
            };
        }

        private static bool TryResolveBuildTarget(string buildTargetValue, out BuildTarget target, out string error)
        {
            if (string.IsNullOrWhiteSpace(buildTargetValue))
            {
                target = EditorUserBuildSettings.activeBuildTarget;
                error = null;
                return true;
            }

            if (Enum.TryParse(buildTargetValue, true, out target))
            {
                error = null;
                return true;
            }

            error = "Unknown buildTarget: " + buildTargetValue;
            return false;
        }

        private static List<string> ResolveBuildScenes(List<string> requestedScenes)
        {
            if (requestedScenes != null && requestedScenes.Count > 0)
            {
                return requestedScenes.Where(scene => !string.IsNullOrWhiteSpace(scene)).Distinct().ToList();
            }

            return EditorBuildSettings.scenes
                .Where(scene => scene != null && scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .Distinct()
                .ToList();
        }

        private static string ResolveBuildLocation(string requestedLocationPath, BuildTarget target, bool buildAppBundle)
        {
            if (!string.IsNullOrWhiteSpace(requestedLocationPath))
            {
                return Path.IsPathRooted(requestedLocationPath)
                    ? requestedLocationPath
                    : Path.GetFullPath(Path.Combine(ProjectRoot(), requestedLocationPath));
            }

            var buildsDirectory = Path.Combine(ProjectRoot(), "Builds", target.ToString());
            var productName = SanitizeFileName(PlayerSettings.productName);
            string outputPath;

            switch (target)
            {
                case BuildTarget.StandaloneOSX:
                    outputPath = Path.Combine(buildsDirectory, productName + ".app");
                    break;
                case BuildTarget.Android:
                    outputPath = Path.Combine(buildsDirectory, productName + (buildAppBundle ? ".aab" : ".apk"));
                    break;
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    outputPath = Path.Combine(buildsDirectory, productName + ".exe");
                    break;
                default:
                    outputPath = Path.Combine(buildsDirectory, productName);
                    break;
            }

            var directory = Path.HasExtension(outputPath)
                ? Path.GetDirectoryName(outputPath)
                : outputPath;
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return outputPath;
        }

        private static string ProjectRoot()
        {
            return Path.GetDirectoryName(Application.dataPath) ?? Directory.GetCurrentDirectory();
        }

        private static string SanitizeFileName(string value)
        {
            var invalidCharacters = Path.GetInvalidFileNameChars();
            var characters = (value ?? "UnityBuild")
                .Select(character => invalidCharacters.Contains(character) ? '_' : character)
                .ToArray();
            return new string(characters);
        }

        private static UnityBridgeCommandResponse SetSerializedProperty(UnityEngine.Object target, string propertyPath, UnityBridgeSetComponentPropertyRequest request, string lastReloadAtUtc)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyPath);
            if (property == null)
            {
                return Fail("Serialized property was not found: " + propertyPath, lastReloadAtUtc);
            }

            Undo.RecordObject(target, "UnityBridge Set Serialized Property");

            switch (request.valueType)
            {
                case "bool":
                    property.boolValue = request.boolValue;
                    break;
                case "int":
                    property.intValue = request.intValue;
                    break;
                case "float":
                    property.floatValue = request.floatValue;
                    break;
                case "string":
                    property.stringValue = request.stringValue ?? string.Empty;
                    break;
                case "enum":
                    if (!string.IsNullOrWhiteSpace(request.stringValue))
                    {
                        var index = Array.IndexOf(property.enumNames, request.stringValue);
                        if (index < 0)
                        {
                            return Fail("Enum value was not found: " + request.stringValue, lastReloadAtUtc);
                        }

                        property.enumValueIndex = index;
                    }
                    else
                    {
                        property.enumValueIndex = request.intValue;
                    }

                    break;
                case "object_reference":
                    property.objectReferenceValue = string.IsNullOrWhiteSpace(request.objectReferencePath)
                        ? null
                        : AssetDatabase.LoadMainAssetAtPath(request.objectReferencePath);
                    break;
                case "vector2":
                    if (request.vector2Value == null)
                    {
                        return Fail("vector2 value payload is required.", lastReloadAtUtc);
                    }

                    property.vector2Value = new Vector2(request.vector2Value.x, request.vector2Value.y);
                    break;
                case "vector3":
                    if (request.vector3Value == null)
                    {
                        return Fail("vector3 value payload is required.", lastReloadAtUtc);
                    }

                    property.vector3Value = ToVector3(request.vector3Value);
                    break;
                case "vector4":
                    if (request.vector4Value == null)
                    {
                        return Fail("vector4 value payload is required.", lastReloadAtUtc);
                    }

                    property.vector4Value = new Vector4(request.vector4Value.x, request.vector4Value.y, request.vector4Value.z, request.vector4Value.w);
                    break;
                case "color":
                    if (request.colorValue == null)
                    {
                        return Fail("color value payload is required.", lastReloadAtUtc);
                    }

                    property.colorValue = new Color(request.colorValue.r, request.colorValue.g, request.colorValue.b, request.colorValue.a);
                    break;
                default:
                    return Fail("Unsupported valueType: " + request.valueType, lastReloadAtUtc);
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);

            var component = target as Component;
            if (component != null)
            {
                MarkSceneDirty(component.gameObject);
            }
            else
            {
                AssetDatabase.SaveAssets();
            }

            return Ok("Updated serialized property: " + propertyPath, lastReloadAtUtc);
        }

        private static void PlaceGameObject(GameObject gameObject, string parentPath, string scenePath, string lastReloadAtUtc, out string placementError)
        {
            placementError = null;

            if (!string.IsNullOrWhiteSpace(parentPath))
            {
                var parent = UnityBridgeStateBuilder.FindGameObjectByHierarchyPath(parentPath);
                if (parent == null)
                {
                    placementError = "Parent hierarchy object was not found: " + parentPath;
                    return;
                }

                Undo.SetTransformParent(gameObject.transform, parent.transform, "UnityBridge Set Parent");
                return;
            }

            if (!string.IsNullOrWhiteSpace(scenePath))
            {
                var scene = SceneManager.GetSceneByPath(scenePath);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    placementError = "Open scene was not found: " + scenePath;
                    return;
                }

                SceneManager.MoveGameObjectToScene(gameObject, scene);
            }
        }

        private static Component FindComponent(GameObject gameObject, string componentType, int componentIndex)
        {
            var components = gameObject.GetComponents<Component>()
                .Where(component => MatchesComponentType(component, componentType))
                .ToList();

            if (componentIndex >= 0)
            {
                return componentIndex < components.Count ? components[componentIndex] : null;
            }

            return components.FirstOrDefault();
        }

        private static bool MatchesComponentType(Component component, string componentType)
        {
            if (component == null)
            {
                return false;
            }

            var fullName = component.GetType().FullName ?? string.Empty;
            var shortName = component.GetType().Name ?? string.Empty;
            return string.Equals(fullName, componentType, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(shortName, componentType, StringComparison.OrdinalIgnoreCase);
        }

        private static Type ResolveComponentType(string componentType)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch
                {
                    continue;
                }

                foreach (var type in types)
                {
                    if (type == null || !typeof(Component).IsAssignableFrom(type))
                    {
                        continue;
                    }

                    var fullName = type.FullName ?? string.Empty;
                    var shortName = type.Name ?? string.Empty;
                    if (string.Equals(fullName, componentType, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(shortName, componentType, StringComparison.OrdinalIgnoreCase))
                    {
                        return type;
                    }
                }
            }

            return null;
        }

        private static Vector3 ToVector3(UnityBridgeVector3Value value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static void BuildMethodArguments(UnityBridgeInvokeComponentMethodRequest request, out object[] arguments, out Type[] argumentTypes)
        {
            BuildMethodArguments(request.argumentType, request.boolValue, request.intValue, request.floatValue, request.stringValue, out arguments, out argumentTypes);
        }

        private static void BuildMethodArguments(string argumentType, bool boolValue, int intValue, float floatValue, string stringValue, out object[] arguments, out Type[] argumentTypes)
        {
            switch (argumentType)
            {
                case null:
                case "":
                case "none":
                    arguments = Array.Empty<object>();
                    argumentTypes = Type.EmptyTypes;
                    return;
                case "bool":
                    arguments = new object[] { boolValue };
                    argumentTypes = new[] { typeof(bool) };
                    return;
                case "int":
                    arguments = new object[] { intValue };
                    argumentTypes = new[] { typeof(int) };
                    return;
                case "float":
                    arguments = new object[] { floatValue };
                    argumentTypes = new[] { typeof(float) };
                    return;
                case "string":
                    arguments = new object[] { stringValue ?? string.Empty };
                    argumentTypes = new[] { typeof(string) };
                    return;
                default:
                    throw new InvalidOperationException("Unsupported method argumentType: " + argumentType);
            }
        }

        private static EventModifiers BuildModifiers(bool shift, bool control, bool alt, bool command)
        {
            var modifiers = EventModifiers.None;
            if (shift)
            {
                modifiers |= EventModifiers.Shift;
            }

            if (control)
            {
                modifiers |= EventModifiers.Control;
            }

            if (alt)
            {
                modifiers |= EventModifiers.Alt;
            }

            if (command)
            {
                modifiers |= EventModifiers.Command;
            }

            return modifiers;
        }

        private static EventType ParseEventType(string eventType, EventType fallback)
        {
            EventType parsedType;
            return Enum.TryParse(eventType, true, out parsedType) ? parsedType : fallback;
        }

        private static string ResolveWindowLabel(EditorWindow editorWindow)
        {
            var title = editorWindow.titleContent != null ? editorWindow.titleContent.text : string.Empty;
            return string.IsNullOrWhiteSpace(title) ? editorWindow.GetType().FullName : title;
        }

        private static UnityBridgeCommandResponse CaptureWindow(UnityBridgeCaptureWindowRequest request, string lastReloadAtUtc)
        {
            if (request == null || (string.IsNullOrWhiteSpace(request.type) && string.IsNullOrWhiteSpace(request.title)))
            {
                return Fail("capture_window requires type and/or title.", lastReloadAtUtc);
            }

            var editorWindow = UnityBridgeStateBuilder.FindEditorWindow(request.type, request.title);
            if (editorWindow == null)
            {
                return Fail("Matching editor window was not found.", lastReloadAtUtc);
            }

            editorWindow.Focus();
            editorWindow.Repaint();
            InternalEditorUtility.RepaintAllViews();

            var rect = editorWindow.position;
            var padding = Math.Max(0, request.padding);
            var width = Math.Max(1, Mathf.RoundToInt(rect.width) - (padding * 2));
            var height = Math.Max(1, Mathf.RoundToInt(rect.height) - (padding * 2));
            var pixels = InternalEditorUtility.ReadScreenPixel(
                new Vector2(rect.x + padding, rect.y + padding),
                width,
                height);

            if (pixels == null || pixels.Length == 0)
            {
                return Fail("Window capture did not return any pixels.", lastReloadAtUtc);
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();

            var outputPath = ResolveCaptureOutputPath(request.outputPath, editorWindow.titleContent != null ? editorWindow.titleContent.text : "window");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Path.Combine(Application.dataPath, ".."));
            File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            return Ok("Captured window to: " + outputPath, lastReloadAtUtc);
        }

        private static string ResolveCaptureOutputPath(string outputPath, string name)
        {
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                return Path.GetFullPath(outputPath);
            }

            var safeName = string.IsNullOrWhiteSpace(name) ? "window" : name.Replace(" ", "-");
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "UnityBridgeCaptures"));
            return Path.Combine(root, safeName + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
        }

        private static Type ResolveType(string typeName)
        {
            var directType = Type.GetType(typeName);
            if (directType != null)
            {
                return directType;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch
                {
                    continue;
                }

                foreach (var type in types)
                {
                    if (type == null)
                    {
                        continue;
                    }

                    var fullName = type.FullName ?? string.Empty;
                    var shortName = type.Name ?? string.Empty;
                    if (string.Equals(fullName, typeName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(shortName, typeName, StringComparison.OrdinalIgnoreCase))
                    {
                        return type;
                    }
                }
            }

            return null;
        }

        private static void MarkSceneDirty(GameObject gameObject)
        {
            if (gameObject != null && gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
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

        private static bool IsSceneAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            return AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;
        }

        private static bool TryResolveNamedBuildTarget(string buildTargetName, string buildTargetGroupName, out BuildTargetGroup buildTargetGroup, out NamedBuildTarget namedBuildTarget, out string error)
        {
            error = null;
            buildTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);

            if (!string.IsNullOrWhiteSpace(buildTargetGroupName))
            {
                if (!Enum.TryParse(buildTargetGroupName, true, out buildTargetGroup))
                {
                    namedBuildTarget = NamedBuildTarget.Unknown;
                    error = "Unknown buildTargetGroup: " + buildTargetGroupName;
                    return false;
                }
            }
            else if (!string.IsNullOrWhiteSpace(buildTargetName))
            {
                BuildTarget buildTarget;
                if (!Enum.TryParse(buildTargetName, true, out buildTarget))
                {
                    namedBuildTarget = NamedBuildTarget.Unknown;
                    error = "Unknown buildTarget: " + buildTargetName;
                    return false;
                }

                buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
            }

            namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            if (namedBuildTarget == NamedBuildTarget.Unknown)
            {
                error = "Could not resolve NamedBuildTarget for group: " + buildTargetGroup;
                return false;
            }

            return true;
        }

        private static void ApplyPhysics3DSettings(SerializedObject serializedObject, UnityBridgePhysics3DSettingsRequest request)
        {
            if (request.setGravity && request.gravity != null)
            {
                serializedObject.FindProperty("m_Gravity").vector3Value = new Vector3(request.gravity.x, request.gravity.y, request.gravity.z);
            }

            if (request.setBounceThreshold)
            {
                serializedObject.FindProperty("m_BounceThreshold").floatValue = request.bounceThreshold;
            }

            if (request.setDefaultMaxDepenetrationVelocity)
            {
                serializedObject.FindProperty("m_DefaultMaxDepenetrationVelocity").floatValue = request.defaultMaxDepenetrationVelocity;
            }

            if (request.setSleepThreshold)
            {
                serializedObject.FindProperty("m_SleepThreshold").floatValue = request.sleepThreshold;
            }

            if (request.setDefaultContactOffset)
            {
                serializedObject.FindProperty("m_DefaultContactOffset").floatValue = request.defaultContactOffset;
            }

            if (request.setDefaultSolverIterations)
            {
                serializedObject.FindProperty("m_DefaultSolverIterations").intValue = request.defaultSolverIterations;
            }

            if (request.setDefaultSolverVelocityIterations)
            {
                serializedObject.FindProperty("m_DefaultSolverVelocityIterations").intValue = request.defaultSolverVelocityIterations;
            }

            if (request.setQueriesHitBackfaces)
            {
                serializedObject.FindProperty("m_QueriesHitBackfaces").boolValue = request.queriesHitBackfaces;
            }

            if (request.setQueriesHitTriggers)
            {
                serializedObject.FindProperty("m_QueriesHitTriggers").boolValue = request.queriesHitTriggers;
            }

            if (request.setEnableAdaptiveForce)
            {
                serializedObject.FindProperty("m_EnableAdaptiveForce").boolValue = request.enableAdaptiveForce;
            }

            if (request.setSimulationMode)
            {
                SimulationMode simulationMode;
                if (Enum.TryParse(request.simulationMode, true, out simulationMode))
                {
                    serializedObject.FindProperty("m_SimulationMode").intValue = (int)simulationMode;
                }
            }

            if (request.setAutoSyncTransforms)
            {
                serializedObject.FindProperty("m_AutoSyncTransforms").boolValue = request.autoSyncTransforms;
            }

            if (request.setReuseCollisionCallbacks)
            {
                serializedObject.FindProperty("m_ReuseCollisionCallbacks").boolValue = request.reuseCollisionCallbacks;
            }

            if (request.setInvokeCollisionCallbacks)
            {
                serializedObject.FindProperty("m_InvokeCollisionCallbacks").boolValue = request.invokeCollisionCallbacks;
            }

            if (request.setContactPairsMode)
            {
                TrySetSerializedEnumValue(serializedObject.FindProperty("m_ContactPairsMode"), request.contactPairsMode);
            }

            if (request.setBroadphaseType)
            {
                TrySetSerializedEnumValue(serializedObject.FindProperty("m_BroadphaseType"), request.broadphaseType);
            }

            if (request.setFrictionType)
            {
                TrySetSerializedEnumValue(serializedObject.FindProperty("m_FrictionType"), request.frictionType);
            }

            if (request.setEnableEnhancedDeterminism)
            {
                serializedObject.FindProperty("m_EnableEnhancedDeterminism").boolValue = request.enableEnhancedDeterminism;
            }

            if (request.setImprovedPatchFriction)
            {
                serializedObject.FindProperty("m_ImprovedPatchFriction").boolValue = request.improvedPatchFriction;
            }

            if (request.setGenerateOnTriggerStayEvents)
            {
                serializedObject.FindProperty("m_GenerateOnTriggerStayEvents").boolValue = request.generateOnTriggerStayEvents;
            }

            if (request.setSolverType)
            {
                TrySetSerializedEnumValue(serializedObject.FindProperty("m_SolverType"), request.solverType);
            }

            if (request.setDefaultMaxAngularSpeed)
            {
                serializedObject.FindProperty("m_DefaultMaxAngularSpeed").floatValue = request.defaultMaxAngularSpeed;
            }

            if (request.setFastMotionThreshold)
            {
                serializedObject.FindProperty("m_FastMotionThreshold").floatValue = request.fastMotionThreshold;
            }

            if (request.setIncrementalStaticBroadphase)
            {
                serializedObject.FindProperty("m_IncrementalStaticBroadphase").boolValue = request.incrementalStaticBroadphase;
            }
        }

        private static void ApplyPhysics2DSettings(SerializedObject serializedObject, UnityBridgePhysics2DSettingsRequest request)
        {
            if (request.setGravity && request.gravity != null)
            {
                serializedObject.FindProperty("m_Gravity").vector2Value = new Vector2(request.gravity.x, request.gravity.y);
            }

            if (request.setVelocityIterations)
            {
                serializedObject.FindProperty("m_VelocityIterations").intValue = request.velocityIterations;
            }

            if (request.setPositionIterations)
            {
                serializedObject.FindProperty("m_PositionIterations").intValue = request.positionIterations;
            }

            if (request.setBounceThreshold)
            {
                serializedObject.FindProperty("m_BounceThreshold").floatValue = request.bounceThreshold;
            }

            if (request.setMaxLinearCorrection)
            {
                serializedObject.FindProperty("m_MaxLinearCorrection").floatValue = request.maxLinearCorrection;
            }

            if (request.setMaxAngularCorrection)
            {
                serializedObject.FindProperty("m_MaxAngularCorrection").floatValue = request.maxAngularCorrection;
            }

            if (request.setMaxTranslationSpeed)
            {
                serializedObject.FindProperty("m_MaxTranslationSpeed").floatValue = request.maxTranslationSpeed;
            }

            if (request.setMaxRotationSpeed)
            {
                serializedObject.FindProperty("m_MaxRotationSpeed").floatValue = request.maxRotationSpeed;
            }

            if (request.setBaumgarteScale)
            {
                serializedObject.FindProperty("m_BaumgarteScale").floatValue = request.baumgarteScale;
            }

            if (request.setBaumgarteTimeOfImpactScale)
            {
                serializedObject.FindProperty("m_BaumgarteTimeOfImpactScale").floatValue = request.baumgarteTimeOfImpactScale;
            }

            if (request.setTimeToSleep)
            {
                serializedObject.FindProperty("m_TimeToSleep").floatValue = request.timeToSleep;
            }

            if (request.setLinearSleepTolerance)
            {
                serializedObject.FindProperty("m_LinearSleepTolerance").floatValue = request.linearSleepTolerance;
            }

            if (request.setAngularSleepTolerance)
            {
                serializedObject.FindProperty("m_AngularSleepTolerance").floatValue = request.angularSleepTolerance;
            }

            if (request.setDefaultContactOffset)
            {
                serializedObject.FindProperty("m_DefaultContactOffset").floatValue = request.defaultContactOffset;
            }

            if (request.setSimulationMode)
            {
                SimulationMode2D simulationMode;
                if (Enum.TryParse(request.simulationMode, true, out simulationMode))
                {
                    serializedObject.FindProperty("m_SimulationMode").intValue = (int)simulationMode;
                }
            }

            if (request.setMaxSubStepCount)
            {
                serializedObject.FindProperty("m_MaxSubStepCount").intValue = request.maxSubStepCount;
            }

            if (request.setMinSubStepFPS)
            {
                serializedObject.FindProperty("m_MinSubStepFPS").intValue = request.minSubStepFPS;
            }

            if (request.setUseSubStepping)
            {
                serializedObject.FindProperty("m_UseSubStepping").boolValue = request.useSubStepping;
            }

            if (request.setUseSubStepContacts)
            {
                serializedObject.FindProperty("m_UseSubStepContacts").boolValue = request.useSubStepContacts;
            }

            if (request.setQueriesHitTriggers)
            {
                serializedObject.FindProperty("m_QueriesHitTriggers").boolValue = request.queriesHitTriggers;
            }

            if (request.setQueriesStartInColliders)
            {
                serializedObject.FindProperty("m_QueriesStartInColliders").boolValue = request.queriesStartInColliders;
            }

            if (request.setCallbacksOnDisable)
            {
                serializedObject.FindProperty("m_CallbacksOnDisable").boolValue = request.callbacksOnDisable;
            }

            if (request.setReuseCollisionCallbacks)
            {
                serializedObject.FindProperty("m_ReuseCollisionCallbacks").boolValue = request.reuseCollisionCallbacks;
            }

            if (request.setAutoSyncTransforms)
            {
                serializedObject.FindProperty("m_AutoSyncTransforms").boolValue = request.autoSyncTransforms;
            }
        }

        private static UnityEngine.Object LoadProjectSettingsAsset(string assetPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(assetPath).FirstOrDefault(asset => asset != null);
        }

        private static void SaveProjectSettingsAsset(UnityEngine.Object asset, SerializedObject serializedObject)
        {
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        private static void TrySetSerializedEnumValue(SerializedProperty property, string value)
        {
            if (property == null || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (property.propertyType == SerializedPropertyType.Enum && property.enumDisplayNames != null)
            {
                for (var index = 0; index < property.enumDisplayNames.Length; index++)
                {
                    if (string.Equals(property.enumDisplayNames[index], value, StringComparison.OrdinalIgnoreCase))
                    {
                        property.enumValueIndex = index;
                        return;
                    }
                }
            }

            int rawValue;
            if (int.TryParse(value, out rawValue))
            {
                property.intValue = rawValue;
            }
        }

        private static void AddUniqueStringArrayValue(SerializedProperty property, string value)
        {
            if (property == null || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            for (var index = 0; index < property.arraySize; index++)
            {
                if (string.Equals(property.GetArrayElementAtIndex(index).stringValue, value, StringComparison.Ordinal))
                {
                    return;
                }
            }

            property.InsertArrayElementAtIndex(property.arraySize);
            property.GetArrayElementAtIndex(property.arraySize - 1).stringValue = value;
        }

        private static void RemoveStringArrayValue(SerializedProperty property, string value)
        {
            if (property == null || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            for (var index = property.arraySize - 1; index >= 0; index--)
            {
                if (string.Equals(property.GetArrayElementAtIndex(index).stringValue, value, StringComparison.Ordinal))
                {
                    property.DeleteArrayElementAtIndex(index);
                }
            }
        }

        private static void AddSortingLayer(SerializedProperty property, string name)
        {
            if (property == null || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            for (var index = 0; index < property.arraySize; index++)
            {
                var element = property.GetArrayElementAtIndex(index);
                if (string.Equals(element.FindPropertyRelative("name").stringValue, name, StringComparison.Ordinal))
                {
                    return;
                }
            }

            property.InsertArrayElementAtIndex(property.arraySize);
            var newElement = property.GetArrayElementAtIndex(property.arraySize - 1);
            newElement.FindPropertyRelative("name").stringValue = name;
            newElement.FindPropertyRelative("uniqueID").intValue = GenerateSortingLayerId(property);
            newElement.FindPropertyRelative("locked").boolValue = false;
        }

        private static void RenameSortingLayer(SerializedProperty property, UnityBridgeSortingLayerRenameRequest request)
        {
            if (property == null || request == null || string.IsNullOrWhiteSpace(request.newName))
            {
                return;
            }

            var element = FindSortingLayer(property, request);
            if (element != null)
            {
                element.FindPropertyRelative("name").stringValue = request.newName;
            }
        }

        private static void RemoveSortingLayer(SerializedProperty property, string name)
        {
            if (property == null || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            for (var index = property.arraySize - 1; index >= 0; index--)
            {
                var element = property.GetArrayElementAtIndex(index);
                if (string.Equals(element.FindPropertyRelative("name").stringValue, name, StringComparison.Ordinal))
                {
                    property.DeleteArrayElementAtIndex(index);
                }
            }
        }

        private static SerializedProperty FindSortingLayer(SerializedProperty property, UnityBridgeSortingLayerRenameRequest request)
        {
            for (var index = 0; index < property.arraySize; index++)
            {
                var element = property.GetArrayElementAtIndex(index);
                var nameProperty = element.FindPropertyRelative("name");
                var uniqueIdProperty = element.FindPropertyRelative("uniqueID");
                if (request.uniqueId >= 0 && uniqueIdProperty.intValue == request.uniqueId)
                {
                    return element;
                }

                if (!string.IsNullOrWhiteSpace(request.name) &&
                    string.Equals(nameProperty.stringValue, request.name, StringComparison.Ordinal))
                {
                    return element;
                }
            }

            return null;
        }

        private static int GenerateSortingLayerId(SerializedProperty property)
        {
            var usedIds = new HashSet<int>();
            for (var index = 0; index < property.arraySize; index++)
            {
                usedIds.Add(property.GetArrayElementAtIndex(index).FindPropertyRelative("uniqueID").intValue);
            }

            var candidate = 1;
            while (usedIds.Contains(candidate))
            {
                candidate++;
            }

            return candidate;
        }

        private static SerializedProperty FindPerPlatformQualityEntry(SerializedProperty property, string platform)
        {
            for (var index = 0; index < property.arraySize; index++)
            {
                var entry = property.GetArrayElementAtIndex(index);
                if (string.Equals(entry.displayName, platform, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        private static void SetIfRequested(SerializedProperty property, bool shouldSet, string value)
        {
            if (property == null || !shouldSet)
            {
                return;
            }

            property.stringValue = value ?? string.Empty;
        }

        private static void SetIfRequested(SerializedProperty property, bool shouldSet, int value)
        {
            if (property == null || !shouldSet)
            {
                return;
            }

            property.intValue = value;
        }

        private static void SetIfRequested(SerializedProperty property, bool shouldSet, float value)
        {
            if (property == null || !shouldSet)
            {
                return;
            }

            property.floatValue = value;
        }

        private static void SetIfRequested(SerializedProperty property, bool shouldSet, bool value)
        {
            if (property == null || !shouldSet)
            {
                return;
            }

            property.boolValue = value;
        }

        private static void ReplaceProjectSettingsFloat(List<string> lines, string prefix, bool shouldSet, float value)
        {
            if (!shouldSet)
            {
                return;
            }

            for (var index = 0; index < lines.Count; index++)
            {
                if (lines[index].StartsWith(prefix, StringComparison.Ordinal))
                {
                    lines[index] = prefix + value.ToString("0.0######", System.Globalization.CultureInfo.InvariantCulture);
                    return;
                }
            }
        }

        private static void ReplaceSerializedRational(List<string> lines, string prefix, bool shouldSet, float value)
        {
            if (!shouldSet)
            {
                return;
            }

            for (var index = 0; index < lines.Count; index++)
            {
                if (!lines[index].StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                long denominator = 1;
                long numerator = 1000000;
                for (var childIndex = index + 1; childIndex < Math.Min(index + 5, lines.Count); childIndex++)
                {
                    if (lines[childIndex].StartsWith("      m_Denominator: ", StringComparison.Ordinal))
                    {
                        long.TryParse(lines[childIndex].Substring("      m_Denominator: ".Length), out denominator);
                    }
                    else if (lines[childIndex].StartsWith("      m_Numerator: ", StringComparison.Ordinal))
                    {
                        long.TryParse(lines[childIndex].Substring("      m_Numerator: ".Length), out numerator);
                    }
                }

                var count = (long)Math.Round(value * numerator / Math.Max(1d, denominator));
                for (var childIndex = index + 1; childIndex < Math.Min(index + 5, lines.Count); childIndex++)
                {
                    if (lines[childIndex].StartsWith("    m_Count: ", StringComparison.Ordinal))
                    {
                        lines[childIndex] = "    m_Count: " + count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        return;
                    }
                }

                return;
            }
        }

        private static string GetProjectSettingsPath(string fileName)
        {
            return Path.Combine(Application.dataPath, "..", "ProjectSettings", fileName);
        }

        private static UnityBridgeTextureImporterSummary ErrorTextureImporterSummary(string assetPath, string message)
        {
            return new UnityBridgeTextureImporterSummary
            {
                assetPath = assetPath,
                found = false,
                message = message
            };
        }
    }
}
