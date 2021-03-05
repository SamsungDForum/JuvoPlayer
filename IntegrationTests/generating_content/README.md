## Video codecs

- h264 - libx264
- hevc - libx265 -x265-params
- vp9  - libvpx-vp9
- vp8(only with -c:a libvorbis and .webm output) - libvpx
- mpeg4 - libxvid
- mpeg2 - mpeg2video

## Audio codecs

- aac - aac
- ac3 - ac3
- mp3 - libmp3lame
- mp2 - libtwolame
- eac3- eac3

> To get bash equivalents of these commands simply replace newline escaping ` and \\
## Initial settings
```javascript
$fps=30;
$seconds=60;
$name="single_stream";
```
## MP4
Powershell
```javascript
ffmpeg -y -re `
  -f lavfi -i color=color=black -f lavfi -i aevalsrc='sin(1000*t*2*PI*t/2.5)':s=44100:d=20 `
  -c:v libx264 -r $fps `
  -c:a aac -b:a 128k `
  -pix_fmt yuv420p `
  -t $seconds `
  -map 0:v:0 -map 0:v:0 -map 0:v:0 -map 1:a:0 `
  -b:v:0 600000  -filter:v:0 "scale=-2:360, fps=$fps, drawtext=fontfile=Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*2:text='%{eif\:t*10\:d}' `
	            , drawtext=fontfile=Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*7/2:text='%{eif\:w\:d}x%{eif\:h\:d}'" `
  -b:v:1 750000  -filter:v:1 "scale=-2:480, fps=$fps, drawtext=fontfile=Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*2:text='%{eif\:t*10\:d}' `
	            , drawtext=fontfile=Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*7/2:text='%{eif\:w\:d}x%{eif\:h\:d}'" `
  -b:v:2 1500000 -filter:v:2 "scale=-2:720, fps=$fps, drawtext=fontfile=Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*2:text='%{eif\:t*10\:d}' `
	            , drawtext=fontfile=Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*7/2:text='%{eif\:w\:d}x%{eif\:h\:d}'" `
  sample.mp4
```

## DASH
Powershell
```javascript
ffmpeg -y -re `
  -f lavfi -i color=color=black -f lavfi -i aevalsrc="sin(1000*t*2*PI*t/${seconds})":s=44100:d=$seconds `
  -c:v libx264 -r $fps `
  -c:a aac -b:a 128k `
  -pix_fmt yuv420p `
  -t $seconds `
  -map 0:v:0 -map 0:v:0 -map 0:v:0 -map 1:a:0 `
  -b:v:0 6500000  -filter:v:0 "scale=1280:720, fps=$fps, drawtext=fontfile=Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*2:text='%{eif\:t*10\:d}' `
	            , drawtext=fontfile=Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*7/2:text='%{eif\:w\:d}x%{eif\:h\:d}'" `
  -b:v:1 10000000  -filter:v:1 "scale=1920:1080, fps=$fps, drawtext=fontfile=Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*2:text='%{eif\:t*10\:d}' `
	            , drawtext=fontfile=Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*7/2:text='%{eif\:w\:d}x%{eif\:h\:d}'" `
  -b:v:2 44000000 -filter:v:2 "scale=3840:2160, fps=$fps, drawtext=fontfile=Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*2:text='%{eif\:t*10\:d}' `
	            , drawtext=fontfile=Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*7/2:text='%{eif\:w\:d}x%{eif\:h\:d}'" `
  -adaptation_sets "id=0,seg_duration=2,streams=v  id=1,seg_duration=2,streams=a" `
  -f dash manifest.mpd
  ```
Bash
```shell
ffmpeg -y -re \
  -f lavfi -i color=color=black -f lavfi -i aevalsrc='sin(1000*t*2*PI*t/2.5)':s=44100:d=20 \
  -c:v libx264 -r $fps \
  -c:a aac -b:a 128k \
  -pix_fmt yuv420p \
  -t $seconds \
  -map 0:v:0 -map 0:v:0 -map 0:v:0 -map 1:a:0 \
  -b:v:0 6500000  -filter:v:0 "scale=-2:720, fps=$fps, drawtext=fontfile=Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*2:text='%{eif\:t*10\:d}' \
	            , drawtext=fontfile=Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*7/2:text='%{eif\:w\:d}x%{eif\:h\:d}'" \
  -b:v:1 10000000  -filter:v:1 "scale=-2:1080, fps=$fps, drawtext=fontfile=Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*2:text='%{eif\:t*10\:d}' \
	            , drawtext=fontfile=Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*7/2:text='%{eif\:w\:d}x%{eif\:h\:d}'" \
  -b:v:2 44000000 -filter:v:2 "scale=-2:2160, fps=$fps, drawtext=fontfile=Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*2:text='%{eif\:t*10\:d}' \
	            , drawtext=fontfile=Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*7/2:text='%{eif\:w\:d}x%{eif\:h\:d}'" \
  -adaptation_sets "id=0,seg_duration=2,streams=v  id=1,seg_duration=2,streams=a" \
  -f dash dashtest.mpd
  ```

## DASH with HLS
Couldn't produce hls output by itself so far
```javascript
ffmpeg -y -re `
  -f lavfi -i color=color=black -f lavfi -i aevalsrc='sin(1000*t*2*PI*t/2.5)':s=44100:d=20 `
  -c:v libx264 -r $fps `
  -c:a aac -b:a 128k `
  -pix_fmt yuv420p `
  -t $seconds `
  -map 0:v:0 -map 0:v:0 -map 0:v:0 -map 1:a:0 `
  -b:v:0 600000  -filter:v:0 "scale=-2:360, fps=$fps, drawtext=fontfile=Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*2:text='%{eif\:t*10\:d}' `
	            , drawtext=fontfile=Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*7/2:text='%{eif\:w\:d}x%{eif\:h\:d}'" `
  -b:v:1 750000  -filter:v:1 "scale=-2:480, fps=$fps, drawtext=fontfile=Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*2:text='%{eif\:t*10\:d}' `
	            , drawtext=fontfile=Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*7/2:text='%{eif\:w\:d}x%{eif\:h\:d}'" `
  -b:v:2 1500000 -filter:v:2 "scale=-2:720, fps=$fps, drawtext=fontfile=Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*2:text='%{eif\:t*10\:d}' `
	            , drawtext=fontfile=Calibri.ttf: fontcolor=white:fontsize=(h/10):x=(w-text_w)/2:y=(text_h)*7/2:text='%{eif\:w\:d}x%{eif\:h\:d}'" `
  -hls_playlist 1 ` #create hls playlist aswell
  -adaptation_sets "id=0,seg_duration=2,streams=v  id=1,seg_duration=2,streams=a" `
  -f dash sample.mpd
```

## WIDEVINE
 Each stream needs descriptor with 'in=generated_basic.mp4,stream=<STREAM_NUM>,init_segment=<STREAM_NUM>/<STREAM_NUM>.mp4,segment_template=<STREAM_NUM>/$Number$.m4s' or similar pattern. Shaka organizes content into adaptation sets according to its own judgement.
 LicenseServerUri for this particular content : "https://proxy.uat.widevine.com/proxy?provider=widevine_test"
```javascript
.\packager-win.exe 'in=sample.mp4,stream=0,init_segment=0/0.mp4,segment_template=0/$Number$.m4s'  `
  'in=sample.mp4,stream=1,init_segment=1/1.mp4,segment_template=1/$Number$.m4s' `
  'in=sample.mp4,stream=2,init_segment=2/2.mp4,segment_template=2/$Number$.m4s' `
  'in=sample.mp4,stream=3,init_segment=3/3.mp4,segment_template=3/$Number$.m4s' `
  --segment_duration 2 --segment_sap_aligned --fragment_sap_aligned `
  --generate_static_live_mpd --mpd_output widevine_sample.mpd `
  --hls_master_playlist_output h264_master.m3u8 `
  --enable_widevine_encryption `
  --key_server_url https://license.uat.widevine.com/cenc/getcontentkey/widevine_test `
  --content_id 7465737420636f6e74656e74206964 `
  --signer widevine_test `
  --aes_signing_key 1ae8ccd0e7985cc0b6203a55855a1034afc252980e970ca90e5202689f947ab9 `
  --aes_signing_iv d58ce954203b7c9a9a9d467f59839249 `
  --clear_lead 0
```
