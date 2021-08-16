import os
import shutil
import cv2
import pytesseract
import statistics
import numpy
from juvo_test_utils import Action
import matplotlib.pyplot as plt


class VideoAnalyzer(object):

    class Timestamp(object):
        def __init__(self, time=None, stamp=None, resolution=None):
            self.time = time
            self.stamp = stamp
            self.resolution = resolution

        def is_after(self, previous):
            return self.stamp > previous.stamp

        def is_before(self, previous):
            return self.stamp < previous.stamp

        def significantly_after(self, previous, pace):
            return self.stamp - previous.stamp > 2*pace*(self.time-previous.time)

        def resolution_changed(self, previous):
            return len(previous.resolution) > 3 and len(self.resolution) > 3 and previous.resolution != self.resolution

        def is_empty(self):
            return self.stamp == -1

    cutouts = {
        1080: {'time': [100, 230, 750, 1200], 'res': [230, 360, 600, 1350]},
        270: {'time': [26, 56, 180, 290], 'res': [56, 82, 150, 330]},
        576: {'time': [64, 120, 280, 430], 'res': [120, 180, 200, 510]}
    }

    def __init__(self, path, interval=1, pace=10):
        self.path = path
        self.interval = interval
        self.pace = pace
        self.scale_percent = 100

    def run(self):
        frames_paths = sorted(os.listdir(self.path), key=self.numeric_order)

        self.prepare_cache()
        resolutions = open("video_cache/resolutions.txt", "w+")
        numbers = open("video_cache/times.txt", "w+")
        numbers_count = []

        times = numpy.arange(0.0, len(frames_paths), self.interval)
        for time, frame_path in zip(times, frames_paths):
            frame = self.get_frame(frame_path)
            self.save_img(self.cutout(
                frame, VideoAnalyzer.cutouts[frame.shape[0]]['res']), 'resolution', time)
            digit_frames = self.bounding_boxes(self.cutout(
                frame, VideoAnalyzer.cutouts[frame.shape[0]]['time']))
            numbers_count.append(len(digit_frames))
            for idx, img in enumerate(digit_frames):
                self.save_img(img, 'time', f'{time}.{idx}')
                numbers.write(f"video_cache/time{time}.{idx}.png\n")
            resolutions.write(f"video_cache/resolution{time}.png\n")

        resolutions.close()
        numbers.close()

        resolutions = self.split(pytesseract.image_to_string(
            "video_cache/resolutions.txt", config="--psm 7 --oem 1 -l eng -c tessedit_char_whitelist=0123456789Ox "))
        numbers = self.split(pytesseract.image_to_string(
            "video_cache/times.txt", config="--psm 7 --oem 1 -l eng -c tessedit_char_whitelist=0123456789Ox "))
        print(numbers)
        timestamps = []
        curr_num_idx = 0
        for index, time in enumerate(times):
            stamp = ''.join(
                numbers[curr_num_idx:curr_num_idx+numbers_count[index]])
            resolution = resolutions[index]
            timestamps.append(self.Timestamp(
                time, self.intTryParse(stamp), resolution))
            curr_num_idx += numbers_count[index]

        for stamp in timestamps:
            print(stamp.stamp)

        actions = self.detect_actions(timestamps)
        for action in actions:
            print(action.name, action.time,
                  action.seek_destination, action.new_resolution)
        return actions

    def numeric_order(self, str):
        return len(str), str.lower()

    def split(self, text):
        return text.replace(u"\n", "").replace(u"\x0c", "*").split('*')[0:-1]

    def prepare_image(self, image):
        _, image = cv2.threshold(image, 150, 255, cv2.THRESH_BINARY)
        width = int(image.shape[1] * self.scale_percent / 100)
        height = int(image.shape[0] * self.scale_percent / 100)
        dim = (width, height)
        image = cv2.resize(image, dim)
        return image

    def prepare_cache(self):
        shutil.rmtree('video_cache', ignore_errors=True)
        os.mkdir('video_cache')

    def clear_cache(self):
        shutil.rmtree('video_cache')

    def cutout(self, frame, box):
        res_img = frame[box[0]:box[1], box[2]:box[3]]
        res_img = self.prepare_image(res_img)
        return res_img

    def bounding_boxes(self, img):
        img = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
        contours, hierarchy = cv2.findContours(img, 1, 2)
        imgs = []
        for contour in sorted(contours, key=lambda contour: cv2.boundingRect(contour)[0]):
            if cv2.contourArea(contour) < 500 and cv2.contourArea(contour) > 70:
                [X, Y, W, H] = cv2.boundingRect(contour)
                WHITE = [255, 255, 255]
                pad = 7
                imgs.append(cv2.copyMakeBorder(
                    img[Y:Y + H, X:X + W].copy(), pad, pad, pad, pad, cv2.BORDER_CONSTANT, value=WHITE))
        return imgs

    def save_img(self, image, name, number):
        cv2.imwrite(f'video_cache/{name}{number}.png', image)

    def get_frame(self, frame_path):
        frame = cv2.imread(self.path + '/' + frame_path)
        frame = cv2.resize(frame, (480, 270))
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
        if not previous.is_empty():
            actions.append(Action('play', previous.time))

        for now in timestamps[1:]:
            if(previous.is_empty() and not now.is_empty()):
                actions.append(Action('play', now.time))

            elif now.resolution_changed(previous) and not (previous.stamp == 0 and actions[-1].name == 'play' and actions[-1].time == previous.time):
                actions.append(Action('changeVideo', now.time,
                               new_resolution=now.resolution))

            elif now.is_before(previous):
                if(now.is_empty()):
                    actions.append(Action('stop', now.time))
                elif actions[-1].name != 'changeVideo' or abs(actions[-1].time-now.time) > 1:
                    actions.append(
                        Action('seek', now.time, seek_destination=now.stamp/self.pace))

            elif now.is_after(previous):
                if now.significantly_after(previous, self.pace):
                    actions.append(
                        Action('seek', now.time, seek_destination=now.stamp/self.pace))
                elif self.is_paused(actions):
                    actions.append(Action('resume', now.time))

            elif(now.stamp == previous.stamp and not now.is_empty() and not self.is_paused(actions)):
                actions.append(Action('pause', previous.time))

            previous = now
        return actions

    def is_paused(self, actions):
        return self.last_action_index(actions, 'pause') > max(
            self.last_action_index(actions, 'resume'),
            self.last_action_index(actions, 'play'))

    def last_action_index(self, actions, action):
        for i in reversed(range(len(actions))):
            if actions[i].name == action:
                return i
        return -1


if __name__ == "__main__":
    va = VideoAnalyzer('test_change_resolution')
    va.run()
