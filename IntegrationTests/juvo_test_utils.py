import os
import paramiko
from time import sleep
from subprocess import Popen, PIPE, DEVNULL
import requests
import inspect

class Recorder(object):
    def __init__(self, ip, record_delay):
        self.ip = ip
        self.record_delay = record_delay

    def start(self, name):
        self.name = name
        self.recording = Popen(f'''ffmpeg -loglevel verbose -y
         -f h264 -avioflags +direct -fflags +nobuffer -i async:tcp://{self.ip}:5301
         -f s16le -ar 48k -ac 2 -avioflags +direct -fflags +nobuffer -i async:tcp://{self.ip}:5303
         -c:v copy -c:a aac -b:a 128k {name}.mkv'''.replace('\n', ' '), stdin=PIPE , shell=True, stderr=DEVNULL, stdout=DEVNULL)
        sleep(self.record_delay)

    def stop(self):
        sleep(self.record_delay)
        self.recording.communicate(input=b'q')
        p = Popen(f"ffmpeg -y -i {self.name}.mkv -c copy {self.name}.mp4", stderr=DEVNULL, stdout=DEVNULL)
        p.communicate()

    def cleanup(self):
        os.remove(f'{self.name}.mp4')
        os.remove(f'{self.name}.mkv')

class RestPlayer(object):
    def __init__(self, player_address):
        self.player_address = player_address
        
    def start(self, app_name, server, username, password):
        self.app_name = app_name
        self.ssh = paramiko.SSHClient()
        self.ssh.load_system_host_keys()
        self.ssh.connect(server, username=username, password=password)
        self.ssh.exec_command('app_launcher -s '+app_name)

    def send(self, body):
        return requests.post(self.player_address, data=body)
        
    def stop(self):
        self.ssh.exec_command('app_launcher -t '+self.app_name)
        self.ssh.close()

class Action(object):
    def __init__(self, name, time, seek_destination=None, new_resolution=None):
        self.name = name
        self.time = time
        self.seek_destination = seek_destination
        self.new_resolution = new_resolution

class Scenario(object):
    def __init__(self, queue=None, sender=None, name=None):
        if name is not None:
            self.name = name
        else:
            self.name = inspect.stack()[1].function
        self.queue = queue
        self.sender=sender

    def run(self, recorder=None):
        if(recorder):
            recorder.start(name=self.name)
        for message in self.queue:
            response = self.sender.send(message[0])
            if response.status_code != requests.codes.ok:
                break
            sleep(message[1])
        if(recorder):
            recorder.stop()
