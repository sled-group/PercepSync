namespace Sled.PercepSync
{
    using System;
    using System.IO;
    using System.Reflection;
    using Microsoft.Psi;
    using Microsoft.Psi.Imaging;

    public struct DisplayInput
    {
        public DisplayInput(Shared<Image> frame, float audioLevel)
        {
            Frame = frame;
            AudioLevel = audioLevel;
        }

        public Shared<Image> Frame { get; }
        public float AudioLevel { get; }
    }

    /// <summary>
    /// Displays a video and other sensor streams in a window.
    /// </summary>
    public class VideoPlayer : Gtk.Window, IConsumer<DisplayInput>
    {
        // singleton app thread and app instance shared across VideoPlayers
        // private static Thread? appThread = null;

        // \psi related properties
        public readonly Pipeline pipeline;
        public Receiver<DisplayInput> In { get; }

        // GTK related properties
        private Gtk.Image displayImage = default!;
        private Gtk.Label displayText = default!;
        private Gtk.LevelBar displayLevel = default!;
        private byte[] imageData = new byte[640 * 480 * 3];

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoPlayer"/> class.
        /// </summary>
        public VideoPlayer(Pipeline pipeline)
            : base("Video Player")
        {
            this.pipeline = pipeline;
            In = pipeline.CreateReceiver<DisplayInput>(this, ReceiveDisplayInput, nameof(In));

            InitWindow();
        }

        private void InitWindow()
        {
            using Stream? stream = Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream("PercepSync.VideoPlayer.xml");
            if (stream is null)
            {
                throw new FileNotFoundException("VideoPlayer.xml not found.");
            }
            using StreamReader reader = new StreamReader(stream);
            var builder = new Gtk.Builder();
            builder.AddFromString(reader.ReadToEnd());
            Add((Gtk.Widget)builder.GetObject("root"));

            // get the widgets which we will modify from the xml
            displayImage = (Gtk.Image)builder.GetObject("image");
            if (displayImage is null)
            {
                throw new Exception("Image widget couldn't be found");
            }
            displayText = (Gtk.Label)builder.GetObject("value")!;
            if (displayText is null)
            {
                throw new Exception("Value widget couldn't be found");
            }
            displayLevel = (Gtk.LevelBar)builder.GetObject("level")!;
            if (displayLevel is null)
            {
                throw new Exception("Level widget couldn't be found");
            }

            DeleteEvent += Window_DeleteEvent;
        }

        void Window_DeleteEvent(object o, Gtk.DeleteEventArgs args)
        {
            // Prvent the default action (which is to destroy the window)
            args.RetVal = true;

            // Hide the window
            Hide();
        }

        private void ReceiveDisplayInput(DisplayInput displayInput, Envelope envelope)
        {
            // copy the frame image to the pixel buffer
            var pixbuf = ImageToPixbuf(displayInput.Frame);

            // clamp level to between 0 and 20
            var audioLevel =
                displayInput.AudioLevel < 0
                    ? 0
                    : displayInput.AudioLevel > 20
                        ? 20
                        : displayInput.AudioLevel;

            // redraw on the UI thread
            Gtk.Application.Invoke(
                (sender, e) =>
                {
                    displayImage.Pixbuf = pixbuf;
                    displayLevel.Value = audioLevel;
                    displayText.Text = audioLevel.ToString("0.0");
                }
            );
        }

        private Gdk.Pixbuf ImageToPixbuf(Shared<Image> image)
        {
            var length = image.Resource.Stride * image.Resource.Height;
            if (imageData.Length != length)
            {
                imageData = new byte[length];
            }

            image.Resource.CopyTo(imageData);
            return new Gdk.Pixbuf(
                imageData,
                false,
                8,
                image.Resource.Width,
                image.Resource.Height,
                image.Resource.Stride
            );
        }
    }
}
