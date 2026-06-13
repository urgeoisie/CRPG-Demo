using System.Collections.Generic;
using UnityEngine;

namespace MirrorCase2D
{
    public sealed class SceneAudioController : MonoBehaviour
    {
        private const float MasterVolume = 0.62f;

        private static SceneAudioController instance;

        private AudioSource ambientSource;
        private AudioSource sfxSource;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private string currentAmbient;
        private float nextStepTime;

        public static SceneAudioController Instance => instance;

        public static SceneAudioController Ensure()
        {
            if (instance != null) return instance;

            SceneAudioController existing = FindFirstObjectByType<SceneAudioController>();
            if (existing != null)
            {
                instance = existing;
                instance.Initialize();
                return instance;
            }

            GameObject audioObject = new GameObject("Scene Audio Controller");
            instance = audioObject.AddComponent<SceneAudioController>();
            instance.Initialize();
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            Initialize();
        }

        private void Initialize()
        {
            if (ambientSource != null && sfxSource != null) return;

            ambientSource = gameObject.GetComponent<AudioSource>();
            if (ambientSource == null) ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.loop = true;
            ambientSource.playOnAwake = false;
            ambientSource.volume = 0.28f * MasterVolume;
            ambientSource.spatialBlend = 0f;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
            sfxSource.volume = 0.72f * MasterVolume;
            sfxSource.spatialBlend = 0f;
        }

        public void SetRoom(string roomId)
        {
            Initialize();
            string nextAmbient = AmbientForRoom(roomId);
            if (currentAmbient == nextAmbient && ambientSource.isPlaying) return;

            currentAmbient = nextAmbient;
            ambientSource.clip = Load(nextAmbient);
            ambientSource.volume = AmbientVolume(roomId) * MasterVolume;
            if (ambientSource.clip != null) ambientSource.Play();
        }

        public void PlayButton() => Play("ui_click", 0.55f);
        public void PlayInteract() => Play("interact_soft", 0.7f);
        public void PlayPage() => Play("dialogue_page", 0.62f);
        public void PlayChoice() => Play("choice_select", 0.72f);
        public void PlayEvidence() => Play("evidence_note", 0.82f);
        public void PlayClose() => Play("dialogue_close", 0.48f);
        public void PlayTransition() => Play("room_transition", 0.72f);

        public void PlayFootstep(string roomId, Vector2 input)
        {
            if (input.sqrMagnitude < 0.05f || Time.unscaledTime < nextStepTime) return;
            nextStepTime = Time.unscaledTime + 0.36f;
            string clipName = roomId == "corridor" || roomId == "hospital" ? "footstep_tile" : "footstep_soft";
            Play(clipName, 0.42f);
        }

        private void Play(string clipName, float volumeScale)
        {
            Initialize();
            AudioClip clip = Load(clipName);
            if (clip == null) return;
            sfxSource.pitch = Random.Range(0.96f, 1.04f);
            sfxSource.PlayOneShot(clip, volumeScale);
        }

        private AudioClip Load(string clipName)
        {
            if (clips.TryGetValue(clipName, out AudioClip cached)) return cached;
            AudioClip clip = Resources.Load<AudioClip>("Audio/" + clipName);
            clips[clipName] = clip;
            return clip;
        }

        private static string AmbientForRoom(string roomId)
        {
            switch (roomId)
            {
                case "hospital": return "ambient_hospital";
                case "police":
                case "briefing": return "ambient_office";
                case "bedroom": return "ambient_rain";
                case "corridor": return "ambient_corridor";
                case "mirror": return "ambient_dream";
                case "crime":
                case "missing":
                case "relation": return "ambient_room";
                default: return "ambient_void";
            }
        }

        private static float AmbientVolume(string roomId)
        {
            switch (roomId)
            {
                case "corridor": return 0.36f;
                case "bedroom": return 0.34f;
                case "mirror": return 0.32f;
                default: return 0.24f;
            }
        }
    }
}
