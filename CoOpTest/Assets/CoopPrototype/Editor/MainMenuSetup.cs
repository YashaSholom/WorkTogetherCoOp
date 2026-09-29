using System;
using System.IO;
using System.Linq;
using CoopPrototype.Frontend;
using CoopPrototype.Steam;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CoopPrototype.Editor
{
    public static class MainMenuSetup
    {
        const string Root="Assets/CoopPrototype";
        public const string MenuPath=Root+"/Scenes/MainMenu.unity";
        public const string GamePath=Root+"/Scenes/GameWorld.unity";
        [MenuItem("Coop Prototype/Menu/Repair Battery Category")]
        public static void RepairBatteryCategory()
        {
            var battery = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Root + "/Items/Battery.asset");
            Undo.RecordObject(battery, "Restore battery socket category");
            battery.category = "Battery";
            battery.displayName = "Battery";
            EditorUtility.SetDirty(battery);
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Coop Prototype/Menu/Create Main Menu and Game World")]
        public static void Create()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            if(File.Exists(MenuPath)) { EditorSceneManager.OpenScene(MenuPath); return; }
            EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);
            var source=Object.FindFirstObjectByType<NetworkSession>();
            var player=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/NetworkPlayer.prefab");
            var lobby=new GameObject("Lobby Member"); lobby.AddComponent<NetworkObject>(); lobby.AddComponent<LobbyMember>();
            var lobbyPrefab=PrefabUtility.SaveAsPrefabAsset(lobby,Root+"/Prefabs/LobbyMember.prefab"); Object.DestroyImmediate(lobby);
            var list=AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(Root+"/Prefabs/NetworkPrefabs.asset");
            if(!list.PrefabList.Any(p=>p.Prefab==lobbyPrefab)) list.Add(new NetworkPrefab{Prefab=lobbyPrefab});
            EditorUtility.SetDirty(list);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var scope=Object.Instantiate(source.gameObject); scope.name="Session Scope"; SceneManager.MoveGameObjectToScene(scope,scene);
            var session=scope.GetComponent<NetworkSession>(); session.spawnPoints=Array.Empty<Transform>(); session.overviewCamera=null; session.showPrototypeGUI=false;
            session.manager.NetworkConfig.PlayerPrefab=lobbyPrefab;
            session.manager.NetworkConfig.EnableSceneManagement=true;
            session.manager.NetworkConfig.ConnectionApproval=true;
            if(source.steam!=null)
            {
                var steam=Object.Instantiate(source.steam.gameObject); steam.name="Steam Services"; SceneManager.MoveGameObjectToScene(steam,scene);
                session.steam=steam.GetComponent<SteamLobby>(); session.steam.session=session; session.steam.transport=scope.GetComponent<SteamP2PTransport>();
            }
            var flow=scope.AddComponent<GameFlow>(); flow.gameplayPlayerPrefab=player;
            var document=scope.AddComponent<UIDocument>();
            var panel=ScriptableObject.CreateInstance<PanelSettings>(); panel.scaleMode=PanelScaleMode.ScaleWithScreenSize; panel.referenceResolution=new Vector2Int(1440,1000); panel.match=.5f;
            AssetDatabase.CreateAsset(panel,Root+"/UI/MainMenuPanel.asset"); document.panelSettings=panel;
            document.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root+"/UI/MainMenu.uxml"); document.sortingOrder=10;
            var presenter=scope.AddComponent<MenuPresenter>();
            var backdrop=new GameObject("Menu Clearing").AddComponent<MenuBackdrop>();
            var appearance=player.GetComponent<CharacterAppearance>();
            backdrop.partyPreviews=new CharacterPreview[4];
            for(int i=0;i<4;i++)
            {
                var preview=new GameObject("Crew Preview "+(i+1)); preview.transform.SetParent(backdrop.transform);
                preview.transform.position=new Vector3(1.7f+i*1.35f,0,i*.45f); preview.transform.rotation=Quaternion.Euler(0,165,0);
                var visual=Object.Instantiate(player.transform.Find(BeanCrewSetup.VisualName).gameObject,preview.transform); visual.transform.localPosition=Vector3.zero;
                var p=preview.AddComponent<CharacterPreview>(); p.animator=visual.GetComponent<Animator>();
                p.variants=appearance.variants.Select(r=>visual.GetComponentsInChildren<Renderer>(true).First(v=>v.name==r.name)).ToArray();
                p.Show(i); backdrop.partyPreviews[i]=p;
            }
            var composition=scope.AddComponent<MenuCompositionRoot>(); composition.session=session; composition.flow=flow; composition.presenter=presenter; composition.backdrop=backdrop;
            composition.voice=scope.AddComponent<Voice.ProximityVoice>();
            var camera=new GameObject("Menu Camera").AddComponent<Camera>(); camera.tag="MainCamera";
            camera.transform.position=new Vector3(3.5f,3.3f,-9.5f); camera.transform.LookAt(new Vector3(1.8f,1.0f,.4f)); camera.fieldOfView=40;
            camera.gameObject.AddComponent<AudioListener>(); camera.backgroundColor=new Color(.6f,.76f,.7f); camera.clearFlags=CameraClearFlags.SolidColor;
            var light=new GameObject("Warm afternoon sun").AddComponent<Light>(); light.type=LightType.Directional; light.intensity=1.4f; light.color=new Color(1,.87f,.67f); light.shadows=LightShadows.Soft; light.transform.rotation=Quaternion.Euler(48,-32,0);
            RenderSettings.ambientLight=new Color(.55f,.66f,.6f);
            BuildClearing(backdrop.transform);
            EditorSceneManager.SaveScene(scene,MenuPath);
            EditorSceneManager.CloseScene(SceneManager.GetSceneByPath(PrototypeBuilder.ScenePath),true);
            AssetDatabase.SaveAssets();
            if(!File.Exists(GamePath)) AssetDatabase.CopyAsset(PrototypeBuilder.ScenePath,GamePath);
            var game=EditorSceneManager.OpenScene(GamePath);
            var oldSession=Object.FindFirstObjectByType<NetworkSession>();
            var context=new GameObject("Game Scene Context").AddComponent<GameSceneContext>(); context.spawnPoints=oldSession.spawnPoints; context.loadingCamera=oldSession.overviewCamera;
            if(oldSession.steam!=null) Object.DestroyImmediate(oldSession.steam.gameObject);
            Object.DestroyImmediate(oldSession.gameObject);
            EditorSceneManager.SaveScene(game);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(MenuPath,true),new EditorBuildSettingsScene(GamePath,true),new EditorBuildSettingsScene(PrototypeBuilder.ScenePath,true)};
            EditorSceneManager.OpenScene(MenuPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Menu] MainMenu and GameWorld saved. Original workshop preserved.");
        }
        static Material Material(string name, Color color)
        {
            var folder=Root+"/UI/Materials"; Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            var path=folder+"/"+name+".mat"; var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null) { mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=color}; AssetDatabase.CreateAsset(mat,path); }
            return mat;
        }
        static GameObject Piece(Transform parent,string name,PrimitiveType shape,Vector3 position,Vector3 scale,Material material)
        {
            var obj=GameObject.CreatePrimitive(shape); obj.name=name; obj.transform.SetParent(parent); obj.transform.localPosition=position; obj.transform.localScale=scale;
            Object.DestroyImmediate(obj.GetComponent<Collider>()); obj.GetComponent<Renderer>().sharedMaterial=material; return obj;
        }
        static void BuildClearing(Transform root)
        {
            var grass=Material("Moss grass",new Color(.27f,.40f,.20f)); var leaf=Material("Tree canopy",new Color(.16f,.30f,.15f));
            var leafLight=Material("Sunlit leaves",new Color(.40f,.51f,.21f)); var bark=Material("Tree bark",new Color(.26f,.17f,.095f)); var stone=Material("Warm stone",new Color(.45f,.45f,.34f));
            Piece(root,"Grass clearing",PrimitiveType.Cylinder,new Vector3(1,-.16f,1),new Vector3(22,.15f,20),grass);
            var rand=new System.Random(29);
            for(int i=0;i<14;i++)
            {
                float x=-8+i*1.35f,z=4.8f+(float)rand.NextDouble()*3;
                float h=2.7f+(float)rand.NextDouble()*1.8f;
                var tree=new GameObject("Tree "+(i+1)); tree.transform.SetParent(root);
                Piece(tree.transform,"Trunk",PrimitiveType.Cylinder,new Vector3(x,h*.4f,z),new Vector3(.32f,h*.4f,.32f),bark);
                Piece(tree.transform,"Lower crown",PrimitiveType.Sphere,new Vector3(x,h,z),new Vector3(2.6f,2.9f,2.1f),i%2==0?leaf:leafLight);
                Piece(tree.transform,"Upper crown",PrimitiveType.Sphere,new Vector3(x+.25f,h+.9f,z),new Vector3(1.9f,2.2f,1.7f),leaf);
            }
            for(int i=0;i<60;i++)
            {
                float x=-8+(float)rand.NextDouble()*17,z=-1+(float)rand.NextDouble()*8;
                if(x>1 && x<7 && z<2.5f) continue;
                var blade=Piece(root,"Grass tuft "+i,PrimitiveType.Cube,new Vector3(x,.12f,z),new Vector3(.055f,.25f,.12f),leafLight);
                blade.transform.rotation=Quaternion.Euler(0,rand.Next(180),rand.Next(-25,25));
            }
            for(int i=0;i<7;i++) Piece(root,"Rock "+i,PrimitiveType.Sphere,new Vector3(-3+i*1.65f,.1f,3.4f),new Vector3(.75f,.4f,.6f),stone);
        }
    }
}
