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
            AddSceneFrame(viewRoot.transform);

            View pointView = null;
            var pointContainer = default(GameObject);
            if (model.RenderPoints)
            {
                pointContainer = new GameObject("Points");
                pointContainer.transform.SetParent(viewRoot.transform, false);

                TryRenderPointView(model, positions, pointContainer, pointSize, out pointView);
                AddPointMarkers(pointContainer.transform, positions, model.PointColors, model.PointSizes, pointSize);
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

            var shader = Shader.Find("IATK/LinesShader");
            if (shader == null)
            {
                Debug.LogWarning("IATK/LinesShader was not found in this build. Link rendering will be skipped.");
                Object.Destroy(linkContainer);
                return null;
            }

            var material = new Material(shader)
            {
                renderQueue = 3000,
                enableInstancing = true
            };

            try
            {
                return builder.apply(linkContainer, material, "LinkView");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("Failed to create IATK link view. Falling back to point-only rendering. " + ex);
                Object.Destroy(linkContainer);
                return null;
            }
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

            pointView.SetMinX(-0.55f);
            pointView.SetMaxX(0.55f);
            pointView.SetMinY(0f);
            pointView.SetMaxY(0.9f);
            pointView.SetMinZ(-0.55f);
            pointView.SetMaxZ(0.55f);
        }

        private static void AddPointMarkers(
            Transform parent,
            Vector3[] positions,
            Color[] colors,
            float[] sizes,
            float basePointSize)
        {
            if (positions == null || positions.Length == 0)
            {
                return;
            }

            var markerRoot = new GameObject("VisiblePointMarkers");
            markerRoot.transform.SetParent(parent, false);
            var markerSize = Mathf.Clamp(basePointSize * 0.11f, 0.0035f, 0.010f);
            const int maxMarkers = 1600;
            var stride = Mathf.Max(1, Mathf.CeilToInt(positions.Length / (float)maxMarkers));

            for (var i = 0; i < positions.Length; i += stride)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "Point_" + i;
                marker.transform.SetParent(markerRoot.transform, false);
                marker.transform.localPosition = positions[i];

                var sizeMultiplier = sizes != null && i < sizes.Length ? Mathf.Clamp(sizes[i], 0.75f, 1.4f) : 1f;
                marker.transform.localScale = Vector3.one * markerSize * sizeMultiplier;

                var collider = marker.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.Destroy(collider);
                }

                var renderer = marker.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = CreateUnlitMaterial(colors != null && i < colors.Length ? colors[i] : Color.cyan);
                }
            }
        }

        private static void AddSceneFrame(Transform parent)
        {
            var frameRoot = new GameObject("DataSpaceFrame");
            frameRoot.transform.SetParent(parent, false);

            var gridMaterial = CreateUnlitMaterial(new Color(0.22f, 0.28f, 0.34f, 0.35f));
            for (var i = 0; i <= 10; i++)
            {
                var t = -0.5f + (i / 10f);
                AddLine(frameRoot.transform, new Vector3(-0.5f, 0f, t), new Vector3(0.5f, 0f, t), gridMaterial, 0.0025f);
                AddLine(frameRoot.transform, new Vector3(t, 0f, -0.5f), new Vector3(t, 0f, 0.5f), gridMaterial, 0.0025f);
            }

            var xMaterial = CreateUnlitMaterial(new Color(1f, 0.25f, 0.2f, 1f));
            var yMaterial = CreateUnlitMaterial(new Color(0.25f, 1f, 0.35f, 1f));
            var yGuideMaterial = CreateUnlitMaterial(new Color(0.25f, 1f, 0.35f, 0.35f));
            var zMaterial = CreateUnlitMaterial(new Color(0.25f, 0.55f, 1f, 1f));
            AddLine(frameRoot.transform, new Vector3(-0.55f, 0f, -0.55f), new Vector3(0.6f, 0f, -0.55f), xMaterial, 0.008f);
            AddLine(frameRoot.transform, new Vector3(-0.55f, 0f, -0.55f), new Vector3(-0.55f, 0.82f, -0.55f), yMaterial, 0.01f);
            AddLine(frameRoot.transform, new Vector3(-0.55f, 0f, -0.55f), new Vector3(-0.55f, 0f, 0.6f), zMaterial, 0.008f);
            AddLine(frameRoot.transform, new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0.7f, -0.5f), yGuideMaterial, 0.004f);
            AddLine(frameRoot.transform, new Vector3(-0.5f, 0f, 0.5f), new Vector3(-0.5f, 0.7f, 0.5f), yGuideMaterial, 0.004f);

            for (var i = 1; i <= 4; i++)
            {
                var y = i * 0.16f;
                AddLine(frameRoot.transform, new Vector3(-0.59f, y, -0.55f), new Vector3(-0.51f, y, -0.55f), yMaterial, 0.005f);
                AddLine(frameRoot.transform, new Vector3(-0.5f, y, -0.5f), new Vector3(0.5f, y, -0.5f), yGuideMaterial, 0.0015f);
                AddLine(frameRoot.transform, new Vector3(-0.5f, y, 0.5f), new Vector3(0.5f, y, 0.5f), yGuideMaterial, 0.0015f);
                AddLine(frameRoot.transform, new Vector3(-0.5f, y, -0.5f), new Vector3(-0.5f, y, 0.5f), yGuideMaterial, 0.0015f);
                AddLine(frameRoot.transform, new Vector3(0.5f, y, -0.5f), new Vector3(0.5f, y, 0.5f), yGuideMaterial, 0.0015f);
            }

            AddLabel(frameRoot.transform, "X", new Vector3(0.66f, 0f, -0.55f), new Color(1f, 0.35f, 0.3f, 1f));
            AddLabel(frameRoot.transform, "Y / value", new Vector3(-0.6f, 0.9f, -0.55f), new Color(0.45f, 1f, 0.45f, 1f));
            AddLabel(frameRoot.transform, "Z", new Vector3(-0.55f, 0f, 0.66f), new Color(0.35f, 0.65f, 1f, 1f));
        }

        private static void AddLine(Transform parent, Vector3 start, Vector3 end, Material material, float width)
        {
            var lineObject = new GameObject("Line");
            lineObject.transform.SetParent(parent, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startWidth = width;
            line.endWidth = width;
            line.material = material;
            line.numCapVertices = 2;
        }

        private static void AddLabel(Transform parent, string value, Vector3 position, Color color)
        {
            var labelObject = new GameObject("AxisLabel_" + value);
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = position;
            labelObject.transform.localScale = Vector3.one * 0.08f;
            var label = labelObject.AddComponent<TextMesh>();
            label.text = value;
            label.characterSize = 0.25f;
            label.anchor = TextAnchor.MiddleCenter;
            label.color = color;
        }

        private static Material CreateUnlitMaterial(Color color)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                throw new System.InvalidOperationException("Unable to find a runtime shader for point markers or axes.");

            var material = new Material(shader);
            material.color = color;
            return material;
        }

        private static void TryRenderPointView(
            PointRenderModel model,
            Vector3[] positions,
            GameObject pointContainer,
            float pointSize,
            out View pointView)
        {
            pointView = null;
            if (positions == null || positions.Length == 0)
                return;

            try
            {
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
                if (pointMaterial == null)
                {
                    Debug.LogWarning("IATK point material was not available in this build. Falling back to primitive markers only.");
                    return;
                }

                pointView = builder.apply(pointContainer, pointMaterial, "PointView");
                if (pointView == null)
                {
                    Debug.LogWarning("IATK point view creation returned null. Falling back to primitive markers only.");
                    return;
                }

                ConfigurePointView(pointView, pointSize);
                pointView.SetSizeChannel(model.PointSizes);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("Failed to create IATK point view. Falling back to primitive markers only. " + ex);
                pointView = null;
            }
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
