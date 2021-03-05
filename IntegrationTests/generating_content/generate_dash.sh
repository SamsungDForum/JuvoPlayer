fps=30;
seconds=60;
name="dashtest";

path="testcontent/$name"

mkdir -p "$path"
cd $path
ffmpeg -y -re \
	-f lavfi -i color=color=black -f lavfi -i aevalsrc="sin(1000*t*2*PI*t/$seconds)":s=44100:d=$seconds \
	-c:v libx264 -r $fps \
	-c:a aac -b:a 128k \
	-pix_fmt yuv420p \
	-t $seconds \
	-map 0:v:0 -map 0:v:0 -map 0:v:0 -map 1:a:0 \
	-b:v:0 6500000  -filter:v:0 "scale=1280:720, fps=30, drawtext=fontfile=../../Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*2:text='%{eif\:t*10\:d}' \
		, drawtext=fontfile=../../Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*7/2:text='%{eif\:w\:d}x%{eif\:h\:d}'" \
	-b:v:1 10000000  -filter:v:1 "scale=1920:1080, fps=30, drawtext=fontfile=../../Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*2:text='%{eif\:t*10\:d}' \
		, drawtext=fontfile=../../Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*7/2:text='%{eif\:w\:d}x%{eif\:h\:d}'" \
	-b:v:2 44000000 -filter:v:2 "scale=3840:2160, fps=30, drawtext=fontfile=../../Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*2:text='%{eif\:t*10\:d}' \
		, drawtext=fontfile=../../Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*7/2:text='%{eif\:w\:d}x%{eif\:h\:d}'" \
	-adaptation_sets "id=0,seg_duration=2,streams=v  id=1,seg_duration=2,streams=a" \
	-f dash manifest.mpd

cd ../..

