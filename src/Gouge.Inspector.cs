using System.Numerics;
using ImGuiNET;
using Raylib_cs;
using static Raylib_cs.Raylib;

internal static partial class Gouge {

    private static void DrawPartInspector() {

        ImGui.SetNextWindowPos(Vector2.Zero);
        ImGui.SetNextWindowSize(new Vector2(340, GetScreenHeight()));

        if (!ImGui.Begin("Part", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse)) {
            ImGui.End();
            return;
        }

        ImGui.Text(_mode3D ? "Mode: 3D" : "Mode: 2D");

        if (_activePart < 0 || _activePart >= Map.Parts.Count) {
            ImGui.Separator();
            ImGui.Text("No part selected.");
            ImGui.End();
            return;
        }

        var part = Map.Parts[_activePart];

        ImGui.Separator();
        ImGui.Text($"Selected Part: {_activePart}");

        var height = part.Height;
        if (ImGui.DragFloat("Height", ref height, DragSpeed, 0.1f, 1000f))
            part.Height = height;

        var yOffset = part.YOffset;
        if (ImGui.DragFloat("YOffset", ref yOffset, DragSpeed, -1000f, 1000f))
            part.YOffset = yOffset;

        DrawSurfaceEditor("Floor", part.Floor);
        DrawSurfaceEditor("Wall", part.Wall);
        DrawSurfaceEditor("Ceil", part.Ceil);

        ImGui.End();
    }

    private static void DrawSurfaceEditor(string label, Surface surface) {

        if (!ImGui.CollapsingHeader(label))
            return;

        DrawTextureField($"{label} Texture", surface);

        var mode = (int)surface.Mode;
        var modeNames = Enum.GetNames<TileMode>();
        if (ImGui.Combo($"{label} Mode", ref mode, modeNames, modeNames.Length))
            surface.Mode = (TileMode)mode;

        var wrap = (int)surface.Wrap;
        var wrapNames = Enum.GetNames<TextureWrap>();
        if (ImGui.Combo($"{label} Wrap", ref wrap, wrapNames, wrapNames.Length))
            surface.Wrap = (TextureWrap)wrap;

        var tiling = surface.Tiling;
        if (ImGui.DragFloat2($"{label} Tiling", ref tiling, DragSpeed, 0.01f, 1000f))
            surface.Tiling = tiling;

        var offset = surface.Offset;
        if (ImGui.DragFloat2($"{label} Offset", ref offset, DragSpeed))
            surface.Offset = offset;
    }

    private static void DrawTextureField(string label, Surface surface) {

        var texture = surface.Texture;
        if (ImGui.InputText(label, ref texture, 256))
            surface.Texture = texture;

        if (TextureFiles.Length == 0)
            return;

        var currentIndex = Array.IndexOf(TextureFiles, surface.Texture);
        if (currentIndex < 0)
            currentIndex = 0;

        if (ImGui.Combo($"{label} Presets", ref currentIndex, TextureFiles, TextureFiles.Length))
            surface.Texture = TextureFiles[currentIndex];
    }
}
