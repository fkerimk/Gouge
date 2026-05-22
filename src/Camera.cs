using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

internal static partial class Gouge {
    
    private const float Camera3DMoveSpeed = 6f;
    private const float Camera3DMouseSensitivity = 0.0035f;
    private const float Camera3DPitchLimit = 1.35f;
    
    private static float _camera3DHeight = 2.5f;
    private static float _camera3DYaw = -MathF.PI * 0.5f;
    private static float _camera3DPitch = -0.35f;

    private static void UpdateCamera() {

        if (_mode3D)
             UpdateCamera3D();
        else UpdateCamera2D();
    }

    private static void UpdateCamera3D() {
        
        if (!_io.WantCaptureMouse && IsMouseButtonDown(MouseButton.Right)) {

            var mouseDelta = GetMouseDelta();
                
            _camera3DYaw += mouseDelta.X * Camera3DMouseSensitivity;
            _camera3DPitch = Math.Clamp(_camera3DPitch - mouseDelta.Y * Camera3DMouseSensitivity, -Camera3DPitchLimit, Camera3DPitchLimit);
        }

        var forward = new Vector3(
                
            MathF.Cos(_camera3DPitch) * MathF.Cos(_camera3DYaw),
            MathF.Sin(_camera3DPitch),
            MathF.Cos(_camera3DPitch) * MathF.Sin(_camera3DYaw)
        );

        var forward2D = Vector2.Normalize(new Vector2(forward.X, forward.Z));
        var right2D = new Vector2(-forward2D.Y, forward2D.X);
            
        var move = Vector2.Zero;

        if (IsKeyDown(KeyboardKey.W)) move += forward2D;
        if (IsKeyDown(KeyboardKey.S)) move -= forward2D;
        if (IsKeyDown(KeyboardKey.A)) move -= right2D;
        if (IsKeyDown(KeyboardKey.D)) move += right2D;

        if (IsKeyDown(KeyboardKey.Q)) _camera3DHeight -= Camera3DMoveSpeed * GetFrameTime();
        if (IsKeyDown(KeyboardKey.E)) _camera3DHeight += Camera3DMoveSpeed * GetFrameTime();

        if (move != Vector2.Zero)
            Render.Cam2D.Target += Vector2.Normalize(move) * Camera3DMoveSpeed * GetFrameTime();

        Render.Cam3D.Position = new Vector3(Render.Cam2D.Target.X, _camera3DHeight, Render.Cam2D.Target.Y);
        Render.Cam3D.Target = Render.Cam3D.Position + forward;
        Render.Cam3D.Up = Vector3.UnitY;
    }

    private static void UpdateCamera2D() {
        
        if (_io.WantCaptureMouse)
            return;

        if (IsMouseButtonDown(MouseButton.Middle))
            Render.Cam2D.Target -= GetMouseDelta() / Render.Cam2D.Zoom;

        if (GetMouseWheelMove() == 0) return;

        var mouseWorldBeforeZoom = _mouseWorldPos;

        Render.Cam2D.Zoom += GetMouseWheelMove() * Render.Cam2D.Zoom / 2f;
        Render.Cam2D.Zoom = MathF.Max(Render.Cam2D.Zoom, 2f);

        Render.Cam2D.Target = mouseWorldBeforeZoom - (GetMousePosition() - Render.Cam2D.Offset) / Render.Cam2D.Zoom;
    }
}
