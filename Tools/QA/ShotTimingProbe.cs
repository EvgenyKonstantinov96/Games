extern alias engine;
#pragma warning disable 0649, 0414
global using Vector2=engine::UnityEngine.Vector2;
global using Vector3=engine::UnityEngine.Vector3;
global using Mathf=engine::UnityEngine.Mathf;
using System;
using UnityEngine;

// Scene/graph stand-ins observe the production control flow. These do not
// validate native Animator/Transform scheduling or actual rendered frames.
namespace UnityEngine
{
    static class Time { public static float deltaTime; }
    struct Quaternion
    {
        public static Quaternion AngleAxis(float value,Vector3 axis)=>default;
        public static Quaternion operator *(Quaternion a,Quaternion b)=>default;
    }
    sealed class Node { public bool active;public void SetActive(bool value)=>active=value; }
    sealed class Transform
    {
        public readonly Node gameObject=new();
        public Quaternion rotation,localRotation;
        public Vector3 position;
        public Vector3 up=>Vector3.up;
        public Vector3 forward=>Vector3.forward;
    }
}
namespace ZombieSurvivalRun
{
    enum ZSRState { Playing,Paused }
    sealed class ZSRGame
    {
        public static ZSRGame Instance;
        public ZSRState State;
        public bool IsRunning=>State==ZSRState.Playing;
        public void Footstep(Vector3 point) {}
    }
    partial class ShotActor
    {
        readonly Transform transform=new(),weaponSocket=new(),muzzleFlash=new(),footLeft=new(),footRight=new();
        Transform barrelRotor,spine,head;
        sealed class Graph { public bool valid=true;public bool IsValid()=>valid; }
        sealed class Pose { public double GetTime()=>0;public void SetTime(double time) {} }
        readonly Graph graph=new();
        readonly Pose currentPose=new(),previousPose=new();
        readonly ZSRUpperBodyAim upperAim=new();
        bool hero=true,moving=true,walking,firing,hasWalkClips=true;
        Motion motion;
        // Declared for both baseline and fixed source extraction.
        bool shotPending;
        float flashTime,recoil,aimTime,kick,barrelSpeed,barrelAngle;
        float gaitPhase,rate=1,desiredRate=1,walkBlend,legWeight,age,actionSpeed=1,blend,blendDuration=.09f;
        float steeringRoll,steeringHeading,supportWeight,breathPhase;
        Vector3 aimPoint;
        float observedRecoil;
        int samples;
        void Sample()=>samples++;
        void ApplyUpperBodyAim(float dt)=>observedRecoil=recoil;
        void ApplyAim() {}
        void ApplySupportGrip(float dt) {}
        void Frame(int hz){Time.deltaTime=1f/hz;observedRecoil=float.NaN;LateUpdate();}
        static void Need(bool ok,string message){if(!ok)throw new Exception(message);}
        static ShotActor New(WeaponType weapon)
        {
            ZSRGame.Instance=new();var actor=new ShotActor();actor.ConfigureWeapon(weapon);return actor;
        }
        void NeedFirstShot(int hz,WeaponType weapon,string condition)
        {
            Frame(hz);
            Need(firing&&muzzleFlash.gameObject.active,$"{weapon}/{hz} Hz {condition}: first flash survives until a render opportunity");
            Need(Math.Abs(observedRecoil-kick)<.000001f,$"{weapon}/{hz} Hz {condition}: pose sees full shot recoil before decay ({observedRecoil} vs {kick})");
        }
        public static void Check(int hz,WeaponType weapon)
        {
            var actor=New(weapon);actor.Fire(Vector3.forward*10);actor.NeedFirstShot(hz,weapon,"normal");
            for(int frame=0;frame<hz;frame++)actor.Frame(hz);
            Need(!actor.firing&&!actor.muzzleFlash.gameObject.active&&actor.recoil==0,$"{weapon}/{hz}: flash expiry and recoil recovery");

            actor=New(weapon);actor.Fire(Vector3.forward*10);
            ZSRGame.Instance.State=ZSRState.Paused;
            for(int frame=0;frame<hz;frame++)actor.Frame(hz);
            Need(actor.samples==0&&actor.recoil==actor.kick&&actor.firing,$"{weapon}/{hz}: pause preserves pending shot");
            ZSRGame.Instance.State=ZSRState.Playing;actor.NeedFirstShot(hz,weapon,"resume");

            actor=New(weapon);actor.graph.valid=false;actor.Fire(Vector3.forward*10);
            for(int frame=0;frame<hz;frame++)actor.Frame(hz);
            Need(actor.samples==0&&actor.recoil==actor.kick&&actor.firing,$"{weapon}/{hz}: invalid graph preserves pending shot");
            actor.graph.valid=true;actor.NeedFirstShot(hz,weapon,"graph ready");

            actor=New(weapon);
            for(int shot=0;shot<12;shot++)
            {
                actor.Fire(Vector3.forward*10);actor.NeedFirstShot(hz,weapon,"repeated shot");
                actor.Frame(hz);
            }
            actor.Fire(Vector3.forward*10);actor.Die();actor.Frame(hz);
            Need(!actor.firing&&!actor.shotPending&&!actor.muzzleFlash.gameObject.active,$"{weapon}/{hz}: death clears pending flash");
            actor=New(weapon);actor.Fire(Vector3.forward*10);actor.ResetMotion(.3f);
            Need(!actor.firing&&!actor.shotPending&&actor.recoil==0,$"{weapon}/{hz}: reset clears pending shot and recoil");
            Console.WriteLine($"SHOT TIMING PASS {weapon} {hz} Hz: first flash, full pre-decay recoil, expiry, recovery, pause, invalid graph, repeated shots, death and reset. Actual Fire/LateUpdate/control methods; pose/scene/graph observers, no native render.");
        }
    }
}
static class ShotTimingProbe
{
    static int Main()
    {
        int failures=0;
        foreach(int hz in new[]{20,30,60,120})foreach(var weapon in Enum.GetValues<ZombieSurvivalRun.WeaponType>())
        {
            try{ZombieSurvivalRun.ShotActor.Check(hz,weapon);}
            catch(Exception error){Console.WriteLine("SHOT TIMING FAIL: "+error.Message);failures++;}
        }
        return failures==0?0:1;
    }
}
