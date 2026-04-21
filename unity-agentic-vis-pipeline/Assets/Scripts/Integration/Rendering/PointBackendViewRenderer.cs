using ImmersiveTaxiVis.Integration.Models;

namespace ImmersiveTaxiVis.Integration.Rendering
{
    public class PointBackendViewRenderer : IBackendViewRenderer
    {
        public bool CanRender(PointRenderModel model)
        {
            if (model == null)
            {
                return false;
            }

            return model.ViewType == "POINT" || model.ViewType == "POINTS";
        }

        public RenderedViewHandle Render(PointRenderModel model, BackendViewRenderContext context)
        {
            return IatkJsonViewRenderer.RenderPointLikeView(model, context, null);
        }

        public bool TryUpdateState(RenderedViewHandle handle, PointRenderModel model, BackendViewRenderContext context)
        {
            return IatkJsonViewRenderer.TryUpdatePointLikeView(handle, model, context, null);
        }
    }
}
