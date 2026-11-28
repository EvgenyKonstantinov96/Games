extern alias engine;
global using Vector2 = engine::UnityEngine.Vector2;
global using Vector3 = engine::UnityEngine.Vector3;
global using Mathf = engine::UnityEngine.Mathf;
using System;

namespace UnityEngine
{
    public class Transform
    {
        public Vector3 position;
        public Quaternion localRotation;
    }
    public struct Quaternion
    {
        public Vector3 Angles;
        public static Quaternion Euler(float x,float y,float z) => new() { Angles = new Vector3(x,y,z) };
        public static Quaternion operator *(Quaternion a,Quaternion b) => new() { Angles = a.Angles+b.Angles };
    }
    public static class Time { public static float deltaTime; }
}

namespace ZombieSurvivalRun
{
    using UnityEngine;
    class ZSRGame
    {
        public static readonly ZSRGame Instance = new();
        public bool IsRunning = true;
    }
    partial class ZSRSecondaryMotion
    {
        readonly Transform neck = new(), tie = new(), tail = new();
        Quaternion tieRest = new(), tailRest = new();
        Vector3 previous, previousVelocity;
        Vector2 angle, angularVelocity;
        readonly Transform pack = new();
        Quaternion packRest = new();
        Vector2 packAngle, packVelocity;

        public void Check(int hz)
        {
            Time.deltaTime=1f/hz;
            for(int frame=0;frame<hz*20;frame++)
            {
                float t=frame/(float)hz;
                neck.position=new Vector3(MathF.Sin(t*6)*1.2f,1.7f+MathF.Sin(t*13)*.035f,0);
                LateUpdate();
                if(!float.IsFinite(angle.x)||!float.IsFinite(angle.y)||Math.Abs(angle.x)>7.01f||Math.Abs(angle.y)>9.01f)
                    throw new Exception("Ponytail spring outside its anatomical limits");
                if(!float.IsFinite(packAngle.x)||!float.IsFinite(packAngle.y)||Math.Abs(packAngle.x)>2.201f||Math.Abs(packAngle.y)>2.501f)
                    throw new Exception("Strapped pack exceeded angular limits");
            }
            var saved=(angle,angularVelocity,tail.localRotation.Angles);
            var packSaved=(packAngle,packVelocity,pack.localRotation.Angles);
            ZSRGame.Instance.IsRunning=false;
            for(int frame=0;frame<hz*2;frame++)LateUpdate();
            if(saved!=(angle,angularVelocity,tail.localRotation.Angles))throw new Exception("Pause moved the ponytail");
            if(packSaved!=(packAngle,packVelocity,pack.localRotation.Angles))throw new Exception("Pause moved the pack");
            ZSRGame.Instance.IsRunning=true;
            for(int frame=0;frame<hz*3;frame++)LateUpdate();
            if(angle.magnitude>.001f)throw new Exception("Spring failed to settle");
            if(packAngle.magnitude>.001f)throw new Exception("Pack failed to settle");
        }
    }
}

class SecondaryMotionProbe
{
    static void Main()
    {
        foreach(int hz in new[]{20,30,60,120})new ZombieSurvivalRun.ZSRSecondaryMotion().Check(hz);
        Console.WriteLine("SECONDARY PASS: bounded ponytail and pack springs at 20/30/60/120 Hz, frozen pause and neutral recovery after stopping. Scene stand-ins.");
    }
}
