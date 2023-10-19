namespace Sled.PercepSyncHoloLensCapture
{
    /// <summary>
    /// Top-level PercepSyncHoloLensCapture configuration
    /// </summary>
    public class Config
    {
        /// <summary>
        /// PercepSync configuration
        /// </summary>
        public PercepSyncConfig PercepSync { get; set; } = new PercepSyncConfig();

        /// <summary>
        /// Sensors configuration
        /// </summary>
        public SensorsConfig Sensors { get; set; } = new SensorsConfig();

        /// <summary>
        /// Configuration for videos captured by the main RGB camera.
        /// </summary>
        public VideoConfig Video { get; set; } = new VideoConfig();

        /// <summary>
        /// Configuration for infrared cameras
        /// </summary>
        public InfraredConfig Infrared { get; set; } = new InfraredConfig();

        /// <summary>
        /// Gray-scale camera config
        /// </summary>
        public GrayConfig Gray { get; set; } = new GrayConfig();

        /// <summary>
        /// Tts config
        /// </summary>
        public TtsConfig Tts { get; set; } = new TtsConfig();
    }

    /// <summary>
    /// PercepSync configuration
    /// </summary>
    public class PercepSyncConfig
    {
        /// <summary>
        /// Address of the PercepSync server
        /// </summary>
        public string Address { get; set; } = "0.0.0.0";
    }

    /// <summary>
    /// Configuration for which sensor to enable
    /// </summary>
    public class SensorsConfig
    {
        /// <summary>
        /// HoloLens 2 Diagnostics
        /// </summary>
        public bool Diagnostics { get; set; } = false;

        /// <summary>
        /// Main RGB Camera
        /// </summary>
        public bool Video { get; set; } = true;

        /// <summary>
        /// Main RGB Camera with mixed reality elements
        /// </summary>
        public bool Preview { get; set; } = false;

        /// <summary>
        /// Long-throw, low-frequency (1-5 FPS) far-depth sensing depth camera
        /// </summary>
        public bool Depth { get; set; } = false;

        /// <summary>
        /// Depth camera calibration map
        /// </summary>
        public bool DepthCalibrationMap { get; set; } = false;

        /// <summary>
        /// High-frequency (45 FPS) near-depth sensing depth camera used for hand tracking.
        /// </summary>
        public bool Ahat { get; set; } = false;

        /// <summary>
        /// AHAT depth camera calibration map
        /// </summary>
        public bool AhatCalibrationMap { get; set; } = false;

        /// <summary>
        /// Depth infrared camera
        /// </summary>
        public bool DepthInfrared { get; set; } = false;

        /// <summary>
        /// AHAT infrared camera
        /// </summary>
        public bool AhatInfrared { get; set; } = false;

        /// <summary>
        /// Gray-scale front cameras
        /// </summary>
        public bool GrayFrontCameras { get; set; } = false;

        /// <summary>
        /// Gray-scale front camera calibration map
        /// </summary>
        public bool GrayFrontCameraCalibrationMap { get; set; } = false;

        /// <summary>
        /// Gray-scale side cameras
        /// </summary>
        public bool GraySideCameras { get; set; } = false;

        /// <summary>
        /// Gray-scale side camera calibration map
        /// </summary>
        public bool GraySideCameraCalibrationMap { get; set; } = false;

        /// <summary>
        /// Inertial measurement unit, i.e., accelerometer
        /// </summary>
        public bool Imu { get; set; } = false;

        /// <summary>
        /// Head tracking
        /// </summary>
        public bool Head { get; set; } = false;

        /// <summary>
        /// Eye tracking
        /// </summary>
        public bool Eyes { get; set; } = false;

        /// <summary>
        /// Hands tracking
        /// </summary>
        public bool Hands { get; set; } = false;

        /// <summary>
        /// Audio captured by the microphone
        /// </summary>
        public bool Audio { get; set; } = true;

        /// <summary>
        /// Scene understanding
        /// </summary>
        public bool SceneUnderstanding { get; set; } = false;
    }

    /// <summary>
    /// Configuration for videos captured by the main RGB camera.
    /// See https://docs.microsoft.com/en-us/windows/mixed-reality/develop/platform-capabilities-and-apis/locatable-camera#hololens-2
    /// for different possible modes, such as: 896x504 @30/15; 960x540 @30,15; 1128x636 @30,15; 1280x720 @30/15 etc.
    /// </summary>
    public class VideoConfig
    {
        /// <summary>
        /// Frames per second
        /// </summary>
        public int Fps { get; set; } = 30;

        /// <summary>
        /// Width of frames in pixels
        /// </summary>
        public int Width { get; set; } = 896;

        /// <summary>
        /// Height of frames in pixels
        /// </summary>
        public int Height { get; set; } = 504;
    }

    /// <summary>
    /// Configuration for infrared cameras
    /// </summary>
    public class InfraredConfig
    {
        /// <summary>
        /// Whether to encode infrared cameras or not
        /// </summary>
        public bool Encode { get; set; } = false;
    }

    /// <summary>
    /// Configuration for gray-scale cameras
    /// </summary>
    public class GrayConfig
    {
        /// <summary>
        /// Encoding method for gray-scale cameras.
        /// </summary>
        public string EncodeMethod { get; set; } = "jpeg";
    }

    /// <summary>
    /// Configuration for text-to-speech
    /// </summary>
    public class TtsConfig
    {
        /// <summary>
        /// Max duration for text-to-speech audio in seconds. The longer it is, the more memory it takes.
        /// The buffer is circular, so if text-to-speech audio goes over this limit, the earlier speech will be overwritten.
        /// </summary>
        public double MaxTtsDurationInSeconds { get; set; } = 10;
    }
}
