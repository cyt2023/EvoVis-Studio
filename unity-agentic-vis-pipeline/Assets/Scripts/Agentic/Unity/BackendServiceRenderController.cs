using System.Collections;
using ImmersiveTaxiVis.Integration.Models;
using UnityEngine;

namespace ImmersiveTaxiVis.Integration.Runtime
{
    public class BackendServiceRenderController : MonoBehaviour
    {
        [Header("Backend Service")]
        public string backendBaseUrl = "http://127.0.0.1:8000";
        public string workflowId = "test3";
        public int requestTimeoutSeconds = 30;
        public bool checkHealthBeforeRender = true;
        public bool renderOnStart = true;
        public bool useRunEndpoint = false;
        public bool autoStartLocalBackend = true;
        public DesktopBackendServiceController backendServiceController;

        [Header("Task Request")]
        [TextArea(2, 4)]
        public string taskText = "Find concentrated morning pickup hotspots in the Hurricane Sandy sample.";
        public string dataset = "hurricane_sandy_2012_100k_sample.csv";
        public string requestedViewType = "Point";

        [Header("Rendering")]
        public Transform renderRoot;
        public float pointSize = 0.1f;
        public bool renderLinks = true;
        public Vector3 renderRootLocalPosition = Vector3.zero;
        public Vector3 renderRootLocalScale = Vector3.one;

        [Header("Debug")]
        public bool verboseLogging = true;

        private BackendWorkflowClient client;
        private WorkflowRuntimeRenderCoordinator renderCoordinator;
        private Transform configuredRenderRoot;
        private float configuredPointSize = -1f;
        private bool configuredRenderLinks;

        private void Awake()
        {
            client = new BackendWorkflowClient(backendBaseUrl, requestTimeoutSeconds);
            if (backendServiceController == null)
                backendServiceController = FindObjectOfType<DesktopBackendServiceController>();
        }

        private void Start()
        {
            if (renderOnStart)
                StartCoroutine(RequestAndRender());
        }

        [ContextMenu("Request Backend And Render")]
        public void RequestAndRenderFromContextMenu()
        {
            StartCoroutine(RequestAndRender());
        }

        [ContextMenu("Clear Backend Render")]
        public void ClearRender()
        {
            EnsureRenderRoot();
            ConfigureRenderRootTransform();
            RebuildCoordinator();
            renderCoordinator.ClearRenderedViews();
        }

        private IEnumerator RequestAndRender()
        {
            EnsureRenderRoot();
            ConfigureRenderRootTransform();
            RebuildCoordinator();
            client = new BackendWorkflowClient(backendBaseUrl, requestTimeoutSeconds);

            if (backendServiceController != null && autoStartLocalBackend)
            {
                yield return backendServiceController.EnsureBackendReady();
                if (!backendServiceController.IsHealthy)
                {
                    Debug.LogError("Desktop backend is not ready: " + backendServiceController.StatusMessage);
                    yield break;
                }
            }

            if (checkHealthBeforeRender)
            {
                var healthOk = false;
                var healthMessage = string.Empty;
                yield return client.CheckHealth((ok, message) =>
                {
                    healthOk = ok;
                    healthMessage = message;
                });

                if (!healthOk)
                {
                    Debug.LogError("EvoFlow backend health check failed: " + healthMessage);
                    yield break;
                }

                if (verboseLogging)
                    Debug.Log("EvoFlow backend health check passed: " + healthMessage);
            }

            var fetchOk = false;
            var responseJson = string.Empty;

            if (useRunEndpoint)
            {
                var runRequest = new BackendWorkflowRunRequest
                {
                    task = taskText,
                    dataset = dataset,
                    workflowId = workflowId,
                    viewType = requestedViewType
                };

                yield return client.RunWorkflowForUnityRender(runRequest, (ok, message) =>
                {
                    fetchOk = ok;
                    responseJson = message;
                });
            }
            else
            {
                yield return client.FetchUnityRenderJson(workflowId, (ok, message) =>
                {
                    fetchOk = ok;
                    responseJson = message;
                });
            }

            if (!fetchOk)
            {
                Debug.LogError("Failed to fetch Unity render JSON from EvoFlow backend: " + responseJson);
                yield break;
            }

            RenderBackendJson(responseJson);
        }

        private void RenderBackendJson(string responseJson)
        {
            if (!BackendWorkflowClient.TryParseRenderResult(responseJson, out var backendResult, out var errorMessage))
            {
                Debug.LogError("Failed to render EvoFlow backend response: " + errorMessage);
                return;
            }

            var renderResult = renderCoordinator.Render(backendResult);
            if (verboseLogging)
            {
                Debug.Log(string.Format(
                    "Rendered workflow '{0}' from backend service. Views: {1}. Selected points: {2}. Backend built: {3}.",
                    workflowId,
                    renderResult.RenderedViewCount,
                    backendResult.resultSummary != null ? backendResult.resultSummary.selectedPointCount : 0,
                    backendResult.resultSummary != null && backendResult.resultSummary.backendBuilt));
            }
        }

        private void EnsureRenderRoot()
        {
            if (renderRoot != null)
                return;

            var rootObject = new GameObject("BackendServiceRenderRoot");
            rootObject.transform.SetParent(transform, false);
            renderRoot = rootObject.transform;
        }

        private void ConfigureRenderRootTransform()
        {
            if (renderRoot == null)
                return;

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

            renderCoordinator = new WorkflowRuntimeRenderCoordinator(renderRoot, pointSize, renderLinks);
            configuredRenderRoot = renderRoot;
            configuredPointSize = pointSize;
            configuredRenderLinks = renderLinks;
        }
    }
}
