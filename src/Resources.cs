using Raylib_cs;
using static Raylib_cs.Raylib;

internal static class Resources {
    
    private static readonly Dictionary<string, object> Library = [];
    private static readonly Dictionary<string, string[]> FileLists = [];
    
    public static string FindResourceFile(params string[] relativePathParts) {

        relativePathParts = relativePathParts.Prepend("Resources").ToArray();
        
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null) {

            var candidate = Path.Combine([current.FullName, .. relativePathParts]);

            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new FileNotFoundException($"Resource not found: {Path.Combine(relativePathParts)}");
    }

    public static T GetResource<T>(params string[] relativePathParts) {
        
        if (typeof(T) == typeof(Texture2D)) {
            
            var path = FindResourceFile(relativePathParts);

            if (Library.TryGetValue(path, out var resource))
                return (T)resource;

            Library[path] = LoadTexture(path);
            return (T)Library[path];
        }

        if (typeof(T) == typeof(Shader)) {

            var shaderName = Path.GetFileNameWithoutExtension(relativePathParts[^1]);
            var key = Path.Combine(["shader", shaderName]);

            if (Library.TryGetValue(key, out var resource))
                return (T)resource;

            var vertexPath = FindResourceFile("shader", $"{shaderName}.vs");
            var fragmentPath = FindResourceFile("shader", $"{shaderName}.fs");

            Library[key] = LoadShader(vertexPath, fragmentPath);
            return (T)Library[key];
        }

        throw new FileNotFoundException($"Unsupported resource type: {typeof(T).Name}");
    }

    public static unsafe Texture2D GetCubemap(params string[] relativePathParts) {

        var path = FindResourceFile(relativePathParts);
        var key = $"{path}#cubemap";

        if (Library.TryGetValue(key, out var resource))
            return (Texture2D)resource;

        var image = LoadImage(path);

        if (image.Data == null)
            throw new FileNotFoundException($"Cubemap image could not be loaded: {path}");

        var cubemap = LoadTextureCubemap(image, CubemapLayout.AutoDetect);
        UnloadImage(image);

        Library[key] = cubemap;
        return (Texture2D)Library[key];
    }

    public static string[] GetResourceFiles(params string[] relativePathParts) {

        var directory = FindResourceDirectory(relativePathParts);

        if (FileLists.TryGetValue(directory, out var files))
            return files;

        files = Directory.GetFiles(directory)
            .Select(Path.GetFileName)
            .Where(file => !string.IsNullOrWhiteSpace(file))
            .Cast<string>()
            .OrderBy(file => file)
            .ToArray();

        FileLists[directory] = files;
        return files;
    }

    public static string FindResourceDirectory(params string[] relativePathParts) {

        relativePathParts = relativePathParts.Prepend("Resources").ToArray();

        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null) {

            var candidate = Path.Combine([current.FullName, .. relativePathParts]);

            if (Directory.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException($"Resource directory not found: {Path.Combine(relativePathParts)}");
    }

    public static bool HasResourceFile(params string[] relativePathParts) {

        relativePathParts = relativePathParts.Prepend("Resources").ToArray();

        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null) {

            var candidate = Path.Combine([current.FullName, .. relativePathParts]);

            if (File.Exists(candidate))
                return true;

            current = current.Parent;
        }

        return false;
    }
}
