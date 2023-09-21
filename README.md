# PercepSync

## Quick Start

Connect a webcam and a microphone to your machine, then download the latest binary from the [Releases page](https://github.com/sled-group/PercepSync/releases) and run it.

```bash
# First make it executable
$ chmod +x PercepSync

# Now, run it!
$ ./PercepSync
```

You can then run the sample Python script to see what's being streamed. Make sure you have access to a display.

```bash
# Install the required packages
$ pip install -r samples/requirements.txt

# Now, run it!
$ python samples/simple_subscriber.py
```

## Switching Input Devices

### Video

By default, `PercepSync` uses `/dev/video0`, but if you want to use another video device, you can pass it in using the `--camera-device-id` option.

```bash
# First find out available video devices.
$ ls -ltrh /dev/video*
crw-rw----+ 1 root video 81, 1 Sep 21 08:50 /dev/video1
crw-rw----+ 1 root video 81, 0 Sep 21 08:50 /dev/video0

# Let's use /dev/video1
$ ./PercepSync --camera-device-id /dev/video1
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

$ ./PercepSync --audio-device-name plughw:2,0
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
