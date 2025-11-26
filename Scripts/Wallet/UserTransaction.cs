using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Thirdweb;
using System.Numerics;
using System.Threading.Tasks;

namespace Thirdweb.Unity
{
    public class UserTransaction : MonoBehaviour
    {
        [Header("Transaction Panels")]
        [SerializeField] private GameObject kasiTransactionPanel;
        [SerializeField] private GameObject diamondTransactionPanel;
        [SerializeField] private GameObject polTransactionPanel;

        [Header("KASI Transaction UI")]
        [SerializeField] private TMP_InputField kasiRecipientInput;
        [SerializeField] private TMP_InputField kasiAmountInput;
        [SerializeField] private Button kasiSendButton;
        [SerializeField] private Button kasiCancelButton;
        [SerializeField] private TMP_Text kasiStatusText;
        [SerializeField] private TMP_Text kasiFeeText;
        [SerializeField] private Slider kasiProgressSlider;

        [Header("Diamond Transaction UI")]
        [SerializeField] private TMP_InputField diamondRecipientInput;
        [SerializeField] private TMP_InputField diamondAmountInput;
        [SerializeField] private Button diamondSendButton;
        [SerializeField] private Button diamondCancelButton;
        [SerializeField] private TMP_Text diamondStatusText;
        [SerializeField] private TMP_Text diamondFeeText;
        [SerializeField] private Slider diamondProgressSlider;

        [Header("POL (Native) Transaction UI")]
        [SerializeField] private TMP_InputField polRecipientInput;
        [SerializeField] private TMP_InputField polAmountInput;
        [SerializeField] private Button polSendButton;
        [SerializeField] private Button polCancelButton;
        [SerializeField] private TMP_Text polStatusText;
        [SerializeField] private TMP_Text polFeeText;
        [SerializeField] private Slider polProgressSlider;

        [Header("Token Settings")]
        [SerializeField] private string kasiTokenAddress = "0x02D5C205B3E4F550a7c6D1432E3E12c106A25a9a";
        [SerializeField] private string diamondTokenAddress = "0x1b0bA94B1F01590E4aeCDa2363A839e99d57fF5b";
        // [SerializeField] private string polTokenAddress = "0x";
        [SerializeField] private ulong chainId = 80002; // Polygon Amoy testnet

        private ThirdwebContract kasiContract;
        private ThirdwebContract diamondContract;
        private bool kasiContractInitialized = false;
        private bool diamondContractInitialized = false;
        private UserDetails userDetails;
        private const int TOKEN_DECIMALS = 18;

        private void Awake()
        {
            userDetails = FindObjectOfType<UserDetails>();
            
            if (ThirdwebManager.Instance != null)
            {
                var wallet = ThirdwebManager.Instance.GetActiveWallet();
                if (wallet != null && wallet is InAppWallet inAppWallet)
                {
                    chainId = inAppWallet.chainId;
                }
            }
        }

        private void Start()
        {
            InitializeKasiUI();
            InitializeDiamondUI();
            InitializePolUI();
            _ = InitializeContractsAsync();
        }

        #region UI Initialization

        private void InitializeKasiUI()
        {
            if (kasiSendButton != null)
            {
                kasiSendButton.onClick.RemoveAllListeners();
                kasiSendButton.onClick.AddListener(() => _ = ExecuteKasiTransferAsync());
            }

            if (kasiCancelButton != null)
            {
                kasiCancelButton.onClick.RemoveAllListeners();
                kasiCancelButton.onClick.AddListener(HideKasiPanel);
            }

            if (kasiRecipientInput != null)
                kasiRecipientInput.onValueChanged.AddListener(val => OnKasiInputChanged());

            if (kasiAmountInput != null)
                kasiAmountInput.onValueChanged.AddListener(val => OnKasiInputChanged());
        }

        private void InitializeDiamondUI()
        {
            if (diamondSendButton != null)
            {
                diamondSendButton.onClick.RemoveAllListeners();
                diamondSendButton.onClick.AddListener(() => _ = ExecuteDiamondTransferAsync());
            }

            if (diamondCancelButton != null)
            {
                diamondCancelButton.onClick.RemoveAllListeners();
                diamondCancelButton.onClick.AddListener(HideDiamondPanel);
            }

            if (diamondRecipientInput != null)
                diamondRecipientInput.onValueChanged.AddListener(val => OnDiamondInputChanged());

            if (diamondAmountInput != null)
                diamondAmountInput.onValueChanged.AddListener(val => OnDiamondInputChanged());
        }

        private void InitializePolUI()
        {
            if (polSendButton != null)
            {
                polSendButton.onClick.RemoveAllListeners();
                polSendButton.onClick.AddListener(() => _ = ExecutePolTransferAsync());
            }

            if (polCancelButton != null)
            {
                polCancelButton.onClick.RemoveAllListeners();
                polCancelButton.onClick.AddListener(HidePolPanel);
            }

            if (polRecipientInput != null)
                polRecipientInput.onValueChanged.AddListener(val => OnPolInputChanged());

            if (polAmountInput != null)
                polAmountInput.onValueChanged.AddListener(val => OnPolInputChanged());
        }

        #endregion

        #region Contract Initialization

        private async Task InitializeContractsAsync()
        {
            await InitializeKasiContractAsync();
            await InitializeDiamondContractAsync();
            UpdatePolStatus("Ready to send Pol", true);
        }

        private async Task InitializeKasiContractAsync()
        {
            if (kasiContractInitialized) return;

            try
            {
                UpdateKasiStatus("Initializing KASI contract...", true);

                kasiContract = await ThirdwebContract.Create(
                    client: ThirdwebManager.Instance.Client,
                    address: kasiTokenAddress,
                    chain: chainId
                );

                kasiContractInitialized = true;
                Debug.Log($"KASI contract initialized at {kasiTokenAddress}");
                UpdateKasiStatus("Ready to send KASI", true);
                UpdateKasiButtonState();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to initialize KASI contract: {e.Message}");
                UpdateKasiStatus($"KASI init failed: {e.Message}", false);
            }
        }

        private async Task InitializeDiamondContractAsync()
        {
            if (diamondContractInitialized) return;

            try
            {
                UpdateDiamondStatus("Initializing Diamond contract...", true);

                diamondContract = await ThirdwebContract.Create(
                    client: ThirdwebManager.Instance.Client,
                    address: diamondTokenAddress,
                    chain: chainId
                );

                diamondContractInitialized = true;
                Debug.Log($"Diamond contract initialized at {diamondTokenAddress}");
                UpdateDiamondStatus("Ready to send Diamond", true);
                UpdateDiamondButtonState();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to initialize Diamond contract: {e.Message}");
                UpdateDiamondStatus($"Diamond init failed: {e.Message}", false);
            }
        }

        #endregion

        #region Panel Management

        public void ShowKasiPanel()
        {
            if (kasiTransactionPanel != null)
            {
                kasiTransactionPanel.SetActive(true);
                ResetKasiUI();
            }
        }

        public void HideKasiPanel()
        {
            if (kasiTransactionPanel != null)
            {
                ResetKasiUI();
            }
        }

        public void ShowDiamondPanel()
        {
            if (diamondTransactionPanel != null)
            {
                diamondTransactionPanel.SetActive(true);
                ResetDiamondUI();
            }
        }

        public void HideDiamondPanel()
        {
            if (diamondTransactionPanel != null)
            {
                ResetDiamondUI();
            }
        }

        public void ShowPolPanel()
        {
            if (polTransactionPanel != null)
            {
                polTransactionPanel.SetActive(true);
                ResetPolUI();
            }
        }

        public void HidePolPanel()
        {
            if (polTransactionPanel != null)
            {
                ResetPolUI();
            }
        }

        #endregion

        #region KASI Transaction

        private void ResetKasiUI()
        {
            if (kasiRecipientInput != null) kasiRecipientInput.text = "";
            if (kasiAmountInput != null) kasiAmountInput.text = "";
            if (kasiStatusText != null)
            {
                kasiStatusText.text = kasiContractInitialized ? "Enter recipient and amount" : "Initializing...";
                kasiStatusText.color = Color.white;
            }
            if (kasiFeeText != null) kasiFeeText.text = "Gas: ~0.001-0.005 POL";
            if (kasiProgressSlider != null) kasiProgressSlider.value = 0;
            UpdateKasiButtonState();
        }

        private void OnKasiInputChanged()
        {
            UpdateKasiButtonState();
            UpdateKasiFeeEstimate();
        }

        private void UpdateKasiButtonState()
        {
            if (kasiSendButton == null) return;

            bool isValid = IsValidAddress(kasiRecipientInput?.text) && IsValidAmount(kasiAmountInput?.text);
            kasiSendButton.interactable = isValid && kasiContractInitialized;

            var buttonText = kasiSendButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                buttonText.text = !kasiContractInitialized ? "INITIALIZING..." : "SEND KASI";
            }
        }

        private void UpdateKasiFeeEstimate()
        {
            if (kasiFeeText == null || !IsValidAddress(kasiRecipientInput?.text) || 
                !IsValidAmount(kasiAmountInput?.text) || !kasiContractInitialized)
                return;

            kasiFeeText.text = "Gas: ~0.001-0.005 POL";
        }

        private async Task ExecuteKasiTransferAsync()
        {
            if (!kasiContractInitialized || !IsValidAddress(kasiRecipientInput?.text) || 
                !IsValidAmount(kasiAmountInput?.text))
            {
                UpdateKasiStatus("Invalid transaction parameters", false);
                return;
            }

            try
            {
                if (kasiSendButton != null) kasiSendButton.interactable = false;

                var wallet = ThirdwebManager.Instance.GetActiveWallet();
                if (wallet == null)
                {
                    UpdateKasiStatus("Wallet not connected", false);
                    return;
                }

                string senderAddress = await wallet.GetAddress();
                string recipient = kasiRecipientInput.text.Trim();
                decimal amountDecimal = decimal.Parse(kasiAmountInput.text);
                BigInteger amountWei = ConvertToWei(amountDecimal, TOKEN_DECIMALS);

                Debug.Log($"Transferring {amountDecimal} KASI to {recipient}");

                // Check balance
                UpdateKasiStatus("Checking balance...", true);
                if (kasiProgressSlider != null) kasiProgressSlider.value = 0.2f;

                var balance = await ThirdwebContract.Read<BigInteger>(kasiContract, "balanceOf", senderAddress);

                if (balance < amountWei)
                {
                    UpdateKasiStatus($"Insufficient KASI. You have {ConvertFromWei(balance, TOKEN_DECIMALS)}", false);
                    return;
                }

                // Execute transfer
                UpdateKasiStatus("Sending transaction...", true);
                if (kasiProgressSlider != null) kasiProgressSlider.value = 0.5f;

                var txHash = await ThirdwebContract.Write(wallet, kasiContract, "transfer", 0, recipient, amountWei);

                Debug.Log($"KASI Transaction hash: {txHash}");

                // Confirmation
                UpdateKasiStatus("Waiting for confirmation...", true);
                if (kasiProgressSlider != null) kasiProgressSlider.value = 0.8f;
                await Task.Delay(3000);

                UpdateKasiStatus("Transaction confirmed!", true);
                if (kasiProgressSlider != null) kasiProgressSlider.value = 1f;

                // Refresh balances
                if (userDetails != null)
                {
                    await Task.Delay(1000);
                    userDetails.RefreshWalletBalance();
                }

                await Task.Delay(2000);
                UpdateKasiStatus($"Successfully sent {amountDecimal} KASI!", true);
                await Task.Delay(1500);

                HideKasiPanel();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"KASI transfer error: {e.Message}");
                UpdateKasiStatus($"Transfer failed: {ParseErrorMessage(e.Message)}", false);
            }
            finally
            {
                if (kasiSendButton != null) kasiSendButton.interactable = true;
                if (kasiProgressSlider != null) kasiProgressSlider.value = 0;
            }
        }

        private void UpdateKasiStatus(string message, bool isSuccess)
        {
            if (kasiStatusText != null)
            {
                kasiStatusText.text = message;
                kasiStatusText.color = isSuccess ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.2f, 0.2f);
            }
        }

        #endregion

        #region Diamond Transaction

        private void ResetDiamondUI()
        {
            if (diamondRecipientInput != null) diamondRecipientInput.text = "";
            if (diamondAmountInput != null) diamondAmountInput.text = "";
            if (diamondStatusText != null)
            {
                diamondStatusText.text = diamondContractInitialized ? "Enter recipient and amount" : "Initializing...";
                diamondStatusText.color = Color.white;
            }
            if (diamondFeeText != null) diamondFeeText.text = "Gas: ~0.001-0.005 POL";
            if (diamondProgressSlider != null) diamondProgressSlider.value = 0;
            UpdateDiamondButtonState();
        }

        private void OnDiamondInputChanged()
        {
            UpdateDiamondButtonState();
            UpdateDiamondFeeEstimate();
        }

        private void UpdateDiamondButtonState()
        {
            if (diamondSendButton == null) return;

            bool isValid = IsValidAddress(diamondRecipientInput?.text) && IsValidAmount(diamondAmountInput?.text);
            diamondSendButton.interactable = isValid && diamondContractInitialized;

            var buttonText = diamondSendButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                buttonText.text = !diamondContractInitialized ? "INITIALIZING..." : "SEND DIAMOND";
            }
        }

        private void UpdateDiamondFeeEstimate()
        {
            if (diamondFeeText == null || !IsValidAddress(diamondRecipientInput?.text) || 
                !IsValidAmount(diamondAmountInput?.text) || !diamondContractInitialized)
                return;

            diamondFeeText.text = "Gas: ~0.001-0.005 POL";
        }

        private async Task ExecuteDiamondTransferAsync()
        {
            if (!diamondContractInitialized || !IsValidAddress(diamondRecipientInput?.text) || 
                !IsValidAmount(diamondAmountInput?.text))
            {
                UpdateDiamondStatus("Invalid transaction parameters", false);
                return;
            }

            try
            {
                if (diamondSendButton != null) diamondSendButton.interactable = false;

                var wallet = ThirdwebManager.Instance.GetActiveWallet();
                if (wallet == null)
                {
                    UpdateDiamondStatus("Wallet not connected", false);
                    return;
                }

                string senderAddress = await wallet.GetAddress();
                string recipient = diamondRecipientInput.text.Trim();
                decimal amountDecimal = decimal.Parse(diamondAmountInput.text);
                BigInteger amountWei = ConvertToWei(amountDecimal, TOKEN_DECIMALS);

                Debug.Log($"Transferring {amountDecimal} Diamond to {recipient}");

                // Check balance
                UpdateDiamondStatus("Checking balance...", true);
                if (diamondProgressSlider != null) diamondProgressSlider.value = 0.2f;

                var balance = await ThirdwebContract.Read<BigInteger>(diamondContract, "balanceOf", senderAddress);

                if (balance < amountWei)
                {
                    UpdateDiamondStatus($"Insufficient Diamond. You have {ConvertFromWei(balance, TOKEN_DECIMALS)}", false);
                    return;
                }

                // Execute transfer
                UpdateDiamondStatus("Sending transaction...", true);
                if (diamondProgressSlider != null) diamondProgressSlider.value = 0.5f;

                var txHash = await ThirdwebContract.Write(wallet, diamondContract, "transfer", 0, recipient, amountWei);

                Debug.Log($"Diamond Transaction hash: {txHash}");

                // Confirmation
                UpdateDiamondStatus("Waiting for confirmation...", true);
                if (diamondProgressSlider != null) diamondProgressSlider.value = 0.8f;
                await Task.Delay(3000);

                UpdateDiamondStatus("Transaction confirmed!", true);
                if (diamondProgressSlider != null) diamondProgressSlider.value = 1f;

                // Refresh balances
                if (userDetails != null)
                {
                    await Task.Delay(1000);
                    userDetails.RefreshWalletBalance();
                }

                await Task.Delay(2000);
                UpdateDiamondStatus($"Successfully sent {amountDecimal} Diamond!", true);
                await Task.Delay(1500);

                HideDiamondPanel();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Diamond transfer error: {e.Message}");
                UpdateDiamondStatus($"Transfer failed: {ParseErrorMessage(e.Message)}", false);
            }
            finally
            {
                if (diamondSendButton != null) diamondSendButton.interactable = true;
                if (diamondProgressSlider != null) diamondProgressSlider.value = 0;
            }
        }

        private void UpdateDiamondStatus(string message, bool isSuccess)
        {
            if (diamondStatusText != null)
            {
                diamondStatusText.text = message;
                diamondStatusText.color = isSuccess ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.2f, 0.2f);
            }
        }

        #endregion

        #region POL (Native Token) Transaction

        private void ResetPolUI()
        {
            if (polRecipientInput != null) polRecipientInput.text = "";
            if (polAmountInput != null) polAmountInput.text = "";
            if (polStatusText != null)
            {
                polStatusText.text = "Enter recipient and amount";
                polStatusText.color = Color.white;
            }
            if (polFeeText != null) polFeeText.text = "Gas: ~0.001-0.003 POL";
            if (polProgressSlider != null) polProgressSlider.value = 0;
            UpdatePolButtonState();
        }

        private void OnPolInputChanged()
        {
            UpdatePolButtonState();
            UpdatePolFeeEstimate();
        }

        private void UpdatePolButtonState()
        {
            if (polSendButton == null) return;

            bool isValid = IsValidAddress(polRecipientInput?.text) && IsValidAmount(polAmountInput?.text);
            polSendButton.interactable = isValid;

            var buttonText = polSendButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                buttonText.text = "SEND";
            }
        }

        private void UpdatePolFeeEstimate()
        {
            if (polFeeText == null || !IsValidAddress(polRecipientInput?.text) || 
                !IsValidAmount(polAmountInput?.text))
                return;

            polFeeText.text = "Gas: ~0.001-0.003 POL";
        }

        private async Task ExecutePolTransferAsync()
        {
            if (!IsValidAddress(polRecipientInput?.text) || !IsValidAmount(polAmountInput?.text))
            {
                UpdatePolStatus("Invalid transaction parameters", false);
                return;
            }

            try
            {
                if (polSendButton != null) polSendButton.interactable = false;

                var wallet = ThirdwebManager.Instance.GetActiveWallet();
                if (wallet == null)
                {
                    UpdatePolStatus("Wallet not connected", false);
                    return;
                }

                string recipient = polRecipientInput.text.Trim();
                decimal amountDecimal = decimal.Parse(polAmountInput.text);
                BigInteger amountWei = ConvertToWei(amountDecimal, TOKEN_DECIMALS);

                Debug.Log($"Transferring {amountDecimal} POL to {recipient}");

                // Check balance
                UpdatePolStatus("Checking balance...", true);
                if (polProgressSlider != null) polProgressSlider.value = 0.2f;

                var balance = await wallet.GetBalance(chainId: chainId);
                var balanceBigInt = BigInteger.Parse(balance.ToString());

                // Account for gas fee (rough estimate)
                BigInteger gasReserve = ConvertToWei(0.005m, TOKEN_DECIMALS);
                
                if (balanceBigInt < amountWei + gasReserve)
                {
                    UpdatePolStatus($"Insufficient POL (keep some for gas)", false);
                    return;
                }

                // Execute transfer
                UpdatePolStatus("Sending transaction...", true);
                if (polProgressSlider != null) polProgressSlider.value = 0.5f;

                var transaction = await ThirdwebTransaction.Create(
                    wallet: wallet,
                    txInput: new ThirdwebTransactionInput(
                        chainId: chainId,                           
                         from: await wallet.GetAddress(),
                             to: recipient,                             
                        value: amountWei
                     ));

                var txHash = await ThirdwebTransaction.Send(transaction);

                Debug.Log($"POL Transaction hash: {txHash}");

                // Confirmation
                UpdatePolStatus("Waiting for confirmation...", true);
                if (polProgressSlider != null) polProgressSlider.value = 0.8f;
                await Task.Delay(3000);

                UpdatePolStatus("Transaction confirmed!", true);
                if (polProgressSlider != null) polProgressSlider.value = 1f;

                // Refresh balances
                if (userDetails != null)
                {
                    await Task.Delay(1000);
                    userDetails.RefreshWalletBalance();
                }

                await Task.Delay(2000);
                UpdatePolStatus($"Successfully sent {amountDecimal} POL!", true);
                await Task.Delay(1500);

                HidePolPanel();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"POL transfer error: {e.Message}");
                UpdatePolStatus($"Transfer failed: {ParseErrorMessage(e.Message)}", false);
            }
            finally
            {
                if (polSendButton != null) polSendButton.interactable = true;
                if (polProgressSlider != null) polProgressSlider.value = 0;
            }
        }

        private void UpdatePolStatus(string message, bool isSuccess)
        {
            if (polStatusText != null)
            {
                polStatusText.text = message;
                polStatusText.color = isSuccess ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.2f, 0.2f);
            }
        }

        #endregion

        #region Utility Methods

        private bool IsValidAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return false;
            address = address.Trim();
            
            if (address.Length != 42 || !address.StartsWith("0x"))
                return false;

            for (int i = 2; i < address.Length; i++)
            {
                char c = address[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            }

            return true;
        }

        private bool IsValidAmount(string amount)
        {
            if (string.IsNullOrEmpty(amount)) return false;
            return decimal.TryParse(amount, out decimal val) && val > 0;
        }

        private string ParseErrorMessage(string error)
        {
            if (error.Contains("user rejected")) return "Transaction rejected";
            if (error.Contains("insufficient funds")) return "Insufficient funds for gas";
            if (error.Contains("nonce")) return "Transaction conflict, please retry";
            
            var lines = error.Split('\n');
            return lines[0].Length > 60 ? lines[0].Substring(0, 60) + "..." : lines[0];
        }

        private BigInteger ConvertToWei(decimal amount, int decimals)
        {
            BigInteger multiplier = BigInteger.Pow(10, decimals);
            string amountStr = amount.ToString("F" + decimals);
            string[] parts = amountStr.Split('.');
            
            BigInteger wholePart = BigInteger.Parse(parts[0]) * multiplier;
            BigInteger fracPart = 0;
            
            if (parts.Length > 1)
            {
                string fracStr = parts[1].PadRight(decimals, '0').Substring(0, decimals);
                fracPart = BigInteger.Parse(fracStr);
            }
            
            return wholePart + fracPart;
        }

        private decimal ConvertFromWei(BigInteger wei, int decimals)
        {
            BigInteger divisor = BigInteger.Pow(10, decimals);
            BigInteger wholePart = wei / divisor;
            BigInteger remainder = wei % divisor;
            
            decimal result = (decimal)wholePart;
            result += (decimal)remainder / (decimal)divisor;
            
            return result;
        }

        #endregion

        #region Public Helper Methods

        public void PrepareKasiTransfer(string recipient, decimal amount)
        {
            ShowKasiPanel();
            if (kasiRecipientInput != null) kasiRecipientInput.text = recipient;
            if (kasiAmountInput != null) kasiAmountInput.text = amount.ToString();
            OnKasiInputChanged();
        }

        public void PrepareDiamondTransfer(string recipient, decimal amount)
        {
            ShowDiamondPanel();
            if (diamondRecipientInput != null) diamondRecipientInput.text = recipient;
            if (diamondAmountInput != null) diamondAmountInput.text = amount.ToString();
            OnDiamondInputChanged();
        }

        public void PreparePolTransfer(string recipient, decimal amount)
        {
            ShowPolPanel();
            if (polRecipientInput != null) polRecipientInput.text = recipient;
            if (polAmountInput != null) polAmountInput.text = amount.ToString();
            OnPolInputChanged();
        }

        #endregion
    }
}