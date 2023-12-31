namespace Sled.PercepSync
{
    using Microsoft.Psi;
    using NetMQ.Sockets;
    using NetMQ;
    using Microsoft.Psi.Components;
    using MessagePack;
    using Microsoft.CognitiveServices.Speech;
    using Microsoft.Psi.Audio;

    public class TtsRequestReceiver : IDisposable, IProducer<TtsRequest>, ISourceComponent
    {
        private readonly string address;
        private PullSocket? socket;
        private NetMQPoller? poller;
        private readonly Pipeline pipeline;

        public TtsRequestReceiver(Pipeline pipeline, string address)
        {
            this.pipeline = pipeline;
            this.address = address;
            Out = pipeline.CreateEmitter<TtsRequest>(this, nameof(Out));
        }

        public void Start(Action<DateTime> notifyCompletionTime)
        {
            // notify that this is an infinite source component
            notifyCompletionTime(DateTime.MaxValue);

            socket = new PullSocket(address);
            socket.ReceiveReady += ReceiveReady;
            poller = new NetMQPoller { socket };
            poller.RunAsync();
        }

        public void Stop(DateTime finalOriginatingTime, Action notifyCompleted)
        {
            Stop();
            notifyCompleted();
        }

        private void Stop()
        {
            if (socket != null)
            {
                poller?.Dispose();
                socket.Dispose();
                socket = null;
            }
        }

        public Emitter<TtsRequest> Out { get; }

        public void Dispose()
        {
            Stop();
        }

        private void ReceiveReady(object? sender, NetMQSocketEventArgs e)
        {
            if (socket is null)
            {
                throw new Exception("Socket has not been initialized");
            }
            while (socket.TryReceiveFrameBytes(out byte[]? frame))
            {
                if (frame is null)
                {
                    continue;
                }
                var request = MessagePackSerializer.Deserialize<TtsRequest>(frame);
                Out.Post(request, pipeline.GetCurrentTime());
            }
        }
    }

    public class AzureSpeechSynthesizer : IConsumerProducer<TtsRequest, AudioBuffer>, IDisposable
    {
        private readonly string subscriptionKey;
        private readonly string region;
        private readonly string voiceName;
        private SpeechSynthesizer speechSynthesizer;
        private Reframe reframer;

        public AzureSpeechSynthesizer(
            Pipeline pipeline,
            string subscriptionKey,
            string region,
            string voiceName,
            int audioBufferFrameSizeInBytes
        )
        {
            this.subscriptionKey = subscriptionKey;
            this.region = region;
            this.voiceName = voiceName;

            var speechConfig = SpeechConfig.FromSubscription(this.subscriptionKey, this.region);
            speechConfig.SpeechSynthesisVoiceName = this.voiceName;
            speechConfig.SetSpeechSynthesisOutputFormat(
                SpeechSynthesisOutputFormat.Raw16Khz16BitMonoPcm
            );
            speechSynthesizer = new SpeechSynthesizer(speechConfig, null);
            In = pipeline.CreateReceiver<TtsRequest>(this, Receive, nameof(In));
            audioOut = pipeline.CreateEmitter<AudioBuffer>(this, nameof(audioOut));
            reframer = new Reframe(pipeline, audioBufferFrameSizeInBytes);
            audioOut.PipeTo(reframer);
            Out = reframer.Out;
        }

        private async void Receive(TtsRequest req, Envelope envelope)
        {
            var result = await speechSynthesizer.SpeakTextAsync(req.Text);
            if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                var reason = $"Speech synthesis cancelled: {cancellation.Reason}";
                Console.WriteLine($"CANCELED: Reason={cancellation.Reason}");

                if (cancellation.Reason == CancellationReason.Error)
                {
                    throw new Exception(
                        $"{reason}, ErrorCode={cancellation.ErrorCode}, ErrorDetails=[{cancellation.ErrorDetails}]"
                    );
                }
                else
                {
                    throw new Exception(reason);
                }
            }
            using var stream = AudioDataStream.FromResult(result);
            using var memoryStream = new MemoryStream();
            byte[] buffer = new byte[1024];
            uint bytesRead;
            while ((bytesRead = stream.ReadData(buffer)) > 0)
            {
                memoryStream.Write(buffer, 0, (int)bytesRead);
            }
            var audioBuffer = new AudioBuffer(
                memoryStream.ToArray(),
                WaveFormat.Create16kHz1Channel16BitPcm()
            );
            audioOut.Post(audioBuffer, envelope.OriginatingTime);
        }

        public Receiver<TtsRequest> In { get; }
        public Emitter<AudioBuffer> Out { get; private set; }
        private Emitter<AudioBuffer> audioOut { get; set; }

        public void Dispose()
        {
            speechSynthesizer.Dispose();
        }
    }
}
