"""Copy bundled runtime to a built player without importing Python into Unity."""
from pathlib import Path
import shutil, sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from verify_models import verify
root=Path(__file__).resolve().parents[2]
verify()
source=root/'Tools/Recognition/python'
target=root/'Builds/Windows/Recognition/python'
shutil.copytree(source,target,dirs_exist_ok=True,ignore=shutil.ignore_patterns('*.meta','__pycache__','*.pyc'))
licenses=root/'Builds/Windows/ThirdParty'
licenses.mkdir(parents=True,exist_ok=True)
for folder in ['README/AssetLibrary/Documentation','README/MusicReports']:
    shutil.copytree(root/folder,licenses/Path(folder).name,dirs_exist_ok=True)
vector = licenses/'Vector'
vector.mkdir(parents=True,exist_ok=True)
shutil.copy2(root/'WOW/Neonflow_Vector_AssetPack/Documentation/07_CONTENT_AND_RIGHTS.txt',vector/'07_CONTENT_AND_RIGHTS.txt')
shutil.copy2(root/'Assets/NeonflowVector/Fonts/Exo2-OFL.txt',vector/'Exo2-OFL.txt')
shutil.copy2(root/'README/AssetLibrary/Assets/NeonflowUI/Fonts/Exo2-OFL.txt',licenses/'Exo2-OFL.txt')
for p in source.glob('Lib/site-packages/*.dist-info'):
    for f in p.rglob('*'):
        if f.is_file() and ('license' in f.name.lower() or 'notice' in f.name.lower()):
            dest=licenses/p.name/f.relative_to(p);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,dest)
for name in ['RUN_NEONFLOW.txt','THIRD_PARTY_NOTICES.txt','AcceptanceReport.txt']:
    if (root/name).exists():
        shutil.copy2(root/name,root/'Builds/Windows'/name)
print('Runtime and third-party notices packaged:',target)
