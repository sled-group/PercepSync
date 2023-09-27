namespace Sled.PercepSync
{
    using CommandLine;
    using System.IO;
    using System.Reflection;
    using Microsoft.Psi;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Interop.Transport;
    using Microsoft.Psi.Interop.Format;
    using Microsoft.Psi.Interop.Rendezvous;

    /// <summary>
    /// PercepSync synchronizes streams of data from different perceptions and broadcast them.
    /// </summary>
    public class Program
    {
        private static RendezvousServer? rendezvousServer;
        private static Pipeline? percepSyncPipeline;
        private static Preview? preview;
        private static LocalDevicesCapture? localDevicesCapture;
        private static string zeroMQPubAddress = "";
        private static bool enablePreview = false;

        public static readonly string VideoFrameTopic = "videoFrame";
        public static readonly string AudioTopic = "audio";

        public static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args).WithParsed(RunOptions);
        }

        private static void RunOptions(Options opts)
        {
            rendezvousServer = new RendezvousServer(opts.RdzvServerPort);
            enablePreview = opts.EnablePreview;
            zeroMQPubAddress = opts.ZeroMQPubAddress;

            rendezvousServer.Rendezvous.ProcessAdded += (_, process) =>
            {
                ReportProcessAdded(process);

                if (process.Name == nameof(LocalDevicesCapture))
                {
                    Console.WriteLine(
                        $"Starting PercepSync pipeline for {nameof(LocalDevicesCapture)}"
                    );

                    if (enablePreview)
                    {
                        Console.WriteLine("Initializing GTK Application");
                        Gtk.Application.Init();
                        InitializeCssStyles();
                    }

                    CreateAndRunPercepSyncPipeline(process);

                    Console.WriteLine("Press Q or ENTER key to exit.");
                    if (enablePreview)
                    {
                        Console.WriteLine("Press V to start VideoPlayer.");
                        GLib.Idle.Add(new GLib.IdleHandler(RunCliIteration));
                        Gtk.Application.Run();
                    }
                    else
                    {
                        while (true)
                        {
                            RunCliIteration();
                        }
                    }
                }
            };

            rendezvousServer.Start();

            localDevicesCapture = new LocalDevicesCapture(
                "127.0.0.1",
                opts.RdzvServerPort,
                opts.CameraDeviceID,
                opts.AudioDeviceName
            );
            localDevicesCapture.Start();
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

        private static bool RunCliIteration()
        {
            if (Console.KeyAvailable)
            {
                switch (Console.ReadKey(true).Key)
                {
                    case ConsoleKey.Q:
                    case ConsoleKey.Enter:
                        Console.WriteLine("PercepSync manually stopped");
                        StopPercepSync();
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

        private static void CreateAndRunPercepSyncPipeline(
            Rendezvous.Process inputRendezvousProcess
        )
        {
            // Create the \psi pipeline
            percepSyncPipeline = Pipeline.Create();

            // Connect to zeromq publisher socket
            var mq = new NetMQWriter(
                percepSyncPipeline,
                zeroMQPubAddress,
                MessagePackFormat.Instance
            );

            // Create an acoustic features extractor component and pipe the audio to it
            var acousticFeatures = new AcousticFeaturesExtractor(percepSyncPipeline);
            IProducer<RawPixelImage>? serializedWebcam = null;
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
                            serializedWebcam.PipeTo(mq.AddTopic<RawPixelImage>(VideoFrameTopic));
                        }
                        else if (stream.StreamName == LocalDevicesCapture.AudioTopic)
                        {
                            var serializedAudio = deserializedSourceEndpoint.Select(
                                (data) => new AudioBuffer(data.data)
                            );
                            serializedAudio.PipeTo(mq.AddTopic<AudioBuffer>(AudioTopic));
                            serializedAudio
                                .Select(
                                    (buffer) =>
                                        new Microsoft.Psi.Audio.AudioBuffer(
                                            buffer.data,
                                            WaveFormat.Create16kHz1Channel16BitPcm()
                                        )
                                )
                                .PipeTo(acousticFeatures);
                        }
                    }
                }
            }

            if (enablePreview)
            {
                // Connect to VideoPlayer
                preview = new Preview(percepSyncPipeline);
                serializedWebcam
                    ?.Join(acousticFeatures.LogEnergy, RelativeTimeInterval.Past())
                    .Select((data) => new DisplayInput(data.Item1, data.Item2))
                    .PipeTo(preview);
            }

            // Start the pipeline running
            percepSyncPipeline.RunAsync();
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

        private static void StopPercepSync()
        {
            if (localDevicesCapture is not null)
            {
                localDevicesCapture.Dispose();

                if (localDevicesCapture is not null)
                {
                    Console.WriteLine("Stopped Local Device Capture.");
                }

                localDevicesCapture = null;
            }
            if (percepSyncPipeline is not null)
            {
                percepSyncPipeline.Dispose();
                if (percepSyncPipeline is not null)
                {
                    Console.WriteLine("Stopped Capture Server Pipeline.");
                }

                percepSyncPipeline = null;
            }
            if (rendezvousServer is not null)
            {
                rendezvousServer.Dispose();
                if (rendezvousServer is not null)
                {
                    Console.WriteLine("Stopped Rendezvous Server.");
                }
                rendezvousServer = null;
            }
        }
    }
}
