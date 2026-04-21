using ImmersiveTaxiVis.Integration.Models;

namespace ImmersiveTaxiVis.Integration.Rendering
{
    public interface IBackendViewRenderer
    {
        bool CanRender(PointRenderModel model);
        RenderedViewHandle Render(PointRenderModel model, BackendViewRenderContext context);
        bool TryUpdateState(RenderedViewHandle handle, PointRenderModel model, BackendViewRenderContext context);
    }
}
