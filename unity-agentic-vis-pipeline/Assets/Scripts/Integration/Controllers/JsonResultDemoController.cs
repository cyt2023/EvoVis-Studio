using System;
using ImmersiveTaxiVis.Integration.Rendering;
using UnityEngine;

namespace ImmersiveTaxiVis.Integration.Controllers
{
    public class JsonResultDemoController : MonoBehaviour
    {
        [Header("Data Source")]
        public string relativeJsonPath = "result_multiview.json";
        public bool loadOnStart = true;

        [Header("Rendering")]
        public Transform renderRoot;
        public float pointSize = 0.1f;
        public bool renderLinks = true;
        public Vector3 renderRootLocalPosition = Vector3.zero;
        public Vector3 renderRootLocalScale = Vector3.one;
        
        private FrontendRenderCoordinator renderCoordinator;
        private Transform configuredRenderRoot;
        private float configuredPointSize = -1f;
        private bool configuredRenderLinks;

        private void Start()
        {
            if (loadOnStart)
            {
                LoadAndRender();
            }
        }

        [ContextMenu("Load And Render")]
        public void LoadAndRender()
        {
            try
            {
                EnsureRenderRoot();
                ConfigureRenderRootTransform();
                RebuildCoordinator();

                var renderResult = renderCoordinator.LoadMapAndRender(relativeJsonPath);
                if (renderResult.RenderedViewCount == 0)
                {
                    Debug.LogWarning("No supported visualization views were found in the backend result JSON.");
                    return;
                }

                Debug.Log(string.Format(
                    "Loaded '{0}' from StreamingAssets and {1} {2}.",
                    relativeJsonPath,
                    renderResult.AppliedStateOnlyUpdate ? "updated" : "rendered",
                    renderResult.RenderedViewCount == 1 ? "view" : "views"));
            }
            catch (Exception ex)
            {
                EnsureRenderRoot();
                ConfigureRenderRootTransform();
                RebuildCoordinator();
                Debug.LogError(renderCoordinator.BuildLoadErrorMessage(relativeJsonPath, ex));
            }
        }

        [ContextMenu("Clear Render")]
        public void ClearRender()
        {
            EnsureRenderRoot();
            ConfigureRenderRootTransform();
            RebuildCoordinator();
            renderCoordinator.ClearRenderedViews();
        }

        private void EnsureRenderRoot()
        {
            if (renderRoot != null)
            {
                return;
            }

            var rootObject = new GameObject("JsonRenderRoot");
            rootObject.transform.SetParent(transform, false);
            renderRoot = rootObject.transform;
        }

        private void ConfigureRenderRootTransform()
        {
            if (renderRoot == null)
            {
                return;
            }

            renderRoot.localPosition = renderRootLocalPosition;
            renderRoot.localRotation = Quaternion.identity;
            renderRoot.localScale = renderRootLocalScale;
        }

        private void RebuildCoordinator()
        {
            if (renderCoordinator != null &&
                configuredRenderRoot == renderRoot &&
                Mathf.Approximately(configuredPointSize, pointSize) &&
                configuredRenderLinks == renderLinks)
            {
                return;
            }

            renderCoordinator = new FrontendRenderCoordinator(renderRoot, pointSize, renderLinks);
            configuredRenderRoot = renderRoot;
            configuredPointSize = pointSize;
            configuredRenderLinks = renderLinks;
        }
    }
}
