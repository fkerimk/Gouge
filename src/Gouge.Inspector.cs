using System.Numerics;
using ImGuiNET;
using Raylib_cs;
using static Raylib_cs.Raylib;

internal static partial class Gouge {

    private const int InspectorStyleColorCount = 27;

    private static void DrawPartInspector() {

        ImGui.SetNextWindowPos(Vector2.Zero);
        ImGui.SetNextWindowSize(new Vector2(340, GetScreenHeight()));

        PushInspectorTheme();

        const ImGuiWindowFlags windowFlags =
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoCollapse |
            ImGuiWindowFlags.NoTitleBar;

        if (!ImGui.Begin("##part_inspector", windowFlags)) {
            ImGui.End();
            PopInspectorTheme();
            return;
        }

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(16, 16));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 6));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 10));

        DrawInspectorSectionTitle("PART");
        ImGui.Text(_mode3D ? "Mode: 3D" : "Mode: 2D");

        if (_activePart < 0 || _activePart >= Map.Parts.Count) {
            ImGui.Spacing();
            DrawInspectorSectionTitle("SELECTION");
            ImGui.Text("No part selected.");
            ImGui.PopStyleVar(3);
            ImGui.End();
            PopInspectorTheme();
            return;
        }

        var part = Map.Parts[_activePart];

        ImGui.Spacing();
        DrawInspectorSectionTitle("SELECTION");
        ImGui.Text($"Selected Part: {_activePart}");

        var height = part.Height;
        if (ImGui.DragFloat("Height", ref height, DragSpeed, 0.1f, 1000f))
            part.Height = height;
        CommitHistoryOnImGuiEditEnd();

        var yOffset = part.YOffset;
        if (ImGui.DragFloat("YOffset", ref yOffset, DragSpeed, -1000f, 1000f))
            part.YOffset = yOffset;
        CommitHistoryOnImGuiEditEnd();

        DrawSurfaceEditor("Floor", part.Floor);
        DrawSurfaceEditor("Wall", part.Wall);
        DrawSurfaceEditor("Ceil", part.Ceil);

        ImGui.PopStyleVar(3);
        ImGui.End();
        PopInspectorTheme();
    }

    private static void DrawSurfaceEditor(string label, Surface surface) {

        if (!ImGui.CollapsingHeader(label))
            return;

        DrawTextureField($"{label} Texture", surface);

        var mode = (int)surface.Mode;
        var modeNames = Enum.GetNames<TileMode>();
        if (ImGui.Combo($"{label} Mode", ref mode, modeNames, modeNames.Length))
            surface.Mode = (TileMode)mode;
        CommitHistoryOnImGuiChange();

        var wrap = (int)surface.Wrap;
        var wrapNames = Enum.GetNames<TextureWrap>();
        if (ImGui.Combo($"{label} Wrap", ref wrap, wrapNames, wrapNames.Length))
            surface.Wrap = (TextureWrap)wrap;
        CommitHistoryOnImGuiChange();

        var tiling = surface.Tiling;
        if (ImGui.DragFloat2($"{label} Tiling", ref tiling, DragSpeed, 0.01f, 1000f))
            surface.Tiling = tiling;
        CommitHistoryOnImGuiEditEnd();

        var offset = surface.Offset;
        if (ImGui.DragFloat2($"{label} Offset", ref offset, DragSpeed))
            surface.Offset = offset;
        CommitHistoryOnImGuiEditEnd();
    }

    private static void DrawTextureField(string label, Surface surface) {

        var texture = surface.Texture;
        if (ImGui.InputText(label, ref texture, 256))
            surface.Texture = texture;
        CommitHistoryOnImGuiEditEnd();

        if (TextureFiles.Length == 0)
            return;

        var currentIndex = Array.IndexOf(TextureFiles, surface.Texture);
        if (currentIndex < 0)
            currentIndex = 0;

        if (ImGui.Combo($"{label} Presets", ref currentIndex, TextureFiles, TextureFiles.Length))
            surface.Texture = TextureFiles[currentIndex];
        CommitHistoryOnImGuiChange();
    }

    private static void CommitHistoryOnImGuiEditEnd() {

        if (ImGui.IsItemDeactivatedAfterEdit())
            RecordHistorySnapshot();
    }

    private static void CommitHistoryOnImGuiChange() {

        if (ImGui.IsItemEdited())
            RecordHistorySnapshot();
    }

    private static void PushInspectorTheme() {

        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.098f, 0.098f, 0.109f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.196f, 0.196f, 0.216f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.196f, 0.196f, 0.216f, 1f));
        ImGui.PushStyleColor(ImGuiCol.SeparatorHovered, new Vector4(0.345f, 0.247f, 0.156f, 1f));
        ImGui.PushStyleColor(ImGuiCol.SeparatorActive, new Vector4(0.467f, 0.306f, 0.165f, 1f));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.156f, 0.156f, 0.176f, 1f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.274f, 0.274f, 0.294f, 1f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.235f, 0.235f, 0.254f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.156f, 0.156f, 0.176f, 1f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.235f, 0.235f, 0.254f, 1f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.274f, 0.274f, 0.294f, 1f));
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, new Vector4(0.949f, 0.627f, 0.188f, 1f));
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, new Vector4(1f, 0.725f, 0.286f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.156f, 0.156f, 0.176f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.345f, 0.247f, 0.156f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.467f, 0.306f, 0.165f, 1f));
        ImGui.PushStyleColor(ImGuiCol.CheckMark, new Vector4(0.949f, 0.627f, 0.188f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TextSelectedBg, new Vector4(0.467f, 0.306f, 0.165f, 0.35f));
        ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(0.098f, 0.098f, 0.109f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(0.098f, 0.098f, 0.109f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, new Vector4(0.098f, 0.098f, 0.109f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, new Vector4(0.196f, 0.196f, 0.216f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, new Vector4(0.235f, 0.235f, 0.254f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, new Vector4(0.274f, 0.274f, 0.294f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ResizeGrip, new Vector4(0.949f, 0.627f, 0.188f, 0.2f));
        ImGui.PushStyleColor(ImGuiCol.ResizeGripHovered, new Vector4(0.949f, 0.627f, 0.188f, 0.55f));
        ImGui.PushStyleColor(ImGuiCol.ResizeGripActive, new Vector4(1f, 0.725f, 0.286f, 0.85f));
    }

    private static void PopInspectorTheme() =>
        ImGui.PopStyleColor(InspectorStyleColorCount);

    private static void DrawInspectorSectionTitle(string title) {

        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), title);
        ImGui.Separator();
    }
}
