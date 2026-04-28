using UnityEngine;

namespace ImmersiveTaxiVis.Integration.Runtime
{
    public class DesktopAgenticAppBootstrap : MonoBehaviour
    {
        [Header("Auto Setup")]
        public bool createCameraIfMissing = true;
        public bool createLightIfMissing = true;
        public bool createBackendControllerIfMissing = true;
        public bool createAutoRenderControllerIfMissing = false;
        public bool createCommandWindowIfMissing = true;

        [Header("Camera")]
        public Vector3 cameraPosition = new Vector3(0.85f, 0.72f, -1.15f);
        public Vector3 cameraLookTarget = new Vector3(0f, 0.08f, 0f);

        [Header("Backend")]
        public string backendBaseUrl = "http://127.0.0.1:8000";
        public bool autoStartBackendOnAwake = true;

        [Header("Startup Render")]
        public bool renderCachedWorkflowOnStart = false;
        public string startupWorkflowId = "test3";
        public string startupTaskText = "Show all NYC taxi pickup points as a dense 3D point cloud. Do not filter rows. Use pickup location on the ground plane and trip distance or fare as visual height or color.";
        public string startupDataset = "first_week_of_may_2011_10k_sample.csv";
        public string startupViewType = "Auto";

        [Header("Rendering")]
        public float pointSize = 0.045f;
        public bool renderLinks = true;
        public Vector3 renderRootLocalPosition = Vector3.zero;
        public Vector3 renderRootLocalScale = new Vector3(1.85f, 1.85f, 1.85f);
        public int startupRenderPointLimit = 1000;

        private void Awake()
        {
            if (createCameraIfMissing)
                EnsureCamera();
            ApplyCameraPose();

            if (createLightIfMissing)
                EnsureLight();

            var backendController = createBackendControllerIfMissing ? EnsureBackendController() : FindObjectOfType<DesktopBackendServiceController>();
            BackendServiceRenderController autoRenderer = null;
            if (createAutoRenderControllerIfMissing)
                autoRenderer = EnsureAutoRenderController(backendController);

            if (createCommandWindowIfMissing)
                EnsureCommandWindow(backendController);

            if (renderCachedWorkflowOnStart && autoRenderer != null)
                StartCoroutine(StartBackendThenRender(backendController, autoRenderer));
        }

        private void EnsureCamera()
        {
            if (Camera.main != null || FindObjectOfType<Camera>() != null)
                return;

            var cameraObject = new GameObject("DesktopAgenticCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(
                cameraPosition,
                Quaternion.LookRotation((cameraLookTarget - cameraPosition).normalized, Vector3.up));

            var cameraComponent = cameraObject.AddComponent<Camera>();
            cameraComponent.clearFlags = CameraClearFlags.SolidColor;
            cameraComponent.backgroundColor = new Color(0.08f, 0.1f, 0.14f, 1f);
        }

        private void ApplyCameraPose()
        {
            var cameraComponent = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
            if (cameraComponent == null)
                return;

            cameraComponent.transform.SetPositionAndRotation(
                cameraPosition,
                Quaternion.LookRotation((cameraLookTarget - cameraPosition).normalized, Vector3.up));
            cameraComponent.fieldOfView = 42f;
            cameraComponent.nearClipPlane = 0.01f;
        }

        private void EnsureLight()
        {
            if (FindObjectOfType<Light>() != null)
                return;

            var lightObject = new GameObject("DesktopAgenticDirectionalLight");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var lightComponent = lightObject.AddComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightComponent.intensity = 1.2f;
        }

        private DesktopBackendServiceController EnsureBackendController()
        {
            var existing = FindObjectOfType<DesktopBackendServiceController>();
            if (existing != null)
            {
                existing.backendBaseUrl = backendBaseUrl;
                existing.autoStartOnAwake = autoStartBackendOnAwake;
                return existing;
            }

            var controllerObject = new GameObject("DesktopBackendServiceController");
            controllerObject.transform.SetParent(transform, false);

            var controller = controllerObject.AddComponent<DesktopBackendServiceController>();
            controller.backendBaseUrl = backendBaseUrl;
            controller.autoStartOnAwake = autoStartBackendOnAwake;
            return controller;
        }

        private void EnsureCommandWindow(DesktopBackendServiceController backendController)
        {
            var existing = FindObjectOfType<BackendCommandWindowController>();
            if (existing != null)
            {
                ApplyCommandWindowDefaults(existing, backendController);
                return;
            }

            var controllerObject = new GameObject("DesktopAgenticCommandWindow");
            controllerObject.transform.SetParent(transform, false);
            var controller = controllerObject.AddComponent<BackendCommandWindowController>();
            ApplyCommandWindowDefaults(controller, backendController);
        }

        private void ApplyCommandWindowDefaults(BackendCommandWindowController controller, DesktopBackendServiceController backendController)
        {
            controller.backendBaseUrl = backendBaseUrl;
            controller.backendServiceController = backendController;
            controller.autoStartLocalBackend = autoStartBackendOnAwake;
            controller.executeEvoFlow = true;
            controller.requireRealLlm = true;
            controller.requestedViewType = startupViewType;
            controller.pointSize = pointSize;
            controller.renderLinks = renderLinks;
            controller.showWindow = true;
            controller.loadDatasetsOnStart = true;
            controller.windowRect = new Rect(20, 20, 760, 560);
            controller.renderRootLocalPosition = renderRootLocalPosition;
            controller.renderRootLocalScale = renderRootLocalScale;
            controller.renderPointLimit = startupRenderPointLimit;
        }

        private System.Collections.IEnumerator StartBackendThenRender(
            DesktopBackendServiceController backendController,
            BackendServiceRenderController autoRenderer)
        {
            if (backendController != null)
            {
                Debug.Log("Desktop app startup: preparing EvoFlow backend...");
                yield return backendController.EnsureBackendReady();
                Debug.Log("Desktop app startup backend status: " + backendController.StatusMessage);

                if (!backendController.IsHealthy)
                    yield break;
            }

            Debug.Log("Desktop app startup: requesting EvoFlow JSON and rendering workflow '" + startupWorkflowId + "'.");
            yield return autoRenderer.RequestAndRender();
        }

        private BackendServiceRenderController EnsureAutoRenderController(DesktopBackendServiceController backendController)
        {
            var existing = GetComponentInChildren<BackendServiceRenderController>();
            if (existing != null)
            {
                ApplyAutoRenderDefaults(existing, backendController);
                return existing;
            }

            var controllerObject = new GameObject("DesktopAgenticAutoRenderer");
            controllerObject.transform.SetParent(transform, false);
            var controller = controllerObject.AddComponent<BackendServiceRenderController>();
            ApplyAutoRenderDefaults(controller, backendController);
            return controller;
        }

        private void ApplyAutoRenderDefaults(BackendServiceRenderController controller, DesktopBackendServiceController backendController)
        {
            controller.backendBaseUrl = backendBaseUrl;
            controller.workflowId = startupWorkflowId;
            controller.taskText = startupTaskText;
            controller.dataset = startupDataset;
            controller.requestedViewType = startupViewType;
            controller.backendServiceController = backendController;
            controller.autoStartLocalBackend = false;
            controller.checkHealthBeforeRender = true;
            controller.renderOnStart = false;
            controller.useRunEndpoint = false;
            controller.pointSize = pointSize;
            controller.renderLinks = renderLinks;
            controller.renderRootLocalPosition = renderRootLocalPosition;
            controller.renderRootLocalScale = renderRootLocalScale;
            controller.renderPointLimit = startupRenderPointLimit;
            controller.includeSelectedIds = false;
            controller.requireRealLlm = true;
            controller.population = 1;
            controller.generations = 0;
            controller.eliteSize = 1;
        }
    }
}
