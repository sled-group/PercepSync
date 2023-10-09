import zmq
import msgpack

socket = zmq.Context().socket(zmq.PUSH)
socket.connect('tcp://localhost:12346')

while True:
    text = input("TTS Text: ")
    socket.send(msgpack.packb({"text": text}))
