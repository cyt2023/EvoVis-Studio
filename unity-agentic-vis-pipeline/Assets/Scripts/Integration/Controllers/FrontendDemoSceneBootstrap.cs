using UnityEngine;

namespace ImmersiveTaxiVis.Integration.Controllers
{
    public class FrontendDemoSceneBootstrap : MonoBehaviour
    {
        [Header("Auto Setup")]
        public bool createCameraIfMissing = true;
        public bool createLightIfMissing = true;
        public bool createJsonControllerIfMissing = true;

        [Header("Default Camera")]
        public Vector3 cameraPosition = new Vector3(0.5f, 0.5f, -3f);
        public Vector3 cameraLookTarget = new Vector3(0.5f, 0.5f, 0.5f);

        [Header("Default Controller")]
        public string jsonRelativePath = "result_multiview.json";
        public float pointSize = 0.15f;
        public bool renderLinks = true;
        public Vector3 renderRootLocalPosition = new Vector3(0f, 1.2f, 0f);
        public Vector3 renderRootLocalScale = new Vector3(4f, 4f, 4f);

        private void Awake()
        {
            if (createCameraIfMissing)
            {
                EnsureCamera();
            }

            if (createLightIfMissing)
            {
                EnsureLight();
            }

            if (createJsonControllerIfMissing)
            {
                EnsureJsonController();
            }
        }

        private void EnsureCamera()
        {
            if (Camera.main != null || FindObjectOfType<Camera>() != null)
            {
                return;
            }

            var cameraObject = new GameObject("FrontendDemoCamera");
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
            {
                return;
            }

            var lightObject = new GameObject("FrontendDemoDirectionalLight");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var lightComponent = lightObject.AddComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightComponent.intensity = 1.2f;
        }

        private void EnsureJsonController()
        {
            if (GetComponentInChildren<JsonResultDemoController>() != null)
            {
                return;
            }

            var controllerObject = new GameObject("JsonFrontendController");
            controllerObject.transform.SetParent(transform, false);

            var controller = controllerObject.AddComponent<JsonResultDemoController>();
            controller.relativeJsonPath = jsonRelativePath;
            controller.pointSize = pointSize;
            controller.renderLinks = renderLinks;
            controller.loadOnStart = true;
            controller.renderRootLocalPosition = renderRootLocalPosition;
            controller.renderRootLocalScale = renderRootLocalScale;
        }
    }
}
