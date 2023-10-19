// Copied with modifications from: https://github.com/microsoft/psi/blob/ef4b2a627ffefc5415b826930a883b309c0a37cd/Sources/MixedReality/Microsoft.Psi.MixedReality/StereoKit/SpatialSound.cs
// and https://github.com/microsoft/psi/blob/ef4b2a627ffefc5415b826930a883b309c0a37cd/Sources/MixedReality/Microsoft.Psi.MixedReality/StereoKit/StereoKitTransforms.cs
// We need to override the private property "sound", so the only way around it is to copy like this.
namespace Sled.PercepSyncHoloLensCapture
{
    using System;
    using System.IO;
    using MathNet.Spatial.Euclidean;
    using StereoKit;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi;
    using Microsoft.Psi.MixedReality.StereoKit;

    /// <summary>
    /// Static StereoKit transforms which are applied in/out of StereoKit from \psi.
    /// </summary>
    public static class StereoKitTransforms
    {
        /// <summary>
        /// Gets the "world hierarchy" for rendering.
        /// Push this matrix onto StereoKit's <see cref="Hierarchy"/> stack to render content coherently in the world.
        /// </summary>
        /// <remarks>
        /// This matrix is pushed automatically by the <see cref="StereoKitRenderer"/> base class for new rendering components.
        /// The value is null when the HoloLens loses localization.
        /// </remarks>
        public static Matrix? WorldHierarchy { get; internal set; } = Matrix.Identity;

        /// <summary>
        /// Gets or sets the transform from StereoKit to the world.
        /// </summary>
        internal static CoordinateSystem StereoKitToWorld { get; set; } = new CoordinateSystem();

        /// <summary>
        /// Gets or sets the the transform from the world to StereoKit.
        /// </summary>
        internal static CoordinateSystem WorldToStereoKit { get; set; } = new CoordinateSystem();
    }

    /// <summary>
    /// Component that implements a spatial sound renderer.
    /// </summary>
    public class SpatialSound : StereoKitComponent, IConsumer<AudioBuffer>
    {
        private Sound sound;
        private SoundInst soundInst;
        private Point3D worldPosition;
        private float volume;
        private float bufferDurationInSeconds;
        private bool playing = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpatialSound"/> class.
        /// </summary>
        /// <param name="pipeline">The pipeline to add the component to.</param>
        /// <param name="initialPosition">Initial position of spatial sound.</param>
        /// <param name="bufferDurationInSeconds">The duration of stream buffer.</param>
        /// <param name="initialVolume">Initial audio volume (0-1, default 1).</param>
        /// <param name="name">An optional name for the component.</param>
        public SpatialSound(
            Pipeline pipeline,
            Point3D initialPosition,
            double bufferDurationInSeconds,
            double initialVolume = 1,
            string name = nameof(SpatialSound)
        )
            : base(pipeline, name)
        {
            worldPosition = initialPosition;
            volume = (float)initialVolume;
            this.bufferDurationInSeconds = (float)bufferDurationInSeconds;
            In = pipeline.CreateReceiver<AudioBuffer>(this, UpdateAudio, nameof(In));
            PositionInput = pipeline.CreateReceiver<Point3D>(
                this,
                p => worldPosition = p,
                nameof(PositionInput)
            );
            VolumeInput = pipeline.CreateReceiver<double>(
                this,
                v => volume = (float)v,
                nameof(VolumeInput)
            );
        }

        /// <summary>
        /// Gets the receiver of audio.
        /// </summary>
        public Receiver<AudioBuffer> In { get; private set; }

        /// <summary>
        /// Gets receiver for spatial pose.
        /// </summary>
        public Receiver<Point3D> PositionInput { get; private set; }

        /// <summary>
        /// Gets receiver for audio volume.
        /// </summary>
        public Receiver<double> VolumeInput { get; private set; }

        /// <inheritdoc />
        public override bool Initialize()
        {
            sound = Sound.CreateStream(bufferDurationInSeconds);
            return true;
        }

        /// <inheritdoc/>
        public override void Step()
        {
            if (playing)
            {
                soundInst.Volume = volume;

                if (StereoKitTransforms.WorldToStereoKit is not null)
                {
                    soundInst.Position = ComputeSoundPosition();
                }
            }
        }

        private Vec3 ComputeSoundPosition()
        {
            if (StereoKitTransforms.WorldToStereoKit is null)
            {
                return Vec3.Zero;
            }
            else
            {
                return worldPosition.TransformBy(StereoKitTransforms.WorldToStereoKit).ToVec3();
            }
        }

        private void UpdateAudio(AudioBuffer audio)
        {
            var format = audio.Format;
            if (
                format.Channels != 1
                || format.SamplesPerSec != 48000
                || (
                    format.FormatTag != WaveFormatTag.WAVE_FORMAT_IEEE_FLOAT
                    && format.FormatTag != WaveFormatTag.WAVE_FORMAT_EXTENSIBLE
                )
                || format.BitsPerSample != 32
            )
            {
                throw new ArgumentException("Expected 1-channel, 48kHz, float32 audio format.");
            }

            using var stream = new MemoryStream(audio.Data, 0, audio.Length);
            using var reader = new BinaryReader(stream);
            var count = audio.Length / 4;
            var samples = new float[count];
            for (var i = 0; i < count; i++)
            {
                samples[i] = reader.ReadSingle();
            }

            sound.WriteSamples(samples);
            if (!playing)
            {
                soundInst = sound.Play(ComputeSoundPosition(), volume);
                playing = true;
            }
        }
    }
}
