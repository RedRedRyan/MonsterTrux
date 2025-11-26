using UnityEngine;
using System;

namespace RaveLands.Collectibles
{
    /// <summary>
    /// Collectibles class handles the collection of different types of coins.
    /// It plays a sound effect and invokes an event when a coin is collected.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]


    public class Collectible : MonoBehaviour
    {
        public enum CoinType { Green, Blue, Red }
        public CoinType coinType = CoinType.Green;

        public static event Action<int> OnCollected;

        [SerializeField] private AudioClip greenSound;
        [SerializeField] private AudioClip blueSound;
        [SerializeField] private AudioClip redSound;

        private AudioSource audioSource;

        void Start()
        {
            audioSource = GetComponent<AudioSource>();
        }

        void Update()
        {
            transform.localRotation = Quaternion.Euler(0f, Time.time * 100f, 0f);
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                int value = coinType switch
                {
                    CoinType.Green => 1,
                    CoinType.Blue => 2,
                    CoinType.Red => 3,
                    _ => 1
                };

                OnCollected?.Invoke(value);

                AudioClip clipToPlay = coinType switch
                {
                    CoinType.Green => greenSound,
                    CoinType.Blue => blueSound,
                    CoinType.Red => redSound,
                    _ => null
                };

                if (clipToPlay != null && audioSource != null)
                {
                    audioSource.PlayOneShot(clipToPlay);
                    Destroy(gameObject, clipToPlay.length);
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}