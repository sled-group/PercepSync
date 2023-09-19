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
    print(data['originatingTime'])
    if topic == b'videoFrame':
        image_data = data['message']
        image = Image.frombytes('RGB', (image_data['width'], image_data['height']), image_data['pixelData'])
        cv2.imshow("Webcam", np.array(image))
    elif topic == b'audio':
        sa.play_buffer(data['message']['data'], 1, 2, 16000)
    else:
        raise Exception(f'Unknown topic: {topic}')

    c = cv2.waitKey(1)
    if c == 27:
        break

cv2.destroyAllWindows()
