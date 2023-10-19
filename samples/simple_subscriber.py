import zmq
from PIL import Image
import msgpack
import cv2
import numpy as np
import simpleaudio as sa

socket = zmq.Context().socket(zmq.SUB)
socket.connect('tcp://localhost:12345')
socket.setsockopt(zmq.SUBSCRIBE, b'') # NOTE: b'' means all topics

while True:
    topic, message = socket.recv_multipart()
    data = msgpack.unpackb(message)
    if topic == b'perception':
        frame_data = data['message']['frame']
        frame = Image.frombytes('RGB', (frame_data['width'], frame_data['height']), frame_data['pixelData'])
        cv2.imshow("Webcam", np.array(frame))
        sa.play_buffer(data['message']['audio']['buffer'], 1, 2, 16000)
        if data["message"]["transcribedText"]["text"] != '':
            print(f'Transcribed Text: {data["message"]["transcribedText"]["text"]}')
    else:
        raise Exception(f'Unknown topic: {topic}')

    c = cv2.waitKey(1)
    if c == 27:
        break

cv2.destroyAllWindows()
