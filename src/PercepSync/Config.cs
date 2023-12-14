namespace Sled.PercepSync
{
    internal class Config
    {
        public static string DefaultPercepStreamAddress = "tcp://*:12345";
        public static bool DefaultEnablePreview = false;
        public static int DefaultRdzvServerPort = 13331;
        public static bool DefaultEnableTts = false;
        public static string DefaultTtsAddress = "tcp://*:12346";
        public static bool DefaultEnableStt = false;
        public static double DefaultFps = 5;

        public string PercepStreamAddress { get; set; } = DefaultPercepStreamAddress;
        public bool EnablePreview { get; set; } = DefaultEnablePreview;
        public int RdzvServerPort { get; set; } = DefaultRdzvServerPort;
        public bool EnableTts { get; set; } = DefaultEnableTts;
        public string TtsAddress { get; set; } = DefaultTtsAddress;
        public bool EnableStt { get; set; } = DefaultEnableStt;
        public double Fps { get; set; } = DefaultFps;
        public AzureSpeechConfig AzureSpeechConfig { get; set; } = new();
        public LocalConfig? LocalConfig { get; set; } = null;
        public HoloLensConfig? HoloLensConfig { get; set; } = null;
    }

    internal class LocalConfig
    {
        public static string DefaultCameraDeviceId = "/dev/video0";
        public static string DefaultAudioInputDeviceName = "plughw:0,0";
        public static string DefaultAudioOutputDeviceName = "plughw:0,0";

        public string CameraDeviceId { get; set; } = DefaultCameraDeviceId;
        public string AudioInputDeviceName { get; set; } = DefaultAudioInputDeviceName;
        public string AudioOutputDeviceName { get; set; } = DefaultAudioOutputDeviceName;
    }

    internal class HoloLensConfig { }

    /// <summary>
    /// Configuration for Azure Speech service
    /// </summary>
    internal class AzureSpeechConfig
    {
        /// <summary>
        /// Subscription key
        /// </summary>
        public string SubscriptionKey { get; set; } = "";

        /// <summary>
        /// Location/Region
        /// </summary>
        public string Region { get; set; } = "";

        /// <summary>
        /// Speech Synthesis voice name. Refer to https://aka.ms/speech/voices/neural for full list.
        /// </summary>
        public string SpeechSynthesisVoiceName { get; set; } = "en-US-JennyNeural";
    }
}
