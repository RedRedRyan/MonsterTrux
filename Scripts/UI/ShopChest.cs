using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;

public class ShopChest : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    [SerializeField] private TMP_Text kasiTokensText;
    [SerializeField] private TMP_Text diamondsCostText;
    [SerializeField] private Button buyButton;
    [SerializeField] private Image topImage;

    [Header("Animation Settings")]
    [SerializeField] private float hoverAnimationHeight = 15f;
    [SerializeField] private float animationDuration = 0.2f;

    private Vector3 originalTopImagePosition;
    [SerializeField] private int diamondsCost;
    [SerializeField] private int kasiReward;

    void Start()
    {
        // Store original position for animation
        originalTopImagePosition = topImage.transform.localPosition;
        
        // Add click listener to buy button
        buyButton.onClick.AddListener(OnBuyButtonClicked);
    }

    public void InitializeChest(int kasiAmount, int diamondsAmount, Sprite chestSprite = null)
    {
        kasiReward = kasiAmount;
        diamondsCost = diamondsAmount;

        // Update UI
        kasiTokensText.text = kasiAmount.ToString();
        diamondsCostText.text = diamondsAmount.ToString();

        // Set chest image if provided
        if (chestSprite != null)
        {
            topImage.sprite = chestSprite;
        }
    }

    private void OnBuyButtonClicked()
    {
        // Start purchase transaction
        StartCoroutine(ProcessPurchase());
    }

    private IEnumerator ProcessPurchase()
    {
        // Disable button during transaction
        buyButton.interactable = false;

        // TODO: Implement your purchase logic here
        // This is where you'd integrate with your IAP system or currency manager
        
        Debug.Log($"Purchasing {kasiReward} Kasi for {diamondsCost} diamonds");
        
        // Example transaction process
        yield return StartCoroutine(YourPurchaseImplementation());

        // Re-enable button after transaction
        buyButton.interactable = true;
    }

    private IEnumerator YourPurchaseImplementation()
    {
        // Replace this with your actual purchase logic
        // Example: Check if player has enough diamonds, then reward Kasi tokens
        
        // Simulate processing time
        yield return new WaitForSeconds(1f);
        
        // TODO: Add your successful purchase logic here
    }

    // Hover animation methods
    public void OnPointerEnter(PointerEventData eventData)
    {
        AnimateTopImage(originalTopImagePosition + Vector3.up * hoverAnimationHeight);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        AnimateTopImage(originalTopImagePosition);
    }

    private void AnimateTopImage(Vector3 targetPosition)
    {
        StartCoroutine(AnimatePosition(topImage.transform, targetPosition, animationDuration));
    }

    private IEnumerator AnimatePosition(Transform transform, Vector3 targetPosition, float duration)
    {
        Vector3 startPosition = transform.localPosition;
        float time = 0;

        while (time < duration)
        {
            transform.localPosition = Vector3.Lerp(startPosition, targetPosition, time / duration);
            time += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = targetPosition;
    }

    // Public methods for external access
    public int GetDiamondsCost() => diamondsCost;
    public int GetKasiReward() => kasiReward;

    void OnDestroy()
    {
        // Clean up listeners
        if (buyButton != null)
            buyButton.onClick.RemoveListener(OnBuyButtonClicked);
    }
}