FROM wawsrvbuild58:5000/atr:latest

MAINTAINER k.krupa

# Set the timezone so that installing python3-opencv does not require user interaction
ENV TZ=Europe/Warsaw

RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

RUN apt-get update -y

RUN apt install python3 python3-pip -y

RUN apt-get install python3-opencv -y

RUN apt install tesseract-ocr libtesseract-dev ffmpeg -y

RUN python3 -m pip install --upgrade pip

RUN apt-get install libsasl2-dev python-dev libldap2-dev libssl-dev libsnmp-dev -y

COPY requirements.txt requirements.txt

RUN python3 -m pip install Pillow

RUN python3 -m pip install -r requirements.txt

ADD https://github.com/tesseract-ocr/tessdata/raw/master/eng.traineddata /usr/share/tesseract-ocr/4.00/tessdata/eng.traineddata
