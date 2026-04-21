using IATK;
using ImmersiveTaxiVis.Integration.Models;
using UnityEngine;

namespace ImmersiveTaxiVis.Integration.Rendering
{
    public class RenderedViewHandle
    {
        public string ViewName;
        public string ViewType;
        public string ProjectionPlane;
        public GameObject RootObject;
        public View PointView;
        public View LinkView;
        public int PointCount;
        public int LinkCount;
        public Vector3[] Positions;
        public bool RenderPoints;
        public bool RenderLinks;

        public void ApplyPointColors(Color[] colors)
        {
            if (PointView == null || colors == null)
            {
                return;
            }

            PointView.SetColors(colors);
        }

        public void ApplyPointSize(float basePointSize)
        {
            if (PointView == null)
            {
                return;
            }

            PointView.SetSize(basePointSize);
            PointView.SetMinSize(basePointSize * 0.5f);
            PointView.SetMaxSize(basePointSize * 1.75f);
        }

        public void ApplyPointSizeChannel(float[] pointSizes, float basePointSize)
        {
            if (PointView == null)
            {
                return;
            }

            ApplyPointSize(basePointSize);

            if (pointSizes != null && pointSizes.Length == PointCount)
            {
                PointView.SetSizeChannel(pointSizes);
            }
        }

        public void SetVisible(bool visible)
        {
            if (RootObject != null)
            {
                RootObject.SetActive(visible);
            }
        }

        public bool MatchesGeometry(PointRenderModel model)
        {
            if (model == null)
            {
                return false;
            }

            if (PointCount != (model.Positions != null ? model.Positions.Length : 0))
            {
                return false;
            }

            if (LinkCount != (model.Links != null ? model.Links.Length : 0))
            {
                return false;
            }

            if (RenderPoints != model.RenderPoints || RenderLinks != model.RenderLinks)
            {
                return false;
            }

            if ((ProjectionPlane ?? string.Empty) != (model.ProjectionPlane ?? string.Empty))
            {
                return false;
            }

            if (Positions == null || model.Positions == null || Positions.Length != model.Positions.Length)
            {
                return false;
            }

            for (var i = 0; i < Positions.Length; i++)
            {
                if ((Positions[i] - model.Positions[i]).sqrMagnitude > 0.000001f)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
