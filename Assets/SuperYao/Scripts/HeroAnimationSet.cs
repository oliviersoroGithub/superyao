using UnityEngine;

namespace SuperYao
{
    /// <summary>
    /// Les poses d'un héros (Yao, Lou, Moussa), découpées des planches de la version téléphone : une image par pose,
    /// posée en bas au centre. Créé et rempli par le constructeur du stage à partir des dossiers
    /// Heroes/&lt;Héros&gt;/Idle, Run, Jump, Fall, Crouch.
    /// </summary>
    [CreateAssetMenu(menuName = "Super Yao/Animations de héros")]
    public class HeroAnimationSet : ScriptableObject
    {
        public string heroName;
        [Tooltip("Hauteur du héros debout sur les planches, cheveux compris, en pixels (Yao 125, Lou 152, Moussa 144).")]
        public float sourceHeight = 125f;

        public Sprite[] idle, run, jump, fall, crouch;
        [Header("Vol à l'hélice (facultatif)")]
        [Tooltip("En l'air avec l'hélice, presque immobile ou en montant : vol sur place.")]
        public Sprite[] hover;
        [Tooltip("En l'air avec l'hélice, en avançant : vol vers l'avant.")]
        public Sprite[] glide;

        [Header("Marche et course dessinées à part (facultatif)")]
        [Tooltip("Cycle de marche (2 pas). Vide : la course sert aussi pour marcher, comme sur le téléphone.")]
        public Sprite[] walk;
        [Tooltip("Hauteur du héros debout sur les images de marche, en pixels (0 = même que sourceHeight).")]
        public float walkSourceHeight;
        [Tooltip("Hauteur du héros debout sur la planche de course, en pixels (0 = même que sourceHeight).")]
        public float runSourceHeight;
        [Tooltip("Distance parcourue pendant un cycle complet de marche (2 pas), en cases. 0 = règle du téléphone.")]
        public float walkCycle;
        [Tooltip("Distance parcourue pendant un cycle complet de course (2 foulées), en cases. 0 = règle du téléphone.")]
        public float runCycle;

        [Header("Poses du saut (numéros d'image dans « jump »)")]
        public int jumpTakeoff = 2;
        public int jumpRise = 3;
        public int jumpApex = 4;
        public int jumpFall = 5;
        [Tooltip("Pose fixe quand le héros est accroupi (numéro d'image dans « crouch »).")]
        public int crouchFrame;
        [Tooltip("Pose de glissade, quand le héros s'accroupit en avançant vite (numéro dans « crouch », -1 = aucune).")]
        public int crouchSlideFrame = -1;
        [Tooltip("Cadence de l'animation de repos, en images/s (téléphone : 6,7).")]
        public float idleFps = 60f / 9f;
        [Tooltip("Pose de réception, affichée un court instant à l'atterrissage (facultatif).")]
        public Sprite[] land;

        [Header("Poses prêtes pour la suite (pas encore utilisées)")]
        public Sprite[] attack;
        public Sprite[] hurt;
        public Sprite[] death;

        public Sprite FirstSprite => idle != null && idle.Length > 0 ? idle[0] : null;
    }
}
