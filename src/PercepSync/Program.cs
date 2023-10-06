namespace Sled.PercepSync
{
    using System.IO;
    using System.Reflection;
    using Microsoft.Psi;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Interop.Transport;
    using Microsoft.Psi.Interop.Format;
    using Microsoft.Psi.Interop.Rendezvous;
    using Microsoft.Psi.Imaging;
    using System.CommandLine;
    using HoloLensCaptureInterop;

    /// <summary>
    /// PercepSync synchronizes streams of data from different perceptions and broadcast them.
    /// </summary>
    public class Program
    {
        private static RendezvousServer? rendezvousServer;
        private static Pipeline? percepSyncPipeline;
        private static Preview? preview;
        private static LocalDevicesCapture? localDevicesCapture;
        private static readonly ManualResetEventSlim clientConnectedEvent = new();

        private const string VideoFrameTopic = "videoFrame";
        private const string AudioTopic = "audio";
        private const string HoloLensCaptureServerProcessName = "HoloLensCaptureServer"; // HoloLensCaptureApp expects this. Should be changed later
        private const string PercepSyncHoloLensCaptureProcessName = "PercepSyncHoloLensCapture";
        private const string HoloLensCaptureAppVersion = "v1";
        private const string HoloLensVideoStreamName = "VideoEncodedImageCameraView";
        private const string HoloLensAudioStreamName = "Audio";

        public static void Main(string[] args)
        {
            var rootCommand = new RootCommand("PercepSync");
            var zeroMQPubAddressOption = new Option<string>(
                name: "--zeromq-pub-address",
                description: "Address for ZeroMQ publish socket",
                getDefaultValue: () => "tcp://*:12345"
            );
            rootCommand.AddOption(zeroMQPubAddressOption);
            var enablePreviewOption = new Option<bool>(
                name: "--enable-preview",
                description: "Whether to enable preview or not. Only works if you have a display.",
                getDefaultValue: () => false
            );
            rootCommand.AddOption(enablePreviewOption);
            var rdzvServerPortOption = new Option<int>(
                name: "--rdzv-server-port",
                description: "Rendezvous server port",
                getDefaultValue: () => 13331
            );
            rootCommand.AddOption(rdzvServerPortOption);

            var localCommand = new Command("local", description: "Use local devices");
            var localCameraDeviceIDOption = new Option<string>(
                name: "--camera-device-id",
                description: "Camera device ID",
                getDefaultValue: () => "/dev/video0"
            );
            localCommand.Add(localCameraDeviceIDOption);
            var localAudioDeviceNameOption = new Option<string>(
                name: "--audio-device-name",
                description: "Audio device name",
                getDefaultValue: () => "plughw:0,0"
            );
            localCommand.Add(localAudioDeviceNameOption);
            rootCommand.Add(localCommand);
            localCommand.SetHandler(
                (
                    zeroMQPubAddress,
                    enablePreview,
                    rdzvServerPort,
                    localCameraDeviceID,
                    localAudioDeviceName
                ) =>
                {
                    RunPercepSync(
                        zeroMQPubAddress,
                        enablePreview,
                        rdzvServerPort,
                        nameof(LocalDevicesCapture),
                        ConstructLocalSensorStreams,
                        localDevicesCapture: localDevicesCapture = new LocalDevicesCapture(
                            "127.0.0.1",
                            rdzvServerPort,
                            localCameraDeviceID,
                            localAudioDeviceName
                        )
                    );
                },
                zeroMQPubAddressOption,
                enablePreviewOption,
                rdzvServerPortOption,
                localCameraDeviceIDOption,
                localAudioDeviceNameOption
            );

            var hololensCommand = new Command("hololens", description: "Use HoloLens");
            rootCommand.Add(hololensCommand);
            hololensCommand.SetHandler(
                (zeroMQPubAddress, enablePreview, rdzvServerPort) =>
                {
                    RunPercepSync(
                        zeroMQPubAddress,
                        enablePreview,
                        rdzvServerPort,
                        PercepSyncHoloLensCaptureProcessName,
                        ConstructHoloLensSensorStreams,
                        targetProcessVersion: HoloLensCaptureAppVersion
                    );
                },
                zeroMQPubAddressOption,
                enablePreviewOption,
                rdzvServerPortOption
            );
            rootCommand.Invoke(args);
        }

        private static void RunPercepSync(
            string zeroMQPubAddress,
            bool enablePreview,
            int rdzvServerPort,
            string targetProcessName,
            Func<Rendezvous.Process, SensorStreams> ConstructSensorStreams,
            string? targetProcessVersion = null,
            LocalDevicesCapture? localDevicesCapture = null
        )
        {
            if (enablePreview)
            {
                Console.WriteLine("Initializing GTK Application");
                Gtk.Application.Init();
                InitializeCssStyles();
            }
            rendezvousServer = new RendezvousServer(rdzvServerPort);
            rendezvousServer.Rendezvous.ProcessRemoved += (_, process) =>
            {
                ReportProcessRemoved(process);
                if (process.Name == PercepSyncHoloLensCaptureProcessName)
                {
                    // NOTE: We don't want to do this for local device capture
                    // since if that's been removed, the whole PercepSync is also
                    // going down, which causes some race conditions when cleaning up.
                    StopPercepSyncPipeline("Client stopped recording");
                }
            };
            rendezvousServer.Error += (_, ex) =>
            {
                Console.WriteLine();
                StopPercepSyncPipeline($"RENDEZVOUS ERROR: {ex.Message}");
            };
            rendezvousServer.Rendezvous.ProcessAdded += (_, process) =>
            {
                ReportProcessAdded(process);
                if (process.Name == targetProcessName)
                {
                    if (targetProcessVersion is not null && process.Version != targetProcessVersion)
                    {
                        throw new Exception(
                            $"Connection received from unexpected version of {process.Name} (expected {targetProcessVersion}, actual {process.Version})."
                        );
                    }
                    Console.WriteLine($"Starting PercepSync pipeline for {process.Name}");
                    percepSyncPipeline = Pipeline.Create();
                    percepSyncPipeline.PipelineExceptionNotHandled += (_, args) =>
                    {
                        Console.WriteLine(
                            $"SERVER PIPELINE RUNTIME EXCEPTION: {args.Exception.Message}"
                        );
                    };
                    var mqWriter = new NetMQWriter(
                        percepSyncPipeline,
                        zeroMQPubAddress,
                        MessagePackFormat.Instance
                    );
                    var sensorStreams = ConstructSensorStreams(process);
                    sensorStreams.VideoFrameStream.PipeTo(
                        mqWriter.AddTopic<RawPixelImage>(VideoFrameTopic)
                    );
                    sensorStreams.AudioStream.PipeTo(mqWriter.AddTopic<Audio>(AudioTopic));
                    RunPercepSyncPipeline(sensorStreams, enablePreview);
                }
            };
            rendezvousServer.Start();
            if (localDevicesCapture is not null)
            {
                localDevicesCapture.Start();
            }
            RunCliLoop(enablePreview);
        }

        private static void RunPercepSyncPipeline(SensorStreams sensorStreams, bool enablePreview)
        {
            if (percepSyncPipeline is null)
            {
                throw new Exception("Pipeline has not been initialized.");
            }

            if (enablePreview)
            {
                // Connect sensor streams to Preview
                preview = new Preview(percepSyncPipeline);
                var acousticFeatures = new AcousticFeaturesExtractor(percepSyncPipeline);
                sensorStreams.AudioStream
                    .Select(
                        (audio) =>
                            new AudioBuffer(audio.buffer, WaveFormat.Create16kHz1Channel16BitPcm())
                    )
                    .PipeTo(acousticFeatures);
                sensorStreams.VideoFrameStream
                    .Join(acousticFeatures.LogEnergy, RelativeTimeInterval.Past())
                    .Select((data) => new DisplayInput(data.Item1, data.Item2))
                    .PipeTo(preview);
            }

            // Run the pipeline
            percepSyncPipeline.RunAsync();

            // Notify that the client has connected and the pipeline is running
            clientConnectedEvent.Set();
        }

        private static void RunCliLoop(bool enablePreview)
        {
            Console.Write("Waiting for a client to connect ...");
            clientConnectedEvent.Wait();
            Console.WriteLine("Done.");
            Console.WriteLine("Press Q or ENTER key to exit.");
            if (enablePreview)
            {
                Console.WriteLine("Press V to start VideoPlayer.");
                GLib.Idle.Add(new GLib.IdleHandler(() => RunCliIteration(enablePreview)));
                Gtk.Application.Run();
            }
            else
            {
                while (true)
                {
                    RunCliIteration(enablePreview);
                }
            }
        }

        private static void ReportProcessAdded(Rendezvous.Process process)
        {
            Console.WriteLine();
            Console.WriteLine($"PROCESS ADDED: {process.Name}");
            foreach (var endpoint in process.Endpoints)
            {
                if (endpoint is Rendezvous.TcpSourceEndpoint tcpEndpoint)
                {
                    Console.WriteLine($"  ENDPOINT: TCP {tcpEndpoint.Host} {tcpEndpoint.Port}");
                }
                else if (endpoint is Rendezvous.NetMQSourceEndpoint netMQEndpoint)
                {
                    Console.WriteLine($"  ENDPOINT: NetMQ {netMQEndpoint.Address}");
                }
                else if (endpoint is Rendezvous.RemoteExporterEndpoint remoteExporterEndpoint)
                {
                    Console.WriteLine(
                        $"  ENDPOINT: Remote {remoteExporterEndpoint.Host} {remoteExporterEndpoint.Port} {remoteExporterEndpoint.Transport}"
                    );
                }
                else if (
                    endpoint is Rendezvous.RemoteClockExporterEndpoint remoteClockExporterEndpoint
                )
                {
                    Console.WriteLine(
                        $"  ENDPOINT: Remote Clock {remoteClockExporterEndpoint.Host} {remoteClockExporterEndpoint.Port}"
                    );
                }
                else
                {
                    throw new ArgumentException(
                        $"Unknown type of Endpoint ({endpoint.GetType().Name})."
                    );
                }

                foreach (var stream in endpoint.Streams)
                {
                    Console.WriteLine(
                        $"    STREAM: {stream.StreamName} ({stream.TypeName.Split(',')[0]})"
                    );
                }
            }
        }

        private static void ReportProcessRemoved(Rendezvous.Process process)
        {
            Console.WriteLine();
            Console.WriteLine($"PROCESS REMOVED: {process.Name}");
        }

        private static bool RunCliIteration(bool enablePreview)
        {
            if (Console.KeyAvailable)
            {
                switch (Console.ReadKey(true).Key)
                {
                    case ConsoleKey.Q:
                    case ConsoleKey.Enter:
                        StopPercepSyncPipeline("PercepSync manually stopped");
                        Environment.Exit(0);
                        break;
                    case ConsoleKey.V:
                        if (enablePreview)
                        {
                            if (percepSyncPipeline is null)
                            {
                                throw new Exception("Pipeline has not been initialized.");
                            }
                            if (preview is null)
                            {
                                throw new Exception("Preview has not been initialized.");
                            }
                            preview.ShowAll();
                        }
                        break;
                }
            }
            return true;
        }

        private static SensorStreams ConstructLocalSensorStreams(
            Rendezvous.Process inputRendezvousProcess
        )
        {
            IProducer<RawPixelImage>? serializedWebcam = null;
            IProducer<Audio>? serializedAudio = null;
            foreach (var endpoint in inputRendezvousProcess.Endpoints)
            {
                if (
                    endpoint is Rendezvous.NetMQSourceEndpoint mqSourceEndpoint
                    && mqSourceEndpoint is not null
                )
                {
                    foreach (var stream in mqSourceEndpoint.Streams)
                    {
                        // NOTE: MessagePackFormat is not generic and deserializes things as dynamic,
                        // so we need to manually construct things.
                        var deserializedSourceEndpoint = mqSourceEndpoint.ToNetMQSource<dynamic>(
                            percepSyncPipeline,
                            stream.StreamName,
                            MessagePackFormat.Instance
                        );
                        if (stream.StreamName == LocalDevicesCapture.WebcamTopic)
                        {
                            serializedWebcam = deserializedSourceEndpoint.Select(
                                (data) =>
                                    new RawPixelImage(
                                        data.pixelData,
                                        data.width,
                                        data.height,
                                        data.stride
                                    )
                            );
                        }
                        else if (stream.StreamName == LocalDevicesCapture.AudioTopic)
                        {
                            serializedAudio = deserializedSourceEndpoint.Select(
                                (data) => new Audio(data.buffer)
                            );
                        }
                    }
                }
            }
            if (serializedWebcam is null)
            {
                throw new Exception(
                    "Failed to construct the video frame sensor stream from local devices."
                );
            }
            if (serializedAudio is null)
            {
                throw new Exception(
                    "Failed to construct the audio sensor stream from local devices."
                );
            }
            return new SensorStreams(serializedWebcam, serializedAudio);
        }

        private static SensorStreams ConstructHoloLensSensorStreams(
            Rendezvous.Process inputRendezvousProcess
        )
        {
            if (rendezvousServer is null)
            {
                throw new Exception("Rendezvous server has not been initialized.");
            }

            // First connect to remote clock on the client app to synchronize clocks
            foreach (var endpoint in inputRendezvousProcess.Endpoints)
            {
                if (endpoint is Rendezvous.RemoteClockExporterEndpoint remoteClockExporterEndpoint)
                {
                    var remoteClock = remoteClockExporterEndpoint.ToRemoteClockImporter(
                        percepSyncPipeline
                    );
                    Console.Write("    Connecting to clock sync ...");
                    if (!remoteClock.Connected.WaitOne(10000))
                    {
                        Console.WriteLine("FAILED.");
                        throw new Exception("Failed to connect to remote clock exporter.");
                    }

                    Console.WriteLine("DONE.");
                }
            }

            IProducer<RawPixelImage>? videoFrameStream = null;
            IProducer<Audio>? audioStream = null;
            foreach (var endpoint in inputRendezvousProcess.Endpoints)
            {
                if (
                    endpoint is Rendezvous.TcpSourceEndpoint tcpEndpoint
                    && tcpEndpoint.Stream is not null
                )
                {
                    if (tcpEndpoint.Stream.StreamName == HoloLensVideoStreamName)
                    {
                        var sharedEncodedImageSourceEndpoint = tcpEndpoint.ToTcpSource<
                            Shared<EncodedImage>
                        >(percepSyncPipeline, Serializers.SharedEncodedImageFormat());
                        videoFrameStream = sharedEncodedImageSourceEndpoint
                            .Decode(new ImageFromNV12StreamDecoder(), DeliveryPolicy.LatestMessage)
                            .Select(
                                (image) =>
                                {
                                    // NOTE: image.Resource.PixelFormat is PixelFormat.BGRA_32bpp, but
                                    // in actuality it is PixelFormat.RGBA_32bpp, which, for some reason,
                                    // does not exist. So, in order to convert image into RGB_24bpp,
                                    // we simply convert it to PixelFormat.BGR_24bpp so as not to swap
                                    // the color channels.
                                    var rgb24Image = image.Resource.Convert(PixelFormat.BGR_24bpp);
                                    var pixelData = new byte[rgb24Image.Size];
                                    rgb24Image.CopyTo(pixelData);
                                    return new RawPixelImage(
                                        pixelData,
                                        rgb24Image.Width,
                                        rgb24Image.Height,
                                        rgb24Image.Stride
                                    );
                                }
                            );
                    }
                    else if (tcpEndpoint.Stream.StreamName == HoloLensAudioStreamName)
                    {
                        audioStream = tcpEndpoint
                            .ToTcpSource<AudioBuffer>(
                                percepSyncPipeline,
                                Serializers.AudioBufferFormat()
                            )
                            .Select((buffer) => new Audio(buffer.Data));
                    }
                }
                else if (endpoint is not Rendezvous.RemoteClockExporterEndpoint)
                {
                    throw new Exception("Unexpected endpoint type.");
                }
            }

            // Send a server heartbeat
            var serverHeartbeat = Generators.Sequence(
                percepSyncPipeline,
                (0f, 0f),
                _ => (0f, 0f),
                TimeSpan.FromSeconds(0.2) /* 5Hz */
            );
            var heartbeatTcpSource = new TcpWriter<(float, float)>(
                percepSyncPipeline,
                16000,
                Serializers.HeartbeatFormat()
            );
            serverHeartbeat.PipeTo(heartbeatTcpSource);
            rendezvousServer.Rendezvous.TryAddProcess(
                new Rendezvous.Process(
                    HoloLensCaptureServerProcessName, // HoloLensCaptureApp expects this. Should be changed later
                    new[] { heartbeatTcpSource.ToRendezvousEndpoint("0.0.0.0", "ServerHeartbeat") }, // HoloLensCaptureApp expects this stream name. Should be changed later.
                    HoloLensCaptureAppVersion
                )
            );

            if (videoFrameStream is null)
            {
                throw new Exception(
                    "Failed to construct the video frame sensor stream from HoloLens."
                );
            }
            if (audioStream is null)
            {
                throw new Exception("Failed to construct the audio sensor stream from HoloLens.");
            }
            return new SensorStreams(videoFrameStream, audioStream);
        }

        private static void InitializeCssStyles()
        {
            var styleProvider = new Gtk.CssProvider();
            using Stream? stream = Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream("PercepSync.VideoPlayerStyles.css");
            if (stream is null)
            {
                throw new FileNotFoundException("VideoPlayerStyles.css not found.");
            }
            using StreamReader reader = new StreamReader(stream);

            styleProvider.LoadFromData(reader.ReadToEnd());
            Gtk.StyleContext.AddProviderForScreen(
                Gdk.Display.Default.DefaultScreen,
                styleProvider,
                Gtk.StyleProviderPriority.Application
            );
        }

        private static void StopPercepSyncPipeline(string message)
        {
            Console.WriteLine($"Stopping PercepSync pipeline: {message}");
            if (percepSyncPipeline is not null)
            {
                if (rendezvousServer is not null)
                {
                    rendezvousServer.Rendezvous.TryRemoveProcess(HoloLensCaptureServerProcessName);
                    rendezvousServer.Rendezvous.TryRemoveProcess(
                        PercepSyncHoloLensCaptureProcessName
                    );
                    rendezvousServer.Rendezvous.TryRemoveProcess(nameof(LocalDevicesCapture));
                }
                percepSyncPipeline?.Dispose();
                if (percepSyncPipeline is not null)
                {
                    Console.WriteLine("Stopped Capture Server Pipeline.");
                }

                percepSyncPipeline = null;
            }
        }

        private class SensorStreams
        {
            public IProducer<RawPixelImage> VideoFrameStream;
            public IProducer<Audio> AudioStream;

            public SensorStreams(
                IProducer<RawPixelImage> videoFrameStream,
                IProducer<Audio> audioStream
            )
            {
                VideoFrameStream = videoFrameStream;
                AudioStream = audioStream;
            }
        }
    }
}
