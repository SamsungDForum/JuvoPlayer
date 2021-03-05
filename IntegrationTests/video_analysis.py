import os
import shutil
import cv2
import pytesseract
import statistics
import numpy
from juvo_test_utils import Action

class VideoAnalyzer(object):

    class Timestamp(object):
        def __init__(self, time = None, stamp = None, resolution = None):
            self.time = time
            self.stamp = stamp
            self.resolution = resolution

    def __init__(self, path = None, pace=10):
        self.path = path
        self.pace = 10
        self.scale_percent = 25
    
    def run(self):
        vc = cv2.VideoCapture(self.path)
        if vc.isOpened():
             rval , frame = vc.read()
             self.fps = vc.get(cv2.CAP_PROP_FPS)
             frame_count = int(vc.get(cv2.CAP_PROP_FRAME_COUNT))
             self.duration = frame_count/self.fps
        else:
             raise OSError("Failed to open: "+self.path)

        self.prepare_cache()
        resolutions = open("video_cache/resolutions.txt","w+")
        numbers = open("video_cache/times.txt","w+")
        frequency = 1.0
        times = numpy.arange(0.0,round(self.duration*frequency)/frequency,1/frequency)
        for time in times:
            frame = self.get_frame(vc,time)
            self.save_img(self.get_resolution(frame), 'resolution', time)
            self.save_img(self.get_time(frame), 'time', time)
            resolutions.write(f"video_cache/resolution{time}.png\n")
            numbers.write(f"video_cache/time{time}.png\n")

        vc.release()
        resolutions.close()
        numbers.close()

        resolutions = self.split(pytesseract.image_to_string("video_cache/resolutions.txt", config="--psm 7 --oem 1 -l eng -c tessedit_char_whitelist=0123456789Ox "))
        numbers = self.split(pytesseract.image_to_string("video_cache/times.txt", config="--psm 7 --oem 1 -l eng -c tessedit_char_whitelist=0123456789Ox "))
        timestamps = []
        for index,time in enumerate(times):
            stamp = numbers[index]
            resolution = resolutions[index]
            timestamps.append(self.Timestamp(time, self.intTryParse(stamp), resolution))


        self.clear_cache()
        actions = self.detect_actions(timestamps)
        for action in actions:
            print(action.name, action.time, action.seek_destination, action.new_resolution)
        return actions

    def split(self, text):
        return text.replace(u"\n", "").replace(u"\x0c", "*").split('*')[0:-1]

    def prepare_image(self, image):
        ret,image = cv2.threshold(image,127,255,cv2.THRESH_BINARY_INV)
        width = int(image.shape[1] * self.scale_percent / 100)
        height = int(image.shape[0] * self.scale_percent / 100)
        dim = (width, height)
        image = cv2.resize(image, dim)
        return image

    def prepare_cache(self):
        if(os.path.isdir('video_cache')):
            shutil.rmtree('video_cache')
        os.mkdir('video_cache')

    def clear_cache(self):
        shutil.rmtree('video_cache')

    def get_resolution(self, frame):
        res_img = frame[230:360, 600:1350]
        res_img = self.prepare_image(res_img)
        return res_img

    def get_time(self, frame):
        time_img = frame[100:230, 750:1200]
        time_img = self.prepare_image(time_img)
        return time_img

    def save_img(self, image, name, number):
        cv2.imwrite(f'video_cache/{name}{number}.png', image)

    def get_frame(self,video_capture,start_time,timespan=1,sample_count=1):
        offset = start_time*self.fps
        video_capture.set(1, offset)
        rval, frame = video_capture.read()
        return frame

    def process_samples(self, samples):
        timestamps = []
        for sample in samples:
            time, succeded = self.process_time(sample)
            timestamps.append(time)

        return statistics.median_high(timestamps)

    def intTryParse(self, value):
        try:
            return int(value)
        except ValueError:
            return -1

    def detect_actions(self, timestamps):
        previous = timestamps[0]
        actions = []
        if(previous.stamp!=-1):
            actions.append(Action('play', previous.time))

        for index, now in enumerate(timestamps, start=1):
            if(len(previous.resolution)>3 and len(now.resolution)>3 and previous.resolution!=now.resolution):
                actions.append(Action('changeVideo', now.time, new_resolution = now.resolution))

            if(previous.stamp==-1 and now.stamp!=-1):
                actions.append(Action('play', now.time))

            if(previous.stamp-now.stamp>2*self.pace*(now.time-previous.time)):
                if(now.stamp==-1):
                    actions.append(Action('stop',now.time))
                elif(not(actions[-1].name=='changeVideo' and abs(actions[-1].time-now.time)<=1)):
                    actions.append(Action('seek',now.time, seek_destination = now.stamp/self.pace))

            if(now.stamp-previous.stamp>0 and previous.stamp!=-1):
                if(now.stamp-previous.stamp>2*self.pace*(now.time-previous.time)):
                    actions.append(Action('seek',now.time, seek_destination = now.stamp/self.pace))
                elif(self.last_action_index(actions,'pause')>self.last_action_index(actions,'resume')):
                    actions.append(Action('resume',now.time))

            if(now.stamp==previous.stamp and (len(actions)==0 or actions[-1].name!='pause') and now.stamp!=-1):
                actions.append(Action('pause',previous.time))

            previous = now
        return actions

    def last_action_index(self, actions, action):
        for i in reversed(range(len(actions))):
            if actions[i].name == action:
                return i
        return -1

if __name__ == "__main__":
    a = VideoAnalyzer("test_playback_resumes.mp4")
    a.run()