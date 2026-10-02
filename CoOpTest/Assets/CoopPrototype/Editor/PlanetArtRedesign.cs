using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace CoopPrototype.Editor
{
    // One-time authoring: all geometry, materials and volumes are saved and editable.
    public static partial class PlanetArtRedesign
    {
        const string Art = "Assets/CoopPrototype/Art/Planet";
        static Material ivory, orange, charcoal, cyan, warm, lilac, rose, deck, glass, hazard;
        static Dictionary<string,Mesh> meshes = new();
        static Color C(float r,float g,float b) => new Color(r,g,b);
        static Material Mat(string name,Color color,float gloss=.35f,float emission=0)
        {
            string path=Art+"/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null) { m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path); }
            m.color=color; m.SetFloat("_Smoothness",gloss); m.SetFloat("_Metallic",.08f);
            if(emission>0) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor",color*emission); }
            EditorUtility.SetDirty(m); return m;
        }
        static void Palette()
        {
            Directory.CreateDirectory(Art); Directory.CreateDirectory(Art+"/Meshes");
            ivory=Mat("Porcelain hull",C(.88f,.84f,.75f),.48f);
            orange=Mat("Tangerine fittings",C(1,.34f,.025f),.4f);
            charcoal=Mat("Graphite rubber",C(.075f,.065f,.085f),.27f);
            cyan=Mat("Cyan energy",C(.05f,.72f,1),.5f,2.5f);
            warm=Mat("Amber light",C(1,.65f,.22f),.5f,2.2f);
            lilac=Mat("Violet sandstone",C(.40f,.18f,.58f),.16f);
            rose=Mat("Sunlit sandstone",C(.86f,.36f,.12f),.2f);
            deck=Mat("Warm grey deck",C(.34f,.31f,.35f),.5f);
            glass=Mat("Cockpit glass",C(.055f,.09f,.16f),.85f);
            hazard=Mat("Safety gold",C(1,.65f,.055f),.3f);
        }
        static Transform Group(string name,Transform parent)
        { var t=new GameObject(name).transform; t.SetParent(parent,false); return t; }
        static GameObject Shape(Transform p,string name,PrimitiveType type,Vector3 pos,Vector3 size,Material m)
        {
            var o=GameObject.CreatePrimitive(type); o.name=name; o.transform.SetParent(p,false); o.transform.localPosition=pos; o.transform.localScale=size;
            o.GetComponent<Renderer>().sharedMaterial=m; Object.DestroyImmediate(o.GetComponent<Collider>());
            if(type==PrimitiveType.Cube) Bevel(o.GetComponent<MeshFilter>());
            return o;
        }
        static GameObject Box(Transform p,string n,Vector3 pos,Vector3 size,Material m)=>Shape(p,n,PrimitiveType.Cube,pos,size,m);
        static Mesh SaveMesh(string name,List<Vector3> v,List<int> t,List<Vector2> uv=null)
        {
            string path=Art+"/Meshes/"+name+".asset"; var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh!=null)return mesh;
            mesh=new Mesh {name=name}; mesh.SetVertices(v);mesh.SetTriangles(t,0);if(uv!=null)mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,path); return mesh;
        }
        static void Bevel(MeshFilter filter)
        {
            if(filter==null || filter.sharedMesh==null || filter.sharedMesh.name!="Cube")return;
            Vector3 s=filter.transform.localScale; s=new Vector3(Mathf.Abs(s.x),Mathf.Abs(s.y),Mathf.Abs(s.z));
            if(Mathf.Min(s.x,s.y,s.z)<.055f)return;
            string key=$"Round_{s.x:F2}_{s.y:F2}_{s.z:F2}";
            if(!meshes.TryGetValue(key,out var mesh))
            {
                var v=new List<Vector3>();var tri=new List<int>();var uv=new List<Vector2>();
                float r=Mathf.Min(.12f,Mathf.Min(s.x,s.y,s.z)*.19f); Vector3 h=s*.5f;
                for(int face=0;face<6;face++)
                {
                    Vector3 n=face==0?Vector3.right:face==1?Vector3.left:face==2?Vector3.up:face==3?Vector3.down:face==4?Vector3.forward:Vector3.back;
                    Vector3 u=Mathf.Abs(n.y)>.5f?Vector3.right:Vector3.up; Vector3 w=Vector3.Cross(n,u);
                    int offset=v.Count; const int steps=6;
                    for(int y=0;y<=steps;y++)for(int x=0;x<=steps;x++)
                    {
                        float a=x/(float)steps,b=y/(float)steps;
                        Vector3 point=Vector3.Scale(n*.5f+u*(a-.5f)+w*(b-.5f),s);
                        Vector3 inner=new Vector3(Mathf.Clamp(point.x,-h.x+r,h.x-r),Mathf.Clamp(point.y,-h.y+r,h.y-r),Mathf.Clamp(point.z,-h.z+r,h.z-r));
                        point=inner+(point-inner).normalized*r; v.Add(new Vector3(point.x/s.x,point.y/s.y,point.z/s.z)); uv.Add(new Vector2(a,b));
                    }
                    for(int y=0;y<steps;y++)for(int x=0;x<steps;x++) {int k=offset+y*(steps+1)+x;tri.AddRange(new[]{k,k+1,k+steps+2,k,k+steps+2,k+steps+1});}
                }
                mesh=SaveMesh(key,v,tri,uv); meshes[key]=mesh;
            }
            filter.sharedMesh=mesh;
        }
        static GameObject Torus(Transform p,string name,Vector3 position,float radius,float tube,Material mat)
        {
            string key=$"Torus_{radius:F2}_{tube:F2}"; var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Art+"/Meshes/"+key+".asset");
            if(mesh==null)
            {
                var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>(); const int aCount=96,bCount=12;
                for(int a=0;a<=aCount;a++)for(int b=0;b<=bCount;b++){float aa=a*Mathf.PI*2/aCount,bb=b*Mathf.PI*2/bCount;float rad=radius+Mathf.Cos(bb)*tube;v.Add(new Vector3(Mathf.Cos(aa)*rad,Mathf.Sin(aa)*rad,Mathf.Sin(bb)*tube));uv.Add(new Vector2(a/(float)aCount,b/(float)bCount));}
                for(int a=0;a<aCount;a++)for(int b=0;b<bCount;b++){int k=a*(bCount+1)+b;t.AddRange(new[]{k,k+bCount+2,k+1,k,k+bCount+1,k+bCount+2});}
                mesh=SaveMesh(key,v,t,uv);
            }
            var o=Group(name,p).gameObject;o.transform.localPosition=position;o.AddComponent<MeshFilter>().sharedMesh=mesh;o.AddComponent<MeshRenderer>().sharedMaterial=mat;return o;
        }
        static Material Remap(Material m)
        {
            if(m==null)return ivory;string n=m.name;
            if(n.Contains("Hull")||n.Contains("Coffee cup"))return ivory;
            if(n.Contains("Deck"))return deck;
            if(n.Contains("Midnight")||n.Contains("eyes"))return charcoal;
            if(n.Contains("Portal mint")||n.Contains("Xray guide"))return cyan;
            if(n.Contains("Safety amber")||n.Contains("Coffee sleeve"))return orange;
            if(n.Contains("Starlight"))return warm;
            if(n.Contains("Ion lavender"))return lilac;
            return m;
        }
        static void StyleExisting(Transform root)
        {
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if(r is ParticleSystemRenderer || r is SkinnedMeshRenderer || r.GetComponent<TextMesh>()!=null)continue;
                // X-ray contents and status displays retain their special materials and renderer references.
                if(r.gameObject.layer==LayerMask.NameToLayer("XRay") || r.sharedMaterials.Any(m=>m!=null&&m.name.StartsWith("Xray")))continue;
                r.sharedMaterials=r.sharedMaterials.Select(Remap).ToArray(); Bevel(r.GetComponent<MeshFilter>());
            }
            foreach(var t in root.GetComponentsInChildren<TextMesh>(true))t.color=C(.73f,.93f,1);
        }
        static void Landscape(Transform root)
        {
            var env=Group("Velora planet landscape",root);var rand=new System.Random(2047);
            // Ground surrounds, without collisions: game movement stays inside the authored station.
            Shape(env,"Violet desert",PrimitiveType.Cylinder,new Vector3(0,-3.5f,0),new Vector3(350,2,350),lilac);
            var groundVertices=new List<Vector3>{new Vector3(0,-1.49f,0)};var groundTriangles=new List<int>();
            for(int k=0;k<=64;k++){float angle=-Mathf.PI*.5f+k*Mathf.PI/64;groundVertices.Add(new Vector3(Mathf.Cos(angle)*175,-1.49f,Mathf.Sin(angle)*175));}
            for(int k=1;k<=64;k++)groundTriangles.AddRange(new[]{0,k+1,k});
            var amberGround=Group("Amber desert hemisphere",env).gameObject;amberGround.AddComponent<MeshFilter>().sharedMesh=SaveMesh("Amber hemisphere",groundVertices,groundTriangles);amberGround.AddComponent<MeshRenderer>().sharedMaterial=rose;
            for(int i=0;i<62;i++)
            {
                float a=i*Mathf.PI*2/62,rad=55+(float)rand.NextDouble()*60;
                float h=8+(float)rand.NextDouble()*15;
                var tower=Group("Sandstone spire "+i,env);tower.localPosition=new Vector3(Mathf.Cos(a)*rad,-3,Mathf.Sin(a)*rad);
                var v=new List<Vector3>();var t=new List<int>();const int sides=9,levels=9;
                float width=3+(float)rand.NextDouble()*3;
                for(int j=0;j<levels;j++)for(int k=0;k<sides;k++){float angle=k*Mathf.PI*2/sides;float rr=width*(j==levels-1?.45f:1-j*.058f)*(1+(float)rand.NextDouble()*.12f);if(j%3==1)rr*=1.08f;v.Add(new Vector3(Mathf.Cos(angle)*rr,j*h/(levels-1),Mathf.Sin(angle)*rr));}
                for(int j=0;j<levels-1;j++)for(int k=0;k<sides;k++){int n=(k+1)%sides,o=j*sides;t.AddRange(new[]{o+k,o+sides+k,o+sides+n,o+k,o+sides+n,o+n});}
                v.Add(new Vector3(0,h+.7f,0));for(int k=0;k<sides;k++)t.AddRange(new[]{(levels-1)*sides+k,v.Count-1,(levels-1)*sides+(k+1)%sides});
                tower.gameObject.AddComponent<MeshFilter>().sharedMesh=SaveMesh("Mesa valley_"+i,v,t);tower.gameObject.AddComponent<MeshRenderer>().sharedMaterial=tower.localPosition.x>0?rose:lilac;
                if(i%5==0){var floating=Object.Instantiate(tower.gameObject,env);floating.name="Floating island "+i;floating.transform.localPosition+=new Vector3(3,26,5);floating.transform.localScale=new Vector3(.65f,.3f,.65f);floating.transform.localRotation=Quaternion.Euler(0,0,180);}
            }
            var planet=Shape(env,"Ringed peach planet",PrimitiveType.Sphere,root.name=="Spaceport Menu"?new Vector3(35,36,73):new Vector3(-27,28,-75),Vector3.one*16,Mat("Peach planet",C(.85f,.48f,.52f),.2f));
            var orbit=Torus(env,"Planet dust ring",planet.transform.localPosition,11,.18f,Mat("Planet ring",C(.88f,.64f,.72f),.2f));orbit.transform.localRotation=Quaternion.Euler(65,15,20);
        }
        static void Lighting(Transform root)
        {
            foreach(var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))if(l.type==LightType.Directional)l.gameObject.SetActive(false);
            RenderSettings.skybox=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Planet sky.mat");
            if(RenderSettings.skybox==null){var sky=new Material(Shader.Find("Coop/Planet Sunset Sky"));AssetDatabase.CreateAsset(sky,Art+"/Planet sky.mat");RenderSettings.skybox=sky;}
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=C(.70f,.65f,.80f);RenderSettings.ambientEquatorColor=C(.65f,.56f,.53f);RenderSettings.ambientGroundColor=C(.35f,.28f,.40f);RenderSettings.ambientIntensity=1;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.006f;RenderSettings.fogColor=C(.58f,.36f,.68f);
            var sun=Group("Warm sunset key",root).gameObject.AddComponent<Light>();sun.type=LightType.Directional;sun.color=C(1,.76f,.51f);sun.intensity=2.2f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(32,-110,0);
            var fill=Group("Lavender sky fill",root).gameObject.AddComponent<Light>();fill.type=LightType.Directional;fill.color=C(.57f,.58f,1);fill.intensity=.65f;fill.transform.rotation=Quaternion.Euler(48,55,0);
            var practicals=Group("Station practical lighting",root);
            foreach(float x in new[]{-10f,-2f,6f})foreach(float z in new[]{-6f,3f,10f})
            {
                var lamp=Group("Amber canopy wash",practicals).gameObject.AddComponent<Light>();lamp.type=LightType.Point;lamp.transform.position=new Vector3(x,4.5f,z);lamp.color=C(1,.82f,.62f);lamp.intensity=38;lamp.range=16;lamp.shadows=LightShadows.None;
            }
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Art+"/Planet grading.asset");
            if(profile==null){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Art+"/Planet grading.asset");var b=profile.Add<Bloom>(true);b.intensity.Override(.28f);b.threshold.Override(1.1f);b.scatter.Override(.55f);var c=profile.Add<ColorAdjustments>(true);c.postExposure.Override(.1f);c.saturation.Override(8);c.contrast.Override(8);foreach(var component in profile.components)AssetDatabase.AddObjectToAsset(component,profile);EditorUtility.SetDirty(profile);}
            var volume=Group("Planet color and bloom",root).gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=10;volume.sharedProfile=profile;
            foreach(var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)){if(cam.name.Contains("X-ray"))continue;cam.clearFlags=CameraClearFlags.Skybox;cam.farClipPlane=Mathf.Max(cam.farClipPlane,350);var data=cam.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;}
        }
        static void Portal(Transform root)
        {
            var old=root.Find("Arrival portal");if(old==null)return;
            foreach(Transform child in old)child.gameObject.SetActive(false);
            var gate=Group("Porcelain portal gate",old);float radius=root.name=="Spaceport Menu"?2.8f:3.1f;
            Torus(gate,"Graphite inner housing",Vector3.zero,radius,.30f,charcoal);
            Torus(gate,"Porcelain outer housing",new Vector3(0,0,.05f),radius+.3f,.25f,ivory);
            Torus(gate,"Cyan aperture",new Vector3(0,0,-.28f),radius-.2f,.055f,cyan);
            for(int i=0;i<16;i++){float a=i*Mathf.PI*2/16;var segment=Box(gate,"Radial armor "+i,new Vector3(Mathf.Sin(a),Mathf.Cos(a),0)*(radius+.28f),new Vector3(.7f,.45f,.67f),ivory);segment.transform.localRotation=Quaternion.Euler(0,0,-a*Mathf.Rad2Deg);var strip=Box(segment.transform,"Energy strip",new Vector3(0,0,-.53f),new Vector3(.57f,.28f,.09f),cyan);}
            for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;Vector3 p=new Vector3(Mathf.Sin(a),Mathf.Cos(a),0)*(radius+.28f);var unit=Box(gate,"Orange locking module "+i,p+new Vector3(0,0,-.15f),new Vector3(.92f,.72f,.8f),orange);unit.transform.localRotation=Quaternion.Euler(0,0,-i*90);Box(unit.transform,"Lock core",new Vector3(0,0,-.52f),new Vector3(.62f,.22f,.1f),cyan);}
            for(int s=-1;s<=1;s+=2){Box(gate,"Gate foot",new Vector3(s*(radius-.55f),-radius+.2f,0),new Vector3(1.3f,.7f,1.4f),ivory);var brace=Box(gate,"Support brace",new Vector3(s*(radius-.65f),-radius+1,0),new Vector3(.35f,1.9f,.6f),orange);brace.transform.localRotation=Quaternion.Euler(0,0,s*-23);}
            string path=Art+"/Portal energy.mat";var energy=AssetDatabase.LoadAssetAtPath<Material>(path);if(energy==null){energy=new Material(Shader.Find("Coop/Portal Energy"));AssetDatabase.CreateAsset(energy,path);}
            Shape(gate,"Animated portal surface",PrimitiveType.Quad,new Vector3(0,0,.1f),Vector3.one*(radius-.22f)*2,energy);
        }
        static void Architecture(Transform world,bool menu)
        {
            var p=Group("Station architectural detail",world);
            float width=menu?18:30,depth=menu?16:30;
            // Raised panel borders and safety stripes read clearly from the gameplay camera.
            for(float z=-depth/2+.2f;z<depth/2;z+=1.1f)for(int side=-1;side<=1;side+=2){var stripe=Box(p,"Perimeter safety stripe",new Vector3(side*(width/2-.65f),.045f,z),new Vector3(.7f,.025f,.52f),hazard);stripe.transform.localRotation=Quaternion.Euler(0,-25,0);}
            for(int x=-13;x<=13;x+=4)for(int z=-13;z<=13;z+=4)if(!menu){Box(p,"Panel corner fastener",new Vector3(x,.034f,z),new Vector3(.10f,.025f,.1f),ivory);}
            if(!menu)
            {
                // Extend the existing canopy so the yard feels like a coherent station.
                Box(p,"Workshop roof",new Vector3(-4.5f,6.6f,2),new Vector3(19,.3f,18),ivory);
                foreach(float x in new[]{-13f,3f})foreach(float z in new[]{-7f,10f})
                {
                    Box(p,"Porcelain column",new Vector3(x,3.2f,z),new Vector3(.45f,6.4f,.45f),ivory);
                    Box(p,"Column orange foot",new Vector3(x,.55f,z),new Vector3(.7f,.55f,.7f),orange);
                    Box(p,"Column collar",new Vector3(x,5.9f,z),new Vector3(.7f,.6f,.7f),orange);
                }
                foreach(float z in new[]{-6.5f,2f,9.5f}){Box(p,"Roof structural rib",new Vector3(-4.5f,6.38f,z),new Vector3(18,.25f,.35f),charcoal);Box(p,"Warm ceiling strip",new Vector3(-4.5f,6.2f,z),new Vector3(14,.055f,.13f),warm);}
                for(int i=0;i<7;i++)Crate(p,new Vector3(-13.3f,.52f,-10+i*2.8f),i%2==0);
                for(int i=0;i<5;i++){float z=-10+i*4;Box(p,"Lane bollard",new Vector3(7.7f,.7f,z),new Vector3(.34f,1.4f,.34f),ivory);Box(p,"Bollard cap",new Vector3(7.7f,1.4f,z),new Vector3(.46f,.18f,.46f),orange);Box(p,"Bollard warning",new Vector3(7.7f,.9f,z),new Vector3(.37f,.18f,.37f),hazard);}
            }
            else {for(int i=0;i<4;i++)Crate(p,new Vector3(-3+i*2.2f,.45f,5),i%2==0);}
        }
        static void Crate(Transform parent,Vector3 position,bool gold)
        {
            var crate=Group("Modular supply crate",parent);crate.localPosition=position;
            Box(crate,"Crate body",Vector3.zero,new Vector3(.95f,.9f,.9f),gold?hazard:ivory);
            for(int s=-1;s<=1;s+=2){Box(crate,"Corner bumper",new Vector3(s*.4f,0,-.36f),new Vector3(.18f,.95f,.18f),charcoal);Box(crate,"Clamp",new Vector3(s*.4f,0,-.47f),new Vector3(.15f,.24f,.10f),orange);}
            Box(crate,"Inset handle",new Vector3(0,.16f,-.455f),new Vector3(.3f,.13f,.025f),charcoal);
        }
        static void VehicleDetail(Transform world)
        {
            var hover=world.Find("Traveller Shuttle/Hover assembly");if(hover==null)return;
            var trim=Torus(hover,"Orange shuttle belt",new Vector3(0,1.0f,0),1.65f,.08f,orange);trim.transform.localRotation=Quaternion.Euler(90,0,0);
            for(int i=0;i<6;i++){float a=i*Mathf.PI/3;var pod=Group("Circular hover engine "+i,hover);pod.localPosition=new Vector3(Mathf.Cos(a)*1.5f,.85f,Mathf.Sin(a)*1.5f);pod.localRotation=Quaternion.Euler(90,0,0);Torus(pod,"Orange thruster bezel",Vector3.zero,.21f,.045f,orange);Shape(pod,"Engine light",PrimitiveType.Sphere,Vector3.zero,new Vector3(.32f,.32f,.09f),warm);}
            Shape(hover,"Dark cockpit bubble",PrimitiveType.Sphere,new Vector3(0,1.65f,.45f),new Vector3(1.6f,1.0f,1.1f),glass);
        }
        static void FinishProps(Transform world)
        {
            var architecture=world.Find("Station architectural detail");
            if(architecture!=null)
            {
                var roof=architecture.Find("Workshop roof");if(roof!=null)roof.GetComponent<Renderer>().sharedMaterial=deck;
                if(roof!=null&&architecture.Find("Ceiling panel joints")==null)
                {
                    var ribs=Group("Ceiling panel joints",architecture);
                    foreach(float x in new[]{-12f,-7f,-2f,2.5f})Box(ribs,"Panel divider",new Vector3(x,6.38f,2),new Vector3(.08f,.06f,17.6f),charcoal);
                    foreach(float x in new[]{-13.8f,4.8f})Box(ribs,"Orange roof edge",new Vector3(x,6.40f,2),new Vector3(.18f,.27f,18),orange);
                }
            }
            var boot=world.Find("Giant rejection boot");
            if(boot!=null&&boot.Find("Porcelain rejection boot")==null)
            {
                foreach(Transform old in boot)old.gameObject.SetActive(false);
                var shoe=Group("Porcelain rejection boot",boot);
                Box(shoe,"Graphite boot sole",new Vector3(0,-.6f,-.55f),new Vector3(2.1f,.35f,4),charcoal);
                Shape(shoe,"Rounded boot toe",PrimitiveType.Sphere,new Vector3(0,-.04f,-.58f),new Vector3(2.05f,1.4f,3.9f),ivory);
                Shape(shoe,"Porcelain pressure shaft",PrimitiveType.Capsule,new Vector3(0,1.05f,.58f),new Vector3(1.8f,1.9f,1.8f),ivory);
                Box(shoe,"Orange ankle cuff",new Vector3(0,.43f,.45f),new Vector3(2.1f,.48f,2.15f),orange);
                for(int i=0;i<4;i++)Box(shoe,"Rubber instep pad",new Vector3(0,.83f+i*.4f,-.31f),new Vector3(1.43f,.30f,.25f),charcoal);
                for(int i=0;i<7;i++)Box(shoe,"Sole tread",new Vector3(0,-.77f,-2.25f+i*.55f),new Vector3(2.05f,.12f,.15f),charcoal);
                for(int s=-1;s<=1;s+=2)Box(shoe,"Ankle latch",new Vector3(s*.95f,.46f,-.58f),new Vector3(.24f,.28f,.1f),ivory);
            }
            var detail=world.Find("Station equipment finish");if(detail!=null)return;
            detail=Group("Station equipment finish",world);
            foreach(var surface in world.GetComponentsInChildren<MeshFilter>(true).Where(t=>new[]{"Desk","Sorting table","Document collection shelf"}.Contains(t.name)).ToArray())
            {
                var p=surface.transform.position;var scale=surface.transform.lossyScale;
                for(int s=-1;s<=1;s+=2)Box(detail,"Orange workstation corner",p+new Vector3(s*(scale.x*.5f-.12f),.09f,-scale.z*.5f),new Vector3(.23f,.16f,.20f),orange);
                Box(detail,"Workstation front inset",p+new Vector3(0,-.065f,-scale.z*.5f-.015f),new Vector3(scale.x*.65f,.07f,.025f),charcoal);
            }
            var portal=world.Find("Arrival portal/Porcelain portal gate");
            if(portal!=null)foreach(Transform part in portal)
            {
                if(part.name.StartsWith("Radial armor")&&part.Find("Back energy strip")==null)Box(part,"Back energy strip",new Vector3(0,0,.53f),new Vector3(.57f,.28f,.09f),cyan);
                if(part.name.StartsWith("Orange locking")&&part.Find("Back lock core")==null)Box(part,"Back lock core",new Vector3(0,0,.52f),new Vector3(.62f,.22f,.1f),cyan);
            }
        }
        static void Prefabs()
        {
            foreach(string file in new[]{"CargoPackage","CoffeeCup","UvLamp"})
            {
                string path="Assets/CoopPrototype/Prefabs/"+file+".prefab";var root=PrefabUtility.LoadPrefabContents(path);StyleExisting(root.transform);
                if(file=="CargoPackage" && root.transform.Find("Orange edge fittings")==null){var g=Group("Orange edge fittings",root.transform);for(int s=-1;s<=1;s+=2)Box(g,"Corner lock",new Vector3(s*.22f,0,-.255f),new Vector3(.1f,.16f,.025f),orange);}
                PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);
            }
            string playerPath="Assets/CoopPrototype/Prefabs/NetworkPlayer.prefab";var player=PrefabUtility.LoadPrefabContents(playerPath);
            foreach(var cam in player.GetComponentsInChildren<Camera>(true))if(!cam.name.Contains("View Model")){cam.clearFlags=CameraClearFlags.Skybox;cam.GetUniversalAdditionalCameraData().renderPostProcessing=true;}
            PrefabUtility.SaveAsPrefabAsset(player,playerPath);PrefabUtility.UnloadPrefabContents(player);
        }
        [MenuItem("Coop Prototype/Art/Apply Velora Planet Redesign")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");
            EditorSceneManager.SaveOpenScenes();Palette();
            string backup="Tools/Checkpoint/BeforePlanetRedesign";Directory.CreateDirectory(backup);
            foreach(string path in new[]{MainMenuSetup.GamePath,MainMenuSetup.MenuPath,CheckpointSceneSetup.TestScenePath})
            {
                if(!File.Exists(path))continue;string copy=backup+"/"+Path.GetFileName(path);if(!File.Exists(copy))File.Copy(path,copy);
                var scene=EditorSceneManager.OpenScene(path);bool menu=path==MainMenuSetup.MenuPath;var root=GameObject.Find(menu?"Spaceport Menu":"Space Checkpoint").transform;
                if(root.Find("Velora planet landscape")!=null)continue;
                StyleExisting(root);
                foreach(Transform t in root)if(t.name.StartsWith("Star ")||t.name=="Distant lavender moon"||t.name=="Moon orbit"||t.name=="Customs canopy"||t.name=="Canopy column"||t.name=="Canopy trim")t.gameObject.SetActive(false);
                Landscape(root);Portal(root);Architecture(root,menu);Lighting(root);if(!menu)VehicleDetail(root);
                EditorSceneManager.SaveScene(scene);
            }
            Prefabs();AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(MainMenuSetup.GamePath);
        }
        public static string Inspect()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            string Hier(Transform t)=>t.parent==null?t.name:Hier(t.parent)+"/"+t.name;
            return scene.path+"\n"+string.Join("\n",scene.GetRootGameObjects().Select(o=>o.name+" active="+o.activeSelf))+"\n"+string.Join("\n",Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Select(c=>$"camera {c.name}: {c.transform.position}"))+"\n"+string.Join("\n",Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None).Select(c=>Hier(c.transform)))+"\n"+string.Join("\n",Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name=="Arrival portal"||t.name=="Traveller Shuttle").Select(t=>$"{t.name} pos={t.position}"));
        }
        public static string Refine()
        {
            Palette();
            foreach(string path in new[]{MainMenuSetup.GamePath,MainMenuSetup.MenuPath,CheckpointSceneSetup.TestScenePath})
            {
                var scene=EditorSceneManager.OpenScene(path);var root=GameObject.Find(path==MainMenuSetup.MenuPath?"Spaceport Menu":"Space Checkpoint").transform;
                var landscape=root.Find("Velora planet landscape");if(landscape!=null)Object.DestroyImmediate(landscape.gameObject);Landscape(root);
                foreach(Transform old in root.Cast<Transform>().Where(t=>new[]{"Station practical lighting","Planet color and bloom","Warm sunset key","Lavender sky fill"}.Contains(t.name)).ToArray())Object.DestroyImmediate(old.gameObject);Lighting(root);
                // Preserve the earlier Tencent rig experiment, but remove it from the lobby composition.
                if(path==MainMenuSetup.MenuPath){var experiment=GameObject.Find("CharacterCoOp");if(experiment!=null)experiment.SetActive(false);}
                FinishProps(root);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();return "Landscape refined";
        }
        public static string Crew()
        {
            Palette(); string path=Art+"/PlanetCrew.fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);var original=(ModelImporter)AssetImporter.GetAtPath(BeanCrewSetup.CharacterModel);
            importer.globalScale=original.globalScale;importer.useFileScale=true;importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=false;importer.isReadable=true;importer.SaveAndReimport();
            var preview=EditorSceneManager.NewPreviewScene();
            var source=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),preview);
            string playerPath="Assets/CoopPrototype/Prefabs/NetworkPlayer.prefab";var player=PrefabUtility.LoadPrefabContents(playerPath);
            ReplaceCrew(player,source);PrefabUtility.SaveAsPrefabAsset(player,playerPath);PrefabUtility.UnloadPrefabContents(player);
            foreach(string scenePath in new[]{MainMenuSetup.MenuPath,MainMenuSetup.GamePath,CheckpointSceneSetup.TestScenePath})
            {
                var scene=EditorSceneManager.OpenScene(scenePath);
                foreach(var root in scene.GetRootGameObjects().Where(o=>o.activeSelf))ReplaceCrew(root,source);
                EditorSceneManager.SaveScene(scene);
            }
            EditorSceneManager.ClosePreviewScene(preview);AssetDatabase.SaveAssets();
            return "Four astronaut outfits bound to existing crew skeleton";
        }
        public static string Arms()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");Palette();
            string path=Art+"/PlanetArms.fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale=((ModelImporter)AssetImporter.GetAtPath(BeanCrewSetup.ArmsModel)).globalScale;importer.useFileScale=true;importer.bakeAxisConversion=true;importer.animationType=ModelImporterAnimationType.None;importer.importAnimation=false;importer.SaveAndReimport();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);var root=PrefabUtility.LoadPrefabContents("Assets/CoopPrototype/Prefabs/NetworkPlayer.prefab");
            var arms=root.GetComponentInChildren<FirstPersonArms>(true);
            foreach(var target in arms.leftVariants.Concat(arms.rightVariants))
            {
                string[] name=target.name.Split(' ');var source=model.GetComponentsInChildren<MeshRenderer>().First(r=>r.name=="FP_"+name[0]+"_"+name[1]);
                target.GetComponent<MeshFilter>().sharedMesh=source.GetComponent<MeshFilter>().sharedMesh;
                target.transform.localPosition=source.transform.localPosition;target.transform.localRotation=source.transform.localRotation;target.transform.localScale=source.transform.localScale;
                target.sharedMaterials=source.sharedMaterials.Select(m=>m.name.Contains("Ivory")?ivory:m.name.Contains("Rubber")?charcoal:AssetDatabase.LoadAssetAtPath<Material>(Art+"/"+m.name+".mat")).ToArray();
            }
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/CoopPrototype/Prefabs/NetworkPlayer.prefab");PrefabUtility.UnloadPrefabContents(root);AssetDatabase.SaveAssets();return "Eight first-person astronaut arms saved";
        }
        static void ReplaceCrew(GameObject root,GameObject source)
        {
            foreach(var target in root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name.StartsWith("Crew_")))
            {
                var src=source.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(r=>r.name==target.name);if(src==null)continue;
                var animator=target.GetComponentInParent<Animator>();if(animator==null)continue;
                var bones=animator.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.name,t=>t);
                var mapped=src.bones.Select(b=>bones.TryGetValue(b.name,out var found)?found:null).ToArray();
                if(mapped.Any(b=>b==null))throw new InvalidOperationException("Missing astronaut bone in "+target.name);
                string meshPath=Art+"/Meshes/Astronaut_"+target.name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(mesh==null)
                {
                    mesh=Object.Instantiate(src.sharedMesh);mesh.name="Astronaut "+target.name;
                    Matrix4x4 conversion=target.transform.worldToLocalMatrix*animator.transform.localToWorldMatrix*source.transform.worldToLocalMatrix*src.transform.localToWorldMatrix;
                    mesh.vertices=mesh.vertices.Select(conversion.MultiplyPoint3x4).ToArray();
                    mesh.bindposes=mapped.Select(b=>b.worldToLocalMatrix*target.transform.localToWorldMatrix).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,meshPath);
                }
                target.sharedMesh=mesh;target.bones=mapped;target.rootBone=bones["Root"];target.localBounds=mesh.bounds;target.updateWhenOffscreen=true;
                target.sharedMaterials=src.sharedMaterials.Select(m=>m.name.Contains("Ivory")?ivory:m.name.Contains("Rubber")?charcoal:m.name.Contains("Visor")?glass:m.name.Contains("Orange")?orange:m.name.Contains("Glint")?Mat("Visor white reflection",C(.90f,.94f,1),.6f,.3f):Mat(m.name,m.color,.4f)).ToArray();
            }
        }
        public static string Preview(string target)
        {
            bool menu=target=="menu";EditorSceneManager.OpenScene(menu?MainMenuSetup.MenuPath:MainMenuSetup.GamePath);
            var cam=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(c=>!c.name.Contains("X-ray"));
            Vector3 pos=cam.transform.position;Quaternion rot=cam.transform.rotation;float fov=cam.fieldOfView;
            if(!menu){cam.transform.position=target=="close"?new Vector3(-2,2.6f,-5):new Vector3(14,3.5f,6);cam.transform.LookAt(target=="close"?new Vector3(4,2,-12):new Vector3(-1,2,-9));cam.fieldOfView=65;}
            string output="TestResults/PlanetRedesign/"+target+".png";CheckpointSceneSetup.SaveCamera(cam,output,1600,1000);
            cam.transform.SetPositionAndRotation(pos,rot);cam.fieldOfView=fov;return Path.GetFullPath(output);
        }
        public static string ValidateCrew()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");
            EditorSceneManager.OpenScene(MainMenuSetup.MenuPath);
            var crew=GameObject.Find("Crew Preview 1");var animator=crew.GetComponentInChildren<Animator>();
            var camera=Object.FindFirstObjectByType<Camera>();var report=new List<string>();
            foreach(string clipName in new[]{"Idle","Walk","Carry Walk","Throw"})
            {
                var clip=animator.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name==clipName);
                if(clip==null)throw new InvalidOperationException("Missing clip "+clipName);
                var paths=AnimationUtility.GetCurveBindings(clip).Where(b=>b.type==typeof(Transform)).Select(b=>b.path).Distinct().ToArray();
                if(paths.Length==0||paths.Any(p=>!string.IsNullOrEmpty(p)&&animator.transform.Find(p)==null))throw new InvalidOperationException("Clip paths do not match crew skeleton: "+clip.name);
                foreach(float fraction in new[]{.15f,.5f,.85f})
                {
                    clip.SampleAnimation(animator.gameObject,clip.length*fraction);
                    foreach(var renderer in crew.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if(renderer.bones.Any(b=>b==null)||renderer.bones.Length!=renderer.sharedMesh.bindposes.Length)throw new InvalidOperationException("Rig mismatch "+renderer.name);
                        var baked=new Mesh();renderer.BakeMesh(baked);var bounds=baked.bounds;
                        if(baked.vertices.Any(v=>float.IsNaN(v.x)||float.IsInfinity(v.y)) || bounds.size.magnitude>5 || bounds.size.y<.5f)throw new InvalidOperationException("Bad deformed bounds "+renderer.name+" "+bounds);
                        Object.DestroyImmediate(baked);
                    }
                }
                var visible=crew.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.enabled&&r.gameObject.activeInHierarchy);
                camera.transform.position=visible.bounds.center+new Vector3(0,.25f,-3);camera.transform.LookAt(visible.bounds.center);camera.fieldOfView=38;
                CheckpointSceneSetup.SaveCamera(camera,"TestResults/PlanetRedesign/Crew-"+clipName+".png",900,1000);
                report.Add("PASS "+clipName+": four meshes, 3 sampled poses, finite bounds and matching bone/bindpose counts");
            }
            Directory.CreateDirectory("TestResults/PlanetRedesign");File.WriteAllLines("TestResults/PlanetRedesign/CrewValidation.txt",report);
            EditorSceneManager.OpenScene(MainMenuSetup.MenuPath);return string.Join("\n",report);
        }
    }
}
