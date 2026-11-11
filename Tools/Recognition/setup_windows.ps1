$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$runtime = Join-Path $PSScriptRoot 'python'
$models = Join-Path $projectRoot 'Assets/StreamingAssets/Recognition'
New-Item -ItemType Directory -Force $runtime,$models | Out-Null
Invoke-WebRequest 'https://www.python.org/ftp/python/3.11.9/python-3.11.9-embed-amd64.zip' -OutFile (Join-Path $PSScriptRoot 'python.zip')
Expand-Archive -LiteralPath (Join-Path $PSScriptRoot 'python.zip') -DestinationPath $runtime -Force
Set-Content -Encoding ASCII (Join-Path $runtime 'python311._pth') "python311.zip`n.`nLib/site-packages`nimport site"
Invoke-WebRequest 'https://bootstrap.pypa.io/get-pip.py' -OutFile (Join-Path $PSScriptRoot 'get-pip.py')
& (Join-Path $runtime 'python.exe') (Join-Path $PSScriptRoot 'get-pip.py') --no-warn-script-location
if ($LASTEXITCODE -ne 0) { throw 'pip bootstrap failed' }
& (Join-Path $runtime 'python.exe') -m pip install -r (Join-Path $PSScriptRoot 'requirements-windows-lock.txt') --no-warn-script-location
if ($LASTEXITCODE -ne 0) { throw 'Recognition dependencies installation failed' }
Invoke-WebRequest 'https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_lite/float16/1/pose_landmarker_lite.task' -OutFile (Join-Path $models 'pose.task')
Invoke-WebRequest 'https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task' -OutFile (Join-Path $models 'hand.task')
Invoke-WebRequest 'https://alphacephei.com/vosk/models/vosk-model-small-ru-0.22.zip' -OutFile (Join-Path $PSScriptRoot 'vosk.zip')
Expand-Archive -LiteralPath (Join-Path $PSScriptRoot 'vosk.zip') -DestinationPath $models -Force
& (Join-Path $runtime 'python.exe') (Join-Path $PSScriptRoot 'verify_models.py')
if ($LASTEXITCODE -ne 0) { throw 'Model checksum verification failed' }
& (Join-Path $runtime 'python.exe') (Join-Path $models 'worker.py') --smoke
if ($LASTEXITCODE -ne 0) { throw 'Recognition smoke test failed' }
