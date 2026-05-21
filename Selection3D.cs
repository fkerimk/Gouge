using Sickle.Heart.Core;
using Sickle.Heart.Map;
using static Sickle.Heart.Core.Button;

internal static partial class Program {

    private static void Handle3DSelection() {

        _hoveredPart3D = -1;

        if (Gui.WantCaptureMouse)
            return;

        var ray = Util.ScreenToWorld(Input.MousePos, Render.Cam3D);

        if (Picking.TryRaycastPart(Map, ray, out var partIndex, out _))
            _hoveredPart3D = partIndex;

        if (Input.IsButtonPressed(MouseLeft) && _hoveredPart3D != -1)
            _activePart = _hoveredPart3D;
    }
}
