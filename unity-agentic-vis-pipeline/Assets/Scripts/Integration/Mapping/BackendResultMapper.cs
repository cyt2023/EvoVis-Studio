using System.Collections.Generic;
using System.Linq;
using ImmersiveTaxiVis.Integration.Models;
using UnityEngine;

namespace ImmersiveTaxiVis.Integration.Mapping
{
    public static class BackendResultMapper
    {
        private static readonly Color DefaultPointColor = new Color(0.15f, 0.65f, 1f, 1f);
        private static readonly Color SelectedPointColor = new Color(1f, 0.82f, 0.2f, 1f);

        public static List<PointRenderModel> MapSupportedViews(BackendResultRoot result)
        {
            var mappedViews = new List<PointRenderModel>();
            if (result == null || result.visualizationPayload == null || result.visualizationPayload.views == null)
            {
                return mappedViews;
            }

            var selectedIds = new HashSet<string>(
                result.resultSummary != null && result.resultSummary.selectedRowIds != null
                    ? result.resultSummary.selectedRowIds
                    : new string[0]);

            foreach (var view in result.visualizationPayload.views)
            {
                var mapped = MapView(view, selectedIds);
                if (mapped != null)
                {
                    mappedViews.Add(mapped);
                }
            }

            return mappedViews;
        }

        private static PointRenderModel MapView(BackendViewDefinition view, HashSet<string> selectedIds)
        {
            if (view == null || view.points == null || view.points.Length == 0)
            {
                return null;
            }

            var positions = new Vector3[view.points.Length];
            var pointColors = new Color[view.points.Length];
            var pointSizes = Enumerable.Repeat(1f, view.points.Length).ToArray();
            var normalizedViewType = NormalizeViewType(view.viewType);
            var minColorValue = view.points.Min(point => point.colorValue);
            var maxColorValue = view.points.Max(point => point.colorValue);
            var colorValueRange = Mathf.Max(1e-6f, maxColorValue - minColorValue);

            var selectedCount = 0;
            for (var i = 0; i < view.points.Length; i++)
            {
                var point = view.points[i];
                var isSelected = point.isSelected || (!string.IsNullOrEmpty(point.id) && selectedIds.Contains(point.id));
                var colorT = Mathf.Clamp01((point.colorValue - minColorValue) / colorValueRange);

                positions[i] = new Vector3(point.x - 0.5f, point.z, point.y - 0.5f);
                pointColors[i] = isSelected ? SelectedPointColor : ColorForValue(colorT);

                if (isSelected)
                {
                    selectedCount++;
                    pointSizes[i] = 1.35f;
                }
                else if (point.sizeValue > 0f)
                {
                    pointSizes[i] = Mathf.Clamp(0.7f + Mathf.Log10(point.sizeValue + 1f) * 0.35f, 0.75f, 1.4f);
                }
            }

            return new PointRenderModel
            {
                ViewName = string.IsNullOrWhiteSpace(view.viewName) ? "BackendView" : view.viewName,
                ViewType = normalizedViewType,
                ProjectionPlane = NormalizeProjectionPlane(view),
                Positions = positions,
                PointColors = pointColors,
                PointSizes = pointSizes,
                Links = MapLinks(view),
                SelectedCount = selectedCount,
                RenderPoints = ShouldRenderPoints(view.viewType),
                RenderLinks = ShouldRenderLinks(view),
                Visible = view.visible,
                PointSizeScale = view.pointSizeScale <= 0f ? 1f : view.pointSizeScale
            };
        }

        private static Color ColorForValue(float t)
        {
            t = Mathf.Clamp01(t);
            var low = new Color(0.1f, 0.7f, 1f, 1f);
            var mid = new Color(0.18f, 1f, 0.65f, 1f);
            var high = new Color(1f, 0.78f, 0.18f, 1f);
            return t < 0.5f
                ? Color.Lerp(low, mid, t * 2f)
                : Color.Lerp(mid, high, (t - 0.5f) * 2f);
        }


        private static string NormalizeViewType(string viewType)
        {
            if (string.IsNullOrWhiteSpace(viewType))
            {
                return "POINT";
            }

            return viewType.Trim().ToUpperInvariant();
        }

        private static string NormalizeProjectionPlane(BackendViewDefinition view)
        {
            if (view == null)
            {
                return "XY";
            }

            if (!string.IsNullOrWhiteSpace(view.projectionPlane))
            {
                return view.projectionPlane.Trim().ToUpperInvariant();
            }

            var normalizedViewType = NormalizeViewType(view.viewType);
            if (normalizedViewType.EndsWith("_XZ"))
            {
                return "XZ";
            }

            if (normalizedViewType.EndsWith("_YZ"))
            {
                return "YZ";
            }

            return "XY";
        }

        private static bool ShouldRenderPoints(string viewType)
        {
            var normalized = NormalizeViewType(viewType);
            return normalized != "LINK" && normalized != "LINKS";
        }

        private static bool ShouldRenderLinks(BackendViewDefinition view)
        {
            if (view == null)
            {
                return false;
            }

            if (!view.includeLinks)
            {
                return false;
            }

            var normalized = NormalizeViewType(view.viewType);
            return normalized == "STC" || normalized == "LINK" || normalized == "LINKS";
        }

        private static LinkRenderModel[] MapLinks(BackendViewDefinition view)
        {
            if (view.links == null || view.links.Length == 0)
            {
                return new LinkRenderModel[0];
            }

            var validLinks = new List<LinkRenderModel>();
            var maxIndex = view.points != null ? view.points.Length - 1 : -1;

            foreach (var link in view.links)
            {
                if (link == null)
                {
                    continue;
                }

                if (link.originIndex < 0 || link.destinationIndex < 0 || link.originIndex > maxIndex || link.destinationIndex > maxIndex)
                {
                    continue;
                }

                validLinks.Add(new LinkRenderModel
                {
                    OriginIndex = link.originIndex,
                    DestinationIndex = link.destinationIndex
                });
            }

            return validLinks.ToArray();
        }
    }
}
