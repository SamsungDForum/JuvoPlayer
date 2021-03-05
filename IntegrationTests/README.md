# JuvoPlayer integration tests
## Setup
1. Install streaming program on your TV board
2. Install [ffmpeg](https://ffmpeg.org/download.html) on your PC
3. Install Tesseract OCR for [Windows](https://github.com/UB-Mannheim/tesseract/wiki) or [Linux](https://medium.com/quantrium-tech/installing-tesseract-4-on-ubuntu-18-04-b6fcd0cbd78f) and add corresponding entry to PATH variable if neccessary
4. In the Tesseract-OCR directory replace the tessdata/eng.traineddata with [this](https://github.com/tesseract-ocr/tessdata/blob/master/eng.traineddata)
5. Install python dependencies by running
 ```javascript
   > pip install -r requirements.txt
```
6. Install JuvoPlayer.RESTful application on TV board
7. Adjust addresses in config.json to match your own
## Running the tests
1. If you are connecting to your TV from remote device, it may be neccessary to forward some ports:
 ```javascript
   > ssh -L VIDEO_PORT:localhost:VIDEO_PORT -L AUDIO_PORT:localhost:AUDIO_PORT -L 9998:localhost:9998 root@TV_IP
```
9998 is default port on which JuvoPlayer.RESTful is running

2. Run tests
 ```javascript
   > python -m pytest -s
```
