using System.Collections;
using System.IO;
using ImmersiveTaxiVis.Integration.Models;
using UnityEngine;

namespace ImmersiveTaxiVis.Integration.Runtime
{
    public class BackendServiceRenderController : MonoBehaviour
    {
        [Header("Backend Service")]
        public string backendBaseUrl = "http://127.0.0.1:8000";
        public string workflowId = "test3";
        public int requestTimeoutSeconds = 180;
        public bool checkHealthBeforeRender = true;
        public bool renderOnStart = true;
        public bool useRunEndpoint = false;
        public bool autoStartLocalBackend = true;
        public DesktopBackendServiceController backendServiceController;

        [Header("Task Request")]
        [TextArea(2, 4)]
        public string taskText = "Show all NYC taxi pickup points as a dense 3D point cloud. Do not filter rows. Use pickup location on the ground plane and trip distance or fare as visual height or color.";
        public string dataset = "first_week_of_may_2011_10k_sample.csv";
        public string requestedViewType = "Point";
        public bool requireRealLlm = true;
        public int population = 1;
        public int generations = 0;
        public int eliteSize = 1;

        [Header("Rendering")]
        public Transform renderRoot;
        public float pointSize = 0.045f;
        public bool renderLinks = true;
        public Vector3 renderRootLocalPosition = Vector3.zero;
        public Vector3 renderRootLocalScale = Vector3.one;
        public int renderPointLimit = 1000;
        public bool includeSelectedIds = false;

        [Header("Debug")]
        public bool verboseLogging = true;
        public bool saveLastResponseJson = true;
        public string lastResponseFileName = "evoflow-last-render.json";

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

        public void RequestAndRenderNow()
        {
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

        public IEnumerator RequestAndRender()
        {
            if (verboseLogging)
                Debug.Log("BackendServiceRenderController: starting EvoFlow request/render coroutine.");

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
                if (verboseLogging)
                    Debug.Log("Checking EvoFlow backend health at " + backendBaseUrl + "/api/health");

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
                if (verboseLogging)
                    Debug.Log("Requesting EvoFlow dynamic render JSON from " + backendBaseUrl + "/api/render/run");

                var runRequest = new BackendWorkflowRunRequest
                {
                    task = taskText,
                    dataset = dataset,
                    workflowId = workflowId,
                    viewType = requestedViewType,
                    execute = true,
                    requireLlm = requireRealLlm,
                    population = population,
                    generations = generations,
                    eliteSize = eliteSize,
                    timeoutSeconds = requestTimeoutSeconds,
                    limit = renderPointLimit,
                    includeSelectedIds = includeSelectedIds
                };

                yield return client.RunWorkflowForUnityRender(runRequest, (ok, message) =>
                {
                    fetchOk = ok;
                    responseJson = message;
                });
            }
            else
            {
                if (verboseLogging)
                    Debug.Log("Requesting cached EvoFlow render JSON: " + backendBaseUrl + "/api/render/" + workflowId + "?limit=" + renderPointLimit);

                yield return client.FetchUnityRenderJson(workflowId, (ok, message) =>
                {
                    fetchOk = ok;
                    responseJson = message;
                }, renderPointLimit, includeSelectedIds);
            }

            if (!fetchOk)
            {
                Debug.LogError("Failed to fetch Unity render JSON from EvoFlow backend: " + responseJson);
                yield break;
            }

            PersistLastResponseJson(responseJson);
            RenderBackendJson(responseJson);
        }

        private void PersistLastResponseJson(string responseJson)
        {
            if (verboseLogging)
                Debug.Log("Fetched EvoFlow render JSON from " + backendBaseUrl + " for workflow '" + workflowId + "'. Bytes: " + responseJson.Length);

            if (!saveLastResponseJson || string.IsNullOrWhiteSpace(responseJson))
                return;

            try
            {
                var fileName = string.IsNullOrWhiteSpace(lastResponseFileName) ? "evoflow-last-render.json" : lastResponseFileName;
                var outputPath = Path.Combine(Application.persistentDataPath, fileName);
                File.WriteAllText(outputPath, responseJson);
                Debug.Log("Saved latest EvoFlow render JSON to: " + outputPath);
            }
            catch (IOException ex)
            {
                Debug.LogWarning("Failed to save latest EvoFlow render JSON: " + ex.Message);
            }
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
