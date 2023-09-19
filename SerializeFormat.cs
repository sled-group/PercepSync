namespace Sled.PercepSync
{
    using MessagePack;

    /// <summary>
    /// Represents a raw pixel image in BGRA-32.
    /// </summary>
    [MessagePackObject(keyAsPropertyName: true)]
    public class RawPixelImage
    {
        public byte[]? pixelData { get; set; } = null;

        public int width { get; set; }

        public int height { get; set; }
    }
}
