// Copied selectively from https://github.com/microsoft/psi/blob/master/Sources/MixedReality/HoloLensCapture/HoloLensCaptureInterop/Serializers.cs
namespace Sled.PercepSync
{
    using Microsoft.Psi;
    using Microsoft.Psi.Interop.Serialization;
    using Microsoft.Psi.Imaging;
    using Microsoft.Psi.Audio;

    /// <summary>
    /// Provides serializers and deserializers for the various mixed reality streams.
    /// </summary>
    public static class HoloLensSerializers
    {
        /// <summary>
        /// Write <see cref="bool"/> to <see cref="BinaryWriter"/>.
        /// </summary>
        /// <param name="boolean"><see cref="bool"/> to write.</param>
        /// <param name="writer"><see cref="BinaryWriter"/> to which to write.</param>
        public static void WriteBool(bool boolean, BinaryWriter writer) =>
            writer.Write((byte)(boolean ? 0xff : 0));

        /// <summary>
        /// Read <see cref="bool"/> from <see cref="BinaryReader"/>.
        /// </summary>
        /// <param name="reader"><see cref="BinaryReader"/> from which to read.</param>
        /// <returns><see cref="bool"/>.</returns>
        public static bool ReadBool(BinaryReader reader) => reader.ReadByte() == 0xff;

        /// <summary>
        /// Write <see cref="Shared{EncodedImage}"/> to <see cref="BinaryWriter"/>.
        /// </summary>
        /// <param name="sharedEncodedImage"><see cref="Shared{EncodedImage}"/> to write.</param>
        /// <param name="writer"><see cref="BinaryWriter"/> to which to write.</param>
        public static void WriteEncodedImage(
            Shared<EncodedImage>? sharedEncodedImage,
            BinaryWriter writer
        )
        {
            WriteBool(sharedEncodedImage != null, writer);
            if (sharedEncodedImage == null)
            {
                return;
            }

            var image = sharedEncodedImage.Resource;
            var data = image.GetBuffer();
            writer.Write(image.Width);
            writer.Write(image.Height);
            writer.Write((int)image.PixelFormat);
            writer.Write(image.Size);
            writer.Write(data, 0, image.Size);
        }

        /// <summary>
        /// Read <see cref="Shared{EncodedImage}"/> from <see cref="BinaryReader"/>.
        /// </summary>
        /// <param name="reader"><see cref="BinaryReader"/> from which to read.</param>
        /// <param name="payload">The payload of bytes.</param>
        /// <returns><see cref="Shared{EncodedImage}"/>.</returns>
        public static Shared<EncodedImage>? ReadEncodedImage(BinaryReader reader, byte[] payload)
        {
            if (!ReadBool(reader))
            {
                return null;
            }

            var width = reader.ReadInt32();
            var height = reader.ReadInt32();
            var pixelFormat = (PixelFormat)reader.ReadInt32();
            var size = reader.ReadInt32();
            var image = EncodedImagePool.GetOrCreate(width, height, pixelFormat);
            int position = (int)reader.BaseStream.Position;
            image.Resource.CopyFrom(payload, position, size);
            reader.BaseStream.Position = position + size;
            return image;
        }

        /// <summary>
        /// Format for <see cref="Shared{EncodedImage}"/>.
        /// </summary>
        /// <returns><see cref="Format{T}"/> of <see cref="Shared{EncodedImage}"/> serializer/deserializer.</returns>
        public static Format<Shared<EncodedImage>?> SharedEncodedImageFormat() =>
            new(WriteEncodedImage, (reader, payload, _, _) => ReadEncodedImage(reader, payload));

        private static readonly WaveFormat AssumedWaveFormat = WaveFormat.CreateIeeeFloat(48000, 1);

        /// <summary>
        /// Write <see cref="AudioBuffer"/> to <see cref="BinaryWriter"/>.
        /// </summary>
        /// <param name="audioBuffer"><see cref="AudioBuffer"/> to write.</param>
        /// <param name="writer"><see cref="BinaryWriter"/> to which to write.</param>
        public static void WriteAudioBuffer(AudioBuffer audioBuffer, BinaryWriter writer)
        {
            if (
                audioBuffer.Format.Channels != AssumedWaveFormat.Channels
                || audioBuffer.Format.FormatTag != AssumedWaveFormat.FormatTag
                || audioBuffer.Format.BitsPerSample != AssumedWaveFormat.BitsPerSample
                || audioBuffer.Format.SamplesPerSec != AssumedWaveFormat.SamplesPerSec
            )
            {
                throw new ArgumentException("Unexpected audio format.");
            }

            writer.Write(audioBuffer.Length);
            writer.Write(audioBuffer.Data);
        }

        /// <summary>
        /// Read <see cref="AudioBuffer"/> from <see cref="BinaryReader"/>.
        /// </summary>
        /// <param name="reader"><see cref="BinaryReader"/> from which to read.</param>
        /// <returns><see cref="AudioBuffer"/>.</returns>
        public static AudioBuffer ReadAudioBuffer(BinaryReader reader) =>
            new(reader.ReadBytes(reader.ReadInt32()), AssumedWaveFormat);

        /// <summary>
        /// Format for <see cref="AudioBuffer"/>.
        /// </summary>
        /// <returns><see cref="Format{AudioBuffer}"/> serializer/deserializer.</returns>
        public static Format<AudioBuffer> AudioBufferFormat() =>
            new(WriteAudioBuffer, ReadAudioBuffer);

        /// <summary>
        /// Write heartbeat of <see cref="ValueTuple{Single, Single}"/> to <see cref="BinaryWriter"/>.
        /// </summary>
        /// <param name="heartbeat"><see cref="ValueTuple{Single, Single}"/> to write.</param>
        /// <param name="writer"><see cref="BinaryWriter"/> to which to write.</param>
        public static void WriteHeartbeat(
            (float VideoFps, float DepthFps) heartbeat,
            BinaryWriter writer
        )
        {
            writer.Write(heartbeat.VideoFps);
            writer.Write(heartbeat.DepthFps);
        }

        /// <summary>
        /// Read heartbeat of <see cref="ValueTuple{Single, Single}"/> from <see cref="BinaryReader"/>.
        /// </summary>
        /// <param name="reader"><see cref="BinaryReader"/> from which to read.</param>
        /// <returns><see cref="ValueTuple{Single, Single}"/>.</returns>
        public static (float VideoFps, float DepthFps) ReadHeartbeat(BinaryReader reader)
        {
            var videoFps = reader.ReadSingle();
            var depthFps = reader.ReadSingle();
            return (videoFps, depthFps);
        }

        /// <summary>
        /// Format for heartbeat of <see cref="ValueTuple{Single, Single}"/>.
        /// </summary>
        /// <returns><see cref="Format{T}"/> of <see cref="ValueTuple{Single, Single}"/> serializer/deserializer.</returns>
        public static Format<(float VideoFps, float DepthFps)> HeartbeatFormat() =>
            new(WriteHeartbeat, ReadHeartbeat);
    }
}
