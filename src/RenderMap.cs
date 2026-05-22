using System.Numerics;
using CommunityToolkit.HighPerformance.Buffers;
using LibTessDotNet;
using Raylib_cs;
using static Raylib_cs.Raylib;

internal static partial class Render {

    private static readonly Dictionary<string, Lazy<MaterialResource>> Materials = [];

    public static void Map(Map map) {

        for (var i = 0; i < map.Parts.Count; i++) {
            
            var part = map.Parts[i];
            
            var vertices = part.Vertices;
            if (vertices.Count < 3) continue;
            
            var wallDirection = GetWallDirection(map, i);

            DrawMesh(BuildFloorMesh(vertices, part.YOffset, wallDirection, part.Floor), GetMaterial(part.Floor), part.Floor);
            DrawMesh(BuildCeilingMesh(vertices, part.YOffset + part.Height, wallDirection, part.Ceil), GetMaterial(part.Ceil), part.Ceil);
            DrawMesh(BuildWallMesh(map, i, wallDirection), GetMaterial(part.Wall), part.Wall);
        }
    }

    public static void PartOutline(Part part, Color color) {

        if (part.Vertices.Count < 2)
            return;

        var bottom = part.YOffset;
        var top = part.YOffset + part.Height;

        for (var i = 0; i < part.Vertices.Count; i++) {
            var start = part.Vertices[i];
            var end = part.Vertices[(i + 1) % part.Vertices.Count];

            DrawLine3D(new Vector3(start.X, bottom, start.Y), new Vector3(end.X, bottom, end.Y), color);
            DrawLine3D(new Vector3(start.X, top, start.Y), new Vector3(end.X, top, end.Y), color);
            DrawLine3D(new Vector3(start.X, bottom, start.Y), new Vector3(start.X, top, start.Y), color);
        }
    }

    private static void DrawMesh(Mesh mesh, MaterialResource materialResource, Surface surface) {

        if (mesh.VertexCount == 0) return;

        var material = materialResource.Material;

        if (!materialResource.UsesCubemap) {
            
            var texture = Resources.GetResource<Texture2D>("texture", surface.Texture);
            
            SetTextureWrap(texture, surface.Wrap);
            SetMaterialTexture(ref material, MaterialMapIndex.Albedo, texture);
        }

        if (materialResource.CameraPositionLocation >= 0)
            SetShaderValue(material.Shader, materialResource.CameraPositionLocation, Cam3D.Position, ShaderUniformDataType.Vec3);

        UploadMesh(ref mesh, false);
        Raylib.DrawMesh(mesh, material, Matrix4x4.Identity);
        UnloadMesh(mesh);
    }

    private static Mesh BuildFloorMesh(List<Vector2> vertices, float yOffset, float wallDirection, Surface surface) {

        var tess = Tessellate(vertices);
        
        if (tess.ElementCount == 0)
            return new Mesh();

        using var buffer = new ArrayPoolBufferWriter<VertexData>(tess.ElementCount * 3);
        var bounds = GetBounds(vertices);

        for (var i = 0; i < tess.ElementCount; i++) {

            var a = tess.Vertices[tess.Elements[i * 3]].Position;
            var b = tess.Vertices[tess.Elements[i * 3 + 1]].Position;
            var c = tess.Vertices[tess.Elements[i * 3 + 2]].Position;

            if (wallDirection > 0f) {
                
                WriteTriangle(
                    buffer,
                    Vector3.UnitY,
                    (new Vector3(a.X, yOffset, a.Y), GetHorizontalUv(new Vector2(a.X, a.Y), bounds, surface)),
                    (new Vector3(b.X, yOffset, b.Y), GetHorizontalUv(new Vector2(b.X, b.Y), bounds, surface)),
                    (new Vector3(c.X, yOffset, c.Y), GetHorizontalUv(new Vector2(c.X, c.Y), bounds, surface))
                );

                continue;
            }

            WriteTriangle(
                buffer,
                Vector3.UnitY,
                (new Vector3(a.X, yOffset, a.Y), GetHorizontalUv(new Vector2(a.X, a.Y), bounds, surface)),
                (new Vector3(c.X, yOffset, c.Y), GetHorizontalUv(new Vector2(c.X, c.Y), bounds, surface)),
                (new Vector3(b.X, yOffset, b.Y), GetHorizontalUv(new Vector2(b.X, b.Y), bounds, surface))
            );
        }

        return BuildMesh(buffer.WrittenSpan);
    }

    private static Mesh BuildWallMesh(Map map, int partIndex, float wallDirection) {

        var part = map.Parts[partIndex];
        var vertices = part.Vertices;
        
        using var buffer = new ArrayPoolBufferWriter<VertexData>(vertices.Count * 18);
        
        var inward = MathF.Sign(Geometry2D.SignedArea(vertices)) * wallDirection;
        var bottom = part.YOffset;
        var top = part.YOffset + part.Height;

        for (var i = 0; i < vertices.Count; i++) {

            var start = vertices[i];
            var end = vertices[(i + 1) % vertices.Count];
            var delta = end - start;

            if (delta.LengthSquared() <= float.Epsilon)
                continue;

            var normal2D = Vector2.Normalize(new Vector2(-delta.Y, delta.X) * inward);
            var normal = new Vector3(normal2D.X, 0f, normal2D.Y);

            foreach (var face in WallGeometry.GetVisibleWallFaces(map, partIndex, start, end, bottom, top))
                WriteWallSegment(buffer, start, end, face.From, face.To, inward, normal, face.Bottom, face.Top, part.Wall);
        }

        return BuildMesh(buffer.WrittenSpan);
    }

    private static void WriteWallSegment(ArrayPoolBufferWriter<VertexData> buffer, Vector2 edgeStart, Vector2 edgeEnd, float from, float to, float inward, Vector3 normal, float bottom, float top, Surface surface) {

        if (to - from <= 0.0001f)
            return;

        var start = Vector2.Lerp(edgeStart, edgeEnd, from);
        var end = Vector2.Lerp(edgeStart, edgeEnd, to);
        var bottomStart = new Vector3(start.X, bottom, start.Y);
        var bottomEnd = new Vector3(end.X, bottom, end.Y);
        var topStart = new Vector3(start.X, top, start.Y);
        var topEnd = new Vector3(end.X, top, end.Y);
        var uvBottomStart = GetWallUv(start, end, bottom, top, bottom, surface);
        var uvBottomEnd = GetWallUv(start, end, bottom, top, bottom, surface, true);
        var uvTopStart = GetWallUv(start, end, bottom, top, top, surface);
        var uvTopEnd = GetWallUv(start, end, bottom, top, top, surface, true);

        if (inward > 0f) {
            WriteTriangle(buffer, normal, (bottomStart, uvBottomStart), (topEnd, uvTopEnd), (topStart, uvTopStart));
            WriteTriangle(buffer, normal, (bottomStart, uvBottomStart), (bottomEnd, uvBottomEnd), (topEnd, uvTopEnd));
            return;
        }

        WriteTriangle(buffer, normal, (bottomStart, uvBottomStart), (topStart, uvTopStart), (topEnd, uvTopEnd));
        WriteTriangle(buffer, normal, (bottomStart, uvBottomStart), (topEnd, uvTopEnd), (bottomEnd, uvBottomEnd));
    }

    private static Mesh BuildCeilingMesh(List<Vector2> vertices, float height, float wallDirection, Surface surface) {

        var tess = Tessellate(vertices);
        
        if (tess.ElementCount == 0)
            return new Mesh();

        using var buffer = new ArrayPoolBufferWriter<VertexData>(tess.ElementCount * 3);
        
        var bounds = GetBounds(vertices);

        for (var i = 0; i < tess.ElementCount; i++) {

            var a = tess.Vertices[tess.Elements[i * 3]].Position;
            var b = tess.Vertices[tess.Elements[i * 3 + 1]].Position;
            var c = tess.Vertices[tess.Elements[i * 3 + 2]].Position;

            if (wallDirection > 0f) {
                
                WriteTriangle(
                    buffer,
                    -Vector3.UnitY,
                    (new Vector3(a.X, height, a.Y), GetHorizontalUv(new Vector2(a.X, a.Y), bounds, surface)),
                    (new Vector3(c.X, height, c.Y), GetHorizontalUv(new Vector2(c.X, c.Y), bounds, surface)),
                    (new Vector3(b.X, height, b.Y), GetHorizontalUv(new Vector2(b.X, b.Y), bounds, surface))
                );

                continue;
            }

            WriteTriangle(
                buffer,
                -Vector3.UnitY,
                (new Vector3(a.X, height, a.Y), GetHorizontalUv(new Vector2(a.X, a.Y), bounds, surface)),
                (new Vector3(b.X, height, b.Y), GetHorizontalUv(new Vector2(b.X, b.Y), bounds, surface)),
                (new Vector3(c.X, height, c.Y), GetHorizontalUv(new Vector2(c.X, c.Y), bounds, surface))
            );
        }

        return BuildMesh(buffer.WrittenSpan);
    }

    private static Tess Tessellate(List<Vector2> vertices) {

        var contour = vertices
            .Select(vertex => new ContourVertex { Position = new Vec3(vertex.X, vertex.Y, 0f) })
            .ToArray();

        var tess = new Tess();
        tess.AddContour(contour);
        tess.Tessellate();

        return tess;
    }

    private static float GetWallDirection(Map map, int partIndex) {

        var sample = GetInteriorSample(map.Parts[partIndex].Vertices);
        var isInsideAnotherPart = map.Parts.Where((_, i) => i != partIndex).Any(t => Geometry2D.IsPointInPolygon(sample, t.Vertices));

        return isInsideAnotherPart ? -1f : 1f;
    }

    private static Vector2 GetInteriorSample(List<Vector2> vertices) {

        var tess = Tessellate(vertices);

        if (tess.ElementCount == 0)
            return vertices[0];

        var a = tess.Vertices[tess.Elements[0]].Position;
        var b = tess.Vertices[tess.Elements[1]].Position;
        var c = tess.Vertices[tess.Elements[2]].Position;

        return new Vector2(
            (a.X + b.X + c.X) / 3f,
            (a.Y + b.Y + c.Y) / 3f
        );
    }

    private static (Vector2 min, Vector2 max) GetBounds(List<Vector2> vertices) {
        
        var min = new Vector2(float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity);

        foreach (var vertex in vertices) {
            
            min = Vector2.Min(min, vertex);
            max = Vector2.Max(max, vertex);
        }

        return (min, max);
    }

    private static Vector2 GetHorizontalUv(Vector2 position, (Vector2 min, Vector2 max) bounds, Surface surface) {

        var uv = surface.Mode switch {
            
            TileMode.Local => new Vector2(
                
                NormalizeToBounds(position.X, bounds.min.X, bounds.max.X),
                NormalizeToBounds(position.Y, bounds.min.Y, bounds.max.Y)
            ),
            
            _ => position,
        };

        return ApplySurfaceTransform(uv, surface);
    }

    private static Vector2 GetWallUv(Vector2 segmentStart, Vector2 segmentEnd, float bottom, float top, float verticalPosition, Surface surface, bool useEnd = false) {

        var horizontalDistance = useEnd ? Vector2.Distance(segmentStart, segmentEnd) : 0f;
        var verticalDistance = top - verticalPosition;

        var uv = surface.Mode switch {
            
            TileMode.Local => new Vector2(
                
                NormalizeToBounds(horizontalDistance, 0f, Vector2.Distance(segmentStart, segmentEnd)),
                NormalizeToBounds(verticalDistance, 0f, top - bottom)
            ),
            
            _ => new Vector2(horizontalDistance, verticalDistance),
        };

        return ApplySurfaceTransform(uv, surface);
    }

    private static float NormalizeToBounds(float value, float min, float max) {

        var size = max - min;

        if (MathF.Abs(size) <= float.Epsilon)
            return 0f;

        return (value - min) / size;
    }

    private static Vector2 ApplySurfaceTransform(Vector2 uv, Surface surface) {

        var tiling = surface.Mode switch {
            
            TileMode.Local => new Vector2(
                
                uv.X * surface.Tiling.X,
                uv.Y * surface.Tiling.Y
            ),
            
            _ => new Vector2(
                
                DivideByTileSize(uv.X, surface.Tiling.X),
                DivideByTileSize(uv.Y, surface.Tiling.Y)
            ),
        };

        return tiling + surface.Offset;
    }

    private static float DivideByTileSize(float value, float tileSize) {

        if (MathF.Abs(tileSize) <= float.Epsilon)
            return 0f;

        return value / tileSize;
    }

    private static MaterialResource GetMaterial(Surface surface) =>
        Materials.GetValueOrDefault(surface.Texture)?.Value ?? (Materials[surface.Texture] = CreateMaterial(surface.Texture)).Value;

    private static unsafe Lazy<MaterialResource> CreateMaterial(string textureName) => new(() => {
        
        var material = LoadMaterialDefault();
        var shaderName = ResolveShaderName(textureName);
        var shader = Resources.GetResource<Shader>("shader", shaderName);
        var usesCubemap = shaderName == "sky";

        if (usesCubemap) {

            var cubemap = Resources.GetCubemap("texture", textureName);
            material.Maps[(int)MaterialMapIndex.Cubemap].Texture = cubemap;

            var environmentMapLocation = GetShaderLocation(shader, "environmentMap");
            if (environmentMapLocation >= 0)
                SetShaderValue(shader, environmentMapLocation, (int)MaterialMapIndex.Cubemap, ShaderUniformDataType.Int);
            
        } else {
            
            var texture = Resources.GetResource<Texture2D>("texture", textureName);
            SetTextureFilter(texture, TextureFilter.Point);
            
            SetMaterialTexture(ref material, MaterialMapIndex.Albedo, texture);
        }

        material.Shader = shader;

        return new MaterialResource {
            
            Material = material,
            CameraPositionLocation = GetShaderLocation(shader, "uCamPos"),
            UsesCubemap = usesCubemap
        };
    });

    private static string ResolveShaderName(string textureName) {

        var fileName = Path.GetFileNameWithoutExtension(textureName);
        var separatorIndex = fileName.IndexOf('_');
        var shaderName = separatorIndex > 0 ? fileName[..separatorIndex] : fileName;

        return Resources.HasResourceFile("shader", $"{shaderName}.fs") && Resources.HasResourceFile("shader", $"{shaderName}.vs")
            ? shaderName
            : "default";
    }

    private static Mesh BuildMesh(ReadOnlySpan<VertexData> vertices) {

        if (vertices.IsEmpty) return new Mesh();

        var mesh = new Mesh(vertices.Length, vertices.Length / 3);
        mesh.AllocVertices();
        mesh.AllocTexCoords();
        mesh.AllocNormals();

        var positions = mesh.VerticesAs<float>();
        var texCoords = mesh.TexCoordsAs<float>();
        var normals = mesh.NormalsAs<float>();

        for (var i = 0; i < vertices.Length; i++) {
            
            var vertex = vertices[i];

            positions[i * 3] = vertex.Position.X;
            positions[i * 3 + 1] = vertex.Position.Y;
            positions[i * 3 + 2] = vertex.Position.Z;

            texCoords[i * 2] = vertex.Uv.X;
            texCoords[i * 2 + 1] = vertex.Uv.Y;

            normals[i * 3] = vertex.Normal.X;
            normals[i * 3 + 1] = vertex.Normal.Y;
            normals[i * 3 + 2] = vertex.Normal.Z;
        }

        return mesh;
    }

    private static void WriteTriangle(ArrayPoolBufferWriter<VertexData> buffer, Vector3 normal, params (Vector3 position, Vector2 uv)[] vertices) {

        var span = buffer.GetSpan(vertices.Length);

        for (var i = 0; i < vertices.Length; i++)
            span[i] = new VertexData(vertices[i].position, vertices[i].uv, normal);

        buffer.Advance(vertices.Length);
    }

    private readonly record struct VertexData(Vector3 Position, Vector2 Uv, Vector3 Normal);

    private sealed class MaterialResource {
        
        public required Material Material;
        public required int CameraPositionLocation;
        public required bool UsesCubemap;
    }
}
