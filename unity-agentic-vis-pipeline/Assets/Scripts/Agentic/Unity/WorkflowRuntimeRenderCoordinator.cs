using System;
using System.Collections.Generic;
using System.Linq;
using ImmersiveTaxiVis.Integration.Mapping;
using ImmersiveTaxiVis.Integration.Models;
using ImmersiveTaxiVis.Integration.Rendering;
using UnityEngine;

namespace ImmersiveTaxiVis.Integration.Runtime
{
    public class WorkflowRuntimeRenderCoordinator
    {
        private readonly BackendViewRenderContext renderContext;
        private readonly BackendViewRendererRegistry rendererRegistry;
        private readonly List<RenderedViewHandle> renderedViews = new List<RenderedViewHandle>();

        public WorkflowRuntimeRenderCoordinator(Transform renderRoot, float pointSize, bool renderLinks)
        {
            renderContext = new BackendViewRenderContext
            {
                Parent = renderRoot,
                BasePointSize = pointSize,
                DefaultRenderLinks = renderLinks
            };
            rendererRegistry = new BackendViewRendererRegistry();
        }

        public IReadOnlyList<RenderedViewHandle> RenderedViews => renderedViews;

        public RenderExecutionResult Render(BackendResultRoot backendResult)
        {
            var mappedViews = BackendResultMapper.MapSupportedViews(backendResult);
            if (mappedViews.Count == 0)
            {
                ClearRenderedViews();
                return new RenderExecutionResult
                {
                    RelativePath = "in-memory-workflow",
                    RenderedViewCount = 0,
                    BackendResult = backendResult,
                    AppliedStateOnlyUpdate = false
                };
            }

            if (TryApplyStateOnlyUpdate(mappedViews))
            {
                return new RenderExecutionResult
                {
                    RelativePath = "in-memory-workflow",
                    RenderedViewCount = renderedViews.Count,
                    BackendResult = backendResult,
                    AppliedStateOnlyUpdate = true
                };
            }

            ClearRenderedViews();

            for (var i = 0; i < mappedViews.Count; i++)
            {
                var renderer = rendererRegistry.Resolve(mappedViews[i]);
                if (renderer == null)
                {
                    Debug.LogWarning("No backend view renderer is registered for view type '" + mappedViews[i].ViewType + "'.");
                    continue;
                }

                var handle = renderer.Render(mappedViews[i], renderContext);
                if (handle != null)
                    renderedViews.Add(handle);
            }

            return new RenderExecutionResult
            {
                RelativePath = "in-memory-workflow",
                RenderedViewCount = renderedViews.Count,
                BackendResult = backendResult,
                AppliedStateOnlyUpdate = false
            };
        }

        public void ClearRenderedViews()
        {
            for (var i = 0; i < renderedViews.Count; i++)
                DestroyObject(renderedViews[i].RootObject);

            renderedViews.Clear();

            if (renderContext.Parent == null)
                return;

            for (var i = renderContext.Parent.childCount - 1; i >= 0; i--)
                DestroyObject(renderContext.Parent.GetChild(i).gameObject);
        }

        private bool TryApplyStateOnlyUpdate(List<PointRenderModel> mappedViews)
        {
            if (renderedViews.Count == 0 || mappedViews.Count != renderedViews.Count)
                return false;

            var handlesByName = renderedViews.ToDictionary(x => x.ViewName, x => x);

            for (var i = 0; i < mappedViews.Count; i++)
            {
                if (!handlesByName.TryGetValue(mappedViews[i].ViewName, out var handle))
                    return false;

                var renderer = rendererRegistry.Resolve(mappedViews[i]);
                if (renderer == null)
                    return false;

                if (!renderer.TryUpdateState(handle, mappedViews[i], renderContext))
                    return false;
            }

            return true;
        }

        private static void DestroyObject(GameObject target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(target);
            else
                UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
