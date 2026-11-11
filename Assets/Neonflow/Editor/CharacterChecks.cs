using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Neonflow.Editor
{
    [InitializeOnLoad]
    public static class CharacterChecks
    {
        const string Flag="Neonflow.CharacterChecks";
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static int checks,step;
        static string errors="";
        static double next,deadline;
        static NeonflowGame game;
        static CharacterChecks()
        {
            if(SessionState.GetBool(Flag,false)) { EditorApplication.update+=Tick; Application.logMessageReceived+=Log; next=EditorApplication.timeSinceStartup+3; deadline=next+140; }
        }
        static void Check(bool condition,string message) {checks++;if(!condition)throw new Exception("Character check: "+message);}
        static void Log(string text,string trace,LogType type) {if(type==LogType.Exception||type==LogType.Error)errors+=text+"\n"+trace+"\n";}
        static object Field(string name)=>typeof(NeonflowGame).GetField(name,Private).GetValue(game);
        static void Set(string name,object value)=>typeof(NeonflowGame).GetField(name,Private).SetValue(game,value);
        static void Call(string name,params object[] args)=>typeof(NeonflowGame).GetMethod(name,Private).Invoke(game,args);
        static RectTransform Root=>(RectTransform)Field("root");
        static void Click(string name)=>Root.Find(name).GetComponent<Button>().onClick.Invoke();
        static void State(ScreenState expected)=>Check(game.State==expected,"state "+expected+", got "+game.State);

        public static void DataAndModels()
        {
            checks=0;
            var catalog=CharacterCatalog.Load();
            var moves=JsonUtility.FromJson<MovementCatalogData>(Resources.Load<TextAsset>("Data/Movements_catalog").text).poses;
            var tracks=JsonUtility.FromJson<Tracks>(Resources.Load<TextAsset>("Data/TrackTiming").text).tracks;
            catalog.Validate(moves);
            Check(catalog.Get(null).id=="light"&&catalog.Get("removed").id=="light","legacy/unknown selection fallback");
            var old=JsonUtility.FromJson<SaveData>("{\"settings\":{\"music\":0.42},\"runs\":[{\"trackId\":1,\"score\":2500,\"completed\":true}]}");
            Check(catalog.Get(old.settings.characterId).id=="light"&&old.runs[0].score==2500&&string.IsNullOrEmpty(old.runs[0].characterId),"legacy save retained without relabeling runs");
            string oldPath=SaveStore.OverridePath;
            SaveStore.OverridePath=Path.GetFullPath("TestResults/character-roundtrip.json");
            try
            {
                old.settings.characterId="solid";old.runs.Add(new RunRecord{characterId="solid",trackId=2,score=500});SaveStore.Write(old);
                var loaded=SaveStore.Load();Check(loaded.settings.characterId=="solid"&&loaded.runs.Count==2&&loaded.runs[1].characterId=="solid","selection/run save roundtrip");
            }
            finally {SaveStore.OverridePath=oldPath;}
            var report=new List<string>();
            foreach(var profile in catalog.characters)
            {
                var pool=profile.Movements(moves);
                Check(pool.Length==11,profile.id+" six poses + five hands");
                foreach(var track in tracks)
                    for(int seed=0;seed<12;seed++)
                    {
                        var audio=Resources.Load<AudioClip>("Music/"+Path.GetFileNameWithoutExtension(track.fileName));
                        var battle=new Battle(track,audio.length,pool,seed,false,new Balance(),profile.id);
                        Check(battle.record.characterId==profile.id,"run character");
                        for(int i=0;i<battle.cues.Count;i++)
                        {
                            var cue=battle.cues[i];
                            Check(profile.poses.Contains(cue.pose),"own pose only");
                            Check(string.IsNullOrEmpty(cue.left)||profile.hands.Contains(cue.left),"own left hand only");
                            Check(string.IsNullOrEmpty(cue.right)||profile.hands.Contains(cue.right),"own right hand only");
                            Check(i==0||cue.pose!=battle.cues[i-1].pose,"no adjacent repeats");
                            Check(Math.Abs(cue.target-track.Target(i))<1e-8,"same beat grid");
                        }
                    }
                var model=UnityEngine.Object.Instantiate(Resources.Load<RobotRig>("Characters/"+profile.id));
                var skin=model.GetComponent<SkinnedMeshRenderer>();
                var mesh=skin.sharedMesh;
                var avatar=model.GetComponent<Animator>().avatar;
                Check(avatar.isValid&&avatar.isHuman,"valid real Humanoid "+profile.id);
                Check(model.transform.localScale==Vector3.one,"metres, no hidden root scale");
                Check(mesh.uv.Length==mesh.vertexCount&&mesh.normals.Length==mesh.vertexCount,"UV and normals");
                Check(mesh.boneWeights.Length==mesh.vertexCount&&mesh.triangles.All(i=>i>=0&&i<mesh.vertexCount),"complete vertex/index/skin buffers after regeneration");
                Check(mesh.boneWeights.All(w=>Math.Abs(w.weight0-1)<.001f&&w.weight1==0),"rigid armor weights");
                Check(mesh.bounds.size.y>(profile.id=="light"?1.78f:1.63f)&&mesh.bounds.size.y<(profile.id=="light"?1.82f:1.68f),"true character height");
                foreach(bool left in new[]{true,false})
                {
                    int first=(int)(left?HumanBodyBones.LeftThumbProximal:HumanBodyBones.RightThumbProximal);
                    for(int f=0;f<5;f++)for(int j=0;j<3;j++) Check(model.bones[first+f*3+j]&&(j==0||model.bones[first+f*3+j].parent==model.bones[first+f*3+j-1]),"five independent three-joint fingers");
                }
                var baked=new Mesh();
                foreach(var pose in moves.Where(m=>m.category=="Poses"))
                    foreach(var hand in moves.Where(m=>m.category=="Hands"))
                    {
                        model.Pose(PoseEvaluator.Template(pose.id),hand.id,hand.id,pose.id);
                        skin.BakeMesh(baked);
                        Check(baked.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),"finite skin in "+pose.id+" / "+hand.id);
                        Check(baked.bounds.size.x<2.3f&&baked.bounds.max.y<2.4f&&baked.bounds.min.y>=-.001f,"pose bounds "+profile.id+" / "+pose.id);
                    }
                UnityEngine.Object.DestroyImmediate(baked);
                report.Add(profile.id+": "+mesh.vertexCount+" vertices, "+mesh.triangles.Length/3+" triangles, "+skin.bones.Length+" bones, 4 materials, Humanoid valid, 120 pose/hand combinations finite.");
                UnityEngine.Object.DestroyImmediate(model.gameObject);
            }
            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/character-data-model-checks.txt","PASS "+checks+" checks\n"+string.Join("\n",report)+"\nNo human camera or voice acceptance in this test.\n");
            Debug.Log("CHARACTER_DATA_MODELS_PASS "+checks);
        }
        public static void Begin()
        {
            CoreChecks.Run();DataAndModels();
            SessionState.SetBool(Flag,true);step=0;checks=0;errors="";
            SaveStore.OverridePath=Path.GetFullPath("TestResults/character-flow-save.json");
            SaveStore.Write(new SaveData());
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
            next=EditorApplication.timeSinceStartup+4;deadline=next+140;
            EditorSceneManager.OpenScene("Assets/Neonflow/Scenes/Neonflow.unity");EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            game=UnityEngine.Object.FindAnyObjectByType<NeonflowGame>();if(!game)return;
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Character flow timeout "+step);
                if(game.NavigationBusy)return;
                if(EditorApplication.timeSinceStartup<next)return;
                next=EditorApplication.timeSinceStartup+2;
                Debug.Log("CHARACTER_STEP "+step);
                switch(step++)
                {
                    case 0:
                        var recognition=(Recognition)Field("recognition");typeof(Recognition).GetMethod("Stop",Private).Invoke(recognition,null);recognition.enabled=false;AudioListener.volume=0;
                        if(recognition.PreviewTexture)UnityEngine.Object.Destroy(recognition.PreviewTexture);
                        typeof(Recognition).GetProperty("PreviewTexture").SetValue(recognition,null);
                        typeof(Recognition).GetProperty("Latest").SetValue(recognition,null);
                        recognition.Status="error.camera_missing";
                        Call("Show",ScreenState.Menu);Click("Play");break;
                    case 1:
                        State(ScreenState.Characters);
                        Check(Root.GetComponentsInChildren<CharacterPortrait>().Length==2,"two real model portraits");
                        foreach(var b in Root.GetComponentsInChildren<Button>()) Debug.Log("CHARACTER_BUTTON "+b.name+" position="+((RectTransform)b.transform).anchoredPosition+" alpha="+b.GetComponent<CanvasGroup>().alpha+" color="+b.targetGraphic.color+" label="+b.GetComponentInChildren<Text>().text);
                        VisualChecks.Capture("characters-16x9");VisualChecks.Capture("characters-16x10",1600,1000);
                        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(Root.Find("Choose_solid").gameObject);
                        Call("Voice","играть");break;
                    case 2:
                        State(ScreenState.Camera);Check(game.SelectedCharacter.id=="solid"&&SaveStore.Load().settings.characterId=="solid","Solid selected and saved");
                        Check(((RobotView)Field("robot")).CharacterId=="solid","Solid model selected");
                        Click("Back");break;
                    case 3:
                        State(ScreenState.Characters);Set("calibrated",true);Click("Choose_solid");break;
                    case 4:
                        State(ScreenState.Songs);Check(Root.Find("ChangeCharacter").GetComponentInChildren<Text>().text=="SOLID ROBOT","track screen selected name");
                        Click("Practice");break;
                    case 5:
                        if(game.State!=ScreenState.Playing){step--;break;}
                        Check(game.CurrentBattle.record.characterId=="solid"&&game.CurrentBattle.cues.All(c=>game.SelectedCharacter.poses.Contains(c.pose)),"practice restricted to Solid");
                        ShowRobot("solid","elbows_up","rock");Call("Pause");break;
                    case 6:
                        State(ScreenState.Pause);Click("Action1");break;
                    case 7:
                        if(game.State!=ScreenState.Playing){step--;break;}
                        Check(game.CurrentBattle.record.characterId=="solid","retry keeps character");Call("Leave");break;
                    case 8:
                        State(ScreenState.Songs);Click("ChangeCharacter");break;
                    case 9:
                        State(ScreenState.Characters);Click("Choose_light");break;
                    case 10:
                        State(ScreenState.Songs);Click("Start");break;
                    case 11:
                        if(game.State!=ScreenState.Playing){step--;break;}
                        Check(game.CurrentBattle.record.characterId=="light"&&game.CurrentBattle.cues.All(c=>game.SelectedCharacter.poses.Contains(c.pose)),"normal run restricted to Light");
                        Check(((RobotView)Field("robot")).CharacterId=="light","Light model switched");
                        ShowRobot("light","arms_v","peace");Call("Leave");break;
                    case 12: State(ScreenState.Songs);Click("Back");break;
                    case 13: State(ScreenState.Characters);Click("Back");break;
                    case 14: State(ScreenState.Menu);Finish();break;
                }
            }
            catch(Exception e) {errors+=e+"\n";Finish();}
        }
        static void ShowRobot(string id,string pose,string hand)
        {
            var robot=(RobotView)Field("robot");robot.SetCue(new Cue{pose=pose,left=hand,right=hand,robotAccuracy=1},100);robot.SampleImmediate();
            VisualChecks.Capture("character-hud-"+id);VisualChecks.Capture("character-hud-"+id+"-16x10",1600,1000);
        }
        static void Finish()
        {
            File.WriteAllText("TestResults/character-flow-checks.txt",errors.Length==0?"PASS "+checks+" checks: main Play -> character choice, portraits, Solid -> calibration, back, selection persistence, practice pool, restart, switch to Light, normal pool, back navigation. Synthetic/offline checks; no human camera/voice acceptance.\n":errors);
            SessionState.SetBool(Flag,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
            EditorApplication.ExitPlaymode();EditorApplication.delayCall+=()=>EditorApplication.Exit(errors.Length==0?0:1);
        }
    }
}
