using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;

namespace Thirdweb.Unity
{
    public class InAppWalletEmailConnect : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Button connectWalletButton;
        [SerializeField] private GameObject emailInputPanel;
        [SerializeField] private TMP_InputField emailInputField;
        [SerializeField] private Button emailSubmitButton;
        [SerializeField] private TMP_Text statusText;

        [Header("Wallet Display")]
        [SerializeField] private GameObject walletInfoPanel;
        [SerializeField] private TMP_Text walletShortAddress;
        [SerializeField] private TMP_Text walletFullAddress;
        [SerializeField] private TMP_Text walletBalance;
        [SerializeField] private Button refreshBalanceButton;

        [Header("Chain")]
        [SerializeField] private ulong chainId = 1114; // Change chain if needed

        private void Start()
        {
            ThirdwebManager.Instance.Initialize();

            walletInfoPanel.SetActive(false);
            emailInputPanel.SetActive(false);

            connectWalletButton.onClick.AddListener(ShowEmailInput);
            emailSubmitButton.onClick.AddListener(ConnectWithEmail);
            refreshBalanceButton.onClick.AddListener(RefreshWalletBalance);

            CheckExistingConnection();
        }

        private void ShowEmailInput()
        {
            emailInputPanel.SetActive(true);
            connectWalletButton.gameObject.SetActive(false);

            statusText.text = "Enter your email to connect.";
        }

        private async void ConnectWithEmail()
        {
            if (string.IsNullOrEmpty(emailInputField.text))
            {
                statusText.text = "Email cannot be empty.";
                return;
            }

            try
            {
                var walletOptions = new WalletOptions(
    provider: WalletProvider.EcosystemWallet, 
    chainId: 1, 
    inAppWalletOptions: new InAppWalletOptions(authprovider: AuthProvider.Guest)
);
var wallet = await ThirdwebManager.Instance.ConnectWallet(walletOptions);
var address = await wallet.GetAddress();
ThirdwebDebug.Log($"Connected wallet address: {address}");

                
                HandleWalletConnected(wallet);
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
                statusText.text = "Connection failed.";
            }
        }

        private async void HandleWalletConnected(IThirdwebWallet wallet)
        {
            emailInputPanel.SetActive(false);
            walletInfoPanel.SetActive(true);

            // Address
            var address = await wallet.GetAddress();
            walletShortAddress.text = FormatAddress(address);
            walletFullAddress.text = address;

            // Balance
            await UpdateWalletBalance(wallet);

            statusText.text = "Wallet Connected!";
        }

        private async Task UpdateWalletBalance(IThirdwebWallet wallet)
        {
            walletBalance.text = "Loading...";

            var balance = await wallet.GetBalance(chainId);
            var chainData = await Utils.GetChainMetadata(ThirdwebManager.Instance.Client, chainId);
            var symbol = chainData?.NativeCurrency?.Symbol ?? "ETH";

            walletBalance.text = $"{Utils.ToEth(balance.ToString(), 4, true)} {symbol}";
        }

        private string FormatAddress(string address)
        {
            return $"{address.Substring(0, 6)}...{address.Substring(address.Length - 4)}";
        }

        private async void RefreshWalletBalance()
        {
            var wallet = ThirdwebManager.Instance.ActiveWallet;
            if (wallet != null) await UpdateWalletBalance(wallet);
        }

        private async void CheckExistingConnection()
        {
            var wallet = ThirdwebManager.Instance.ActiveWallet;
            if (wallet != null)
            {

                emailInputPanel.SetActive(false);
                connectWalletButton.gameObject.SetActive(false);
                walletInfoPanel.SetActive(true);

                var address = await wallet.GetAddress();
                walletShortAddress.text = FormatAddress(address);
                walletFullAddress.text = address;

                await UpdateWalletBalance(wallet);

            }
        }
    }
}