using System;
using System.IO;
using ImmersiveTaxiVis.Integration.Models;
using UnityEngine;
using OperatorRunner;

namespace ImmersiveTaxiVis.Integration.Runtime
{
    public class WorkflowRuntimeDemoController : MonoBehaviour
    {
        [Header("Workflow Input")]
        public TextAsset workflowJsonAsset;
        public string relativeWorkflowJsonPath = "Agentic/Workflows/test3_workflow.json";
        public bool preferStreamingAssets = false;
        public bool executeOnStart = true;
        public string workflowJsonAbsolutePath = string.Empty;

        [Header("Rendering")]
        public Transform renderRoot;
        public float pointSize = 0.1f;
        public bool renderLinks = true;
        public Vector3 renderRootLocalPosition = Vector3.zero;
        public Vector3 renderRootLocalScale = Vector3.one;

        [Header("Debug")]
        public bool verboseLogging = true;

        private WorkflowRuntime runtime;
        private WorkflowRuntimeRenderCoordinator renderCoordinator;
        private Transform configuredRenderRoot;
        private float configuredPointSize = -1f;
        private bool configuredRenderLinks;

        private void Awake()
        {
            runtime = new WorkflowRuntime();
        }

        private void Start()
        {
            if (executeOnStart)
                ExecuteWorkflow();
        }

        [ContextMenu("Execute Workflow JSON")]
        public void ExecuteWorkflow()
        {
            try
            {
                EnsureRenderRoot();
                ConfigureRenderRootTransform();
                RebuildCoordinator();

                var workflowJson = LoadWorkflowJson(out var workflowSourcePath);
                var response = runtime.RunFromJson(workflowJson, workflowSourcePath);
                if (!response.Success)
                {
                    Debug.LogError("Workflow execution failed: " + string.Join(" | ", response.Errors));
                    return;
                }

                BackendResultRoot backendResult = WorkflowRuntimeBackendResultAdapter.Adapt(response);
                var renderResult = renderCoordinator.Render(backendResult);

                if (verboseLogging)
                {
                    Debug.Log(string.Format(
                        "Workflow '{0}' executed as {1}. Rendered {2} {3}. Selected points: {4}. Backend built: {5}.",
                        response.WorkflowId,
                        response.RequestKind,
                        renderResult.RenderedViewCount,
                        renderResult.RenderedViewCount == 1 ? "view" : "views",
                        response.SelectedPointCount,
                        response.BackendBuilt));

                    Debug.Log("Workflow data path: " + response.Runtime.DataPath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("WorkflowRuntimeDemoController failed: " + ex);
            }
        }

        [ContextMenu("Clear Workflow Render")]
        public void ClearRender()
        {
            EnsureRenderRoot();
            ConfigureRenderRootTransform();
            RebuildCoordinator();
            renderCoordinator.ClearRenderedViews();
        }

        private string LoadWorkflowJson(out string workflowSourcePath)
        {
            if (!string.IsNullOrWhiteSpace(workflowJsonAbsolutePath))
            {
                if (!File.Exists(workflowJsonAbsolutePath))
                    throw new FileNotFoundException("Workflow JSON was not found.", workflowJsonAbsolutePath);

                workflowSourcePath = workflowJsonAbsolutePath;
                return File.ReadAllText(workflowJsonAbsolutePath);
            }

            if (preferStreamingAssets)
            {
                var fullPath = Path.Combine(Application.streamingAssetsPath, relativeWorkflowJsonPath);
                if (!File.Exists(fullPath))
                    throw new FileNotFoundException("Workflow JSON was not found.", fullPath);

                workflowSourcePath = fullPath;
                return File.ReadAllText(fullPath);
            }

            if (workflowJsonAsset == null || string.IsNullOrWhiteSpace(workflowJsonAsset.text))
                throw new InvalidOperationException("Workflow JSON asset is empty.");

            workflowSourcePath = string.Empty;
            return workflowJsonAsset.text;
        }

        private void EnsureRenderRoot()
        {
            if (renderRoot != null)
                return;

            var rootObject = new GameObject("WorkflowRuntimeRenderRoot");
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
