using System;
using System.IO;
using System.Linq;
using System.Text;
using CoopPrototype.Checkpoint;
using CoopPrototype.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CoopPrototype.Editor
{
    public static partial class PlanetArtRedesign
    {
        const string LayoutName = "Checkpoint shared deck layout";
        static GameObject Solid(Transform p, string n, Vector3 at, Vector3 size, Material m)
        {
            var o = Box(p,n,at,size,m); o.AddComponent<BoxCollider>(); return o;
        }
        static void MoveNamed(Transform root, string name, Vector3 at, float yaw = 0)
        {
            var t = root.Find(name); if (t == null) return;
            t.SetPositionAndRotation(at, Quaternion.Euler(0,yaw,0));
        }
        static void Road(Transform p, Vector3 a, Vector3 b, float width = 5.8f)
        {
            Vector3 delta = b-a;
            var surface = Solid(p,"Road segment",(a+b)*.5f-Vector3.up*.18f,new Vector3(width,.36f,delta.magnitude+.15f),deck);
            surface.transform.rotation = Quaternion.LookRotation(delta,Vector3.up);
            foreach (int side in new[]{-1,1})
            {
                var line = Box(surface.transform,"Cyan road edge",new Vector3(side*(width/2-.12f)/width,.57f,0),new Vector3(.025f,.08f,.99f),cyan);
            }
            int count = Mathf.Max(1,Mathf.FloorToInt(delta.magnitude/3));
            for (int i=0;i<count;i++)
            {
                var mark = Group("Flow arrow",p); mark.position=Vector3.Lerp(a,b,(i+.5f)/count)+Vector3.up*.055f;
                mark.rotation=Quaternion.LookRotation(delta,Vector3.up);
                for(int side=-1;side<=1;side+=2)
                {
                    var arm=Box(mark,"Chevron",new Vector3(side*.26f,0,0),new Vector3(.09f,.025f,.78f),cyan);
                    arm.transform.localRotation=Quaternion.Euler(0,-side*45,0);
                }
            }
        }
        static Transform[] Route(Transform p,string name,Vector3[] points)
        {
            var g=Group(name,p);
            return points.Select((point,i)=>{var t=Group("Waypoint "+i,g);t.position=point;t.rotation=Quaternion.Euler(0,90,0);return t;}).ToArray();
        }
        static void Booth(Transform p,int lane,float z)
        {
            var g=Group("Inspection bay "+lane,p);g.position=new Vector3(0,0,z);
            Box(g,"Stop pad",new Vector3(0,.02f,0),new Vector3(9,.035f,5.3f),charcoal);
            foreach(float side in new[]{-2.6f,2.6f})Box(g,"Pad border",new Vector3(0,.055f,side),new Vector3(9,.025f,.08f),cyan);
            Box(g,"Stop line",new Vector3(3,.06f,0),new Vector3(.15f,.03f,5),hazard);
            var shelter=Group("Open officer shelter",g);shelter.localPosition=new Vector3(0,0,3.5f);
            Box(shelter,"Rounded canopy",new Vector3(0,3.5f,0),new Vector3(6.7f,.55f,3.4f),ivory);
            Box(shelter,"Orange fascia",new Vector3(0,3.22f,-1.55f),new Vector3(6.2f,.3f,.25f),orange);
            Box(shelter,"Warm light",new Vector3(0,3.1f,-1.45f),new Vector3(5.6f,.08f,.08f),warm);
            foreach(float x in new[]{-3f,3f})
            {
                Solid(shelter,"Support",new Vector3(x,1.65f,1.2f),new Vector3(.25f,3.3f,.32f),ivory);
                Box(shelter,"Collar",new Vector3(x,2.7f,1.2f),new Vector3(.4f,.4f,.45f),orange);
            }
            foreach(float x in new[]{-4.5f,4.5f})
            {
                Box(g,"Signal bollard",new Vector3(x,.65f,2.5f),new Vector3(.3f,1.3f,.3f),ivory);
                Box(g,"Signal cap",new Vector3(x,1.35f,2.5f),new Vector3(.4f,.15f,.4f),orange);
            }
            if(lane!=2)
            {
                Solid(shelter,"Future decision desk",new Vector3(0,.85f,-.25f),new Vector3(3,.25f,1.3f),ivory);
                Box(shelter,"Desk plinth",new Vector3(0,.4f,-.05f),new Vector3(2,.8f,.8f),charcoal);
                Box(shelter,"Standby screen",new Vector3(0,1.65f,-.05f),new Vector3(1.6f,.9f,.13f),charcoal);
                Box(shelter,"Cyan screen",new Vector3(0,1.65f,-.13f),new Vector3(1.42f,.72f,.02f),cyan);
            }
        }
        static void SharedHall(Transform p)
        {
            var hall=Group("Shared operations hall",p);
            for(int x=-18;x<=18;x+=9)for(int z=12;z<=22;z+=10)
            {
                Solid(hall,"Porcelain column",new Vector3(x,3.7f,z),new Vector3(.45f,7.4f,.45f),ivory);
                Box(hall,"Orange column collar",new Vector3(x,6.8f,z),new Vector3(.8f,.65f,.8f),orange);
                Box(hall,"Orange footing",new Vector3(x,.4f,z),new Vector3(.75f,.65f,.75f),orange);
            }
            Box(hall,"Continuous operations roof",new Vector3(0,7.5f,17),new Vector3(41,.4f,13),ivory);
            foreach(float z in new[]{12f,17f,22f})
            {
                Box(hall,"Graphite roof rib",new Vector3(0,7.22f,z),new Vector3(40,.3f,.3f),charcoal);
                Box(hall,"Amber ceiling strip",new Vector3(0,7.02f,z),new Vector3(35,.06f,.12f),warm);
            }
            // Event board is an editable art prop, ready for a future event feed.
            Box(hall,"Giant event display frame",new Vector3(-2,4.5f,22.5f),new Vector3(11,4.8f,.5f),charcoal);
            Box(hall,"Event display orange trim",new Vector3(-2,4.5f,22.2f),new Vector3(10.6f,4.4f,.12f),orange);
            Box(hall,"Event display panel",new Vector3(-2,4.5f,22.11f),new Vector3(10.2f,4,.06f),glass);
            for(int i=0;i<4;i++)Box(hall,"Broadcast graphic",new Vector3(-4.8f+i*1.9f,4.4f,22.04f),new Vector3(1.4f,.25f+i*.35f,.02f),i%2==0?cyan:warm);
            for(int i=0;i<6;i++)Crate(hall,new Vector3(-18+i*1.15f,.5f,21),i%2==0);
            var cells=Group("Holding cells - layout prototype",hall);cells.position=new Vector3(14,0,19);
            Solid(cells,"Cell back",new Vector3(0,1.7f,2.7f),new Vector3(8,3.4f,.25f),ivory);
            foreach(float x in new[]{-4f,0f,4f})Solid(cells,"Cell side",new Vector3(x,1.7f,0),new Vector3(.2f,3.4f,5.5f),ivory);
            for(float x=-3.8f;x<4;x+=.65f)Solid(cells,"Containment bar",new Vector3(x,1.6f,-2.7f),new Vector3(.07f,3.2f,.07f),cyan);
            Box(cells,"Cell header",new Vector3(0,3.3f,-2.7f),new Vector3(8,.4f,.4f),orange);
            var lounge=Group("Crew rest area",hall);lounge.position=new Vector3(7,0,19);
            Solid(lounge,"Bench cushion",new Vector3(0,.55f,0),new Vector3(3.5f,.45f,1.1f),orange);
            Solid(lounge,"Bench back",new Vector3(0,1.05f,.5f),new Vector3(3.5f,.65f,.25f),ivory);
            Solid(lounge,"Crew table",new Vector3(0,.8f,-1.8f),new Vector3(2.1f,.18f,1.15f),ivory);
            Box(lounge,"Table leg",new Vector3(0,.4f,-1.8f),new Vector3(.3f,.8f,.5f),charcoal);
        }
        [MenuItem("Coop Prototype/Checkpoint/Apply Shared Deck Layout")]
        public static void ApplyCheckpointLayoutMenu() => ApplyCheckpointLayout();
        public static string ApplyCheckpointLayout()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first.");
            EditorSceneManager.SaveOpenScenes();Palette();
            string backup="Tools/Checkpoint/BeforeSharedDeckLayout";Directory.CreateDirectory(backup);
            foreach(string path in new[]{MainMenuSetup.GamePath,CheckpointSceneSetup.TestScenePath})
            {
                if(!File.Exists(path))continue;
                if(!File.Exists(backup+"/"+Path.GetFileName(path)))File.Copy(path,backup+"/"+Path.GetFileName(path));
                var scene=EditorSceneManager.OpenScene(path);var root=GameObject.Find("Space Checkpoint").transform;
                if(root.Find(LayoutName)!=null)continue;
                var previous=Group("Previous checkpoint scenery (preserved)",root);
                string[] keep={"Checkpoint Authority","Checkpoint interface","Traveller Shuttle","Giant rejection boot","Arrival waypoint","Inspection waypoint","Exit waypoint","Arrival portal","Document collection shelf","Collection pedestal","Document delivery 0","Document delivery 1","Inspection Terminal","Sorting table","Station clock","Supply Terminal (Shop)","Truck cargo spots","Velora planet landscape","Station practical lighting","Planet color and bloom","Warm sunset key","Lavender sky fill"};
                foreach(var t in root.Cast<Transform>().ToArray())
                {
                    if(t==previous||keep.Contains(t.name)||t.name.StartsWith("Scanner Tray")||t.name.StartsWith("Sorting Tray")||t.GetComponentInChildren<ContrabandScanner>(true)!=null||t.GetComponent<Unity.Netcode.NetworkObject>()!=null)continue;
                    t.SetParent(previous,true);
                }
                previous.gameObject.SetActive(false);
                var layout=Group(LayoutName,root);
                Solid(layout,"Shared 44 x 40 metre deck",new Vector3(0,-.45f,4),new Vector3(44,.9f,40),deck);
                for(int x=-20;x<=20;x+=4)for(int z=-14;z<=22;z+=4)
                {
                    Box(layout,"Deck tile",new Vector3(x,.006f,z),new Vector3(3.96f,.012f,3.96f),deck);
                    Box(layout,"Panel fastener",new Vector3(x-1.8f,.022f,z-1.8f),new Vector3(.09f,.025f,.09f),ivory);
                }
                foreach(float z in new[]{-10f,0f,10f})
                {
                    Road(layout,new Vector3(-21,0,0),new Vector3(-11,0,z));
                    Road(layout,new Vector3(-11,0,z),new Vector3(11,0,z));
                    Road(layout,new Vector3(11,0,z),new Vector3(22,0,0));
                }
                for(int lane=1;lane<=3;lane++)Booth(layout,lane,(lane-2)*10);
                SharedHall(layout);
                Vector3[] exits={new(0,0,0),new(22,0,0),new(34,-6,0),new(34,-6,8),new(18,-6,8),new(-4,-6,8)};
                for(int i=2;i<exits.Length;i++)Road(layout,exits[i-1],exits[i],6.4f);
                for(int x=-18;x<=18;x+=12)foreach(float z in new[]{-12f,20f})
                {
                    Solid(layout,"Deck support pier",new Vector3(x,-4.7f,z),new Vector3(1.5f,8.5f,1.5f),ivory);
                    Box(layout,"Pier orange ring",new Vector3(x,-3.8f,z),new Vector3(1.8f,.5f,1.8f),orange);
                }
                foreach(float z in new[]{-15.8f,23.8f})
                {
                    Solid(layout,"Perimeter guard",new Vector3(0,.6f,z),new Vector3(44,1.2f,.2f),ivory);
                    Box(layout,"Rail cyan trim",new Vector3(0,1.23f,z),new Vector3(44,.06f,.08f),cyan);
                }
                foreach(float x in new[]{-21.8f,21.8f})foreach(float z in new[]{-9f,15f})Solid(layout,"Side guard",new Vector3(x,.6f,z),new Vector3(.2f,1.2f,12),ivory);
                MoveNamed(root,"Arrival portal",new Vector3(-21.5f,3,0),90);
                MoveNamed(root,"Inspection Terminal",new Vector3(0,0,4.3f));
                MoveNamed(root,"Document collection shelf",new Vector3(-2.8f,.85f,3.1f));
                MoveNamed(root,"Collection pedestal",new Vector3(-2.8f,.4f,3.1f));
                for(int i=0;i<2;i++)
                {
                    MoveNamed(root,"Document delivery "+i,new Vector3(-3.3f+i,1.04f,3.1f));
                    MoveNamed(root,"Scanner Tray "+(i+1),new Vector3(-1+i*2,1.06f,3.4f));
                }
                MoveNamed(root,"Sorting table",new Vector3(-8,.85f,15));
                for(int i=0;i<3;i++)MoveNamed(root,"Sorting Tray "+(i+1),new Vector3(-9.1f+i*1.1f,1.04f,15));
                MoveNamed(root,"Station clock",new Vector3(-9,0,21));
                MoveNamed(root,"Supply Terminal (Shop)",new Vector3(1,0,19));
                var scanner=Object.FindFirstObjectByType<ContrabandScanner>();
                if(scanner!=null)scanner.transform.SetPositionAndRotation(new Vector3(-15,0,16),Quaternion.identity);
                var view=Object.FindFirstObjectByType<CheckpointVehicleView>();
                view.entry.SetPositionAndRotation(new Vector3(-21,0,0),Quaternion.Euler(0,90,0));
                view.stop.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,90,0));
                view.exit.SetPositionAndRotation(exits.Last(),Quaternion.Euler(0,-90,0));
                // Cargo spot positions are stored relative to the original stop pose.
                Vector3 oldPosition=view.vehicle.position;Quaternion oldRotation=view.vehicle.rotation;
                var spots=root.Find("Truck cargo spots");
                if(spots!=null)foreach(Transform spot in spots){Vector3 offset=Quaternion.Inverse(oldRotation)*(spot.position-oldPosition);Quaternion relative=Quaternion.Inverse(oldRotation)*spot.rotation;spot.SetPositionAndRotation(view.stop.TransformPoint(offset),view.stop.rotation*relative);}
                view.vehicle.SetPositionAndRotation(view.stop.position,view.stop.rotation);
                view.arrivalRoute=Route(layout,"Arrival route",new[]{view.entry.position,new Vector3(-11,0,0),view.stop.position});
                view.departureRoute=Route(layout,"Underdeck departure route",exits);
                view.checkpoint.arrivalSeconds=7;view.checkpoint.exitSeconds=12;
                var landscape=root.Find("Velora planet landscape");if(landscape!=null)landscape.position+=Vector3.down*12;
                var context=Object.FindFirstObjectByType<GameSceneContext>();
                if(context!=null)
                {
                    for(int i=0;i<context.spawnPoints.Length;i++)context.spawnPoints[i].position=new Vector3(-5+i*1.5f,1.1f,7);
                    context.loadingCamera.transform.position=new Vector3(39,34,-43);context.loadingCamera.transform.LookAt(new Vector3(0,0,5));
                }
                var session=Object.FindFirstObjectByType<NetworkSession>();
                if(session!=null)for(int i=0;i<session.spawnPoints.Length;i++)session.spawnPoints[i].position=new Vector3(-5+i*1.5f,1.1f,7);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();LayoutFinish();EditorSceneManager.OpenScene(MainMenuSetup.GamePath);
            return "Shared deck authored in GameWorld and CheckpointNetworkTest; prior scenery and scene backups preserved.";
        }
        public static string LayoutInspect()
        {
            var sb=new StringBuilder();var root=GameObject.Find("Space Checkpoint");
            sb.AppendLine(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            if(root!=null)foreach(Transform child in root.transform)sb.AppendLine(child.name+" active="+child.gameObject.activeSelf+" pos="+child.position);
            return sb.ToString();
        }
        public static string LayoutFinish()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first.");
            Palette();var roadMaterial=Mat("Checkpoint road graphite",new Color(.10f,.12f,.17f),.42f);
            foreach(string path in new[]{MainMenuSetup.GamePath,CheckpointSceneSetup.TestScenePath})
            {
                var scene=EditorSceneManager.OpenScene(path);var root=GameObject.Find("Space Checkpoint").transform;var layout=root.Find(LayoutName);
                foreach(var t in layout.GetComponentsInChildren<Transform>().Where(t=>t.name=="Road segment"))
                {
                    t.GetComponent<Renderer>().sharedMaterial=roadMaterial;
                    // Keep road tops above floor panels, avoiding coplanar flicker.
                    if(!t.name.EndsWith("finished"))t.position+=Vector3.up*.035f;
                    t.name="Road segment finished";
                }
                if(layout.Find("Exit ramp guards")==null)
                {
                    var guards=Group("Exit ramp guards",layout);var points=Object.FindFirstObjectByType<CheckpointVehicleView>().departureRoute;
                    for(int i=2;i<points.Length;i++)
                    {
                        Vector3 a=points[i-1].position,b=points[i].position,delta=b-a;
                        Vector3 side=Vector3.Cross(Vector3.up,delta).normalized;
                        foreach(int sign in new[]{-1,1})
                        {
                            var rail=Solid(guards,"Ramp guard",(a+b)*.5f+side*sign*3.15f+Vector3.up*.65f,new Vector3(.18f,1.2f,delta.magnitude),ivory);
                            rail.transform.rotation=Quaternion.LookRotation(delta,Vector3.up);
                            Box(rail.transform,"Amber guard trim",new Vector3(0,.52f,0),new Vector3(1.1f,.06f,.99f),orange);
                        }
                    }
                }
                if(layout.Find("Shared operations hall/Equipment expansion workbench")==null)
                {
                    var hall=layout.Find("Shared operations hall");
                    Solid(hall,"Equipment expansion workbench",new Vector3(-7,.95f,19),new Vector3(5,.22f,1.5f),ivory);
                    for(int i=0;i<3;i++)Box(hall,"Stash locker",new Vector3(-18+i*1.4f,1.3f,19),new Vector3(1.2f,2.6f,1.1f),ivory);
                }
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(MainMenuSetup.GamePath);return "Road contrast and ramp guards finished";
        }
        public static string LayoutPreview(string target)
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first.");
            EditorSceneManager.OpenScene(MainMenuSetup.GamePath);
            var camera=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(c=>!c.name.Contains("X-ray"));
            Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            camera.transform.position=target=="ground"?new Vector3(-14,2.5f,-5):target=="exit"?new Vector3(43,9,-16):new Vector3(42,35,-46);
            camera.transform.LookAt(target=="exit"?new Vector3(19,-2,5):new Vector3(0,target=="ground"?1.8f:0,6));camera.fieldOfView=60;
            string output="TestResults/CheckpointLayout/"+target+".png";CheckpointSceneSetup.SaveCamera(camera,output,1600,1000);
            camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;
            return Path.GetFullPath(output);
        }
        public static string LayoutValidate()
        {
            var root=GameObject.Find("Space Checkpoint");
            if(root==null && !EditorApplication.isPlaying){EditorSceneManager.OpenScene(MainMenuSetup.GamePath);root=GameObject.Find("Space Checkpoint");}
            if(root==null)throw new InvalidOperationException("Open the checkpoint gameplay scene first.");
            var layout=root.transform.Find(LayoutName);
            if(layout==null)throw new InvalidOperationException("Layout missing");
            var view=Object.FindFirstObjectByType<CheckpointVehicleView>();
            if(view.entry.position.x>=view.stop.position.x||view.departureRoute.Length<5||view.departureRoute[2].position.y>=-5)throw new InvalidOperationException("Invalid circulation");
            if(Object.FindFirstObjectByType<ContrabandScanner>()==null||Object.FindFirstObjectByType<CheckpointTerminal>()==null)throw new InvalidOperationException("Operational equipment missing");
            int bays=layout.Cast<Transform>().Count(t=>t.name.StartsWith("Inspection bay"));if(bays!=3)throw new InvalidOperationException("Expected 3 bays");
            int missing=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Sum(o=>o.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
            if(missing>0)throw new InvalidOperationException("Missing scripts: "+missing);
            Physics.SyncTransforms();
            foreach(float z in new[]{-5f,5f,15f})for(float x=-8;x<=8;x+=2)
                if(!Physics.Raycast(new Vector3(x,3,z),Vector3.down,out var hit,4)||hit.point.y < -.1f)throw new InvalidOperationException("Walkable deck missing near "+new Vector3(x,0,z));
            bool below=false;
            for(int i=0;i<=100;i++)
            {
                view.RoutePose(view.departureRoute,i/100f,out var position,out var rotation);
                if(position.y < -5 && position.x < 20)below=true;
                if(float.IsNaN(position.x)||float.IsNaN(rotation.w))throw new InvalidOperationException("Invalid route pose");
            }
            if(!below)throw new InvalidOperationException("Exit never travels under the deck");
            return "PASS: three open bays; shared deck collision; live terminal/scanner; arrival left; departure right descending below deck; no missing scripts.";
        }
    }
}
