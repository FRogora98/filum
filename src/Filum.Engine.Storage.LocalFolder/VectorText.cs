namespace Filum.Engine.Storage.LocalFolder;

// Compact: base64 of the floats.
internal static class VectorText
{
    public static string ToText(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return Convert.ToBase64String(bytes);
    }

    public static float[] FromText(string text)
    {
        var bytes = Convert.FromBase64String(text);
        var vector = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
        return vector;
    }
}
