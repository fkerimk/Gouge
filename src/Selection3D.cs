using Raylib_cs;
using static Raylib_cs.Raylib;

internal static partial class Gouge {

    private static void Handle3DSelection() {

        _hoveredPart3D = -1;

        if (_io.WantCaptureMouse)
            return;

        var ray = RaylibUtil.ScreenToWorld(GetMousePosition(), Render.Cam3D);

        if (Picking.TryRaycastPart(Map, ray, out var partIndex, out _))
            _hoveredPart3D = partIndex;

        if (IsMouseButtonPressed(MouseButton.Left) && _hoveredPart3D != -1)
            _activePart = _hoveredPart3D;
    }
}
