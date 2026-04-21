using System;
using System.Collections;
using ImmersiveTaxiVis.Integration.Models;
using UnityEngine;

namespace ImmersiveTaxiVis.Integration.Runtime
{
    public class BackendCommandWindowController : MonoBehaviour
    {
        [Header("Backend Service")]
        public string backendBaseUrl = "http://127.0.0.1:8000";
        public int requestTimeoutSeconds = 180;
        public bool checkHealthBeforeRender = true;
        public bool autoStartLocalBackend = true;
        public DesktopBackendServiceController backendServiceController;

        [Header("Command")]
        [TextArea(3, 6)]
        public string commandText = "Find concentrated morning pickup hotspots in the Hurricane Sandy sample and render them as a backend-ready point visualization.";
        public string dataset = "hurricane_sandy_2012_100k_sample.csv";
        public string workflowId = "test3";
        public string requestedViewType = "Point";
        public bool executeEvoFlow = false;
        public int population = 6;
        public int generations = 3;
        public int eliteSize = 2;

        [Header("Rendering")]
        public Transform renderRoot;
        public float pointSize = 0.1f;
        public bool renderLinks = true;
        public Vector3 renderRootLocalPosition = Vector3.zero;
        public Vector3 renderRootLocalScale = Vector3.one;

        [Header("Window")]
        public bool showWindow = true;
        public Rect windowRect = new Rect(20, 20, 620, 420);

        private BackendWorkflowClient client;
        private WorkflowRuntimeRenderCoordinator renderCoordinator;
        private Transform configuredRenderRoot;
        private float configuredPointSize = -1f;
        private bool configuredRenderLinks;
        private string statusMessage = "Ready.";
        private bool isRunning;
        private Vector2 scroll;
        private BackendDatasetInfo[] datasets = new BackendDatasetInfo[0];
        private string datasetStatus = "Datasets not loaded.";

        private void Awake()
        {
            client = new BackendWorkflowClient(backendBaseUrl, requestTimeoutSeconds);
            if (backendServiceController == null)
                backendServiceController = FindObjectOfType<DesktopBackendServiceController>();
            EnsureRenderRoot();
            ConfigureRenderRootTransform();
            RebuildCoordinator();
        }

        private void OnGUI()
        {
            if (!showWindow)
                return;

            windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "EvoFlow Command");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("Task command / 任务命令");
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(90));
            commandText = GUILayout.TextArea(commandText, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();

            if (backendServiceController != null)
            {
                GUILayout.Label("Backend: " + backendServiceController.StatusMessage);
                GUILayout.Label("Endpoint: " + backendServiceController.EndpointSummary);
                GUILayout.Label("Resolved Root: " +
                                (string.IsNullOrWhiteSpace(backendServiceController.ResolvedBackendRoot)
                                    ? "(not resolved yet)"
                                    : backendServiceController.ResolvedBackendRoot));
                GUILayout.BeginHorizontal();
                GUI.enabled = !isRunning && !backendServiceController.IsStarting;
                if (GUILayout.Button("Start Backend", GUILayout.Height(24)))
                    StartCoroutine(StartBackend());
                if (GUILayout.Button("Restart Backend", GUILayout.Height(24)))
                    StartCoroutine(RestartBackend());
                if (GUILayout.Button("Stop Backend", GUILayout.Height(24)))
                    backendServiceController.StopOwnedBackendProcess();
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                GUILayout.Label("Layout Hint: " + backendServiceController.ResolutionHint);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Dataset", GUILayout.Width(70));
            dataset = GUILayout.TextField(dataset);
            GUI.enabled = !isRunning;
            if (GUILayout.Button("Load", GUILayout.Width(60)))
                StartCoroutine(LoadDatasets());
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (datasets != null && datasets.Length > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Available", GUILayout.Width(70));
                for (var i = 0; i < Mathf.Min(3, datasets.Length); i++)
                {
                    var item = datasets[i];
                    var label = string.IsNullOrWhiteSpace(item.id) ? "dataset" : item.id;
                    if (GUILayout.Button(label, GUILayout.Height(24)))
                        dataset = string.IsNullOrWhiteSpace(item.id) ? item.relativePath : item.id;
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Workflow", GUILayout.Width(70));
            workflowId = GUILayout.TextField(workflowId, GUILayout.Width(120));
            GUILayout.Label("View", GUILayout.Width(40));
            requestedViewType = GUILayout.TextField(requestedViewType, GUILayout.Width(80));
            executeEvoFlow = GUILayout.Toggle(executeEvoFlow, "Execute EvoFlow");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Preset: Cached test3", GUILayout.Height(24)))
                ApplyCachedPreset();
            if (GUILayout.Button("Preset: Dynamic Point", GUILayout.Height(24)))
                ApplyDynamicPointPreset();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Population", GUILayout.Width(70));
            population = ParseIntField(population, GUILayout.Width(50));
            GUILayout.Label("Generations", GUILayout.Width(80));
            generations = ParseIntField(generations, GUILayout.Width(50));
            GUILayout.Label("Elite", GUILayout.Width(35));
            eliteSize = ParseIntField(eliteSize, GUILayout.Width(50));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = !isRunning;
            if (GUILayout.Button("Run And Render", GUILayout.Height(32)))
                StartCoroutine(RunCommandAndRender());
            if (GUILayout.Button("Clear", GUILayout.Width(90), GUILayout.Height(32)))
                ClearRender();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label("Status: " + statusMessage);
            GUILayout.Label("Dataset status: " + datasetStatus);
            GUI.DragWindow();
        }

        [ContextMenu("Load Backend Datasets")]
        public void LoadDatasetsFromMenu()
        {
            if (!isRunning)
                StartCoroutine(LoadDatasets());
        }

        private IEnumerator StartBackend()
        {
            if (backendServiceController == null)
                yield break;

            yield return backendServiceController.EnsureBackendReady();
        }

        private IEnumerator RestartBackend()
        {
            if (backendServiceController == null)
                yield break;

            yield return backendServiceController.RestartBackend();
        }

        private IEnumerator LoadDatasets()
        {
            if (backendServiceController != null && autoStartLocalBackend)
                yield return backendServiceController.EnsureBackendReady();

            client = new BackendWorkflowClient(backendBaseUrl, requestTimeoutSeconds);
            datasetStatus = "Loading datasets...";

            var okResult = false;
            var message = string.Empty;
            yield return client.FetchDatasets((ok, response) =>
            {
                okResult = ok;
                message = response;
            });

            if (!okResult)
            {
                datasetStatus = "Failed: " + message;
                Debug.LogError("Failed to load datasets: " + message);
                yield break;
            }

            try
            {
                var response = JsonUtility.FromJson<BackendDatasetListResponse>(message);
                datasets = response != null && response.datasets != null ? response.datasets : new BackendDatasetInfo[0];
                datasetStatus = string.Format("Loaded {0} dataset(s).", datasets.Length);
                if (datasets.Length > 0 && string.IsNullOrWhiteSpace(dataset))
                    dataset = datasets[0].id;
            }
            catch (Exception ex)
            {
                datasetStatus = "Parse failed: " + ex.Message;
                Debug.LogError("Failed to parse dataset list: " + ex);
            }
        }

        [ContextMenu("Run Command And Render")]
        public void RunCommandAndRenderFromMenu()
        {
            if (!isRunning)
                StartCoroutine(RunCommandAndRender());
        }

        [ContextMenu("Clear Command Render")]
        public void ClearRender()
        {
            EnsureRenderRoot();
            ConfigureRenderRootTransform();
            RebuildCoordinator();
            renderCoordinator.ClearRenderedViews();
            statusMessage = "Cleared.";
        }

        private IEnumerator RunCommandAndRender()
        {
            isRunning = true;
            statusMessage = "Preparing request...";
            EnsureRenderRoot();
            ConfigureRenderRootTransform();
            RebuildCoordinator();
            client = new BackendWorkflowClient(backendBaseUrl, requestTimeoutSeconds);

            if (backendServiceController != null && autoStartLocalBackend)
            {
                statusMessage = "Starting local backend...";
                yield return backendServiceController.EnsureBackendReady();
                if (!backendServiceController.IsHealthy)
                {
                    statusMessage = "Backend start failed: " + backendServiceController.StatusMessage;
                    isRunning = false;
                    yield break;
                }
            }

            if (checkHealthBeforeRender)
            {
                var healthOk = false;
                var healthMessage = string.Empty;
                statusMessage = "Checking backend health...";
                yield return client.CheckHealth((ok, message) =>
                {
                    healthOk = ok;
                    healthMessage = message;
                });

                if (!healthOk)
                {
                    statusMessage = "Backend health failed: " + healthMessage;
                    Debug.LogError(statusMessage);
                    isRunning = false;
                    yield break;
                }
            }

            var runRequest = new BackendWorkflowRunRequest
            {
                task = commandText,
                dataset = dataset,
                workflowId = workflowId,
                viewType = requestedViewType,
                execute = executeEvoFlow,
                population = population,
                generations = generations,
                eliteSize = eliteSize,
                timeoutSeconds = requestTimeoutSeconds
            };

            var fetchOk = false;
            var responseJson = string.Empty;
            statusMessage = executeEvoFlow ? "Executing EvoFlow..." : "Requesting cached workflow...";
            yield return client.RunWorkflowForUnityRender(runRequest, (ok, message) =>
            {
                fetchOk = ok;
                responseJson = message;
            });

            if (!fetchOk)
            {
                statusMessage = "Request failed: " + responseJson;
                Debug.LogError(statusMessage);
                isRunning = false;
                yield break;
            }

            if (!BackendWorkflowClient.TryParseRenderResult(responseJson, out var backendResult, out var errorMessage))
            {
                statusMessage = "Render failed: " + errorMessage;
                Debug.LogError(statusMessage);
                isRunning = false;
                yield break;
            }

            try
            {
                statusMessage = "Rendering...";
                var renderResult = renderCoordinator.Render(backendResult);
                var selectedCount = backendResult.resultSummary != null ? backendResult.resultSummary.selectedPointCount : 0;
                statusMessage = string.Format("Rendered {0} view(s), selected points: {1}.", renderResult.RenderedViewCount, selectedCount);
            }
            catch (Exception ex)
            {
                statusMessage = "Render failed: " + ex.Message;
                Debug.LogError("Failed to render command response: " + ex);
            }
            finally
            {
                isRunning = false;
            }
        }

        private int ParseIntField(int currentValue, params GUILayoutOption[] options)
        {
            var text = GUILayout.TextField(currentValue.ToString(), options);
            return int.TryParse(text, out var parsed) ? parsed : currentValue;
        }

        private void ApplyCachedPreset()
        {
            executeEvoFlow = false;
            workflowId = "test3";
            requestedViewType = "Point";
            dataset = "hurricane_sandy_2012_100k_sample.csv";
        }

        private void ApplyDynamicPointPreset()
        {
            executeEvoFlow = true;
            requestedViewType = "Point";
            dataset = "hurricane_sandy_2012_100k_sample.csv";
            commandText =
                "Find concentrated morning pickup hotspots in the Hurricane Sandy sample and render them as a backend-ready point visualization.";
        }

        private void EnsureRenderRoot()
        {
            if (renderRoot != null)
                return;

            var rootObject = new GameObject("BackendCommandRenderRoot");
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
