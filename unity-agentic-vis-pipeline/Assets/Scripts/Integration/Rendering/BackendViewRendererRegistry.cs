using System.Collections.Generic;
using ImmersiveTaxiVis.Integration.Models;

namespace ImmersiveTaxiVis.Integration.Rendering
{
    public class BackendViewRendererRegistry
    {
        private readonly List<IBackendViewRenderer> renderers = new List<IBackendViewRenderer>();

        public BackendViewRendererRegistry()
        {
            renderers.Add(new StcBackendViewRenderer());
            renderers.Add(new Projection2DBackendViewRenderer());
            renderers.Add(new LinkBackendViewRenderer());
            renderers.Add(new PointBackendViewRenderer());
        }

        public IBackendViewRenderer Resolve(PointRenderModel model)
        {
            for (var i = 0; i < renderers.Count; i++)
            {
                if (renderers[i].CanRender(model))
                {
                    return renderers[i];
                }
            }

            return null;
        }
    }
}
