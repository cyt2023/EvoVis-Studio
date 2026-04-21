using UnityEngine;

namespace ImmersiveTaxiVis.Integration.Runtime
{
    public class DesktopAgenticAppBootstrap : MonoBehaviour
    {
        [Header("Auto Setup")]
        public bool createCameraIfMissing = true;
        public bool createLightIfMissing = true;
        public bool createBackendControllerIfMissing = true;
        public bool createCommandWindowIfMissing = true;

        [Header("Camera")]
        public Vector3 cameraPosition = new Vector3(0.5f, 0.5f, -3f);
        public Vector3 cameraLookTarget = new Vector3(0.5f, 0.5f, 0.5f);

        [Header("Backend")]
        public string backendBaseUrl = "http://127.0.0.1:8000";
        public bool autoStartBackendOnAwake = true;

        [Header("Rendering")]
        public float pointSize = 0.1f;
        public bool renderLinks = true;
        public Vector3 renderRootLocalPosition = Vector3.zero;
        public Vector3 renderRootLocalScale = Vector3.one;

        private void Awake()
        {
            if (createCameraIfMissing)
                EnsureCamera();

            if (createLightIfMissing)
                EnsureLight();

            var backendController = createBackendControllerIfMissing ? EnsureBackendController() : FindObjectOfType<DesktopBackendServiceController>();
            if (createCommandWindowIfMissing)
                EnsureCommandWindow(backendController);
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
            controller.pointSize = pointSize;
            controller.renderLinks = renderLinks;
            controller.renderRootLocalPosition = renderRootLocalPosition;
            controller.renderRootLocalScale = renderRootLocalScale;
        }
    }
}
