using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

internal static class RaylibUtil {
    public static Ray ScreenToWorld(Vector2 pos, Camera3D cam) => GetScreenToWorldRay(pos, cam);
}
