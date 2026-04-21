using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImmersiveTaxiVis.Integration.IO;
using ImmersiveTaxiVis.Integration.Mapping;
using ImmersiveTaxiVis.Integration.Models;
using UnityEngine;

namespace ImmersiveTaxiVis.Integration.Rendering
{
    public class FrontendRenderCoordinator
    {
        private readonly BackendViewRenderContext renderContext;
        private readonly BackendViewRendererRegistry rendererRegistry;
        private readonly List<RenderedViewHandle> renderedViews = new List<RenderedViewHandle>();

        public FrontendRenderCoordinator(Transform renderRoot, float pointSize, bool renderLinks)
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

        public RenderExecutionResult LoadMapAndRender(string relativeJsonPath)
        {
            var result = LocalJsonResultLoader.LoadFromStreamingAssets(relativeJsonPath);
            var mappedViews = BackendResultMapper.MapSupportedViews(result);

            if (mappedViews.Count == 0)
            {
                ClearRenderedViews();
                return new RenderExecutionResult
                {
                    RelativePath = relativeJsonPath,
                    RenderedViewCount = 0,
                    BackendResult = result,
                    AppliedStateOnlyUpdate = false
                };
            }

            if (TryApplyStateOnlyUpdate(mappedViews))
            {
                return new RenderExecutionResult
                {
                    RelativePath = relativeJsonPath,
                    RenderedViewCount = renderedViews.Count,
                    BackendResult = result,
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
                {
                    renderedViews.Add(handle);
                }
            }

            return new RenderExecutionResult
            {
                RelativePath = relativeJsonPath,
                RenderedViewCount = renderedViews.Count,
                BackendResult = result,
                AppliedStateOnlyUpdate = false
            };
        }

        public void ClearRenderedViews()
        {
            for (var i = 0; i < renderedViews.Count; i++)
            {
                DestroyObject(renderedViews[i].RootObject);
            }

            renderedViews.Clear();

            if (renderContext.Parent == null)
            {
                return;
            }

            for (var i = renderContext.Parent.childCount - 1; i >= 0; i--)
            {
                DestroyObject(renderContext.Parent.GetChild(i).gameObject);
            }
        }

        public string BuildLoadErrorMessage(string relativeJsonPath, Exception ex)
        {
            return string.Format(
                "Failed to load backend result JSON from '{0}'. {1}",
                Path.Combine(Application.streamingAssetsPath, relativeJsonPath),
                ex);
        }

        private bool TryApplyStateOnlyUpdate(List<PointRenderModel> mappedViews)
        {
            if (renderedViews.Count == 0 || mappedViews.Count != renderedViews.Count)
            {
                return false;
            }

            var handlesByName = renderedViews.ToDictionary(x => x.ViewName, x => x);

            for (var i = 0; i < mappedViews.Count; i++)
            {
                if (!handlesByName.TryGetValue(mappedViews[i].ViewName, out var handle))
                {
                    return false;
                }

                var renderer = rendererRegistry.Resolve(mappedViews[i]);
                if (renderer == null)
                {
                    return false;
                }

                if (!renderer.TryUpdateState(handle, mappedViews[i], renderContext))
                {
                    return false;
                }
            }

            return true;
        }

        private static void DestroyObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }

    public class RenderExecutionResult
    {
        public string RelativePath;
        public int RenderedViewCount;
        public BackendResultRoot BackendResult;
        public bool AppliedStateOnlyUpdate;
    }
}
