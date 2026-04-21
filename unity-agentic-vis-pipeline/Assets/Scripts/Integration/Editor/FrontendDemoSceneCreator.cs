#if UNITY_EDITOR
using System.IO;
using ImmersiveTaxiVis.Integration.Controllers;
using ImmersiveTaxiVis.Integration.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ImmersiveTaxiVis.Integration.Editor
{
    public static class FrontendDemoSceneCreator
    {
        private const string SceneDirectory = "Assets/Scenes/Frontend";
        private const string ScenePath = SceneDirectory + "/BackendFrontendDemo.unity";
        private const string AgenticSceneDirectory = "Assets/Scenes/Agentic";
        private const string AgenticScenePath = AgenticSceneDirectory + "/DesktopAgenticApp.unity";

        [MenuItem("Tools/ImmersiveTaxiVis/Create Backend Frontend Demo Scene")]
        public static void CreateBackendFrontendDemoScene()
        {
            EnsureSceneDirectory();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "BackendFrontendDemo";

            var bootstrapObject = new GameObject("FrontendDemoBootstrap");
            var bootstrap = bootstrapObject.AddComponent<FrontendDemoSceneBootstrap>();
            bootstrap.createCameraIfMissing = true;
            bootstrap.createLightIfMissing = true;
            bootstrap.createJsonControllerIfMissing = true;
            bootstrap.jsonRelativePath = "result_multiview.json";
            bootstrap.pointSize = 0.15f;
            bootstrap.renderLinks = true;
            bootstrap.cameraPosition = new Vector3(0.5f, 0.65f, -4.5f);
            bootstrap.cameraLookTarget = new Vector3(0.5f, 0.5f, 0.5f);
            bootstrap.renderRootLocalPosition = new Vector3(0f, 0f, 0f);
            bootstrap.renderRootLocalScale = new Vector3(4f, 4f, 4f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log("Created backend frontend demo scene at " + ScenePath);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        }

        [MenuItem("Tools/ImmersiveTaxiVis/Open Backend Frontend Demo Scene")]
        public static void OpenBackendFrontendDemoScene()
        {
            if (!File.Exists(ScenePath))
            {
                CreateBackendFrontendDemoScene();
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Tools/ImmersiveTaxiVis/Create Desktop Agentic App Scene")]
        public static void CreateDesktopAgenticAppScene()
        {
            EnsureSceneDirectory();
            EnsureAgenticSceneDirectory();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "DesktopAgenticApp";

            var bootstrapObject = new GameObject("DesktopAgenticAppBootstrap");
            var bootstrap = bootstrapObject.AddComponent<DesktopAgenticAppBootstrap>();
            bootstrap.createCameraIfMissing = true;
            bootstrap.createLightIfMissing = true;
            bootstrap.createBackendControllerIfMissing = true;
            bootstrap.createCommandWindowIfMissing = true;
            bootstrap.backendBaseUrl = "http://127.0.0.1:8000";
            bootstrap.autoStartBackendOnAwake = true;
            bootstrap.cameraPosition = new Vector3(0.5f, 0.65f, -4.5f);
            bootstrap.cameraLookTarget = new Vector3(0.5f, 0.5f, 0.5f);
            bootstrap.pointSize = 0.15f;
            bootstrap.renderLinks = true;
            bootstrap.renderRootLocalPosition = new Vector3(0f, 0f, 0f);
            bootstrap.renderRootLocalScale = new Vector3(4f, 4f, 4f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, AgenticScenePath);
            AssetDatabase.Refresh();

            Debug.Log("Created desktop agentic app scene at " + AgenticScenePath);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(AgenticScenePath);
        }

        [MenuItem("Tools/ImmersiveTaxiVis/Open Desktop Agentic App Scene")]
        public static void OpenDesktopAgenticAppScene()
        {
            if (!File.Exists(AgenticScenePath))
            {
                CreateDesktopAgenticAppScene();
                return;
            }

            EditorSceneManager.OpenScene(AgenticScenePath, OpenSceneMode.Single);
        }

        private static void EnsureSceneDirectory()
        {
            if (AssetDatabase.IsValidFolder("Assets/Scenes") == false)
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            if (AssetDatabase.IsValidFolder(SceneDirectory) == false)
            {
                AssetDatabase.CreateFolder("Assets/Scenes", "Frontend");
            }
        }

        private static void EnsureAgenticSceneDirectory()
        {
            if (AssetDatabase.IsValidFolder("Assets/Scenes") == false)
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            if (AssetDatabase.IsValidFolder(AgenticSceneDirectory) == false)
            {
                AssetDatabase.CreateFolder("Assets/Scenes", "Agentic");
            }
        }
    }
}
#endif
