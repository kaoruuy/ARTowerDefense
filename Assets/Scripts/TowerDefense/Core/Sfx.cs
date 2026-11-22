using UnityEngine;

namespace ARTD
{
    /// <summary>効果音ヘルパー（2D で鳴らす。AR の大きな座標でも距離で聞こえなくならない）。</summary>
    public static class Sfx
    {
        static AudioSource source;

        /// <summary>効果音全体の音量（BGM が埋もれないよう控えめ）。</summary>
        public static float MasterVolume = 0.5f;
        /// <summary>同じ音をこの秒数以内に重ねて鳴らさない（敵が多いときの連打で音が飽和するのを防ぐ）。</summary>
        public static float SameClipInterval = 0.08f;
        /// <summary>効果音の優先度（0 が最優先、256 が最低）。BGM(0) より低くして、同時発音数を超えたら効果音から間引かれるようにする。</summary>
        public const int Priority = 200;

        static readonly System.Collections.Generic.Dictionary<AudioClip, float> lastPlayed =
            new System.Collections.Generic.Dictionary<AudioClip, float>();

        public static void Play(AudioClip clip, float volume = 1f)
        {
            if (!clip) return;
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(clip, out float t) && now - t < SameClipInterval) return;
            lastPlayed[clip] = now;
            if (!source)
            {
                var go = new GameObject("ARTD_Sfx");
                Object.DontDestroyOnLoad(go);
                source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.priority = Priority;
            }
            source.PlayOneShot(clip, volume * MasterVolume);
        }

        /// <summary>ループ音用の AudioSource を owner に作る（2D）。</summary>
        public static AudioSource CreateLoop(GameObject owner, AudioClip clip, float volume)
        {
            var s = owner.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = volume * MasterVolume;
            s.priority = Priority + 30;   // ループ音（足音）はさらに低い優先度
            return s;
        }
    }
}
