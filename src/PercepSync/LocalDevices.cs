namespace Sled.PercepSync
{
    using Microsoft.Psi;
    using Microsoft.Psi.Interop.Rendezvous;
    using Microsoft.Psi.Media;
    using Microsoft.Psi.Imaging;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Interop.Transport;
    using HoloLensCaptureInterop;

    public class LocalDevicesCapture : IDisposable
    {
        public static readonly string WebcamAddress = "inproc://local-devices-webcam";
        public static readonly string WebcamTopic = "webcam";
        public static readonly string AudioAddress = "inproc://local-devices-audio";
        public static readonly string AudioTopic = "audio";
        private readonly RendezvousClient rdzvClient;
        private readonly Pipeline pipeline;
        private readonly NetMQWriter<Shared<Image>> mqWebcamWriter;
        private readonly NetMQWriter<AudioBuffer> mqAudioWriter;

        public LocalDevicesCapture(
            string serverAddress,
            int serverPort,
            string cameraDeviceID,
            string audioDeviceName
        )
        {
            pipeline = Pipeline.Create();
            rdzvClient = new RendezvousClient(serverAddress, port: serverPort);

            // Create the webcam component
#if NET7_0
            var webcam = new MediaCapture(
                pipeline,
                640,
                480,
                cameraDeviceID,
                PixelFormatId.YUYV
            ).Select((image) => Shared.Create(image.Resource.Convert(PixelFormat.RGB_24bpp)));
#else
            var webcam = new MediaCapture(pipeline, 640, 480).Out;
#endif

            // Create the audio capture component
#if NET7_0
            var audio = new AudioCapture(
                pipeline,
                new AudioCaptureConfiguration
                {
                    DeviceName = audioDeviceName,
                    Format = WaveFormat.Create16kHz1Channel16BitPcm()
                }
            ).Out;
#else
            var audio = new AudioCapture(pipeline, WaveFormat.Create16kHz1Channel16BitPcm()).Out;
#endif
            // NOTE: We can't use RemoteExporter here b/c \psi uses named memory mapped files
            // to serialize complex types, e.g., RawPixelImage, but named memory mapped files
            // are not supported on *nix systems.
            // https://github.com/dotnet/runtime/issues/21863
            mqWebcamWriter = new NetMQWriter<Shared<Image>>(
                pipeline,
                WebcamTopic,
                WebcamAddress,
                Serializers.SharedImageFormat(),
                name: nameof(mqWebcamWriter)
            );
            webcam.PipeTo(mqWebcamWriter, deliveryPolicy: DeliveryPolicy.LatestMessage);
            mqAudioWriter = new NetMQWriter<AudioBuffer>(
                pipeline,
                AudioTopic,
                AudioAddress,
                Serializers.AudioBufferFormat(),
                name: nameof(mqAudioWriter)
            );
            audio.PipeTo(mqAudioWriter, deliveryPolicy: DeliveryPolicy.LatestMessage);
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
                        new[]
                        {
                            mqWebcamWriter.ToRendezvousEndpoint(),
                            mqAudioWriter.ToRendezvousEndpoint()
                        }
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
