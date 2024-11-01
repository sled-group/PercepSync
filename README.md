# PercepSync

PercepSync provides a single, unified interface for synchronized perceptual data streamed from heterogeneous devices. Develop your embodied AI agent using your local webcam, and deploy the same agent with the HoloLens without any code changes!

## Quick Start

### Local Devices

Connect a webcam and a microphone to your machine, then download the latest binary for your operating system from the [Releases page](https://github.com/sled-group/PercepSync/releases) and run it.

```bash
# First make it executable
$ chmod +x PercepSync

# Now, run it!
$ ./PercepSync local

# If it complains about missing libasound, install it by running the following command
$ sudo apt install libasound2-dev

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

First, install the latest version of `PercepSyncHoloLensCapture` from the [Releases page](https://github.com/sled-group/PercepSync/releases) and install it on your HoloLens 2 by following the steps below:

1. Unzip the `PercepSyncHoloLensCapture` package.
2. Go to the Windows Device Portal for your HoloLens 2, then Views > Apps.
3. Under the Deploy apps section, ensure that you're on the Local Storage tab and choose the `.msixbundle` file from the unzipped package.
4. Press the install button.

Once installed, run `PercepSyncHoloLensCapture`. It automatically places a default config file called `PercepSyncHoloLensCaptureConfig.toml` in the `Documents` folder if it's not already there. The config file is pretty self-explanatory. You can set the address for the `PercepSync` server, as well as pick and choose which sensor to turn on. Note that not all sensors are currently supported by `PercepSync`. You can download `PercepSyncHoloLensCaptureConfig.toml` using the Windows Device Portal (System > Device Manager), modify it as desired and then reupload it.

Once you're satisfied with your config, download the matching version of `PercepSync` from the [Releases page](https://github.com/sled-group/PercepSync/releases) and run it.

```bash
# First make it executable
$ chmod +x PercepSync

# Now, run it!
$ ./PercepSync hololens

# If you want the preview window specify --enable-preview
$ ./PercepSync --enable-preview hololens
```

Now run `PercepSyncHoloLensCapture` on your HoloLens and start capturing. It'll automatically connect to `PercepSync`. You can then run the same Python script in another terminal to see what's being streamed.

```bash
# Install the required packages
$ pip install -r samples/requirements.txt

# Now, run it!
$ python samples/simple_subscriber.py
```

### Text-to-speech and Speech-to-text

You can enable text-to-speech and speech-to-text by passing the `--enable-tts` and `--enable-stt` options. `PercepSync` relies on [Microsoft Azure Speech Service](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/speech-sdk) to handle speech, so make sure you also pass in your Azure credentials via a config file. In the local mode, the speech will be played via the speaker, while in the HoloLens mode, the speech will be played on the HoloLens.

**NOTE: Microsoft Azure Speech Service SDK relies on OpenSSL 1.x, which is no longer shipped with Ubuntu 22.04. As a result, you need to install OpenSSL 1.x from sources. Instructions can be found [here](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/quickstarts/setup-platform?tabs=linux%2Cubuntu%2Cdotnetcli%2Cdotnet%2Cjre%2Cmaven%2Cnodejs%2Cmac%2Cpypi&pivots=programming-language-csharp#platform-requirements). Please make sure you set the environment variable `SSL_CERT_DIR=/etc/ssl/certs`.**

```bash
$ cat config.toml
[azure_speech_config]
subscription_key = "your-azure-subscription-key"
region = "your-region"

# local mode
$ ./PercepSync --config-file config.toml --enable-tts local

# hololens mode
$ ./PercepSync --config-file config.toml --enable-tts hololens
```

Now in another terminal, run the sample scripts.

```bash
# Install the required packages
$ pip install -r samples/requirements.txt

# TTS
$ python samples/simple_tts.py
TTS Text: Hello, world!

# SST
$ python samples/simple_subscriber.py
Transcribed Text: Hello, world!
```

## Configuration

### `PercepSync`

You can configure `PercepSync` via command line options as well as a configuration file. All options are available via the configuration file, but not via command line options. You can refer to the help message via the command line, or [Config.cs](src/PercepSync/Config.cs) for more details. Note that the Pascal Case property names, e.g., `EnablePreview`, are translated into snake case, e.g., `enable_preview` in the toml configuration file.

### `PercepSyncHoloLensCapture`

You can configure `PercepSyncHoloLensCapture` via a configuration file `PercepSyncHoloLensCaptureConfig.toml` placed into the `Documents` folder of the HoloLens 2. If it's already not there, `PercepSyncHoloLensCapture` will create a default one. In order to modify `PercepSyncHoloLensCaptureConfig.toml` download it using the Windows Device Portal (System > Device Manager), modify it as desired and then reupload it. You can refer to [Config.cs](src/PercepSyncHoloLensCapture/Config.cs) to see which configuration options are available.

## Perceptual Sensor Stream Data Format

`PercepSync` uses [ZeroMQ](https://zeromq.org/) to publish data from different input devices. Data that can be synchronized will be synchronized and published to a single topic. The serialization format is [MessagePack](https://msgpack.org/).

Currently, one topic for synchronized perception data is available:

- `perception`

```python
"""
This packet of data is synchronized based on the FPS rate, which can be configured via the configuration file. For example, if the FPS rate is 5 (default), PercepSync will generate this packet roughly every 1/5 = 0.2 seconds, and the audio buffer will roughly be 0.2 seconds long.

If speech is detected, a transcribed text will be included in the packet. Otherwise, it'll be an empty string. Note that the transcribed text will be included in the packet at the end of the speech, since we can't go back in time.
"""
{
    "message": {
        "frame": {
            "pixelData": bytes, # raw pixels in RGB 24-bit for a single frame
            "width": int,
            "height": int,
            "stride": int,
        },
        "audio": {
            "buffer": bytes, # audio buffer in 16KHz, 1 channel, 16-bit PCM
        },
        "transcribedText": {
            "text": str, # if no speech is detected, empty string.
        },
    },
    "originatingTime": int,
}
```

## Text-to-speech Data Format

`PercepSync` uses [ZeroMQ](https://zeromq.org/) to accept text-to-speech requests data from different clients. It uses the [Push-Pull pattern](https://learning-0mq-with-pyzmq.readthedocs.io/en/latest/pyzmq/patterns/pushpull.html). The serialization format is [MessagePack](https://msgpack.org/). Please see the [sample script](samples/simple_tts.py) for more details. See below for the request format:

```python
{
    "text": str
}
```

## Switching Local Devices on Linux

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

By default, `PercepSync` uses `plughw:0,0` as both input and output devices, but if you want to use another audio device, you can pass it in using the `--audio-input-device-name` and `--audio-output-device-name` options. The first number refers to the "card" number, and the second number refers to the "device" number. You can find out all the output devices with `aplay -l`, and input devices with `arecord -l`.

```bash
# For output devices.
$ aplay -l
**** List of PLAYBACK Hardware Devices ****
card 0: Device [Device], device 3: HDMI 0 [HDMI 0]
  Subdevices: 1/1
  Subdevice #0: subdevice #0
...

$ arecord -l
**** List of CAPTURE Hardware Devices ****
card 1: Device [Device], device 0: USB Audio [USB Audio]
  Subdevices: 0/1
  Subdevice #0: subdevice #0
...

$ ./PercepSync local --audio-output-device-name plughw:0,3 --audio-input-device-name plughw:1,0
```

## Development

### `PercepSync`

The main server application `PercepSync` is a cross-platform application that targets both Linux and Windows. On Linux, it runs on .NET 7.0 while on Windows it runs on .NET Framework 4.7.2. All of these versions of .NET may be confusing, and you can read more about the history of .NET [here](https://learn.microsoft.com/en-us/dotnet/core/introduction#net-history).

If you're making changes only to `PercepSync`, it's often most convenient to develop on Linux. We recommend using Visual Studio Code to do so as it supports C# and .NET quite well. Open the root of the repository using Visual Studio Code, and place the following files under the `.vscode` folder to set up debugging with `PercepSync`.

- `tasks.json`

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "build-percepsync-net7.0",
      "command": "dotnet",
      "type": "process",
      "args": [
        "build",
        "${workspaceFolder}/src/PercepSync/PercepSync.csproj",
        "-f",
        "net7.0",
        "/property:GenerateFullPaths=true",
        "/consoleloggerparameters:NoSummary;ForceNoAlign"
      ],
      "problemMatcher": "$msCompile"
    },
    {
      "label": "publish-percepsync-net7.0",
      "command": "dotnet",
      "type": "process",
      "args": [
        "publish",
        "-f",
        "net7.0",
        "${workspaceFolder}/src/PercepSync/PercepSync.csproj",
        "/property:GenerateFullPaths=true",
        "/consoleloggerparameters:NoSummary;ForceNoAlign"
      ],
      "problemMatcher": "$msCompile"
    },
    {
      "label": "watch-percepsync-net7.0",
      "command": "dotnet",
      "type": "process",
      "args": [
        "watch",
        "run",
        "--project",
        "${workspaceFolder}/src/PercepSync/PercepSync.csproj",
        "${workspaceFolder}/PercepSync.sln",
        "-f",
        "net7.0"
      ],
      "problemMatcher": "$msCompile"
    }
  ]
}
```

- `launch.json`

```json
{
  // Use IntelliSense to learn about possible attributes.
  // Hover to view descriptions of existing attributes.
  // For more information, visit: https://go.microsoft.com/fwlink/?linkid=830387
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Debug PercepSync",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build-percepsync-net7.0",
      "program": "${workspaceFolder}/src/PercepSync/bin/Debug/net7.0/PercepSync.dll",
      "args": [
        // Your command line arguments go here...
        // For example...
        "--config-file",
        "config.toml",
        "--enable-tts",
        "hololens"
      ],
      "cwd": "${workspaceFolder}",
      "env": {
        "SSL_CERT_DIR": "/etc/ssl/certs"
      },
      "console": "integratedTerminal",
      "stopAtEntry": false,
      "justMyCode": false
    },
    {
      "name": ".NET Core Attach",
      "type": "coreclr",
      "request": "attach"
    }
  ]
}
```

We currently don't have integration testing between `PercepSync` and `PercepSyncHoloLensCapture`, so if you're making a substantial change, you should manually test it by running an official release of `PercepSyncHoloLensCapture` on a HoloLens 2 (or even an emulator).

There may be cases where you need to develop `PercepSync` on Windows, e.g., you need to make changes for both `PercepSync` and `PercepSyncHoloLensCapture` as described below. In this case, you can use Visual Studio to do so.

### `PercepSyncHoloLensCapture`

`PercepSyncHoloLensCapture` is a [UWP](https://learn.microsoft.com/en-us/windows/uwp/get-started/universal-application-platform-guide) application, and therefore can only be developed on a Windows machine using Visual Studio. Open the solution file `PercepSync.sln` at the root of the repository with Visual Studio to start developing. You can follow the instructions from the [`psi-samples`](https://github.com/microsoft/psi-samples/tree/main/Samples/HoloLensSample#building-and-deploying) repository to set things up so that you can load your code directly onto your HoloLens 2.

### `PercepSync` and `PercepSyncHoloLensCapture` together

Sometimes you may need to make changes on both applications. In this case, your best bet is to use Visual Studio on Windows. You can set it up so that both can be run at the same time by setting up [multiple startup projects](https://learn.microsoft.com/en-us/visualstudio/ide/how-to-set-multiple-startup-projects?view=vs-2022).

### Code Quality and Pre-commit Hooks

We use various tools to maintain code quality automatically. These tools are automatically run on every PR against the main branch and every PR committed to it. They are also run locally on every commit as pre-commit hooks. You can also set up some of the tools with your IDE so they are run every time you save a file, which makes the whole process a lot smoother.

#### Pre-commit Hooks

```bash
# Install pre-commit by following the instructions specified here: https://pre-commit.com/#install
# For MacOS, we recommend using homebrew.
# For Linux, use the 0-dependency zipapp. If you choose tor un `sudo pip install pre-commit` instead,
# just be mindful that it may affect your virtualenvs.

# Install pre-commit hooks to the repo
$ pre-commit install

# or with 0-dependency zipapp (version 3.4.0)
$ python path/to/pre-commit-3.4.0.pyz install

# Install all the necessary local tools
$ dotnet tool restore
```

#### Code Formatter

We use [CSharpier](https://csharpier.com/) as our automatic code-formatter. It is automatically run against every commit as part of pre-commit hooks. However, it is highly recommended that you set up your text editor or IDE to run it automatically after each save. For VSCode, you can install the [official extension](https://marketplace.visualstudio.com/items?itemName=csharpier.csharpier-vscode).

## Release

Simply create a tag of form `v\d+.\d+.\d+` and push it. Both Linux and Windows versions of `PercepSync` as well as `PercepSyncHoloLensCapture` will be automatically built, then added it to an auto-generated release page.
