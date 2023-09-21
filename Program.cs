namespace Sled.PercepSync
{
    using CommandLine;
    using System.IO;
    using System.Reflection;
    using Microsoft.Psi;
    using Microsoft.Psi.Media;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Imaging;
    using Microsoft.Psi.Interop.Transport;
    using Microsoft.Psi.Interop.Format;

    /// <summary>
    /// PercepSync synchronizes streams of data from different perceptions and broadcast them.
    /// </summary>
    public class Program
    {
        private static Pipeline? percepSyncPipeline = null!;
        private static Preview? preview = null;
        private static string cameraDeviceID = "";
        private static string audioDeviceName = "";
        private static string zeroMQPubAddress = "";
        private static bool enablePreview = false;

        public static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args).WithParsed(RunOptions);
        }

        private static void RunOptions(Options opts)
        {
            cameraDeviceID = opts.CameraDeviceID;
            audioDeviceName = opts.AudioDeviceName;
            zeroMQPubAddress = opts.ZeroMQPubAddress;
            enablePreview = opts.EnablePreview;

            if (enablePreview)
            {
                Console.WriteLine("Initializing GTK Application");
                Gtk.Application.Init();
                InitializeCssStyles();
            }

            CreateAndRunPercepSyncPipeline();

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

        private static void CreateAndRunPercepSyncPipeline()
        {
            // Create the \psi pipeline
            percepSyncPipeline = Pipeline.Create();

            // Create the webcam component
            var webcam = new MediaCapture(
                percepSyncPipeline,
                640,
                480,
                cameraDeviceID,
                PixelFormatId.YUYV
            );
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
                percepSyncPipeline,
                new AudioCaptureConfiguration
                {
                    DeviceName = audioDeviceName,
                    Format = WaveFormat.Create16kHz1Channel16BitPcm()
                }
            );
            var serializedAudio = audio.Select((buffer) => new AudioBuffer(buffer.Data));

            // Connect to zeromq publisher socket
            var mq = new NetMQWriter(
                percepSyncPipeline,
                zeroMQPubAddress,
                MessagePackFormat.Instance
            );
            serializedWebcam.PipeTo(mq.AddTopic<RawPixelImage>("videoFrame"));
            serializedAudio.PipeTo(mq.AddTopic<AudioBuffer>("audio"));

            // Create an acoustic features extractor component and pipe the audio to it
            var acousticFeatures = new AcousticFeaturesExtractor(percepSyncPipeline);
            audio.PipeTo(acousticFeatures);

            if (enablePreview)
            {
                // Connect to VideoPlayer
                preview = new Preview(percepSyncPipeline);
                serializedWebcam
                    .Join(acousticFeatures.LogEnergy, RelativeTimeInterval.Past())
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
        }
    }
}
