import zmq
from PIL import Image
import msgpack
import cv2
import numpy as np

socket = zmq.Context().socket(zmq.SUB)
socket.connect('tcp://localhost:12345')
socket.setsockopt(zmq.SUBSCRIBE, b'video-frame') # NOTE: b'' means all topics

while True:
    topic, message = socket.recv_multipart()
    data = msgpack.unpackb(message)
    print(data['originatingTime'])
    image_data = data['message']
    image = Image.frombytes('RGBA', (image_data['width'], image_data['height']), image_data['pixelData'])
    cv2.imshow("Webcam", cv2.cvtColor(np.array(image), cv2.COLOR_RGB2BGR))

    c = cv2.waitKey(1)
    if c == 27:
        break

cv2.destroyAllWindows()
