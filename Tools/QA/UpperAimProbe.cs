using UnityEngine;
using System;
using ZombieSurvivalRun;

static class UpperAimProbe
{
    static void Need(bool ok,string text){if(!ok)throw new Exception(text);}
    static Vector3 Rotate(Vector3 value)
    {
        // Rodrigues rotation avoids native Quaternion calls in this managed runner.
        var axis=new Vector3(.3f,.7f,-.4f).normalized;
        const float angle=1.173f;
        return value*MathF.Cos(angle)+Vector3.Cross(axis,value)*MathF.Sin(angle)+axis*Vector3.Dot(axis,value)*(1-MathF.Cos(angle));
    }
    static void CheckReach(Vector3 shoulder,Vector3 elbow,Vector3 wrist,Vector3 target,out Vector3 joint,out Vector3 end)
    {
        Need(ZSRArmReach.Solve(shoulder,elbow,wrist,target,out joint,out end),"Arm solves finite nonzero segments");
        float upper=(elbow-shoulder).magnitude,lower=(wrist-elbow).magnitude;
        float tolerance=MathF.Max(upper,lower)*.0005f;
        Need(float.IsFinite(joint.x)&&float.IsFinite(joint.y)&&float.IsFinite(joint.z)&&float.IsFinite(end.x)&&float.IsFinite(end.y)&&float.IsFinite(end.z),"Finite solved arm");
        Need(Math.Abs((joint-shoulder).magnitude-upper)<tolerance&&Math.Abs((end-joint).magnitude-lower)<tolerance,"Scaled arm segment lengths preserved");
        float clearance=MathF.Min(upper,lower)*.02f;
        float expected=Math.Clamp((target-shoulder).magnitude,Math.Abs(upper-lower)+clearance,upper+lower-clearance);
        Need(Math.Abs((end-shoulder).magnitude-expected)<tolerance,"Scale-relative near/far reach limits");
        if((target-shoulder).magnitude>Math.Abs(upper-lower)+clearance&&(target-shoulder).magnitude<upper+lower-clearance)
            Need((end-target).magnitude<tolerance,"Scaled reachable grip reached");
    }
    static void CheckScaledArms()
    {
        int cases=0;
        foreach(var segments in new[]{new Vector2(.28f,.26f),new Vector2(.002f,.002f),new Vector2(.002f,.0012f)})
        {
            var elbow=new Vector3(.6f,-.8f,0)*segments.x;
            var wrist=elbow+new Vector3(-.36f,.48f,.8f)*segments.y;
            float reach=segments.x+segments.y;
            foreach(var target in new[]{new Vector3(.04f,-.02f,.30f)*reach,new Vector3(-.4f,.8f,1.2f)*reach,new Vector3(0,0,.75f)*reach,new Vector3(.001f,0,.002f)*reach,Vector3.zero})
            {
                CheckReach(Vector3.zero,elbow,wrist,target,out var baseJoint,out var baseEnd);
                foreach(float scale in new[]{.01f,.1f,1f,10f})
                {
                    var translation=new Vector3(.7f,-1.3f,2.1f)*reach*scale;
                    CheckReach(translation,translation+Rotate(elbow)*scale,translation+Rotate(wrist)*scale,translation+Rotate(target)*scale,out var joint,out var end);
                    float tolerance=MathF.Max(segments.x,segments.y)*scale*.001f;
                    Need((joint-(translation+Rotate(baseJoint)*scale)).magnitude<tolerance,$"Elbow invariant: segments {segments}, target {target}, scale {scale}, error {(joint-(translation+Rotate(baseJoint)*scale)).magnitude}, tolerance {tolerance}");
                    Need((end-(translation+Rotate(baseEnd)*scale)).magnitude<tolerance,$"Grip invariant: segments {segments}, target {target}, scale {scale}");
                    cases++;
                }
            }
        }
        // A fully straight/folded sample has no unique bend plane, but still needs finite fixed lengths.
        CheckReach(Vector3.zero,new Vector3(0,.002f,0),new Vector3(0,.004f,0),new Vector3(0,1,0),out _,out _);
        CheckReach(Vector3.zero,new Vector3(0,.002f,0),Vector3.zero,Vector3.zero,out _,out _);
        Need(!ZSRArmReach.Solve(Vector3.zero,new Vector3(float.NaN,0,0),Vector3.one,Vector3.forward,out _,out _),"Reject nonfinite segments");
        Need(!ZSRArmReach.Solve(Vector3.zero,Vector3.right,new Vector3(1,.00000001f,0),Vector3.forward,out _,out _),"Reject numerically collapsed reach interval");
        Console.WriteLine($"ARM SCALE PASS: {cases} transformed solves at .01–10x, 2 mm equal/unequal segments, reachable/overreaching/near-shoulder targets, full fold, straight sample, finite lengths and invalid guards. Actual production vector math.");
    }
    static void Main()
    {
        for(int i=0;i<4000;i++)
        {
            float t=i*.017f;
            var shoulder=new Vector3(.2f,1.5f,0f);
            var elbow=shoulder+new Vector3(.10f,-.24f,-.03f);
            var wrist=elbow+new Vector3(-.15f,.10f,.19f);
            var target=shoulder+new Vector3(MathF.Sin(t)*.65f,MathF.Cos(t*.73f)*.50f,MathF.Sin(t*.37f)*.43f);
            Need(ZSRArmReach.Solve(shoulder,elbow,wrist,target,out var joint,out var end),"Arm solves nondegenerate target");
            float upper=(elbow-shoulder).magnitude,lower=(wrist-elbow).magnitude;
            Need(Math.Abs((joint-shoulder).magnitude-upper)<.0001f&&Math.Abs((end-joint).magnitude-lower)<.0001f,"Arm segment lengths preserved");
            var direction=(end-shoulder).normalized;
            Need(Vector3.Dot(Vector3.ProjectOnPlane(joint-shoulder,direction),Vector3.ProjectOnPlane(elbow-shoulder,direction))>-.0001f,"Elbow remains on sampled bend side");
            float clearance=MathF.Min(upper,lower)*.02f;
            if((target-shoulder).magnitude<upper+lower-clearance&&(target-shoulder).magnitude>Math.Abs(upper-lower)+clearance)
                Need((end-target).magnitude<.0001f,"Reachable grip target reached");
        }
        Need(!ZSRArmReach.Solve(Vector3.zero,Vector3.zero,Vector3.zero,Vector3.one,out _,out _),"Reject zero-length arm");
        Console.WriteLine("ARM REACH PASS: 4000 moving/reachable/unreachable targets, fixed segment lengths, sampled elbow side and degenerate arm guard. Actual production vector math.");
        CheckScaledArms();
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
