namespace Sled.PercepSync
{
    using CommandLine;

    internal class Options
    {
        [Option(
            'c',
            "camera-device-id",
            Required = false,
            HelpText = "Camera device ID (default: /dev/video0)"
        )]
        public string CameraDeviceID { get; set; } = "/dev/video0";

        [Option(
            'a',
            "audio-device-name",
            Required = false,
            HelpText = "Audio device name (default: plughw:0,0)"
        )]
        public string AudioDeviceName { get; set; } = "plughw:0,0";

        [Option(
            'z',
            "zeromq-pub-address",
            Required = false,
            HelpText = "Address for ZeroMQ publish socket (default: tcp://*:12345)"
        )]
        public string ZeroMQPubAddress { get; set; } = "tcp://*:12345";
    }
}
