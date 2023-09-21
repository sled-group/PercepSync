namespace Sled.PercepSync
{
    using CommandLine;

    internal class Options
    {
        [Option(
            "camera-device-id",
            Required = false,
            HelpText = "Camera device ID (default: /dev/video0)"
        )]
        public string CameraDeviceID { get; set; } = "/dev/video0";

        [Option(
            "audio-device-name",
            Required = false,
            HelpText = "Audio device name (default: plughw:0,0)"
        )]
        public string AudioDeviceName { get; set; } = "plughw:0,0";

        [Option(
            "zeromq-pub-address",
            Required = false,
            HelpText = "Address for ZeroMQ publish socket (default: tcp://*:12345)"
        )]
        public string ZeroMQPubAddress { get; set; } = "tcp://*:12345";

        [Option(
            "enable-preview",
            Required = false,
            HelpText = "Whether to enable preview or not. Only works if you have a display. (default: false)"
        )]
        public bool EnablePreview { get; set; } = false;
    }
}
