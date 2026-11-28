extern alias engine;
global using Vector2=engine::UnityEngine.Vector2;
global using Vector3=engine::UnityEngine.Vector3;
global using Mathf=engine::UnityEngine.Mathf;
using System;
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
    }
    public sealed class Transform
    {
        public Transform parent;
        public Quaternion localRotation=Quaternion.identity;
        public Vector3 localPosition;
        public Vector3 position=>parent==null?localPosition:parent.position+parent.rotation*localPosition;
        public Quaternion rotation
        {
            get=>parent==null?localRotation:parent.rotation*localRotation;
            set=>localRotation=parent==null?value:Quaternion.Inverse(parent.rotation)*value;
        }
        public Vector3 up=>rotation*Vector3.up;
        public Vector3 forward=>rotation*Vector3.forward;
    }
}
namespace ZombieSurvivalRun
{
    partial class AimRig
    {
        readonly Transform transform=new(),spine=new(),head=new(),weaponSocket=new();
        readonly ZSRUpperBodyAim upperAim=new();
        bool hero=true,sampledUpperBody=true,moving=true,walking;
        Motion motion=Motion.Fire;
        float aimTime=.55f,recoil=.057f,breathPhase;
        float steeringRoll,steeringHeading;
        Vector3 aimPoint;
        Vector3 socketPosition;
        Quaternion socketRest=Quaternion.identity,sampledSpine=Quaternion.identity,sampledHead=Quaternion.identity;
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
    }
}
static class UpperRigProbe
{
    static void Main(){foreach(int hz in new[]{20,30,60,120}){new ZombieSurvivalRun.AimRig().Check(hz);new ZombieSurvivalRun.AimRig().CheckSocket(hz);}}
}
