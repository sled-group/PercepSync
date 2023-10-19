// Copied from https://github.com/microsoft/psi-samples/blob/760862bdc435288eca7dbd892979353e3d5318a7/Samples/LinuxSpeechSample/ContinuousSpeechRecognizer.cs
// with some modifications.
namespace Sled.PercepSync
{
    using System;
    using Microsoft.CognitiveServices.Speech;
    using Microsoft.CognitiveServices.Speech.Audio;
    using Microsoft.Psi;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Components;

    /// <summary>
    /// Component that wraps the Azure Cognitive Services speech recognizer.
    /// </summary>
    public class ContinuousAzureSpeechRecognizer
        : ConsumerProducer<AudioBuffer, string>,
            ISourceComponent,
            IDisposable
    {
        private readonly PushAudioInputStream pushStream;
        private readonly AudioConfig audioInput;
        private readonly SpeechRecognizer recognizer;

        private string recognizedText = "";

        /// <summary>
        /// Initializes a new instance of the <see cref="ContinuousSpeechRecognizer"/> class.
        /// </summary>
        /// <param name="pipeline">The pipeline in which to create the component.</param>
        /// <param name="subscriptionKey">The subscription key for the Azure speech resource.</param>
        /// <param name="region">The service region of the Azure speech resource.</param>
        public ContinuousAzureSpeechRecognizer(
            Pipeline pipeline,
            string subscriptionKey,
            string region
        )
            : base(pipeline)
        {
            var config = SpeechConfig.FromSubscription(subscriptionKey, region);
            pushStream = AudioInputStream.CreatePushStream();
            audioInput = AudioConfig.FromStreamInput(pushStream);
            recognizer = new SpeechRecognizer(config, audioInput);
        }

        /// <inheritdoc/>
        public void Start(Action<DateTime> notifyCompletionTime)
        {
            recognizer.Recognized += Recognizer_Recognized;
            recognizer.StartContinuousRecognitionAsync().Wait();
            notifyCompletionTime(DateTime.MaxValue);
        }

        /// <inheritdoc/>
        public void Stop(DateTime finalOriginatingTime, Action notifyCompleted)
        {
            recognizer.Recognized -= Recognizer_Recognized;
            pushStream.Close();
            recognizer.StopContinuousRecognitionAsync().Wait();
            notifyCompleted();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            recognizer.Dispose();
            audioInput.Dispose();
            pushStream.Dispose();
        }

        /// <inheritdoc/>
        protected override void Receive(AudioBuffer data, Envelope envelope)
        {
            pushStream.Write(data.Data);
            Out.Post(recognizedText, envelope.OriginatingTime + data.Duration);
            if (recognizedText != "")
            {
                recognizedText = "";
            }
        }

        /// <summary>
        /// Handler for the speech recognized event from the recognizer. Sets the recognized text to be posted.
        /// </summary>
        private void Recognizer_Recognized(object? sender, SpeechRecognitionEventArgs e)
        {
            recognizedText = e.Result.Text;
        }
    }
}
