using System.Numerics;
using Sickle.Heart.Core;
using static Sickle.Heart.Core.Button;

internal static partial class Program {
    
    private const float Camera3DHeight = 2.5f;
    private const float Camera3DMoveSpeed = 6f;
    private const float Camera3DMouseSensitivity = 0.0035f;
    private const float Camera3DPitchLimit = 1.35f;
    
    private static float _camera3DYaw = -MathF.PI * 0.5f;
    private static float _camera3DPitch = -0.35f;

    private static void UpdateCamera() {

        if (_mode3D)
             UpdateCamera3D();
        else UpdateCamera2D();
    }

    private static void UpdateCamera3D() {
        
        if (!Gui.WantCaptureMouse && Input.IsButtonDown(MouseRight)) {

            var mouseDelta = Input.MouseDelta;
                
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

        if (Input.IsButtonDown(KeyBoardW)) move += forward2D;
        if (Input.IsButtonDown(KeyBoardS)) move -= forward2D;
        if (Input.IsButtonDown(KeyBoardA)) move -= right2D;
        if (Input.IsButtonDown(KeyBoardD)) move += right2D;

        if (move != Vector2.Zero)
            Render.Cam2D.Target += Vector2.Normalize(move) * Camera3DMoveSpeed * Time.Delta;

        Render.Cam3D.Position = new Vector3(Render.Cam2D.Target.X, Camera3DHeight, Render.Cam2D.Target.Y);
        Render.Cam3D.Target = Render.Cam3D.Position + forward;
        Render.Cam3D.Up = Vector3.UnitY;
    }

    private static void UpdateCamera2D() {
        
        if (Gui.WantCaptureMouse)
            return;

        if (Input.IsButtonDown(MouseMiddle))
            Render.Cam2D.Target -= Input.MouseDelta / Render.Cam2D.Zoom;

        if (Input.MouseScroll == 0) return;

        var mouseWorldBeforeZoom = Input.MouseWorldPos;

        Render.Cam2D.Zoom += Input.MouseScroll * Render.Cam2D.Zoom / 2f;
        Render.Cam2D.Zoom = MathF.Max(Render.Cam2D.Zoom, 2f);

        Render.Cam2D.Target = mouseWorldBeforeZoom - (Input.MousePos - Render.Cam2D.Offset) / Render.Cam2D.Zoom;
    }
}