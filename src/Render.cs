using System.Numerics;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.Triangulate.Polygon;
using Raylib_cs;
using static Raylib_cs.Raylib;
using static Raylib_cs.CameraProjection;

internal static partial class Render {
    
    public static Camera2D Cam2D = new() {
        
        Offset = Vector2.Zero,
        Target = Vector2.Zero,
        Rotation = 0,
        Zoom = 60
    };

    public static Camera3D Cam3D = new() {
        
        Position = Vector3.Zero,
        Target = Vector3.Zero,
        Up = Vector3.UnitY,
        FovY = 90,
        Projection = Perspective
    };
    
    public static void Grid(float spacing, Color color) {
        
        var topLeft = GetScreenToWorld2D(Vector2.Zero, Cam2D);
        var bottomRight = GetScreenToWorld2D(new Vector2(GetScreenWidth(), GetScreenHeight()), Cam2D);

        var startX = MathF.Floor(topLeft.X / spacing) * spacing;
        var startY = MathF.Floor(topLeft.Y / spacing) * spacing;
        
        var endX = MathF.Ceiling(bottomRight.X / spacing) * spacing;
        var endY = MathF.Ceiling(bottomRight.Y / spacing) * spacing;

        for (var x = startX; x <= endX; x += spacing)
            DrawLineV(topLeft with { X = x }, bottomRight with { X = x }, color);

        for (var y = startY; y <= endY; y += spacing)
            DrawLineV(topLeft with { Y = y }, bottomRight with { Y = y }, color);
    }
    
    public static void Shape(List<Vector2> vertices, Color defaultColor, Color failColor) {
        
        if (vertices.Count < 3) return;

        var gf = NtsGeometryServices.Instance.CreateGeometryFactory();

        var coords = vertices.Select(v => new Coordinate(v.X, v.Y)).Append(new Coordinate(vertices[0].X, vertices[0].Y)).ToArray();

        var poly = gf.CreatePolygon(coords);

        var fail = !poly.IsValid;
        var geom = fail ? GeometryFixer.Fix(poly) : poly;
        var color = fail ? failColor : defaultColor;

        if (geom.IsEmpty) return;

        var tris = PolygonTriangulator.Triangulate(geom);

        for (var i = 0; i < tris.NumGeometries; i++) {
            
            var c = tris.GetGeometryN(i).Coordinates;
            if (c.Length < 3) continue;

            DrawTriangle(new Vector2((float)c[0].X, (float)c[0].Y), new Vector2((float)c[1].X, (float)c[1].Y), new Vector2((float)c[2].X, (float)c[2].Y), color);
        }
    }
    
    public static void Line(Vector2 start, Vector2 end, Color color) => DrawLineV(start, end, color);

    public static void Rectangle(Color color, Vector2 pos, Vector2 size, float roundness = 0) {

        var rect = new Rectangle(pos, size);
        
        if (roundness == 0)
            DrawRectangleRec(rect, color);
        else DrawRectangleRounded(rect, roundness, 0, color);
    }
    
    public static void Square(Color color, Vector2 pos, float size, float roundness = 0) => Rectangle(color, new Vector2(pos.X - size * .5f, pos.Y - size * .5f), new Vector2(size, size), roundness);

}