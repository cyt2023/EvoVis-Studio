using ImmersiveTaxiVis.Integration.Models;

namespace ImmersiveTaxiVis.Integration.Rendering
{
    public class StcBackendViewRenderer : IBackendViewRenderer
    {
        public bool CanRender(PointRenderModel model)
        {
            if (model == null)
            {
                return false;
            }

            return model.ViewType == "STC";
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
