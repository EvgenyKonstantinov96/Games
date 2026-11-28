using UnityEngine;
using System;
using ZombieSurvivalRun;

static class UpperAimProbe
{
    static void Need(bool ok,string text){if(!ok)throw new Exception(text);}
    static void Main()
    {
        foreach(int hz in new[]{20,30,60,120})
        {
            var aim=new ZSRUpperBodyAim();float dt=1f/hz;
            for(int i=0;i<hz*20;i++)
            {
                var before=aim.Angles;
                float t=i/(float)hz;
                var value=aim.Tick(new Vector3(MathF.Sin(t*9)*40,MathF.Cos(t*5)*20,10),true,dt);
                Need(float.IsFinite(value.x)&&float.IsFinite(value.y),"Finite shoulder pose");
                Need(Math.Abs(value.x)<=28.001f&&value.y>=-12.001f&&value.y<=8.001f,"Anatomical aiming limits");
                Need((value-before).magnitude<=150*dt+.001f,"Bounded target switch velocity");
            }
            for(int i=0;i<hz*2;i++)aim.Tick(new Vector3(10,2,20),true,dt);
            var expected=new Vector2(MathF.Atan2(10,20)*180/MathF.PI,-MathF.Atan2(2,MathF.Sqrt(500))*180/MathF.PI);
            Need((aim.Angles-expected).magnitude<.01f,"Converged target at every frame rate");
            var held=aim.Angles;
            for(int i=0;i<500;i++)aim.Tick(Vector3.zero,false,0);
            Need(held==aim.Angles,"Paused shoulder pose");
            for(int i=0;i<hz*2;i++)aim.Tick(new Vector3(20,0,-10),true,dt);
            Need(aim.Angles.magnitude<.01f,"Do not twist toward passed enemies");
            aim.Tick(new Vector3(-50,20,2),true,3f);
            Need(aim.Angles.magnitude<=7.501f,"Cap stalled frame response");
            for(int i=0;i<hz*2;i++)aim.Tick(Vector3.forward,false,dt);
            Need(aim.Angles.magnitude<.01f,"Item/reload/stopped aiming recovery");
            aim.Reset();Need(aim.Angles==Vector2.zero,"Restart reset");
            Console.WriteLine($"UPPER AIM PASS {hz} Hz: 20 s rapid targets, limits, turn speed, convergence, pause, passed enemy, stalled frame, neutral and restart.");
        }
        foreach(float kick in new[]{0f,.025f,.043f,.057f,.105f})
            Need(ZSRUpperBodyAim.RecoilPitch(kick)>=-6f&&ZSRUpperBodyAim.RecoilPitch(kick)<=0f,"Four weapon shoulder recoil limits");
    }
}
