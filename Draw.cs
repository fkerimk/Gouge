using System.Numerics;
using Sickle.Heart.Core;

internal static partial class Program {
    
    private static void Draw() {
        
        Render.Start();

        if (_mode3D)
             Draw3D();
        else Draw2D();

        Gui.Start();
        
        DrawPartInspector();
        
        Gui.Stop();
        
        Render.Stop();
    }
    
    private static void Draw2D() {
        
        Render.Begin2D();

        DrawGrid();

        var hoveredVertex = _selectedVertex == (-1, -1)
            ? FindHoveredVertex()
            : (-1, -1);
        var hoveredLine = hoveredVertex == (-1, -1) && _selectedVertex == (-1, -1) && !_selectedLine.HasValue
            ? FindHoveredLine()
            : (part: -1, start: -1, end: -1, point: Vector2.Zero);
        var hoveredPart = hoveredVertex == (-1, -1) && hoveredLine.part == -1 && _selectedVertex == (-1, -1) && !_selectedLine.HasValue && !_selectedPart.HasValue
            ? Map.FindPartContaining(Input.MouseWorldPos)
            : -1;

        Vector2? previewVertex = null;

        Handle2DEditing(ref hoveredVertex, ref hoveredLine, ref hoveredPart, ref previewVertex);

        DrawParts(hoveredVertex, hoveredLine, hoveredPart);
        DrawRectPreview();

        if (previewVertex.HasValue)
            Render.Square(Colors.Orange, previewVertex.Value, .1f);

        Render.End2D();
    }

    private static void Draw3D() {
        
        Render.Begin3D();
            
        Render.Map(Map);

        if (_hoveredPart3D != -1 && _hoveredPart3D < Map.Parts.Count && _hoveredPart3D != _activePart)
            Render.PartOutline(Map.Parts[_hoveredPart3D], Colors.Gold);

        if (_activePart != -1 && _activePart < Map.Parts.Count)
            Render.PartOutline(Map.Parts[_activePart], Colors.Orange);
            
        Render.End3D();
    }
}
