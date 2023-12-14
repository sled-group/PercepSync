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
    using Tomlyn;
    using HoloLensCaptureInterop;

    /// <summary>
    /// PercepSync synchronizes streams of data from different perceptions and broadcast them.
    /// </summary>
    public class PercepSync
    {
        private static RendezvousServer? rendezvousServer;
        private static Pipeline? percepSyncPipeline;
        private static Preview? preview;
        private static readonly ManualResetEventSlim clientConnectedEvent = new();

        private const string PerceptionTopic = "perception";
        private const string PercepSyncHoloLensCaptureProcessName = "PercepSyncHoloLensCapture";
        private const string PercepSyncHoloLensCaptureAPIVersion = "v1";
        private const string HoloLensVideoStreamName = "VideoEncodedImageCameraView";
        private const string HoloLensAudioStreamName = "Audio";

        public static void Main(string[] args)
        {
            var rootCommand = new RootCommand("PercepSync");
            var configFileOption = new Option<string>(
                name: "--config-file",
                description: "Path to config file (TOML)"
            );
            rootCommand.AddOption(configFileOption);
            var percepStreamAddressOption = new Option<string>(
                name: "--percep-stream-address",
                description: "Address for perception streams. Implemented as a ZeroMQ publish socket.",
                getDefaultValue: () => Config.DefaultPercepStreamAddress
            );
            rootCommand.AddOption(percepStreamAddressOption);
            var enablePreviewOption = new Option<bool>(
                name: "--enable-preview",
                description: "Whether to enable preview or not. Only works if you have a display.",
                getDefaultValue: () => Config.DefaultEnablePreview
            );
            rootCommand.AddOption(enablePreviewOption);
            var rdzvServerPortOption = new Option<int>(
                name: "--rdzv-server-port",
                description: "Rendezvous server port",
                getDefaultValue: () => Config.DefaultRdzvServerPort
            );
            rootCommand.AddOption(rdzvServerPortOption);
            var enableTtsOption = new Option<bool>(
                name: "--enable-tts",
                description: "Whether to enable text-to-speech or not. Make sure to set Azure creds if enabled.",
                getDefaultValue: () => Config.DefaultEnableTts
            );
            rootCommand.AddOption(enableTtsOption);
            var ttsAddressOption = new Option<string>(
                name: "--tts-address",
                description: "Address for text-to-speech server. Implemented as a ZeroMQ pull socket.",
                getDefaultValue: () => Config.DefaultTtsAddress
            );
            rootCommand.AddOption(ttsAddressOption);
            var enableSttOption = new Option<bool>(
                name: "--enable-stt",
                description: "Whether to enable speech-to-text or not. Make sure to set Azure creds if enabled.",
                getDefaultValue: () => Config.DefaultEnableStt
            );
            rootCommand.AddOption(enableSttOption);

            var localCommand = new Command("local", description: "Use local devices");
            var localCameraDeviceIDOption = new Option<string>(
                name: "--camera-device-id",
                description: "Camera device ID. Only used for Linux.",
                getDefaultValue: () => LocalConfig.DefaultCameraDeviceId
            );
            localCommand.Add(localCameraDeviceIDOption);
            var localAudioInputDeviceNameOption = new Option<string>(
                name: "--audio-input-device-name",
                description: "Audio input device name. Only used for Linux.",
                getDefaultValue: () => LocalConfig.DefaultAudioInputDeviceName
            );
            localCommand.Add(localAudioInputDeviceNameOption);
            var localAudioOutputDeviceNameOption = new Option<string>(
                name: "--audio-output-device-name",
                description: "Audio output device name. Only used for Linux.",
                getDefaultValue: () => LocalConfig.DefaultAudioOutputDeviceName
            );
            localCommand.Add(localAudioOutputDeviceNameOption);
            rootCommand.Add(localCommand);

            Config CreateConfig(
                string? configFile,
                string? percepStreamAddress,
                bool enablePreview,
                int rdzvServerPort,
                bool enableTts,
                string? ttsAddress,
                bool enableStt
            )
            {
                Config config;
                if (configFile is null)
                {
                    config = new();
                }
                else
                {
                    using (var sr = new StreamReader(configFile))
                    {
                        config = Toml.ToModel<Config>(sr.ReadToEnd());
                    }
                }
                if (
                    percepStreamAddress is not null
                    && percepStreamAddress != Config.DefaultPercepStreamAddress
                )
                {
                    config.PercepStreamAddress = percepStreamAddress;
                }
                if (enablePreview != Config.DefaultEnablePreview)
                {
                    config.EnablePreview = enablePreview;
                }
                if (rdzvServerPort != Config.DefaultRdzvServerPort)
                {
                    config.RdzvServerPort = rdzvServerPort;
                }
                if (enableTts != Config.DefaultEnableTts)
                {
                    config.EnableTts = enableTts;
                }
                if (ttsAddress is not null && ttsAddress != Config.DefaultTtsAddress)
                {
                    config.TtsAddress = ttsAddress;
                }
                if (enableStt != Config.DefaultEnableStt)
                {
                    config.EnableStt = enableStt;
                }
                return config;
            }

            localCommand.SetHandler(
                (context) =>
                {
                    var config = CreateConfig(
                        context.ParseResult.GetValueForOption(configFileOption),
                        context.ParseResult.GetValueForOption(percepStreamAddressOption),
                        context.ParseResult.GetValueForOption(enablePreviewOption),
                        context.ParseResult.GetValueForOption(rdzvServerPortOption),
                        context.ParseResult.GetValueForOption(enableTtsOption),
                        context.ParseResult.GetValueForOption(ttsAddressOption),
                        context.ParseResult.GetValueForOption(enableSttOption)
                    );
                    var cameraDeviceID = context.ParseResult.GetValueForOption(
                        localCameraDeviceIDOption
                    );
                    var audioInputDeviceName = context.ParseResult.GetValueForOption(
                        localAudioInputDeviceNameOption
                    );
                    var audioOutputDeviceName = context.ParseResult.GetValueForOption(
                        localAudioOutputDeviceNameOption
                    );
                    if (config.LocalConfig is null)
                    {
                        // LocalConfig wasn't specified from the config file,
                        // so create one based on the values from the CLI.
                        config.LocalConfig = new()
                        {
                            CameraDeviceId = cameraDeviceID!,
                            AudioInputDeviceName = audioInputDeviceName!,
                            AudioOutputDeviceName = audioOutputDeviceName!,
                        };
                    }
                    else
                    {
                        // LocalConfig was specified from the config file.
                        // Override values if specified via CLI.
                        if (cameraDeviceID != LocalConfig.DefaultCameraDeviceId)
                        {
                            config.LocalConfig.CameraDeviceId = cameraDeviceID!;
                        }
                        if (audioInputDeviceName != LocalConfig.DefaultAudioInputDeviceName)
                        {
                            config.LocalConfig.AudioInputDeviceName = audioInputDeviceName!;
                        }
                        if (audioOutputDeviceName != LocalConfig.DefaultAudioOutputDeviceName)
                        {
                            config.LocalConfig.AudioOutputDeviceName = audioOutputDeviceName!;
                        }
                    }
                    RunPercepSync(config, nameof(LocalDevicesCapture), ConstructLocalSensorStreams);
                    var localDevicesCapture = new LocalDevicesCapture(
                        "127.0.0.1",
                        config.RdzvServerPort,
                        config.LocalConfig.CameraDeviceId,
                        config.LocalConfig.AudioInputDeviceName
                    );
                    localDevicesCapture.Start();
                    RunCliLoop(config.EnablePreview);
                }
            );

            var hololensCommand = new Command("hololens", description: "Use HoloLens");
            rootCommand.Add(hololensCommand);
            hololensCommand.SetHandler(
                (
                    configFile,
                    percepStreamAddress,
                    enablePreview,
                    rdzvServerPort,
                    enableTts,
                    ttsAddress,
                    enableStt
                ) =>
                {
                    var config = CreateConfig(
                        configFile,
                        percepStreamAddress,
                        enablePreview,
                        rdzvServerPort,
                        enableTts,
                        ttsAddress,
                        enableStt
                    );
                    if (config.HoloLensConfig is null)
                    {
                        config.HoloLensConfig = new();
                    }
                    RunPercepSync(
                        config,
                        PercepSyncHoloLensCaptureProcessName,
                        ConstructHoloLensSensorStreams,
                        targetProcessVersion: PercepSyncHoloLensCaptureAPIVersion
                    );
                    RunCliLoop(config.EnablePreview);
                },
                configFileOption,
                percepStreamAddressOption,
                enablePreviewOption,
                rdzvServerPortOption,
                enableTtsOption,
                ttsAddressOption,
                enableSttOption
            );
            rootCommand.Invoke(args);
        }

        private static void RunPercepSync(
            Config config,
            string targetProcessName,
            Func<Rendezvous.Process, AzureSpeechSynthesizer?, SensorStreams> ConstructSensorStreams,
            string? targetProcessVersion = null
        )
        {
            if (config.EnablePreview)
            {
                Console.WriteLine("Initializing GTK Application");
                Gtk.Application.Init();
                InitializeCssStyles();
            }
            rendezvousServer = new RendezvousServer(config.RdzvServerPort);
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

                    // Set up a rendezvous endpoint for text-to-speech
                    var percepDurationInSeconds = 1 / config.Fps;
                    var audioBufferFrameSizeInBytes = (int)
                        Math.Ceiling(
                            Serializers.AssumedWaveFormat.AvgBytesPerSec * percepDurationInSeconds
                        );
                    AzureSpeechSynthesizer? speechSynthesizer = null;
                    if (config.EnableTts)
                    {
                        var ttsReceiver = new TtsRequestReceiver(
                            percepSyncPipeline,
                            config.TtsAddress
                        );
                        speechSynthesizer = new AzureSpeechSynthesizer(
                            percepSyncPipeline,
                            config.AzureSpeechConfig.SubscriptionKey,
                            config.AzureSpeechConfig.Region,
                            config.AzureSpeechConfig.SpeechSynthesisVoiceName,
                            audioBufferFrameSizeInBytes
                        );
                        ttsReceiver.PipeTo(speechSynthesizer);
                        if (config.LocalConfig is not null)
                        {
                            // Hook up speech synthesizer to the speaker
                            var audioPlayer = new AudioPlayer(
                                percepSyncPipeline,
#if NET7_0
                                new AudioPlayerConfiguration(
                                    config.LocalConfig.AudioOutputDeviceName
                                )
#else
                                new AudioPlayerConfiguration()
#endif
                            );
                            speechSynthesizer.PipeTo(audioPlayer);
                        }
                    }

                    // Construct sensor streams
                    var sensorStreams = ConstructSensorStreams(process, speechSynthesizer);
                    var videoFrameStream = sensorStreams.VideoFrameStream.Sample(
                        TimeSpan.FromSeconds(percepDurationInSeconds)
                    );

                    var audioBufferStream = sensorStreams.AudioBufferStream.Reframe(
                        audioBufferFrameSizeInBytes
                    );
                    var videoAudioStream = videoFrameStream.Join(
                        audioBufferStream,
                        Reproducible.Nearest<AudioBuffer>(
                            TimeSpan.FromSeconds(percepDurationInSeconds)
                        )
                    );
                    IProducer<Perception> percepStream;
                    Perception CreatePerception(
                        Shared<Image> frame,
                        AudioBuffer audioBuffer,
                        string transcription = ""
                    )
                    {
                        var pixelData = new byte[frame.Resource.Size];
                        frame.Resource.CopyTo(pixelData);
                        var rawPixelFrame = new RawPixelImage(
                            pixelData,
                            frame.Resource.Width,
                            frame.Resource.Height,
                            frame.Resource.Stride
                        );

                        return new Perception(
                            rawPixelFrame,
                            new Audio(audioBuffer.Data),
                            new TranscribedText(transcription)
                        );
                    }
                    if (config.EnableStt)
                    {
                        var speechRecognizer = new ContinuousAzureSpeechRecognizer(
                            percepSyncPipeline,
                            config.AzureSpeechConfig.SubscriptionKey,
                            config.AzureSpeechConfig.Region
                        );
                        audioBufferStream.PipeTo(speechRecognizer);
                        percepStream = videoAudioStream
                            .Join(
                                speechRecognizer,
                                Reproducible.Nearest<string>(
                                    TimeSpan.FromSeconds(percepDurationInSeconds / 2)
                                )
                            )
                            .Select(
                                (tuple) =>
                                    CreatePerception(
                                        tuple.Item1,
                                        tuple.Item2,
                                        transcription: tuple.Item3
                                    )
                            );
                    }
                    else
                    {
                        percepStream = videoAudioStream.Select(
                            (tuple) => CreatePerception(tuple.Item1, tuple.Item2)
                        );
                    }
                    var percepStreamMQWriter = new NetMQWriter<Perception>(
                        percepSyncPipeline,
                        PerceptionTopic,
                        config.PercepStreamAddress,
                        MessagePackFormat.Instance
                    );
                    percepStream.PipeTo(percepStreamMQWriter);

                    if (config.EnablePreview)
                    {
                        // Connect sensor streams to Preview
                        preview = new Preview(percepSyncPipeline);
                        var acousticFeatures = new AcousticFeaturesExtractor(percepSyncPipeline);
                        sensorStreams.AudioBufferStream.PipeTo(acousticFeatures);
                        percepStream
                            .Join(acousticFeatures.LogEnergy, RelativeTimeInterval.Past())
                            .Select((data) => new DisplayInput(data.Item1.Frame, data.Item2))
                            .PipeTo(preview);
                    }

                    // Run the pipeline
                    percepSyncPipeline.RunAsync();

                    // Notify that the client has connected and the pipeline is running
                    clientConnectedEvent.Set();
                }
            };

            rendezvousServer.Start();
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
            Rendezvous.Process inputRendezvousProcess,
            AzureSpeechSynthesizer? speechSynthesizer
        )
        {
            IProducer<Shared<Image>>? videoFrameStream = null;
            IProducer<AudioBuffer>? audioBufferStream = null;
            foreach (var endpoint in inputRendezvousProcess.Endpoints)
            {
                if (
                    endpoint is Rendezvous.NetMQSourceEndpoint mqSourceEndpoint
                    && mqSourceEndpoint is not null
                )
                {
                    if (mqSourceEndpoint.Address == LocalDevicesCapture.WebcamAddress)
                    {
                        foreach (var stream in mqSourceEndpoint.Streams)
                        {
                            if (stream.StreamName == LocalDevicesCapture.WebcamTopic)
                            {
                                videoFrameStream = mqSourceEndpoint.ToNetMQSource<Shared<Image>>(
                                    percepSyncPipeline,
                                    stream.StreamName,
                                    Serializers.SharedImageFormat()
                                );
                            }
                        }
                    }
                    else if (mqSourceEndpoint.Address == LocalDevicesCapture.AudioAddress)
                    {
                        foreach (var stream in mqSourceEndpoint.Streams)
                        {
                            if (stream.StreamName == LocalDevicesCapture.AudioTopic)
                            {
                                audioBufferStream = mqSourceEndpoint.ToNetMQSource<AudioBuffer>(
                                    percepSyncPipeline,
                                    stream.StreamName,
                                    Serializers.AudioBufferFormat()
                                );
                            }
                        }
                    }
                }
            }
            if (videoFrameStream is null)
            {
                throw new Exception(
                    "Failed to construct the video frame stream from local devices."
                );
            }
            if (audioBufferStream is null)
            {
                throw new Exception(
                    "Failed to construct the audio buffer stream from local devices."
                );
            }
            return new SensorStreams(videoFrameStream, audioBufferStream);
        }

        private static SensorStreams ConstructHoloLensSensorStreams(
            Rendezvous.Process inputRendezvousProcess,
            AzureSpeechSynthesizer? speechSynthesizer
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

            IProducer<Shared<Image>>? videoFrameStream = null;
            IProducer<AudioBuffer>? audioBufferStream = null;
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
                            // NOTE: image.Resource.PixelFormat is PixelFormat.BGRA_32bpp, but
                            // in actuality it is PixelFormat.RGBA_32bpp, which, for some reason,
                            // does not exist. So, in order to convert image into RGB_24bpp,
                            // we simply convert it to PixelFormat.BGR_24bpp so as not to swap
                            // the color channels.
                            .Select(
                                (image) =>
                                    Shared.Create(image.Resource.Convert(PixelFormat.BGR_24bpp))
                            );
                    }
                    else if (tcpEndpoint.Stream.StreamName == HoloLensAudioStreamName)
                    {
                        audioBufferStream = tcpEndpoint.ToTcpSource<AudioBuffer>(
                            percepSyncPipeline,
                            Serializers.AudioBufferFormat()
                        );
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
            var endpoints = new List<Rendezvous.Endpoint>()
            {
                heartbeatTcpSource.ToRendezvousEndpoint("0.0.0.0", "ServerHeartbeat")
            };
            if (speechSynthesizer is not null)
            {
                var ttsSender = new TcpWriter<AudioBuffer>(
                    percepSyncPipeline,
                    14001,
                    Serializers.AudioBufferFormat(),
                    name: "TtsSender"
                );
                speechSynthesizer.PipeTo(ttsSender);
                endpoints.Add(ttsSender.ToRendezvousEndpoint("0.0.0.0", "TTSAudioBuffer"));
            }
            rendezvousServer.Rendezvous.TryAddProcess(
                new Rendezvous.Process(
                    nameof(PercepSync),
                    endpoints,
                    PercepSyncHoloLensCaptureAPIVersion
                )
            );

            if (videoFrameStream is null)
            {
                throw new Exception("Failed to construct the video frame stream from HoloLens.");
            }
            if (audioBufferStream is null)
            {
                throw new Exception("Failed to construct the audio buffer stream from HoloLens.");
            }
            return new SensorStreams(videoFrameStream, audioBufferStream);
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
                    rendezvousServer.Rendezvous.TryRemoveProcess(nameof(PercepSync));
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
            public IProducer<Shared<Image>> VideoFrameStream;
            public IProducer<AudioBuffer> AudioBufferStream;

            public SensorStreams(
                IProducer<Shared<Image>> videoFrameStream,
                IProducer<AudioBuffer> audioBufferStream
            )
            {
                VideoFrameStream = videoFrameStream;
                AudioBufferStream = audioBufferStream;
            }
        }
    }
}
