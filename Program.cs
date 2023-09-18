namespace Sled.PercepSync
{
    using System.IO;
    using System.Reflection;
    using Microsoft.Psi;
    using Microsoft.Psi.Media;
    using Microsoft.Psi.Audio;
    using NetMQ;
    using NetMQ.Sockets;
    using MessagePack;
    using Microsoft.Psi.Imaging;

    /// <summary>
    /// PercepSync synchronizes streams of data from different perceptions and broadcast them.
    /// </summary>
    public class Program
    {
        private static Pipeline? percepSyncPipeline = null!;
        private static VideoPlayer? videoPlayer = null;
        private static PublisherSocket? pubSocket = null;

        public static void Main()
        {
            Console.WriteLine("Initializing GTK Application");
            Gtk.Application.Init();
            InitializeCssStyles();

            CreateAndRunPercepSyncPipeline();

            GLib.Idle.Add(new GLib.IdleHandler(RunCliIteration));

            Console.WriteLine("Press Q or ENTER key to exit.");
            Console.WriteLine("Press V to start VideoPlayer.");

            Gtk.Application.Run();
        }

        private static bool RunCliIteration()
        {
            if (Console.KeyAvailable)
            {
                switch (Console.ReadKey(true).Key)
                {
                    case ConsoleKey.Q:
                    case ConsoleKey.Enter:
                        StopPercepSync("PercepSync manually stopped");
                        Environment.Exit(0);
                        break;
                    case ConsoleKey.V:
                        if (percepSyncPipeline is null)
                        {
                            throw new Exception("Pipeline has not been initialized.");
                        }
                        if (videoPlayer is null)
                        {
                            throw new Exception("DisplayInput producer has not been initialized.");
                        }
                        videoPlayer.ShowAll();
                        break;
                }
            }
            return true;
        }

        private static void CreateAndRunPercepSyncPipeline()
        {
            // Create the \psi pipeline
            percepSyncPipeline = Pipeline.Create();

            // Create the webcam component
            var webcam = new MediaCapture(
                percepSyncPipeline,
                640,
                480,
                "/dev/video0",
                PixelFormatId.YUYV
            );

            // Create the audio capture component
            var audio = new AudioCapture(
                percepSyncPipeline,
                new AudioCaptureConfiguration
                {
                    DeviceName = "plughw:2,0",
                    Format = WaveFormat.Create16kHz1Channel16BitPcm()
                }
            );

            // Create an acoustic features extractor component and pipe the audio to it
            var acousticFeatures = new AcousticFeaturesExtractor(percepSyncPipeline);
            audio.PipeTo(acousticFeatures);

            // Connect to VideoPlayer
            videoPlayer = new VideoPlayer(percepSyncPipeline);
            webcam
                .Join(acousticFeatures.LogEnergy, RelativeTimeInterval.Past())
                .Select((data) => new DisplayInput(data.Item1, data.Item2))
                .PipeTo(videoPlayer);

            // Connect to zeromq publisher socket
            pubSocket = new PublisherSocket();
            pubSocket.Bind("tcp://*:12345");
            webcam.Do(
                (image, e) =>
                {
                    var bgra32Image = image.Resource.Convert(PixelFormat.BGRA_32bpp);
                    var imageBuffer = new byte[bgra32Image.Size];
                    bgra32Image.CopyTo(imageBuffer);
                    pubSocket
                        .SendMoreFrame("video_frame")
                        .SendFrame(
                            MessagePackSerializer.Serialize(
                                new ImageMessage
                                {
                                    OriginatingTime = e.OriginatingTime,
                                    PixelData = imageBuffer,
                                    Width = bgra32Image.Width,
                                    Height = bgra32Image.Height,
                                }
                            )
                        );
                }
            );

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

        private static void StopPercepSync(string message)
        {
            if (percepSyncPipeline is not null)
            {
                percepSyncPipeline.Dispose();
                if (percepSyncPipeline is not null)
                {
                    Console.WriteLine("Stopped Capture Server Pipeline.");
                }

                percepSyncPipeline = null;
            }

            if (pubSocket is not null)
            {
                pubSocket.Dispose();
                if (pubSocket is not null)
                {
                    Console.WriteLine("Stopped ZeroMQ Publisher Socket.");
                }

                pubSocket = null;
            }
        }
    }
}
