using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;
using Unity.VisualScripting;
using WalletConnectUnity.Core.Networking;
using UnityEngine.XR;

namespace Thirdweb.Unity
{
    public class UserDetails : MonoBehaviour
    {
        [Header("Connection UI")]
        [SerializeField] TMP_Text statusText;
        [SerializeField] public ulong chainId;

        [Header("Wallet Info Display")]
        [SerializeField] private TMP_Text[] walletAddressTexts; // Array for multiple address texts
        [SerializeField] private TMP_Text[] walletBalanceTexts; // Array for multiple balance texts
        [SerializeField] private TMP_Text[] kasiBalanceTexts;   // Array for multiple KASI balance texts
        [SerializeField] private TMP_Text[] diamondBalanceTexts; // Array for multiple Diamond balance texts
        [SerializeField] private Button refreshBalanceButton;

        [Header("APP Functions")]
        [SerializeField] private TMP_Text fullwalletAddressText;
        [SerializeField] private Button[] copyAddressButton;

        [Header("Scene Management")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private void Awake()
        {
            var inAppWallet = new InAppWallet();
            chainId = inAppWallet.chainId;
        }

        private void Start()
        {
            // Set up refresh balance button
            if (refreshBalanceButton != null)
            {
                refreshBalanceButton.onClick.RemoveAllListeners();
                refreshBalanceButton.onClick.AddListener(RefreshWalletBalance);
            }

            // Set up copy address button
            if (copyAddressButton != null && copyAddressButton.Length > 0)
            {
                foreach (var button in copyAddressButton)
                {
                    if (button != null)
                    {
                        button.onClick.RemoveAllListeners();
                        button.onClick.AddListener(CopyAddressToClipboard);
                    }
                }
            }
            
            HandleSuccessfulConnection(ThirdwebManager.Instance.GetActiveWallet());
        }

        private async System.Threading.Tasks.Task HandleSuccessfulConnection(IThirdwebWallet wallet)
        {
            try
            {
                // Get and Display wallet address
                var address = await wallet.GetAddress();
                UpdateWalletAddressTexts(address);

                if (fullwalletAddressText != null)
                    fullwalletAddressText.text = $"{address}";

                // Get and Display wallet balances
                await UpdateWalletBalance(wallet);
                await UpdateKasiBalance(wallet);
                await UpdateDiamondBalance(wallet);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error handling successful connection: {e.Message}");
                UpdateStatusTexts("Error displaying wallet info");
            }
        }

        private void UpdateWalletAddressTexts(string address)
        {
            string formattedAddress = FormatAddress(address);
            if (walletAddressTexts != null)
            {
                foreach (var text in walletAddressTexts)
                {
                    if (text != null)
                        text.text = $"{formattedAddress}";
                }
            }
        }

        private void UpdateStatusTexts(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }

        private async System.Threading.Tasks.Task UpdateWalletBalance(IThirdwebWallet wallet)
        {
            if (walletBalanceTexts != null && walletBalanceTexts.Length > 0)
            {
                try
                {
                    // Set all balance texts to "Loading..."
                    foreach (var text in walletBalanceTexts)
                    {
                        if (text != null)
                            text.text = "Loading...";
                    }

                    var balance = await wallet.GetBalance(chainId: chainId);
                    var chainDetails = await Utils.GetChainMetadata(
                        client: ThirdwebManager.Instance.Client,
                        chainId: chainId
                    );
                    var symbol = chainDetails?.NativeCurrency?.Symbol ?? "ETH";
                    var balanceDiamond = Utils.ToEth(
                        wei: balance.ToString(),
                        decimalsToDisplay: 4,
                        addCommas: true
                    );

                    // Update all balance texts
                    foreach (var text in walletBalanceTexts)
                    {
                        if (text != null)
                            text.text = $"{balanceDiamond} {symbol}";
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error updating wallet balance: {e.Message}");
                    foreach (var text in walletBalanceTexts)
                    {
                        if (text != null)
                            text.text = "Error loading balance";
                    }
                }
            }
        }

        private async System.Threading.Tasks.Task UpdateKasiBalance(IThirdwebWallet wallet)
        {
            if (kasiBalanceTexts != null && kasiBalanceTexts.Length > 0)
            {
                try
                {
                    // Set all KASI texts to "Loading..."
                    foreach (var text in kasiBalanceTexts)
                    {
                        if (text != null)
                            text.text = "Loading...";
                    }

                    // Get the KASI token contract
                    var contract = await ThirdwebManager.Instance.GetContract(
                        address: "0x02D5C205B3E4F550a7c6D1432E3E12c106A25a9a", 
                        chainId: chainId
                    );

                    // Get the wallet address
                    string address = await wallet.GetAddress();

                    // Read the balance using the ERC20 balanceOf function
                    var balanceResult = await contract.Read<System.Numerics.BigInteger>(
                        "function balanceOf(address who) view returns (uint256)",
                        address
                    );

                    // Get token decimals for proper formatting
                    var decimalsResult = await contract.Read<int>(
                        "function decimals() view returns (uint8)"
                    );

                    // Format the balance
                    var kasiBalanceFormatted = FormatTokenAmount(
                        balanceResult.ToString(), 
                        decimalsResult, 
                        4, 
                        true
                    );

                    // Update all KASI balance texts
                    foreach (var text in kasiBalanceTexts)
                    {
                        if (text != null)
                            text.text = $"{kasiBalanceFormatted} {"KASI"}";
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error updating Kasi balance: {e.Message}");
                    foreach (var text in kasiBalanceTexts)
                    {
                        if (text != null)
                            text.text = "Error loading Kasi balance";
                    }
                }
            }
        }

        private async System.Threading.Tasks.Task UpdateDiamondBalance(IThirdwebWallet wallet)
        {
            if (diamondBalanceTexts != null && diamondBalanceTexts.Length > 0)
            {
                try
                {
                    // Set all Diamond texts to "Loading..."
                    foreach (var text in diamondBalanceTexts)
                    {
                        if (text != null)
                            text.text = "Loading...";
                    }

                    // Get the Diamond token contract
                    var contract = await ThirdwebManager.Instance.GetContract(
                        address: "0x1b0bA94B1F01590E4aeCDa2363A839e99d57fF5b", 
                        chainId: chainId
                    );

                    // Get the wallet address
                    string address = await wallet.GetAddress();

                    // Read the balance using the ERC20 balanceOf function
                    var balanceResult = await contract.Read<System.Numerics.BigInteger>(
                        "function balanceOf(address who) view returns (uint256)",
                        address
                    );

                    // Get token decimals for proper formatting
                    var decimalsResult = await contract.Read<int>(
                        "function decimals() view returns (uint8)"
                    );

                    // Format the balance
                    var diamondBalanceFormatted = FormatTokenAmount(
                        balanceResult.ToString(), 
                        decimalsResult, 
                        4, 
                        true
                    );

                    // Update all Diamond balance texts
                    foreach (var text in diamondBalanceTexts)
                    {
                        if (text != null)
                            text.text = $"{diamondBalanceFormatted} {"Diamond"}";
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error updating Diamond balance: {e.Message}");
                    foreach (var text in diamondBalanceTexts)
                    {
                        if (text != null)
                            text.text = "Error loading Diamond balance";
                    }
                }
            }
        }

        // Helper method to format token amounts with proper decimals
        private string FormatTokenAmount(string rawAmount, int decimals, int displayDecimals = 4, bool addCommas = true)
        {
            try
            {
                System.Numerics.BigInteger rawValue = System.Numerics.BigInteger.Parse(rawAmount);
                System.Numerics.BigInteger divisor = System.Numerics.BigInteger.Pow(10, decimals);
                
                decimal tokenAmount = (decimal)rawValue / (decimal)divisor;
                
                string formatString = addCommas ? "N" + displayDecimals : "F" + displayDecimals;
                return tokenAmount.ToString(formatString);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error formatting token amount: {e.Message}");
                return "0";
            }
        }

        public async void RefreshWalletBalance()
        {
            var wallet = ThirdwebManager.Instance.GetActiveWallet();
            if (wallet != null)
            {
                await UpdateWalletBalance(wallet);
                await UpdateKasiBalance(wallet);
                await UpdateDiamondBalance(wallet);
            }
        }

        private void CopyAddressToClipboard()
        {
            if (fullwalletAddressText != null && !string.IsNullOrEmpty(fullwalletAddressText.text))
            {
                GUIUtility.systemCopyBuffer = fullwalletAddressText.text;
                UpdateStatusTexts("Address copied to clipboard!");
            }
        }

        private string FormatAddress(string address)
        {
            // Format address to show first 6 and last 4 characters
            return address.Length > 10
                ? $"{address.Substring(0, 6)}...{address.Substring(address.Length - 4)}"
                : address;
        }

        private void LoadMainMenu()
        {
            if (!string.IsNullOrEmpty(mainMenuSceneName))
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
            else
            {
                Debug.LogError("Main menu scene name is not set!");
            }
        }
    }
}