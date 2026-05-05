using ImmersiveTaxiVis.Integration.Models;
using UnityEngine;

namespace ImmersiveTaxiVis.Integration.Rendering
{
    public class Projection2DBackendViewRenderer : IBackendViewRenderer
    {
        public bool CanRender(PointRenderModel model)
        {
            if (model == null)
            {
                return false;
            }

            return model.ViewType == "PROJECTION2D" ||
                   model.ViewType == "PROJECTION2D_XY" ||
                   model.ViewType == "PROJECTION2D_XZ" ||
                   model.ViewType == "PROJECTION2D_YZ";
        }

        public RenderedViewHandle Render(PointRenderModel model, BackendViewRenderContext context)
        {
            return IatkJsonViewRenderer.RenderPointLikeView(model, context, Project);
        }

        public bool TryUpdateState(RenderedViewHandle handle, PointRenderModel model, BackendViewRenderContext context)
        {
            return IatkJsonViewRenderer.TryUpdatePointLikeView(handle, model, context, Project);
        }

        private Vector3[] Project(PointRenderModel model)
        {
            var source = model.Positions;
            var projected = new Vector3[source.Length];
            var plane = string.IsNullOrWhiteSpace(model.ProjectionPlane) ? "XY" : model.ProjectionPlane.ToUpperInvariant();

            for (var i = 0; i < source.Length; i++)
            {
                var position = source[i];
                switch (plane)
                {
                    case "XZ":
                        projected[i] = new Vector3(position.x, 0f, position.z);
                        break;
                    case "YZ":
                        projected[i] = new Vector3(0f, 0f, position.z);
                        break;
                    default:
                        projected[i] = new Vector3(position.x, 0f, position.z);
                        break;
                }
            }

            return projected;
        }
    }
}
