using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Bruitages du jeu : les 16 sons de la version téléphone (assets/sounds), joués par leur nom
    /// (« jump », « coin », « stomp »…). Rien ne sonne quand le bouton « Bruits » est sur « Muet ».
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SoundFx : MonoBehaviour
    {
        [System.Serializable]
        public struct Entry
        {
            public string name;
            public AudioClip clip;
        }

        public Entry[] sounds;
        [Range(0f, 1f)] public float volume = 0.8f;

        static SoundFx instance;
        AudioSource source;

        void Awake()
        {
            instance = this;
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
        }

        void OnEnable()
        {
            // Les gains du stage (pièce, banane, écrasement…) sonnent tout seuls.
            if (StageState.Instance != null) StageState.Instance.Gained += OnGained;
        }

        void Start()
        {
            if (StageState.Instance != null)
            {
                StageState.Instance.Gained -= OnGained;
                StageState.Instance.Gained += OnGained;
            }
        }

        void OnDisable()
        {
            if (StageState.Instance != null) StageState.Instance.Gained -= OnGained;
        }

        void OnDestroy() { if (instance == this) instance = null; }

        static void OnGained(string what)
        {
            switch (what)
            {
                case "banana": Play("fruit"); break;
                case "diamond": Play("life"); break;
                default: Play(what); break; // coin, power, break, stomp
            }
        }

        public static void Play(string name)
        {
            if (instance == null || (StageState.Instance != null && StageState.Instance.Muted)) return;
            foreach (var s in instance.sounds)
                if (s.name == name && s.clip != null)
                {
                    instance.source.PlayOneShot(s.clip, instance.volume);
                    return;
                }
        }
    }
}
