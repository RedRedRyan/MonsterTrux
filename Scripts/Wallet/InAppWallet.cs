using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;
using Unity.VisualScripting;
using WalletConnectUnity.Core.Networking;

namespace Thirdweb.Unity
{
    public class InAppWallet : MonoBehaviour
    {
        [Header("Connection UI")]
        [SerializeField] private Button connectWalletButton;
        [SerializeField] private GameObject emailInputCanvas;
        [SerializeField] private TMP_InputField emailInputField;
        [SerializeField] private Button emailSubmitButton;
        [SerializeField] TMP_Text statusText;
        [SerializeField] public ulong chainId = 80002;

        [Header("Wallet Info Display")]
        [SerializeField] private TMP_Text walletAddressText;
        [SerializeField] private TMP_Text diamondBalanceText;
        [SerializeField] private TMP_Text kasiBalanceText;
        [SerializeField] private Button refreshBalanceButton;

        [Header("APP Functions")]
        [SerializeField] private TMP_Text fullwalletAddressText;
        [SerializeField] private Button copyAddressButton;

        [Header("Scene Management")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private void Start()
        {
            // Hide email Input Canvas initially
            if (emailInputCanvas != null)
                emailInputCanvas.SetActive(false);
            
            // Set up connect wallet button
            if (connectWalletButton != null)
            {
                connectWalletButton.onClick.RemoveAllListeners();
                connectWalletButton.onClick.AddListener(OnConnectWalletClicked);
            }
            
            // Set up email authentication
            if (emailSubmitButton != null)
            {
                emailSubmitButton.onClick.RemoveAllListeners();
                emailSubmitButton.onClick.AddListener(ConnectWithEmail);
            }

            // Set up refresh balance button
            if (refreshBalanceButton != null)
            {
                refreshBalanceButton.onClick.RemoveAllListeners();
                refreshBalanceButton.onClick.AddListener(RefreshWalletBalance);
            }

            // Set up copy address button
            if (copyAddressButton != null)
            {
                copyAddressButton.onClick.RemoveAllListeners();
                copyAddressButton.onClick.AddListener(CopyAddressToClipboard);
            }

            CheckExistingConnection();
        }

        private async void OnConnectWalletClicked()
        {
            // Check if wallet is already connected
            var existingWallet = ThirdwebManager.Instance.GetActiveWallet();
            if (existingWallet != null)
            {
                // Wallet is already connected, go directly to main menu
                LoadMainMenu();
                return;
            }

            // No wallet connected, show email input
            ShowEmailInput();
        }

        private void ShowEmailInput()
        {
            // Show email input canvas
            if (emailInputCanvas != null)
                emailInputCanvas.SetActive(true);

            // Hide connect wallet button
            if (connectWalletButton != null)
                connectWalletButton.gameObject.SetActive(false);

            if (statusText != null)
                statusText.text = "Enter your email to connect";
        }

        private async void ConnectWithEmail()
        {
            if (emailInputField == null || string.IsNullOrEmpty(emailInputField.text))
            {
                if (statusText != null)
                    statusText.text = "Please enter a valid email address";
                return;
            }

            try
            {
                if (statusText != null)
                    statusText.text = "Connecting...";
                
                var InAppWalletOptions = new InAppWalletOptions(email: emailInputField.text);
                var options = new WalletOptions(
                    provider: WalletProvider.InAppWallet,
                    chainId: chainId,
                    inAppWalletOptions: InAppWalletOptions
                );
                
                var wallet = await ThirdwebManager.Instance.ConnectWallet(options);
                
                // Handle successful connection
                if (wallet != null)
                {
                    await HandleSuccessfulConnection(wallet);
                    // After successful connection, load main menu
                    LoadMainMenu();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error connecting with email: {e.Message}");
                if (statusText != null)
                    statusText.text = $"Connection error: {e.Message}";
            }
        }

        private async System.Threading.Tasks.Task HandleSuccessfulConnection(IThirdwebWallet wallet)
        {
            try
            {
                // Hide connect wallet button
                if (connectWalletButton != null)
                    connectWalletButton.gameObject.SetActive(false);

                // Hide email input canvas
                if (emailInputCanvas != null)
                    emailInputCanvas.SetActive(false);

                // Get and Display wallet address
                var address = await wallet.GetAddress();
                if (walletAddressText != null)
                {
                    string formattedAddress = FormatAddress(address);
                    walletAddressText.text = $"{formattedAddress}";
                }

                if (fullwalletAddressText != null)
                    fullwalletAddressText.text = $"{address}";

                // Get and Display wallet balance
                await UpdateWalletBalance(wallet);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error handling successful connection: {e.Message}");
                if (statusText != null)
                    statusText.text = "Error displaying wallet info";
            }
        }

        private async System.Threading.Tasks.Task UpdateWalletBalance(IThirdwebWallet wallet)
        {
            if (diamondBalanceText != null)
            {
                try
                {
                    diamondBalanceText.text = "Loading balance...";

                    var balance = await wallet.GetBalance(chainId: chainId);
                    var chainDetails = await Utils.GetChainMetadata(
                        client: ThirdwebManager.Instance.Client,
                        chainId: chainId
                    );
                    var symbol = chainDetails?.NativeCurrency?.Symbol ?? "ETH";
                    var balanceEth = Utils.ToEth(
                        wei: balance.ToString(),
                        decimalsToDisplay: 4,
                        addCommas: true
                    );
                    diamondBalanceText.text = $"Balance: {balanceEth} {symbol}";
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error updating wallet balance: {e.Message}");
                    diamondBalanceText.text = "Error loading balance";
                }
            }
        }
        

        private async void RefreshWalletBalance()
        {
            var wallet = ThirdwebManager.Instance.GetActiveWallet();
            if (wallet != null)
            {
                await UpdateWalletBalance(wallet);
            }
        }

        private void CopyAddressToClipboard()
        {
            if (fullwalletAddressText != null && !string.IsNullOrEmpty(fullwalletAddressText.text))
            {
                GUIUtility.systemCopyBuffer = fullwalletAddressText.text;
                if (statusText != null)
                    statusText.text = "Address copied to clipboard!";
            }
        }

        private string FormatAddress(string address)
        {
            // Format address to show first 6 and last 4 characters
            return address.Length > 10
                ? $"{address.Substring(0, 6)}...{address.Substring(address.Length - 4)}"
                : address;
        }

        private async void CheckExistingConnection()
        {
            try
            {
                // Check if a wallet is already connected from previous sessions
                var wallet = ThirdwebManager.Instance.GetActiveWallet();
                if (wallet != null)
                {
                    // Wallet is connected, update UI and proceed to main menu
                    await HandleSuccessfulConnection(wallet);
                    
                    // Optionally, you can automatically load main menu after a brief delay
                    // to show the wallet info, or wait for user interaction
                    if (statusText != null)
                        statusText.text = "Wallet connected! Click play to continue.";
                    
                    // Re-enable the connect wallet button but change its text to indicate continuation
                    if (connectWalletButton != null)
                    {
                        connectWalletButton.gameObject.SetActive(true);
                        var buttonText = connectWalletButton.GetComponentInChildren<TMP_Text>();
                        if (buttonText != null)
                            buttonText.text = "PLAY";
                        
                        // Update the click listener to go directly to main menu
                        connectWalletButton.onClick.RemoveAllListeners();
                        connectWalletButton.onClick.AddListener(LoadMainMenu);
                    }
                }
                else
                {
                    // No wallet connected, ensure connect button is set up for email flow
                    if (connectWalletButton != null)
                    {
                        connectWalletButton.gameObject.SetActive(true);
                        var buttonText = connectWalletButton.GetComponentInChildren<TMP_Text>();
                        if (buttonText != null)
                            buttonText.text = "PLAY";
                        
                        connectWalletButton.onClick.RemoveAllListeners();
                        connectWalletButton.onClick.AddListener(OnConnectWalletClicked);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error checking existing connection: {e.Message}");
                if (statusText != null)
                    statusText.text = "Error checking wallet connection";
            }
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