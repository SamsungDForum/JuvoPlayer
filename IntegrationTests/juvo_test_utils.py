import os
import paramiko
from scp import SCPClient
from time import sleep
from subprocess import Popen, PIPE, DEVNULL
import requests
import inspect
import shutil

MAX_RETRIES = 10


class RestPlayer(object):
    def __init__(self, player_address, app_name):
        self.player_address = player_address
        self.app_name = app_name

    def start(self, server, username, password):
        self.ssh = paramiko.SSHClient()
        self.ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
        self.ssh.connect(server, username=username, password=password)
        self.ssh.exec_command('app_launcher -s ' + self.app_name)
        r = None
        for _ in range(MAX_RETRIES):
            try:
                r = requests.post(self.player_address, data={
                                  'order': 'state'}, timeout=2)
            except requests.exceptions.RequestException:
                continue
            if r.status_code == requests.codes.ok:
                return
        raise ConnectionError('Player did not respond')

    def send(self, body):
        return requests.post(self.player_address, data=body)

    def stop(self):
        self.ssh.exec_command('app_launcher -t ' + self.app_name)
        self.ssh.close()

    def cleanup_images(self):
        self.ssh.exec_command(
            'find /tmp -name "source_JuvoPlayer.RESTful*.png" -delete')

    def pull_images(self, dirname, cleanup=True):
        os.mkdir(dirname)
        _, stdout, _ = self.ssh.exec_command(
            'ls /tmp | grep source_JuvoPlayer.RESTful')
        files = stdout.read().decode("utf-8").split('\n')[:-1]
        with SCPClient(self.ssh.get_transport()) as scp:
            for file in files:
                scp.get('/tmp/'+file, dirname)
        if(cleanup):
            self.cleanup_images()


class Action(object):
    def __init__(self, name, time, seek_destination=None, new_resolution=None):
        self.name = name
        self.time = time
        self.seek_destination = seek_destination
        self.new_resolution = new_resolution


class Scenario(object):
    def __init__(self, queue=None, sender=None, test_name=None):
        if test_name is not None:
            self.test_name = test_name
        else:
            self.test_name = inspect.stack()[1].function
        self.queue = queue
        self.sender = sender

    def cleanup(self):
        shutil.rmtree(self.test_name, ignore_errors=True)

    def run(self, record=True):
        if(record):
            response = self.sender.send({'order': 'screenon'})
            if response.status_code != requests.codes.ok:
                return
        for message in self.queue:
            response = self.sender.send(message[0])
            if response.status_code != requests.codes.ok:
                break
            sleep(message[1])
        if(record):
            response = self.sender.send({'order': 'screenoff'})
            if response.status_code != requests.codes.ok:
                return
        self.cleanup()
        self.sender.pull_images(self.test_name, cleanup=True)
