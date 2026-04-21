using System.Collections.Generic;
using IATK;
using ImmersiveTaxiVis.Integration.Models;
using UnityEngine;

namespace ImmersiveTaxiVis.Integration.Rendering
{
    public static class IatkJsonViewRenderer
    {
        private static readonly Color DefaultLinkColor = new Color(1f, 1f, 1f, 0.35f);

        public static RenderedViewHandle RenderPointLikeView(
            PointRenderModel model,
            BackendViewRenderContext context,
            System.Func<PointRenderModel, Vector3[]> positionTransformer)
        {
            var parent = context.Parent;
            var pointSize = context.BasePointSize * Mathf.Max(0.01f, model.PointSizeScale);
            var renderLinks = model.RenderLinks && context.DefaultRenderLinks;
            var positions = positionTransformer != null ? positionTransformer(model) : model.Positions;

            var viewRoot = new GameObject(model.ViewName);
            viewRoot.transform.SetParent(parent, false);

            View pointView = null;
            if (model.RenderPoints)
            {
                var pointContainer = new GameObject("Points");
                pointContainer.transform.SetParent(viewRoot.transform, false);

                var builder = new ViewBuilder(MeshTopology.Points, model.ViewName)
                    .initialiseDataView(positions.Length)
                    .setDataDimension(ToAxis(positions, 0), ViewBuilder.VIEW_DIMENSION.X)
                    .setDataDimension(ToAxis(positions, 1), ViewBuilder.VIEW_DIMENSION.Y)
                    .setDataDimension(ToAxis(positions, 2), ViewBuilder.VIEW_DIMENSION.Z)
                    .setColors(model.PointColors)
                    .setSize(model.PointSizes)
                    .createIndicesPointTopology()
                    .updateView();

                var pointMaterial = IATKUtil.GetMaterialFromTopology(AbstractVisualisation.GeometryType.Points);
                pointView = builder.apply(pointContainer, pointMaterial, "PointView");
                ConfigurePointView(pointView, pointSize);
                pointView.SetSizeChannel(model.PointSizes);
            }

            View linkView = null;
            if (renderLinks && model.Links != null && model.Links.Length > 0)
            {
                linkView = RenderLinks(viewRoot.transform, model, positions);
                if (linkView == null)
                {
                    Debug.LogWarning("Link rendering was skipped because the line view could not be created.");
                }
            }

            viewRoot.SetActive(model.Visible);

            return new RenderedViewHandle
            {
                ViewName = model.ViewName,
                ViewType = model.ViewType,
                ProjectionPlane = model.ProjectionPlane,
                RootObject = viewRoot,
                PointView = pointView,
                LinkView = linkView,
                PointCount = positions != null ? positions.Length : 0,
                LinkCount = model.Links != null ? model.Links.Length : 0,
                Positions = positions,
                RenderPoints = model.RenderPoints,
                RenderLinks = renderLinks
            };
        }

        public static bool TryUpdatePointLikeView(
            RenderedViewHandle handle,
            PointRenderModel model,
            BackendViewRenderContext context,
            System.Func<PointRenderModel, Vector3[]> positionTransformer)
        {
            if (handle == null || model == null)
            {
                return false;
            }

            var transformedPositions = positionTransformer != null ? positionTransformer(model) : model.Positions;
            var comparisonModel = new PointRenderModel
            {
                ViewName = model.ViewName,
                ViewType = model.ViewType,
                ProjectionPlane = model.ProjectionPlane,
                Positions = transformedPositions,
                PointColors = model.PointColors,
                PointSizes = model.PointSizes,
                Links = model.Links,
                SelectedCount = model.SelectedCount,
                RenderPoints = model.RenderPoints,
                RenderLinks = model.RenderLinks && context.DefaultRenderLinks,
                Visible = model.Visible,
                PointSizeScale = model.PointSizeScale
            };

            if (!handle.MatchesGeometry(comparisonModel))
            {
                return false;
            }

            var pointSize = context.BasePointSize * Mathf.Max(0.01f, model.PointSizeScale);
            handle.ApplyPointColors(model.PointColors);
            handle.ApplyPointSizeChannel(model.PointSizes, pointSize);
            handle.SetVisible(model.Visible);
            return true;
        }

        private static View RenderLinks(Transform parent, PointRenderModel model, Vector3[] positions)
        {
            var linePositions = new List<Vector3>();
            var lineColors = new List<Color>();
            var lineIndices = new List<int>();

            for (var i = 0; i < model.Links.Length; i++)
            {
                var link = model.Links[i];
                var origin = positions[link.OriginIndex];
                var destination = positions[link.DestinationIndex];
                var color = DeriveLinkColor(model, link);

                var baseIndex = linePositions.Count;
                linePositions.Add(origin);
                linePositions.Add(destination);
                lineColors.Add(color);
                lineColors.Add(color);
                lineIndices.Add(baseIndex);
                lineIndices.Add(baseIndex + 1);
            }

            if (linePositions.Count == 0)
            {
                return null;
            }

            var linkContainer = new GameObject("Links");
            linkContainer.transform.SetParent(parent, false);

            var builder = new ViewBuilder(MeshTopology.Lines, parent.name + "_Links")
                .initialiseDataView(linePositions.Count)
                .setDataDimension(ToAxis(linePositions.ToArray(), 0), ViewBuilder.VIEW_DIMENSION.X)
                .setDataDimension(ToAxis(linePositions.ToArray(), 1), ViewBuilder.VIEW_DIMENSION.Y)
                .setDataDimension(ToAxis(linePositions.ToArray(), 2), ViewBuilder.VIEW_DIMENSION.Z)
                .setColors(lineColors.ToArray());

            builder.Indices = lineIndices;
            builder.updateView();

            var material = new Material(Shader.Find("IATK/LinesShader"))
            {
                renderQueue = 3000,
                enableInstancing = true
            };

            return builder.apply(linkContainer, material, "LinkView");
        }

        private static void ConfigurePointView(View pointView, float pointSize)
        {
            pointView.SetSize(pointSize);
            pointView.SetMinSize(pointSize * 0.5f);
            pointView.SetMaxSize(pointSize * 1.75f);

            pointView.SetMinNormX(0f);
            pointView.SetMaxNormX(1f);
            pointView.SetMinNormY(0f);
            pointView.SetMaxNormY(1f);
            pointView.SetMinNormZ(0f);
            pointView.SetMaxNormZ(1f);

            pointView.SetMinX(0f);
            pointView.SetMaxX(1f);
            pointView.SetMinY(0f);
            pointView.SetMaxY(1f);
            pointView.SetMinZ(0f);
            pointView.SetMaxZ(1f);
        }

        private static Color DeriveLinkColor(PointRenderModel model, LinkRenderModel link)
        {
            var originColor = model.PointColors[link.OriginIndex];
            var destinationColor = model.PointColors[link.DestinationIndex];
            var color = Color.Lerp(originColor, destinationColor, 0.5f);
            color.a = Mathf.Min(originColor.a, destinationColor.a);

            if (color.maxColorComponent <= 0f)
            {
                color = DefaultLinkColor;
            }

            if (color.a <= 0f)
            {
                color.a = DefaultLinkColor.a;
            }

            return color;
        }

        private static float[] ToAxis(Vector3[] positions, int axisIndex)
        {
            var data = new float[positions.Length];
            for (var i = 0; i < positions.Length; i++)
            {
                data[i] = positions[i][axisIndex];
            }

            return data;
        }
    }
}
