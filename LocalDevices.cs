namespace Sled.PercepSync
{
    using Microsoft.Psi;
    using Microsoft.Psi.Interop.Rendezvous;
    using Microsoft.Psi.Media;
    using Microsoft.Psi.Imaging;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Interop.Transport;
    using Microsoft.Psi.Interop.Format;

    public class LocalDevicesCapture : IDisposable
    {
        public static readonly string WebcamTopic = "webcam";
        public static readonly string AudioTopic = "audio";
        private readonly RendezvousClient rdzvClient;
        private readonly Pipeline pipeline;
        private readonly NetMQWriter mqWriter;

        public LocalDevicesCapture(
            string serverAddress,
            int serverPort,
            string cameraDeviceID,
            string audioDeviceName,
            string mqAddress = "inproc://local-devices-capture"
        )
        {
            pipeline = Pipeline.Create();
            rdzvClient = new RendezvousClient(serverAddress, port: serverPort);

            // Create the webcam component
            var webcam = new MediaCapture(pipeline, 640, 480, cameraDeviceID, PixelFormatId.YUYV);
            var serializedWebcam = webcam.Select(
                (image) =>
                {
                    var rgb24Image = image.Resource.Convert(PixelFormat.RGB_24bpp);
                    var pixelData = new byte[rgb24Image.Size];
                    rgb24Image.CopyTo(pixelData);
                    return new RawPixelImage(
                        pixelData,
                        image.Resource.Width,
                        image.Resource.Height,
                        image.Resource.Stride
                    );
                }
            );

            // Create the audio capture component
            var audio = new AudioCapture(
                pipeline,
                new AudioCaptureConfiguration
                {
                    DeviceName = audioDeviceName,
                    Format = WaveFormat.Create16kHz1Channel16BitPcm()
                }
            );
            var serializedAudio = audio.Select((buffer) => new Audio(buffer.Data));

            // NOTE: We can't use RemoteExporter here b/c \psi uses named memory mapped files
            // to serialize complex types, e.g., RawPixelImage, but named memory mapped files
            // are not supported on *nix systems.
            // https://github.com/dotnet/runtime/issues/21863
            mqWriter = new NetMQWriter(pipeline, mqAddress, MessagePackFormat.Instance);
            serializedWebcam.PipeTo(
                mqWriter.AddTopic<RawPixelImage>(WebcamTopic),
                deliveryPolicy: DeliveryPolicy.LatestMessage
            );
            serializedAudio.PipeTo(
                mqWriter.AddTopic<Audio>(AudioTopic),
                deliveryPolicy: DeliveryPolicy.LatestMessage
            );
        }

        public void Start()
        {
            rdzvClient.Start();

            if (rdzvClient.IsActive)
            {
                rdzvClient.Connected.WaitOne();
                rdzvClient.Rendezvous.TryAddProcess(
                    new Rendezvous.Process(
                        nameof(LocalDevicesCapture),
                        new[] { mqWriter.ToRendezvousEndpoint() }
                    )
                );
                pipeline.RunAsync();
            }
        }

        public void Stop()
        {
            rdzvClient.Rendezvous.TryRemoveProcess(nameof(LocalDevicesCapture));
            rdzvClient.Dispose();
            pipeline.Dispose();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
