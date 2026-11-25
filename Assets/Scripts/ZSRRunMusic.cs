using UnityEngine;

namespace ZombieSurvivalRun
{
    sealed class ZSRRunMusic : MonoBehaviour
    {
        const string VolumeKey = "zsr_music_volume_v1";
        AudioSource menu, run;
        int choice;

        public string Label => choice == 0 ? "МУЗЫКА ВЫКЛ." : choice == 1 ? "МУЗЫКА ТИХО" : "МУЗЫКА ВКЛ.";

        void Awake()
        {
            choice = Mathf.Clamp(PlayerPrefs.GetInt(VolumeKey, 2), 0, 2);
            menu = CreateChannel("Menu");
            run = CreateChannel("Run");
        }

        AudioSource CreateChannel(string cue)
        {
            var clip = Resources.Load<AudioClip>("Audio/" + cue);
            if (clip == null) return null;
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        public void Cycle()
        {
            choice = (choice + 1) % 3;
            PlayerPrefs.SetInt(VolumeKey, choice);
            PlayerPrefs.Save();
        }

        public void Mix(ZSRState state, float dt)
        {
            float gain = choice == 2 ? .54f : choice == 1 ? .23f : 0f;
            bool playing = state == ZSRState.Playing;
            bool paused = state == ZSRState.Paused;
            Fade(menu, playing || paused ? 0f : gain * .8f, dt);
            Fade(run, playing ? gain : paused ? gain * .24f : 0f, dt);
        }

        static void Fade(AudioSource source, float to, float dt)
        {
            if (source == null) return;
            if (to > 0f && !source.isPlaying) source.Play();
            source.volume = Mathf.MoveTowards(source.volume, to, dt * .85f);
            if (to == 0f && source.volume == 0f && source.isPlaying) source.Stop();
        }
    }
}
