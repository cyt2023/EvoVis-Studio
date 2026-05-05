using System;
using System.Collections;
using ImmersiveTaxiVis.Integration.Models;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        public string commandText = "Show all NYC taxi pickup points as a dense 3D point cloud. Do not filter rows. Use pickup location on the ground plane and trip distance or fare as visual height or color.";
        public string dataset = "first_week_of_may_2011_10k_sample.csv";
        public string workflowId = "test3";
        public string requestedViewType = "Auto";
        public bool executeEvoFlow = true;
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

        [Header("Window")]
        public bool showWindow = true;
        public bool useCanvasUi = true;
        public bool hideWindowAfterRender = true;
        public bool loadDatasetsOnStart = true;
        public Rect windowRect = new Rect(20, 20, 760, 560);

        private BackendWorkflowClient client;
        private WorkflowRuntimeRenderCoordinator renderCoordinator;
        private Transform configuredRenderRoot;
        private float configuredPointSize = -1f;
        private bool configuredRenderLinks;
        private string statusMessage = "Ready. Edit the task and click Run And Render.";
        private bool isRunning;
        private Vector2 scroll;
        private BackendDatasetInfo[] datasets = new BackendDatasetInfo[0];
        private string datasetStatus = "Datasets not loaded.";
        private InputField taskInputField;
        private Text statusText;
        private Text backendText;
        private Text datasetText;
        private GameObject canvasRoot;

        private void Awake()
        {
            client = new BackendWorkflowClient(backendBaseUrl, requestTimeoutSeconds);
            if (backendServiceController == null)
                backendServiceController = FindObjectOfType<DesktopBackendServiceController>();
            EnsureRenderRoot();
            ConfigureRenderRootTransform();
            RebuildCoordinator();
        }

        private void Start()
        {
            if (useCanvasUi)
                BuildCanvasUi();

            if (loadDatasetsOnStart && !isRunning)
                StartCoroutine(LoadDatasets());
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                showWindow = !showWindow;
                if (canvasRoot != null)
                    canvasRoot.SetActive(showWindow);
            }

            if (!useCanvasUi)
                return;

            if (taskInputField != null && !taskInputField.isFocused)
                taskInputField.text = commandText;

            if (statusText != null)
                statusText.text = "Status: " + statusMessage;

            if (backendText != null)
            {
                var backendStatus = backendServiceController != null ? backendServiceController.StatusMessage : "No backend controller.";
                backendText.text = "Backend: " + backendStatus + "    Endpoint: " + backendBaseUrl;
            }

            if (datasetText != null)
                datasetText.text = "Dataset: " + dataset + "    " + datasetStatus;
        }

        private void OnGUI()
        {
            if (!showWindow || useCanvasUi)
                return;

            windowRect.width = Mathf.Min(windowRect.width, Screen.width - 40);
            windowRect.height = Mathf.Min(windowRect.height, Screen.height - 40);
            windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "EvoVis Studio");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("Task command / 任务命令");
            GUILayout.Label("Describe the visualization task");
            GUI.SetNextControlName("TaskCommandTextArea");
            commandText = GUILayout.TextArea(commandText, GUILayout.Height(120));

            GUILayout.BeginHorizontal();
            GUI.enabled = !isRunning;
            if (GUILayout.Button(isRunning ? "Running..." : "Run And Render", GUILayout.Height(38)))
                StartCoroutine(RunCommandAndRender());
            if (GUILayout.Button("Clear View", GUILayout.Width(110), GUILayout.Height(38)))
                ClearRender();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label("Status: " + statusMessage);

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
                GUILayout.Label("Backend layout: " + backendServiceController.ResolutionHint);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Dataset", GUILayout.Width(70));
            dataset = GUILayout.TextField(dataset);
            GUI.enabled = !isRunning;
            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
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
            executeEvoFlow = GUILayout.Toggle(executeEvoFlow, "Generate new workflow");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Use cached sample", GUILayout.Height(24)))
                ApplyCachedPreset();
            if (GUILayout.Button("Use dynamic point task", GUILayout.Height(24)))
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

            GUILayout.Label("Dataset status: " + datasetStatus);
            GUI.DragWindow(new Rect(0, 0, windowRect.width, 22));
        }

        private void BuildCanvasUi()
        {
            if (taskInputField != null)
                return;

            EnsureEventSystem();

            canvasRoot = new GameObject("EvoVisStudioCanvas");
            canvasRoot.transform.SetParent(transform, false);
            var canvas = canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasRoot.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasRoot.AddComponent<GraphicRaycaster>();

            var panel = CreateUiObject<RectTransform>("Panel", canvasRoot.transform);
            panel.anchorMin = new Vector2(0.02f, 0.05f);
            panel.anchorMax = new Vector2(0.58f, 0.95f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.09f, 0.11f, 0.94f);
            panelImage.raycastTarget = false;

            CreateLabel(panel, "Title", "EvoVis Studio", 18, new Vector2(16, -14), new Vector2(-16, -40), TextAnchor.MiddleLeft);
            CreateLabel(panel, "TaskLabel", "Describe the visualization task", 14, new Vector2(16, -48), new Vector2(-16, -72), TextAnchor.MiddleLeft);

            taskInputField = CreateInputField(panel, "TaskInput", commandText, new Vector2(16, -76), new Vector2(-16, -216));
            taskInputField.onValueChanged.AddListener(value => commandText = value);

            var runButton = CreateButton(panel, "RunButton", "Run And Render", new Vector2(16, -226), new Vector2(-16, -266));
            runButton.onClick.AddListener(() =>
            {
                Debug.Log("EvoVis canvas UI: Run And Render clicked.");
                if (!isRunning)
                    StartCoroutine(RunCommandAndRender());
            });

            var clearButton = CreateButton(panel, "ClearButton", "Clear View", new Vector2(16, -272), new Vector2(-16, -312));
            clearButton.onClick.AddListener(ClearRender);

            statusText = CreateLabel(panel, "Status", "Status: " + statusMessage, 13, new Vector2(16, -326), new Vector2(-16, -352), TextAnchor.MiddleLeft);
            backendText = CreateLabel(panel, "Backend", "Backend: " + backendBaseUrl, 13, new Vector2(16, -354), new Vector2(-16, -380), TextAnchor.MiddleLeft);
            datasetText = CreateLabel(panel, "Dataset", "Dataset: " + dataset, 13, new Vector2(16, -382), new Vector2(-16, -408), TextAnchor.MiddleLeft);

            CreateLabel(panel, "DatasetFieldLabel", "Dataset", 13, new Vector2(16, -420), new Vector2(82, -446), TextAnchor.MiddleLeft);
            var datasetInput = CreateInputField(panel, "DatasetInput", dataset, new Vector2(88, -418), new Vector2(-126, -450));
            datasetInput.onValueChanged.AddListener(value => dataset = value);

            var refreshButton = CreateButton(panel, "RefreshButton", "Refresh", new Vector2(16, -456), new Vector2(-16, -492));
            refreshButton.onClick.AddListener(() =>
            {
                if (!isRunning)
                    StartCoroutine(LoadDatasets());
            });

            var cachedButton = CreateButton(panel, "CachedButton", "Use cached sample", new Vector2(16, -498), new Vector2(-16, -534));
            cachedButton.onClick.AddListener(() =>
            {
                ApplyCachedPreset();
                if (taskInputField != null)
                    taskInputField.text = commandText;
            });
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
                return;

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private static T CreateUiObject<T>(string name, Transform parent) where T : Component
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            return gameObject.AddComponent<T>();
        }

        private static Text CreateLabel(RectTransform parent, string name, string value, int fontSize, Vector2 offsetMin, Vector2 offsetMax, TextAnchor alignment)
        {
            var text = CreateUiObject<Text>(name, parent);
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            NormalizeTopAnchoredOffsets(ref offsetMin, ref offsetMax);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static InputField CreateInputField(RectTransform parent, string name, string value, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = CreateUiObject<RectTransform>(name, parent);
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            NormalizeTopAnchoredOffsets(ref offsetMin, ref offsetMax);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.02f, 0.02f, 0.025f, 1f);

            var input = rect.gameObject.AddComponent<InputField>();
            input.targetGraphic = image;
            input.interactable = true;
            input.readOnly = false;
            input.lineType = InputField.LineType.MultiLineNewline;
            input.text = value;

            var text = CreateLabel(rect, "Text", value, 14, new Vector2(8, 6), new Vector2(-8, -6), TextAnchor.UpperLeft);
            StretchToParent(text.rectTransform, new Vector2(8, 6), new Vector2(-8, -6));
            text.supportRichText = false;
            text.raycastTarget = false;
            input.textComponent = text;

            var placeholder = CreateLabel(rect, "Placeholder", "Type a visualization task...", 14, new Vector2(8, 6), new Vector2(-8, -6), TextAnchor.UpperLeft);
            StretchToParent(placeholder.rectTransform, new Vector2(8, 6), new Vector2(-8, -6));
            placeholder.color = new Color(0.7f, 0.7f, 0.7f, 0.65f);
            placeholder.raycastTarget = false;
            input.placeholder = placeholder;

            return input;
        }

        private static Button CreateButton(RectTransform parent, string name, string label, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = CreateUiObject<RectTransform>(name, parent);
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            NormalizeTopAnchoredOffsets(ref offsetMin, ref offsetMax);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.18f, 0.2f, 0.23f, 1f);
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var text = CreateLabel(rect, "Label", label, 14, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            StretchToParent(text.rectTransform, Vector2.zero, Vector2.zero);
            text.raycastTarget = false;
            return button;
        }

        private static void StretchToParent(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void NormalizeTopAnchoredOffsets(ref Vector2 offsetMin, ref Vector2 offsetMax)
        {
            if (offsetMin.y > offsetMax.y)
            {
                var lowerY = offsetMax.y;
                offsetMax.y = offsetMin.y;
                offsetMin.y = lowerY;
            }
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
            statusMessage = "Loading datasets...";

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
                statusMessage = "Ready. Edit the task and click Run And Render.";
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
            Debug.Log("EvoVis command window: Run And Render clicked.");
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
                viewType = ResolveRequestViewType(commandText, requestedViewType),
                execute = executeEvoFlow,
                requireLlm = requireRealLlm,
                population = population,
                generations = generations,
                eliteSize = eliteSize,
                timeoutSeconds = requestTimeoutSeconds,
                limit = renderPointLimit,
                includeSelectedIds = false
            };

            var fetchOk = false;
            var responseJson = string.Empty;
            statusMessage = executeEvoFlow ? "Generating visualization from command..." : "Requesting cached workflow...";
            Debug.Log("EvoVis command request viewType: " + runRequest.viewType);
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
                var primaryView = backendResult.visualizationPayload.views[0];
                Debug.Log("EvoVis command response primary viewType: " + primaryView.viewType + ", viewName: " + primaryView.viewName);
                statusMessage = "Rendering...";
                var renderResult = renderCoordinator.Render(backendResult);
                var selectedCount = backendResult.resultSummary != null ? backendResult.resultSummary.selectedPointCount : 0;
                statusMessage = string.Format("Rendered {0} {1} view(s), selected points: {2}.", renderResult.RenderedViewCount, primaryView.viewType, selectedCount);
                if (hideWindowAfterRender)
                {
                    showWindow = false;
                    if (canvasRoot != null)
                        canvasRoot.SetActive(false);
                }
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

        private static string ResolveRequestViewType(string taskText, string configuredViewType)
        {
            var configured = string.IsNullOrWhiteSpace(configuredViewType) ? "Auto" : configuredViewType.Trim();
            if (!string.Equals(configured, "Point", StringComparison.OrdinalIgnoreCase))
                return configured;

            var lowerTask = string.IsNullOrWhiteSpace(taskText) ? string.Empty : taskText.ToLowerInvariant();
            if (lowerTask.Contains("stc") ||
                lowerTask.Contains("space-time") ||
                lowerTask.Contains("space time") ||
                lowerTask.Contains("link") ||
                lowerTask.Contains("origin-destination") ||
                lowerTask.Contains("origin destination") ||
                lowerTask.Contains("projection") ||
                lowerTask.Contains("2d"))
            {
                return "Auto";
            }

            return configured;
        }

        private void ApplyCachedPreset()
        {
            executeEvoFlow = false;
            workflowId = "test3";
            requestedViewType = "Auto";
            dataset = "hurricane_sandy_2012_100k_sample.csv";
        }

        private void ApplyDynamicPointPreset()
        {
            executeEvoFlow = true;
            requestedViewType = "Auto";
            dataset = "first_week_of_may_2011_10k_sample.csv";
            commandText =
                "Show all NYC taxi pickup points as a dense 3D point cloud. Do not filter rows. Use pickup location on the ground plane and trip distance or fare as visual height or color.";
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
