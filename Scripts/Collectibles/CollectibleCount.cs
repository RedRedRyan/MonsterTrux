using TMPro;
using UnityEngine;

namespace RaveLands.Collectibles
{


    public class CollectibleCount : MonoBehaviour
    {
        TMP_Text countText;
        public int count;

        void Awake()
        {
            countText = GetComponent<TMP_Text>();
        }

        void OnEnable() => Collectible.OnCollected += OnCollectibleCollected;
        void OnDisable() => Collectible.OnCollected -= OnCollectibleCollected;

        void OnCollectibleCollected(int value)
        {
            count += value;  // Add specific coin value
            countText.text = count.ToString();
        }
    }
}