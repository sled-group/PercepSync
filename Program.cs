namespace Sled.PercepSync
{
    using System.IO;
    using System.Reflection;
    using Microsoft.Psi;
    using Microsoft.Psi.Media;
    using Microsoft.Psi.Audio;

    /// <summary>
    /// PercepSync synchronizes streams of data from different perceptions and broadcast them.
    /// </summary>
    public class Program
    {
        private static Pipeline? percepSyncPipeline = null!;
        private static VideoPlayer? videoPlayer = null;

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
                        StopComputeServerPipeline("Server manually stopped");
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

        private static void StopComputeServerPipeline(string message)
        {
            if (percepSyncPipeline != null)
            {
                percepSyncPipeline?.Dispose();
                if (percepSyncPipeline != null)
                {
                    Console.WriteLine("Stopped Capture Server Pipeline.");
                }

                percepSyncPipeline = null;
            }
        }
    }
}
