using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

internal static partial class Gouge {
    
    private const float RadToDeg = 180f / MathF.PI;
    private const float DegToRad = MathF.PI / 180f;
    private const float Camera3DMoveSpeed = 6f;
    private const float Camera3DMouseSensitivity = 0.0035f;
    private const float Camera3DPitchLimit = 1.35f;
    private const float CameraLerpSpeed = 15f;
    private const float Camera2DRotationSensitivity = 0.25f;
    private const float RightDragThreshold = 4f;
    
    private static float _camera3DHeight = 2.5f;
    private static float _camera3DDesiredHeight = 2.5f;
    private static float _camera3DYaw = -MathF.PI * 0.5f;
    private static float _camera3DPitch = -0.35f;
    private static Vector2 _camera3DDesiredTarget = Vector2.Zero;

    private static float _camera2DDesiredZoom = Render.Cam2D.Zoom;
    private static Vector2 _camera2DZoomAnchorWorld;
    private static Vector2 _camera2DZoomAnchorScreen;
    private static bool _camera2DHasZoomAnchor;
    private static float _camera2DRotationDragRaw;

    private static void UpdateCamera() {

        if (_mode3D)
             UpdateCamera3D();
        else UpdateCamera2D();
    }

    private static float LerpFactor() => Math.Clamp(GetFrameTime() * CameraLerpSpeed, 0f, 1f);

    private static float LerpTo(float current, float target) => float.Lerp(current, target, LerpFactor());

    private static Vector2 LerpTo(Vector2 current, Vector2 target) => Vector2.Lerp(current, target, LerpFactor());

    private static void UpdateCamera3D() {

        if (IsMouseButtonPressed(MouseButton.Right)) {
            _rightMousePressPos = GetMousePosition();
            _rightMouseDragged = false;
            _rightMouseUsedForCamera = false;
        }
        
        if (!_io.WantCaptureMouse && IsMouseButtonDown(MouseButton.Right)) {

            var mouseScreen = GetMousePosition();

            if (!_rightMouseDragged && Vector2.Distance(mouseScreen, _rightMousePressPos) >= RightDragThreshold) {
                _rightMouseDragged = true;
                _rightMouseUsedForCamera = true;
            }

            var mouseDelta = GetMouseDelta();

            if (mouseDelta != Vector2.Zero)
                _rightMouseUsedForCamera = true;

            _camera3DYaw += mouseDelta.X * Camera3DMouseSensitivity;
            _camera3DPitch = Math.Clamp(_camera3DPitch - mouseDelta.Y * Camera3DMouseSensitivity, -Camera3DPitchLimit, Camera3DPitchLimit);
            Sync2DRotationWith3D();
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

        if (IsKeyDown(KeyboardKey.Q)) _camera3DDesiredHeight -= Camera3DMoveSpeed * GetFrameTime();
        if (IsKeyDown(KeyboardKey.E)) _camera3DDesiredHeight += Camera3DMoveSpeed * GetFrameTime();

        if (move != Vector2.Zero)
            _camera3DDesiredTarget += Vector2.Normalize(move) * Camera3DMoveSpeed * GetFrameTime();

        Render.Cam2D.Target = LerpTo(Render.Cam2D.Target, _camera3DDesiredTarget);
        _camera3DHeight = LerpTo(_camera3DHeight, _camera3DDesiredHeight);

        Render.Cam3D.Position = new Vector3(Render.Cam2D.Target.X, _camera3DHeight, Render.Cam2D.Target.Y);
        Render.Cam3D.Target = Render.Cam3D.Position + forward;
        Render.Cam3D.Up = Vector3.UnitY;
    }

    private static void UpdateCamera2D() {

        Render.Cam2D.Offset = new Vector2(GetScreenWidth() * 0.5f, GetScreenHeight() * 0.5f);
        
        if (_io.WantCaptureMouse)
            return;

        var mouseScreen = GetMousePosition();

        if (IsMouseButtonPressed(MouseButton.Right)) {
            _rightMousePressPos = mouseScreen;
            _rightMouseDragged = false;
            _rightMouseUsedForCamera = false;
            _camera2DRotationDragRaw = Render.Cam2D.Rotation;
        }

        if (IsMouseButtonDown(MouseButton.Right)) {
            if (!_rightMouseDragged && Vector2.Distance(mouseScreen, _rightMousePressPos) >= RightDragThreshold) {
                _rightMouseDragged = true;
                _rightMouseUsedForCamera = true;
            }

            var mouseDelta = GetMouseDelta();

            if (mouseDelta.X != 0f) {
                _rightMouseUsedForCamera = true;
                _camera2DRotationDragRaw -= mouseDelta.X * Camera2DRotationSensitivity;

                var targetRotation =
                    IsKeyDown(KeyboardKey.LeftShift) || IsKeyDown(KeyboardKey.RightShift)
                        ? MathF.Round(_camera2DRotationDragRaw / 45f) * 45f
                        : _camera2DRotationDragRaw;

                RotateCamera2DAroundScreenPoint(targetRotation, mouseScreen);
                Sync3DRotationWith2D();
            }
        }

        if (IsMouseButtonDown(MouseButton.Middle)) {
            var previousMouseScreen = mouseScreen - GetMouseDelta();
            var previousMouseWorld = GetScreenToWorld2D(previousMouseScreen, Render.Cam2D);
            var currentMouseWorld = GetScreenToWorld2D(mouseScreen, Render.Cam2D);
            var delta = previousMouseWorld - currentMouseWorld;

            Render.Cam2D.Target += delta;
            if (_camera2DHasZoomAnchor)
                _camera2DZoomAnchorWorld += delta;
            _camera3DDesiredTarget = Render.Cam2D.Target;
        }

        var wheel = GetMouseWheelMove();

        if (wheel != 0) {
            _camera2DZoomAnchorWorld = GetScreenToWorld2D(mouseScreen, Render.Cam2D);
            _camera2DZoomAnchorScreen = mouseScreen;
            _camera2DHasZoomAnchor = true;
            _camera2DDesiredZoom += wheel * _camera2DDesiredZoom / 2f;
            _camera2DDesiredZoom = MathF.Max(_camera2DDesiredZoom, 2f);
        }

        Render.Cam2D.Zoom = LerpTo(Render.Cam2D.Zoom, _camera2DDesiredZoom);

        if (MathF.Abs(Render.Cam2D.Zoom - _camera2DDesiredZoom) <= 0.001f)
            Render.Cam2D.Zoom = _camera2DDesiredZoom;

        if (_camera2DHasZoomAnchor) {
            var zoomAnchorWorldAfter = GetScreenToWorld2D(_camera2DZoomAnchorScreen, Render.Cam2D);
            Render.Cam2D.Target += _camera2DZoomAnchorWorld - zoomAnchorWorldAfter;
        }

        if (_camera2DHasZoomAnchor && MathF.Abs(Render.Cam2D.Zoom - _camera2DDesiredZoom) <= 0.001f)
            _camera2DHasZoomAnchor = false;

        _camera3DDesiredTarget = Render.Cam2D.Target;
    }

    private static void RotateCamera2DAroundScreenPoint(float targetRotation, Vector2 screenPoint) {

        var anchorWorldBefore = GetScreenToWorld2D(screenPoint, Render.Cam2D);
        Render.Cam2D.Rotation = targetRotation;
        var anchorWorldAfter = GetScreenToWorld2D(screenPoint, Render.Cam2D);
        Render.Cam2D.Target += anchorWorldBefore - anchorWorldAfter;
    }

    private static void Sync2DRotationWith3D() =>
        Render.Cam2D.Rotation = -(_camera3DYaw + MathF.PI * 0.5f) * RadToDeg;

    private static void Sync3DRotationWith2D() =>
        _camera3DYaw = -Render.Cam2D.Rotation * DegToRad - MathF.PI * 0.5f;
}
