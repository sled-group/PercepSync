namespace Sled.PercepSync
{
    using MessagePack;

    /// <summary>
    /// Represents a raw pixel image in BGRA-32.
    /// </summary>
    [MessagePackObject(keyAsPropertyName: true)]
    public class RawPixelImage
    {
        public byte[] pixelData { get; set; }

        public int width { get; set; }

        public int height { get; set; }
        public int stride { get; set; }

        public RawPixelImage(byte[] pixelData, int width, int height, int stride)
        {
            this.pixelData = pixelData;
            this.width = width;
            this.height = height;
            this.stride = stride;
        }
    }

    /// <summary>
    /// Represents an audio buffer whose encoding format is 16KHz, 1 channel, 16-bit PCM
    /// </summary>
    [MessagePackObject(keyAsPropertyName: true)]
    public class Audio
    {
        public byte[] buffer { get; set; }

        public Audio(byte[] buffer)
        {
            this.buffer = buffer;
        }
    }

    /// <summary>
    /// Represents a text-to-speech request from a client
    /// </summary>
    [MessagePackObject(keyAsPropertyName: true)]
    public class TtsRequest
    {
        public string text { get; set; }

        public TtsRequest(string text)
        {
            this.text = text;
        }
    }

    /// <summary>
    /// Represents text-to-speech audio data
    /// </summary>
    [MessagePackObject(keyAsPropertyName: true)]
    public class TtsAudio
    {
        public Audio audio { get; set; }

        public TtsAudio(Audio audio)
        {
            this.audio = audio;
        }
    }
}
