"""Integration check on Google's public pose fixture, not a real camera acceptance test."""
from pathlib import Path
import base64, json, subprocess, sys
root=Path(__file__).resolve().parents[2]
fixture='mediapipe-hands.jpg' if '--hands' in sys.argv else 'mediapipe-pose.jpg'
request=dict(epoch=7,time=12.345,rotation=0,vertical=False,image=base64.b64encode((root/'TestResults'/fixture).read_bytes()).decode())
p=subprocess.run([sys.executable,'-u',str(root/'Assets/StreamingAssets/Recognition/worker.py'),'-2'],input=json.dumps(request)+'\n',text=True,capture_output=True,timeout=90)
results=[json.loads(line) for line in p.stdout.splitlines() if line.startswith('{')]
frame=next((x for x in results if x.get('kind')=='frame'),None)
assert p.returncode==0,(p.stderr,p.stdout)
assert frame and len(frame['pose'])==33,results
assert frame['epoch']==7 and frame['time']==12.345
assert frame['pose'][11]['x']<frame['pose'][12]['x'],'Player left must render on left'
if '--hands' in sys.argv:assert len(frame['left'])==21 and len(frame['right'])==21,frame
report={'status':'PASS','poseLandmarks':len(frame['pose']),'leftHandLandmarks':len(frame['left']),'rightHandLandmarks':len(frame['right']),'timestampEcho':frame['time'],'epochEcho':frame['epoch'],'fixture':'https://storage.googleapis.com/mediapipe-assets/pose.jpg','scope':'Public image inference only. Not webcam acceptance.'}
report['fixture']='https://storage.googleapis.com/mediapipe-assets/'+('woman_hands.jpg' if '--hands' in sys.argv else 'pose.jpg')
(root/'TestResults'/('inference-hands.json' if '--hands' in sys.argv else 'inference.json')).write_text(json.dumps(report,indent=2))
print(json.dumps(report))
