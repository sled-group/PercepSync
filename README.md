# PercepSync

## Quick Start

### Local Devices

Connect a webcam and a microphone to your machine, then download the latest binary from the [Releases page](https://github.com/sled-group/PercepSync/releases) and run it.

```bash
# First make it executable
$ chmod +x PercepSync

# Now, run it!
$ ./PercepSync local

# If you want the preview window specify --enable-preview
$ ./PercepSync --enable-preview local
```

You can then run the sample Python script in another terminal to see what's being streamed. Make sure you have access to a display. Please use this Python script as a reference on how to stream data from `PercepSync` using Python.

```bash
# Install the required packages
$ pip install -r samples/requirements.txt

# Now, run it!
$ python samples/simple_subscriber.py
```

### HoloLens

First, place a file named `CaptureServerIP.txt` in the `Documents` folder of your HoloLens with the IP address of the server you're going to run `PercepSync` from. Then, download the latest binary from the [Releases page](https://github.com/sled-group/PercepSync/releases) and run it.

```bash
# First make it executable
$ chmod +x PercepSync

# Now, run it!
$ ./PercepSync hololens

# If you want the preview window specify --enable-preview
$ ./PercepSync --enable-preview hololens
```

Now run the `HoloLensCaptureApp` on your HoloLens and start capturing. It'll automatically connect to `PercepSync`. You can then run the same Python script in another terminal to see what's being streamed.

```bash
# Install the required packages
$ pip install -r samples/requirements.txt

# Now, run it!
$ python samples/simple_subscriber.py
```

**NOTE: Audio streaming from HoloLens doesn't work yet and it just plays loud static noise. This will be fixed in the next version.**

## Data Format

`PercepSync` uses [ZeroMQ](https://zeromq.org/) to publish data from different input devices. Data that can be synchronized will be synchronized and published to a single topic. The serialization format is [MessagePack](https://msgpack.org/).

Here's the list of available topics and their data formats:

- `videoFrame`

```python
{
    "message": {
        "pixelData": bytes, # raw pixels in RGB 24-bit for a single frame
        "width": int,
        "height": int,
        "stride": int,
    },
    "originatingTime": int,
}
```

- `audio`

```python
{
    "message": {
        "buffer": bytes, # audio buffer in 16KHz, 1 channel, 16-bit PCM
    },
    "originatingTime": int,
}
```

**NOTE: Synchronizing a single video frame and an audio buffer conceptually do not make sense since they operate on different frequencies. What we could do is to pair up a list of video frames and an audio buffer within the same timeframe. Let us know if you need this, and we'll implement it.**

## Switching Local Devices

### Video

By default, `PercepSync` uses `/dev/video0`, but if you want to use another video device, you can pass it in using the `--camera-device-id` option.

```bash
# First find out available video devices.
$ ls -ltrh /dev/video*
crw-rw----+ 1 root video 81, 1 Sep 21 08:50 /dev/video1
crw-rw----+ 1 root video 81, 0 Sep 21 08:50 /dev/video0

# Let's use /dev/video1
$ ./PercepSync local --camera-device-id /dev/video1
```

### Audio

By default, `PercepSync` uses `plughw:0,0`, but if you want to use another audio device, you can pass it in using the `--audio-device-name` option.

```bash
$ pacmd list-sources
2 source(s) available.
  * index: 1
    ...truncated
        alsa.device = "0"
        alsa.card = "2"
    ...truncated

$ ./PercepSync local --audio-device-name plughw:2,0
```

## Development

```bash
# Install pre-commit by following the instructions specified here: https://pre-commit.com/#install
# For MacOS, we recommend using homebrew.
# For Linux, use the 0-dependency zipapp. If you choose tor un `sudo pip install pre-commit` instead,
# just be mindful that it may affect your virtualenvs.

# Install pre-commit hooks
$ pre-commit install

# Install all the necessary local tools
$ dotnet tool restore
```

### Code Quality and Pre-commit Hooks

#### Code Formatter

We use [CSharpier](https://csharpier.com/) as our automatic code-formatter. It is automatically run against every commit as part of pre-commit hooks. However, it is highly recommended that you set up your text editor or IDE to run it automatically after each save. For VSCode, you can install the [official extension](https://marketplace.visualstudio.com/items?itemName=csharpier.csharpier-vscode).
