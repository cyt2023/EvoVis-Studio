using ImmersiveTaxiVis.Integration.Models;

namespace ImmersiveTaxiVis.Integration.Rendering
{
    public class LinkBackendViewRenderer : IBackendViewRenderer
    {
        public bool CanRender(PointRenderModel model)
        {
            if (model == null)
            {
                return false;
            }

            return model.ViewType == "LINK" || model.ViewType == "LINKS";
        }

        public RenderedViewHandle Render(PointRenderModel model, BackendViewRenderContext context)
        {
            model.RenderPoints = false;
            model.RenderLinks = true;
            return IatkJsonViewRenderer.RenderPointLikeView(model, context, null);
        }

        public bool TryUpdateState(RenderedViewHandle handle, PointRenderModel model, BackendViewRenderContext context)
        {
            model.RenderPoints = false;
            model.RenderLinks = true;
            return IatkJsonViewRenderer.TryUpdatePointLikeView(handle, model, context, null);
        }
    }
}
