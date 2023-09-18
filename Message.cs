namespace Sled.PercepSync
{
    using MessagePack;

    [MessagePackObject]
    public abstract class Message
    {
        [Key("originating_time")]
        public DateTime OriginatingTime { get; set; }
    }

    [MessagePackObject]
    public class ImageMessage : Message
    {
        [Key("pixel_data")]
        public byte[]? PixelData { get; set; } = null;

        [Key("width")]
        public int Width { get; set; }

        [Key("height")]
        public int Height { get; set; }
    }
}
