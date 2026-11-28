extern alias engine;
global using Vector2=engine::UnityEngine.Vector2;
global using Vector3=engine::UnityEngine.Vector3;
global using Mathf=engine::UnityEngine.Mathf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using N=System.Numerics;
using UnityEngine;

// Explicit scene/rotation stand-ins. Vector math and the extracted pose methods
// execute as shipped; this does not validate native Animator/Transform scheduling.
namespace UnityEngine
{
    public struct Quaternion
    {
        public N.Quaternion V;
        public static Quaternion identity=>new(){V=N.Quaternion.Identity};
        public static Quaternion Inverse(Quaternion q)=>new(){V=N.Quaternion.Inverse(q.V)};
        public static Quaternion AngleAxis(float degrees,Vector3 axis)=>new(){V=N.Quaternion.CreateFromAxisAngle(new N.Vector3(axis.x,axis.y,axis.z),degrees*MathF.PI/180f)};
        public static Quaternion operator *(Quaternion a,Quaternion b)=>new(){V=N.Quaternion.Normalize(a.V*b.V)};
        public static Vector3 operator *(Quaternion q,Vector3 p)
        {var v=N.Vector3.Transform(new N.Vector3(p.x,p.y,p.z),q.V);return new(v.X,v.Y,v.Z);}
        public static float Angle(Quaternion a,Quaternion b)=>2*MathF.Acos(Math.Clamp(MathF.Abs(N.Quaternion.Dot(a.V,b.V)),0,1))*180/MathF.PI;
        public static Quaternion Slerp(Quaternion a,Quaternion b,float t)=>new(){V=N.Quaternion.Slerp(a.V,b.V,t)};
        public static Quaternion RotateTowards(Quaternion a,Quaternion b,float maxDegrees)=>Slerp(a,b,Math.Min(1,maxDegrees/Math.Max(.00001f,Angle(a,b))));
        public static Quaternion LookRotation(Vector3 forward,Vector3 up)
        {
            var f=N.Vector3.Normalize(new N.Vector3(forward.x,forward.y,forward.z));
            var r=N.Vector3.Normalize(N.Vector3.Cross(new N.Vector3(up.x,up.y,up.z),f));var u=N.Vector3.Cross(f,r);
            var m=new N.Matrix4x4(r.X,r.Y,r.Z,0,u.X,u.Y,u.Z,0,f.X,f.Y,f.Z,0,0,0,0,1);
            return new(){V=N.Quaternion.CreateFromRotationMatrix(m)};
        }
        public static Quaternion FromToRotation(Vector3 from,Vector3 to)
        {
            var a=from.normalized;var b=to.normalized;float dot=Vector3.Dot(a,b);
            if(dot<-.999999f){var axis=Vector3.Cross(a,Vector3.up);if(axis.sqrMagnitude<.001f)axis=Vector3.Cross(a,Vector3.right);return AngleAxis(180,axis.normalized);}
            var cross=Vector3.Cross(a,b);return new(){V=N.Quaternion.Normalize(new N.Quaternion(cross.x,cross.y,cross.z,1+dot))};
        }
    }
    public sealed class Transform
    {
        Transform parentValue;
        readonly List<Transform> children=new();
        public string name;
        public Transform parent
        {
            get=>parentValue;
            set { parentValue?.children.Remove(this);parentValue=value;parentValue?.children.Add(this); }
        }
        public Quaternion localRotation=Quaternion.identity;
        public Vector3 localPosition;
        public Vector3 localScale=Vector3.one;
        public Vector3 lossyScale=>parent==null?localScale:Vector3.Scale(parent.lossyScale,localScale);
        public Vector3 position=>parent==null?localPosition:parent.TransformPoint(localPosition);
        public Quaternion rotation
        {
            get=>parent==null?localRotation:parent.rotation*localRotation;
            set=>localRotation=parent==null?value:Quaternion.Inverse(parent.rotation)*value;
        }
        public Vector3 up=>rotation*Vector3.up;
        public Vector3 forward=>rotation*Vector3.forward;
        public Vector3 TransformPoint(Vector3 p)=>position+rotation*Vector3.Scale(lossyScale,p);
        public Vector3 InverseTransformPoint(Vector3 p)
        {var v=Quaternion.Inverse(rotation)*(p-position);var s=lossyScale;return new(v.x/s.x,v.y/s.y,v.z/s.z);}
        public void SetParent(Transform value,bool worldPositionStays)=>parent=value;
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T:class=>
            new[]{(T)(object)this}.Concat(children.SelectMany(child=>child.GetComponentsInChildren<T>(includeInactive))).ToArray();
    }
    public sealed class GameObject
    {
        public readonly Transform transform;
        public GameObject(string name)=>transform=new(){name=name};
    }
}
namespace ZombieSurvivalRun
{
    enum WeaponType { Pistol,Rifle,Shotgun,Minigun }
    partial class AimRig
    {
        readonly Transform transform=new(),spine=new(),head=new();
        Transform weaponSocket=new();
        readonly ZSRUpperBodyAim upperAim=new();
        bool hero=true,sampledUpperBody=true,moving=true,walking;
        Motion motion=Motion.Fire;
        float aimTime=.55f,recoil=.057f,breathPhase;
        float steeringRoll,steeringHeading;
        Vector3 aimPoint;
        Vector3 socketPosition;
        Quaternion socketRest=Quaternion.identity,sampledSpine=Quaternion.identity,sampledHead=Quaternion.identity;
        Transform supportUpper,supportLower,supportHand;
        Vector3 supportPoint;
        Quaternion supportRotation,sampledSupportUpper,sampledSupportLower,sampledSupportHand;
        float supportWeight;
        bool sampledSupport;
        static bool IsShooting(Motion value)=>value==Motion.Fire||value==Motion.WalkFire;
        static void Need(bool ok,string message){if(!ok)throw new Exception(message);}
        public void Check(int hz)
        {
            spine.parent=transform;head.parent=spine;
            weaponSocket.parent=new Transform{parent=spine};
            sampledHead=Quaternion.AngleAxis(5,Vector3.right);
            float dt=1f/hz;
            for(int frame=0;frame<hz*60;frame++)
            {
                RestoreSampledPose();
                Need(Quaternion.Angle(head.localRotation,sampledHead)<.08f,"Head restored before sparse sampling");
                Need(Quaternion.Angle(spine.localRotation,sampledSpine)<.08f,"Spine restored before sparse sampling");
                float time=frame/(float)hz;
                walking=(frame/(hz*2))%2==0;
                motion=walking?Motion.WalkFire:Motion.Fire;
                steeringRoll=MathF.Sin(time*3)*13;steeringHeading=MathF.Cos(time*2)*32;
                transform.localRotation=Quaternion.AngleAxis(steeringHeading,Vector3.up);
                aimPoint=new Vector3(MathF.Sin(time*7)*20,MathF.Cos(time*3)*8,15);
                recoil=frame%7==0?.105f:0f;
                ApplySteering();
                ApplyUpperBodyAim(dt);
                Need(Quaternion.Angle(head.localRotation,sampledHead)<24f,"Bounded head compensation, no accumulation");
                Need(Quaternion.Angle(spine.localRotation,sampledSpine)<65f,"Bounded combined turning/aiming pose");
            }
            aimTime=recoil=0;moving=false;
            steeringRoll=steeringHeading=0;transform.localRotation=Quaternion.identity;
            for(int i=0;i<hz*3;i++){RestoreSampledPose();ApplyUpperBodyAim(dt);}
            Need(Quaternion.Angle(head.localRotation,sampledHead)<.08f,"Head returns to authored pose after aim");
            Need(Quaternion.Angle(spine.localRotation,sampledSpine)<.30f,"Only subtle idle breathing remains");
            motion=Motion.Death;RestoreSampledPose();ApplyUpperBodyAim(dt);
            Need(Quaternion.Angle(spine.localRotation,sampledSpine)<.08f,"Death is not overridden by aiming");
            Console.WriteLine($"UPPER RIG PASS {hz} Hz: 60 s combined lane banking/heading/rapid aim/recoil, restored sparse tracks, bounded neck/shoulders, neutral recovery and untouched death. Scene/rotation stand-ins.");
        }
        public void CheckSocket(int hz)
        {
            var hand=new Transform();weaponSocket.parent=hand;
            socketPosition=new Vector3(.012f,.037f,.022f);
            socketRest=Quaternion.AngleAxis(8,Vector3.right);
            for(int frame=0;frame<hz*10;frame++)
            {
                float t=frame/(float)hz;
                hand.localRotation=Quaternion.AngleAxis(MathF.Sin(t*8)*2.4f,Vector3.forward)*
                    Quaternion.AngleAxis(MathF.Cos(t*8)*2.8f,Vector3.right);
                hand.localPosition=new Vector3(MathF.Sin(t*8)*.014f,1.4f+MathF.Cos(t*16)*.025f,0);
                weaponSocket.localPosition=socketPosition;
                var rest=hand.rotation*socketRest;
                aimPoint=weaponSocket.position+(rest*Vector3.forward)*10;aimTime=.7f;
                ApplyAim();
                Need(Quaternion.Angle(rest,weaponSocket.rotation)<.08f,"Aiming must preserve authored running roll");
                Need(weaponSocket.localPosition==socketPosition,"Keep calibrated wrist mount offset");
                aimPoint=weaponSocket.position+new Vector3(6,4,10);
                ApplyAim();
                Need(Quaternion.Angle(rest,weaponSocket.rotation)<1.08f,"Fine aim must not flatten carry motion");
                aimTime=0;ApplyAim();
                Need(Quaternion.Angle(weaponSocket.localRotation,socketRest)<.08f,"No-target wrist rotation follows animation");
            }
            Console.WriteLine($"WEAPON SOCKET PASS {hz} Hz: real ApplyAim preserves running roll, wrist mount offset, no-target motion and at most one degree of fine correction. Scene/rotation stand-ins.");
        }
        public void CheckSupport(int hz)
        {
            supportUpper=new Transform{localPosition=new(-.20f,1.50f,.12f)};
            supportLower=new Transform{parent=supportUpper,localPosition=new(0,-.28f,0)};
            supportHand=new Transform{parent=supportLower,localPosition=new(0,-.26f,0)};
            weaponSocket.parent=null;supportRotation=Quaternion.AngleAxis(25,Vector3.forward);
            float dt=1f/hz;
            foreach(var point in new[]{new Vector3(-.045f,.030f,-.035f),new Vector3(-.080f,.045f,.215f),new Vector3(-.080f,.045f,.245f),new Vector3(-.090f,.045f,.255f)})
            {
                supportPoint=point;supportWeight=0;motion=Motion.Fire;
                for(int frame=0;frame<hz*3;frame++)
                {
                    RestoreSupportPose();
                    float t=frame/(float)hz;
                    supportUpper.localRotation=Quaternion.AngleAxis(-20,Vector3.forward);
                    supportLower.localRotation=Quaternion.AngleAxis(-105,Vector3.right);
                    supportHand.localRotation=Quaternion.identity;
                    sampledSupportUpper=supportUpper.localRotation;sampledSupportLower=supportLower.localRotation;sampledSupportHand=supportHand.localRotation;sampledSupport=true;
                    weaponSocket.localPosition=new(.08f,1.35f,.25f);
                    weaponSocket.localRotation=Quaternion.AngleAxis(MathF.Sin(t*8)*2f,Vector3.forward);
                    ApplySupportGrip(dt);
                    Need(Math.Abs((supportLower.position-supportUpper.position).magnitude-.28f)<.0001f&&Math.Abs((supportHand.position-supportLower.position).magnitude-.26f)<.0001f,"Grip IK preserves arm lengths");
                    if(frame>hz/2)Need((supportHand.position-weaponSocket.TransformPoint(point)).magnitude<.002f,"Four support targets track weapon motion");
                }
                motion=Motion.Medkit;
                for(int i=0;i<hz;i++){RestoreSupportPose();ApplySupportGrip(dt);}
                Need(supportWeight==0,"Item frees the support hand");
                Need(Quaternion.Angle(supportUpper.localRotation,sampledSupportUpper)<.08f,"Released arm returns to sampled pose");
                motion=Motion.Fire;ApplySupportGrip(dt);motion=Motion.Death;RestoreSupportPose();ApplySupportGrip(dt);
                Need(supportWeight==0&&Quaternion.Angle(supportLower.localRotation,sampledSupportLower)<.08f,"Death never constrained to gun");
            }
            Console.WriteLine($"SUPPORT GRIP PASS {hz} Hz: real ApplySupportGrip, four targets, wrist following, fixed lengths, restored pose, item release and immediate death release. Scene/rotation stand-ins, unit scale.");
        }

        static Vector3 ReadVector(JsonElement value)=>new(value[0].GetSingle(),value[1].GetSingle(),value[2].GetSingle());
        static Quaternion ReadRotation(JsonElement value)=>new(){V=new(value[0].GetSingle(),value[1].GetSingle(),value[2].GetSingle(),value[3].GetSingle())};
        void LoadPose(JsonElement pose,Dictionary<string,Transform> bones,float unitScale)
        {
            foreach(var bone in pose.GetProperty("bones").EnumerateArray())
            {
                var node=bones[bone.GetProperty("name").GetString()];
                node.localPosition=ReadVector(bone.GetProperty("position"))/unitScale;
                node.localRotation=ReadRotation(bone.GetProperty("rotation"));
                node.localScale=ReadVector(bone.GetProperty("scale"));
            }
        }
        public static void CheckFbx(string path)
        {
            using var document=JsonDocument.Parse(File.ReadAllText(path));
            foreach(var model in document.RootElement.GetProperty("models").EnumerateArray())
            {
                int samples=0;float worst=0f;
                var poses=model.GetProperty("poses").EnumerateArray().ToArray();
                foreach(var weapon in Enum.GetValues<WeaponType>())
                foreach(float actorScale in new[]{.1f,1f,3f})
                foreach(float unitScale in new[]{.01f,1f,100f})
                {
                    var rig=new AimRig();
                    rig.transform.localPosition=new(8,2,-4);
                    rig.transform.localRotation=Quaternion.AngleAxis(37,Vector3.up);
                    rig.transform.localScale=Vector3.one*actorScale;
                    var instance=new Transform{parent=rig.transform,localScale=Vector3.one*unitScale};
                    var bones=poses[0].GetProperty("bones").EnumerateArray().ToDictionary(
                        bone=>bone.GetProperty("name").GetString(),bone=>new Transform{name=bone.GetProperty("name").GetString()});
                    foreach(var bone in poses[0].GetProperty("bones").EnumerateArray())
                    {
                        var parent=bone.GetProperty("parent");
                        bones[bone.GetProperty("name").GetString()].parent=parent.ValueKind==JsonValueKind.Null?instance:bones[parent.GetString()];
                    }
                    rig.LoadPose(poses[0],bones,unitScale);
                    var grip=bones["WeaponGrip"];var barrel=bones["WeaponForward"];var hand=bones["HandR"];
                    rig.AttachWeapon(grip,barrel,hand,weapon);
                    Need((rig.weaponSocket.position-grip.position).magnitude/actorScale<.0001f,"First authored pose calibrates wrist mount at marker");
                    Need(Vector3.Dot(rig.weaponSocket.forward,(barrel.position-grip.position).normalized)>.99999f,"First-pose barrel direction preserved");
                    Need((rig.weaponSocket.lossyScale-Vector3.one*actorScale).magnitude/actorScale<.0001f,"Imported FBX units cancelled by socket scale");
                    var mount=rig.socketPosition;
                    foreach(var pose in poses)
                    {
                        rig.RestoreSupportPose();rig.LoadPose(pose,bones,unitScale);
                        rig.sampledSupportUpper=rig.supportUpper.localRotation;
                        rig.sampledSupportLower=rig.supportLower.localRotation;
                        rig.sampledSupportHand=rig.supportHand.localRotation;rig.sampledSupport=true;
                        float upper=(rig.supportLower.position-rig.supportUpper.position).magnitude;
                        float lower=(rig.supportHand.position-rig.supportLower.position).magnitude;
                        rig.aimTime=0;rig.ApplyAim();
                        Need((rig.weaponSocket.localPosition-mount).magnitude<.0001f,"Authored wrist mount does not drift between samples");
                        Need(Quaternion.Angle(rig.weaponSocket.localRotation,rig.socketRest)<.08f,"Socket follows sampled hand orientation");
                        rig.motion=Motion.Fire;rig.supportWeight=1;
                        var target=rig.weaponSocket.TransformPoint(rig.supportPoint);
                        var shoulder=rig.supportUpper.position;
                        bool reachable=(target-shoulder).magnitude<upper+lower-Math.Min(upper,lower)*.0201f&&
                            (target-shoulder).magnitude>Math.Abs(upper-lower)+Math.Min(upper,lower)*.0201f;
                        rig.ApplySupportGrip(1f/60);
                        float error=(rig.supportHand.position-target).magnitude/actorScale;
                        Need(Math.Abs((rig.supportLower.position-shoulder).magnitude-upper)/actorScale<.0001f&&
                            Math.Abs((rig.supportHand.position-rig.supportLower.position).magnitude-lower)/actorScale<.0001f,"FBX arm segment lengths preserved under imported/actor scales");
                        if(reachable){Need(error<.0003f,$"Reachable FBX support target reached: {model.GetProperty("name").GetString()} {weapon} frame {pose.GetProperty("frame").GetInt32()}, actor {actorScale}, units {unitScale}, error {error}, upper/lower {upper}/{lower}");worst=Math.Max(worst,error);}
                        rig.motion=Motion.Death;rig.RestoreSupportPose();rig.ApplySupportGrip(1f/60);
                        Need(rig.supportWeight==0&&Quaternion.Angle(rig.supportUpper.localRotation,rig.sampledSupportUpper)<.08f,"FBX sampled arm restored and released on death");
                        int frame=pose.GetProperty("frame").GetInt32();
                        Motion action=frame>=199&&frame<=216?Motion.Throw:frame>=171&&frame<=194?Motion.Medkit:
                            frame>=85&&frame<=114?Motion.Reload:Motion.Fire;
                        if(action!=Motion.Fire)
                        {
                            rig.motion=action;rig.supportWeight=1f;
                            for(int tick=0;tick<8;tick++){rig.RestoreSupportPose();rig.ApplySupportGrip(1f/60);}
                            Need(rig.supportWeight==0&&Quaternion.Angle(rig.supportHand.localRotation,rig.sampledSupportHand)<.08f,"FBX item/reload hand returns to authored sample after grip release");
                        }
                        samples++;
                    }
                }
                Console.WriteLine($"FBX GRIP PASS {model.GetProperty("name").GetString()}: {samples} pose/weapon/actor/import-unit cases; first-pose marker and barrel calibration, wrist following, fixed segments, reachable target error <= {worst*1000:F3} mm, item/reload/death restore. SHA256 {model.GetProperty("sha256").GetString()}. Blender FBX poses with scene/rotation stand-ins; no native Unity import/Animator/render validation.");
            }
        }
    }
}
static class UpperRigProbe
{
    static void Main(string[] args){foreach(int hz in new[]{20,30,60,120}){new ZombieSurvivalRun.AimRig().Check(hz);new ZombieSurvivalRun.AimRig().CheckSocket(hz);new ZombieSurvivalRun.AimRig().CheckSupport(hz);}if(args.Length>0)ZombieSurvivalRun.AimRig.CheckFbx(args[0]);}
}
