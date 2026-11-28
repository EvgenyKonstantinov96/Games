#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZombieSurvivalRun.EditorTools
{
    public static class ZSRGameCapture
    {
        [MenuItem("Zombie Survival Run 0.8/5 • Save Game Screenshot")]
        public static void Capture()
        {
            if (!EditorApplication.isPlaying || ZSRGame.Instance == null)
            {
                Debug.LogWarning("[ZSR] Enter Play Mode, then choose Save Game Screenshot.");
                return;
            }
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots"));
            Directory.CreateDirectory(folder);
            var game = ZSRGame.Instance;
            string name = "ZSR-level-"+game.Level+"-"+game.State+"-"+
                DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".png";
            string path = Path.Combine(folder,name);
            ScreenCapture.CaptureScreenshot(path,2);
            Debug.Log("[ZSR] Saving Game View to "+path);
        }

        [MenuItem("Zombie Survival Run 0.8/6 • Open Screenshot Folder")]
        public static void OpenFolder()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots"));
            Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder);
        }
    }
}
#endif
