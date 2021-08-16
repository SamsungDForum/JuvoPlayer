# JuvoPlayer integration tests
IntegrationTests is intented only for internal use as JuvoPlayer.RESTful is an inhouse application
## Setup
### 1. Using docker
* follow the setup according to **ATR in Docker** setup manual in knowledge base
* build docker image from this directory:
 ```javascript
docker build -t juvo_integration .
```
* build JuvoPlayer.RESTful app using platform certificates (only for inhouse use), and move the **.tpk** it to IntegrationTests directory (preferably you can also remove *setup* and *generating_content* subdirectories)
  
* run the tests, remember to adjust custom docker arguments
 ```javascript
python3 atr_in_docker.py -f no --task-scenario JuvoOcrTest --log-level debug --dlog-tags JuvoPlayer UT DOTNET_LAUNCHER --docker-image juvo_integration --custom-docker-arguments="-v /full/path/to/IntegrationTests:/root/atr/src/tests/juvo_ocr_test:rw --env BOARD_USER=put_user_here --env BOARD_PASS=put_password_here"
```
### 2. Without using docker
* Install Tesseract OCR for [Windows](https://github.com/UB-Mannheim/tesseract/wiki) or [Linux](https://medium.com/quantrium-tech/installing-tesseract-4-on-ubuntu-18-04-b6fcd0cbd78f) and add corresponding entry to PATH variable if neccessary
* In the Tesseract-OCR directory replace the tessdata/eng.traineddata with [this](https://github.com/tesseract-ocr/tessdata/blob/master/eng.traineddata)
* Install python dependencies by running:
 ```javascript
   > pip install -r requirements.txt
```
* Install JuvoPlayer.RESTful application on TV board
* Adjust addresses in config.json to match your own
## Running the tests
1. If you are connecting to your TV from remote device, it may be neccessary to forward some ports:
 ```javascript
   > ssh -L 9998:localhost:9998 TV_USER@TV_IP
```
9998 is default port on which JuvoPlayer.RESTful is running

2. Run tests
 ```javascript
   > python -m pytest -s test_dash.py
```
