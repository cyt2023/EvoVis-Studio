using OperatorPackage.Core;

namespace OperatorPackage.Backend
{
    public class AdaptedIATKViewBuilderOperator
    {
        public ViewRepresentation Build(ViewRepresentation view)
        {
            if (view == null)
                return new ViewRepresentation();

            view.EncodingState["AdaptedIATKViewBuilderApplied"] = true;
            view.EncodingState["BackendBuildPending"] = true;
            return view;
        }
    }
}
