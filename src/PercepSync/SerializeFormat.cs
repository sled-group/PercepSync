namespace Sled.PercepSync
{
    using MessagePack;

    [MessagePackObject]
    public class Perception
    {
        [Key("frame")]
        public RawPixelImage Frame { get; set; }

        [Key("audio")]
        public Audio Audio { get; set; }

        [Key("transcribedText")]
        public TranscribedText TranscribedText { get; set; }

        public Perception(RawPixelImage frame, Audio audio, TranscribedText transcribedText)
        {
            Frame = frame;
            Audio = audio;
            TranscribedText = transcribedText;
        }
    }

    /// <summary>
    /// Represents a raw pixel image in BGRA-32.
    /// </summary>
    [MessagePackObject]
    public class RawPixelImage
    {
        [Key("pixelData")]
        public byte[] PixelData { get; set; }

        [Key("width")]
        public int Width { get; set; }

        [Key("height")]
        public int Height { get; set; }

        [Key("stride")]
        public int Stride { get; set; }

        public RawPixelImage(byte[] pixelData, int width, int height, int stride)
        {
            PixelData = pixelData;
            Width = width;
            Height = height;
            Stride = stride;
        }
    }

    /// <summary>
    /// Represents a piece of transcribed text.
    /// </summary>
    [MessagePackObject]
    public class TranscribedText
    {
        [Key("text")]
        public string Text { get; set; }

        public TranscribedText(string text)
        {
            Text = text;
        }
    }

    /// <summary>
    /// Represents an audio buffer whose encoding format is 16KHz, 1 channel, 16-bit PCM
    /// </summary>
    [MessagePackObject]
    public class Audio
    {
        [Key("buffer")]
        public byte[] Buffer { get; set; }

        public Audio(byte[] buffer)
        {
            Buffer = buffer;
        }
    }

    /// <summary>
    /// Represents a text-to-speech request from a client
    /// </summary>
    [MessagePackObject]
    public class TtsRequest
    {
        [Key("text")]
        public string Text { get; set; }

        public TtsRequest(string text)
        {
            Text = text;
        }
    }
}
