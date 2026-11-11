"""Local stdin/stdout inference. No sockets, persistence, camera capture or telemetry."""
import os, sys, json, base64, threading, queue, time
os.environ['TF_CPP_MIN_LOG_LEVEL'] = '3'
os.environ['MPLCONFIGDIR'] = os.path.join(os.environ.get('TEMP', '/tmp'), 'neonflow-mpl')
import numpy as np
import cv2
import mediapipe as mp
from mediapipe.tasks import python
from mediapipe.tasks.python import vision
ROOT = os.path.dirname(os.path.abspath(__file__))
if getattr(sys, 'frozen', False):
    ROOT = os.path.dirname(os.path.dirname(sys.executable))
lock = threading.Lock()
def emit(data):
    with lock:
        print(json.dumps(data, ensure_ascii=True), flush=True)
def points(values):
    return [dict(x=1-p.x, y=p.y, z=p.z, v=getattr(p, 'visibility', 1) if getattr(p, 'visibility', None) is not None else 1) for p in values]
def voice(device):
    try:
        import sounddevice as sd
        from vosk import Model, KaldiRecognizer, SetLogLevel
        SetLogLevel(-1)
        model = Model(os.path.join(ROOT, 'vosk-model-small-ru-0.22'))
        rec = KaldiRecognizer(model, 16000, json.dumps(['пауза','продолжить','повторить','назад','играть','[unk]'], ensure_ascii=False))
        rec.SetWords(True)
        q = queue.Queue(maxsize=16)
        def callback(data, frames, timing, status):
            try: q.put_nowait(bytes(data))
            except queue.Full: pass
        with sd.RawInputStream(samplerate=16000, blocksize=4000, dtype='int16', channels=1, device=None if device<0 else device, callback=callback):
            emit(dict(kind='voice_ready'))
            last = 0
            while True:
                if rec.AcceptWaveform(q.get()):
                    r = json.loads(rec.Result()); command = r.get('text','')
                    confidence = min([w['conf'] for w in r.get('result',[])] or [0])
                    if command in ['пауза','продолжить','повторить','назад','играть'] and confidence >= .8 and time.monotonic()-last > 1.3:
                        emit(dict(kind='voice',command=command)); last=time.monotonic()
    except Exception as e: emit(dict(kind='voice_error',error=str(e)))
def main():
    pose = vision.PoseLandmarker.create_from_options(vision.PoseLandmarkerOptions(base_options=python.BaseOptions(model_asset_path=os.path.join(ROOT,'pose.task')),running_mode=vision.RunningMode.VIDEO,num_poses=1))
    hands = vision.HandLandmarker.create_from_options(vision.HandLandmarkerOptions(base_options=python.BaseOptions(model_asset_path=os.path.join(ROOT,'hand.task')),running_mode=vision.RunningMode.VIDEO,num_hands=2))
    if '--smoke' in sys.argv:
        blank=mp.Image(image_format=mp.ImageFormat.SRGB,data=np.zeros((480,640,3),dtype=np.uint8))
        p=pose.detect_for_video(blank,1); h=hands.detect_for_video(blank,1)
        from vosk import Model,KaldiRecognizer,SetLogLevel
        SetLogLevel(-1); r=KaldiRecognizer(Model(os.path.join(ROOT,'vosk-model-small-ru-0.22')),16000); r.AcceptWaveform(bytes(32000))
        emit(dict(kind='smoke',pose=len(p.pose_landmarks),hands=len(h.hand_landmarks),speech=json.loads(r.FinalResult())));return
    import sounddevice as sd
    try: emit(dict(kind='devices',devices=[dict(id=i,name=d['name']) for i,d in enumerate(sd.query_devices()) if d['max_input_channels']>0]))
    except Exception as e: emit(dict(kind='voice_error',error=str(e)))
    device=int(sys.argv[1]) if len(sys.argv)>1 else -1
    if device != -2: threading.Thread(target=voice,args=(device,),daemon=True).start()
    emit(dict(kind='ready')); seq=0
    for line in sys.stdin:
        try:
            request=json.loads(line); frame=cv2.imdecode(np.frombuffer(base64.b64decode(request['image']),np.uint8),cv2.IMREAD_COLOR)
            if request.get('vertical'):frame=cv2.flip(frame,0)
            angle=request.get('rotation',0)
            if angle==90:frame=cv2.rotate(frame,cv2.ROTATE_90_CLOCKWISE)
            elif angle==180:frame=cv2.rotate(frame,cv2.ROTATE_180)
            elif angle==270:frame=cv2.rotate(frame,cv2.ROTATE_90_COUNTERCLOCKWISE)
            image=mp.Image(image_format=mp.ImageFormat.SRGB,data=cv2.cvtColor(frame,cv2.COLOR_BGR2RGB));seq+=100
            p=pose.detect_for_video(image,seq);h=hands.detect_for_video(image,seq)
            preview=cv2.imencode('.jpg',cv2.flip(frame,1),[cv2.IMWRITE_JPEG_QUALITY,75])[1]
            out=dict(kind='frame',epoch=request['epoch'],time=request['time'],preview=base64.b64encode(preview).decode(),pose=points(p.pose_landmarks[0]) if p.pose_landmarks else [],left=[],right=[])
            # Pose semantic IDs stay anatomical after image reflection. Assign hands by
            # nearest pose wrist, robust to classifier handedness convention changes.
            for hand in h.hand_landmarks:
                if out['pose']:
                    wrist=hand[0]; ds=[(1-wrist.x-out['pose'][i]['x'])**2+(wrist.y-out['pose'][i]['y'])**2 for i in [15,16]]
                    side='left' if ds[0]<ds[1] else 'right'
                else: continue
                out[side]=points(hand)
            emit(out)
        except Exception as e:emit(dict(kind='frame_error',error=str(e)))
    pose.close();hands.close()
if __name__=='__main__':
    try:main()
    except Exception as e:emit(dict(kind='fatal',error=str(e)));sys.exit(1)
