using UnityEngine;

namespace ImmersiveTaxiVis.Integration.Models
{
    public class PointRenderModel
    {
        public string ViewName;
        public string ViewType;
        public string ProjectionPlane;
        public Vector3[] Positions;
        public Color[] PointColors;
        public float[] PointSizes;
        public LinkRenderModel[] Links;
        public int SelectedCount;
        public bool RenderPoints = true;
        public bool RenderLinks = true;
        public bool Visible = true;
        public float PointSizeScale = 1f;
    }

    public class LinkRenderModel
    {
        public int OriginIndex;
        public int DestinationIndex;
    }
}
