namespace Sled.PercepSync
{
    using System;
    using System.IO;
    using System.Reflection;
    using Microsoft.Psi;
    using Microsoft.Psi.Common;
    using Microsoft.Psi.Imaging;

    public class DisplayInput
    {
        public DisplayInput(RawPixelImage videoFrame, float audioLevel)
        {
            VideoFrame = videoFrame;
            AudioLevel = audioLevel;
        }

        public RawPixelImage VideoFrame;
        public float AudioLevel;
    }

    /// <summary>
    /// Displays a video and other sensor streams in a window.
    /// </summary>
    public class Preview : Gtk.Window, IConsumer<DisplayInput>
    {
        // \psi related properties
        public readonly Pipeline pipeline;
        public Receiver<DisplayInput> In { get; }

        // GTK related properties
        private Gtk.Image displayImage = default!;
        private Gtk.Label displayText = default!;
        private Gtk.LevelBar displayLevel = default!;
        private byte[] imageData = new byte[640 * 480 * 3];

        /// <summary>
        /// Initializes a new instance of the <see cref="Preview"/> class.
        /// </summary>
        public Preview(Pipeline pipeline)
            : base("Preview")
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
            var pixbuf = ImageToPixbuf(displayInput.VideoFrame);

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

        private Gdk.Pixbuf ImageToPixbuf(RawPixelImage rawImage)
        {
            // First convert RGB 24-bit to BGR 24-bit
            var image = new Microsoft.Psi.Imaging.Image(
                UnmanagedBuffer.CreateCopyFrom(rawImage.pixelData),
                rawImage.width,
                rawImage.height,
                rawImage.stride,
                PixelFormat.RGB_24bpp
            );
            var bgr24Image = image.Convert(PixelFormat.BGR_24bpp);

            // Copy the pixels
            var length = bgr24Image.Stride * bgr24Image.Height;
            if (imageData.Length != length)
            {
                imageData = new byte[length];
            }
            bgr24Image.CopyTo(imageData);

            return new Gdk.Pixbuf(
                imageData,
                false,
                8,
                bgr24Image.Width,
                bgr24Image.Height,
                bgr24Image.Stride
            );
        }
    }
}
