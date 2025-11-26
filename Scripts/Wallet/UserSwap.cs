using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Numerics;
using System.Threading.Tasks;

namespace Thirdweb.Unity
{
    public class UserSwap : MonoBehaviour
    {
        [Header("Swap Panels")]
        [SerializeField] private GameObject diamondToKasiPanel;
        [SerializeField] private GameObject kasiToDiamondPanel;

        [Header("Diamond to KASI Swap UI")]
        [SerializeField] private TMP_InputField diamondToKasiAmountInput;
        [SerializeField] private Button diamondToKasiSwapButton;
        [SerializeField] private Button diamondToKasiCancelButton;
        [SerializeField] private TMP_Text diamondToKasiStatusText;
        [SerializeField] private TMP_Text diamondToKasiFeeText;
        [SerializeField] private Slider diamondToKasiProgressSlider;
        [SerializeField] private TMP_Text diamondToKasiOutputText;
        [SerializeField] private TMP_Text diamondToKasiDiamondBalance;
        [SerializeField] private TMP_Text diamondToKasiKasiBalance;

        [Header("KASI to Diamond Swap UI")]
        [SerializeField] private TMP_InputField kasiToDiamondAmountInput;
        [SerializeField] private Button kasiToDiamondSwapButton;
        [SerializeField] private Button kasiToDiamondCancelButton;
        [SerializeField] private TMP_Text kasiToDiamondStatusText;
        [SerializeField] private TMP_Text kasiToDiamondFeeText;
        [SerializeField] private Slider kasiToDiamondProgressSlider;
        [SerializeField] private TMP_Text kasiToDiamondOutputText;
        [SerializeField] private TMP_Text kasiToDiamondKasiBalance;
        [SerializeField] private TMP_Text kasiToDiamondDiamondBalance;

        [Header("Swap Settings")]
        [SerializeField] private string faucetAddress = "0x73a0Ce2918B2771b7f10F61444c9D726bDCd8dea";
        [SerializeField] private string kasiTokenAddress = "0x02D5C205B3E4F550a7c6D1432E3E12c106A25a9a";
        [SerializeField] private string diamondTokenAddress = "0x1b0bA94B1F01590E4aeCDa2363A839e99d57fF5b";
        [SerializeField] private ulong chainId = 80002;
        [SerializeField][Range(0.1f, 5f)] private float slippageTolerance = 0.5f;

        private ThirdwebContract faucetContract;
        private ThirdwebContract kasiContract;
        private ThirdwebContract diamondContract;
        private bool faucetContractInitialized = false;
        private bool kasiContractInitialized = false;
        private bool diamondContractInitialized = false;
        private UserDetails userDetails;

        private const int TOKEN_DECIMALS = 18;
        private BigInteger floorPrice = BigInteger.Parse("100000000000000000000"); // 100 KASI per DIAMOND

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
            InitializeDiamondToKasiUI();
            InitializeKasiToDiamondUI();
            _ = InitializeContractsAsync();
        }

        #region UI Initialization

        private void InitializeDiamondToKasiUI()
        {
            if (diamondToKasiSwapButton != null)
            {
                diamondToKasiSwapButton.onClick.RemoveAllListeners();
                diamondToKasiSwapButton.onClick.AddListener(() => _ = ExecuteDiamondToKasiSwapAsync());
            }

            if (diamondToKasiCancelButton != null)
            {
                diamondToKasiCancelButton.onClick.RemoveAllListeners();
                diamondToKasiCancelButton.onClick.AddListener(HideDiamondToKasiPanel);
            }

            if (diamondToKasiAmountInput != null)
                diamondToKasiAmountInput.onValueChanged.AddListener(val => OnDiamondToKasiInputChanged());
        }

        private void InitializeKasiToDiamondUI()
        {
            if (kasiToDiamondSwapButton != null)
            {
                kasiToDiamondSwapButton.onClick.RemoveAllListeners();
                kasiToDiamondSwapButton.onClick.AddListener(() => _ = ExecuteKasiToDiamondSwapAsync());
            }

            if (kasiToDiamondCancelButton != null)
            {
                kasiToDiamondCancelButton.onClick.RemoveAllListeners();
                kasiToDiamondCancelButton.onClick.AddListener(HideKasiToDiamondPanel);
            }

            if (kasiToDiamondAmountInput != null)
                kasiToDiamondAmountInput.onValueChanged.AddListener(val => OnKasiToDiamondInputChanged());
        }

        #endregion

        #region Contract Initialization

        private async Task InitializeContractsAsync()
        {
            await InitializeFaucetContractAsync();
            await InitializeKasiContractAsync();
            await InitializeDiamondContractAsync();
        }

        private async Task InitializeFaucetContractAsync()
        {
            if (faucetContractInitialized) return;

            try
            {
                UpdateDiamondToKasiStatus("Initializing swap contract...", true);
                UpdateKasiToDiamondStatus("Initializing swap contract...", true);

                faucetContract = await ThirdwebContract.Create(
                    client: ThirdwebManager.Instance.Client,
                    address: faucetAddress,
                    chain: chainId,
                    abi: @"
[
  {
    ""type"": ""function"",
    ""name"": ""swapDiamondForKasi"",
    ""inputs"": [
      { ""name"": ""diamondIn"", ""type"": ""uint256"", ""internalType"": ""uint256"" },
      { ""name"": ""minKasiOut"", ""type"": ""uint256"", ""internalType"": ""uint256"" }
    ],
    ""outputs"": [],
    ""stateMutability"": ""nonpayable""
  },
  {
    ""type"": ""function"",
    ""name"": ""swapKasiForDiamond"",
    ""inputs"": [
      { ""name"": ""kasiIn"", ""type"": ""uint256"", ""internalType"": ""uint256"" },
      { ""name"": ""minDiamondOut"", ""type"": ""uint256"", ""internalType"": ""uint256"" }
    ],
    ""outputs"": [],
    ""stateMutability"": ""nonpayable""
  },
  {
    ""type"": ""function"",
    ""name"": ""getReserves"",
    ""inputs"": [],
    ""outputs"": [
      { ""name"": """", ""type"": ""uint256"", ""internalType"": ""uint256"" },
      { ""name"": """", ""type"": ""uint256"", ""internalType"": ""uint256"" }
    ],
    ""stateMutability"": ""view""
  },
  {
    ""type"": ""function"",
    ""name"": ""floorPrice"",
    ""inputs"": [],
    ""outputs"": [
      { ""name"": """", ""type"": ""uint256"", ""internalType"": ""uint256"" }
    ],
    ""stateMutability"": ""view""
  }
]
"
                );

                // Get floor price from contract
                try
                {
                    floorPrice = await ThirdwebContract.Read<BigInteger>(faucetContract, "floorPrice");
                    Debug.Log($"Floor price: {ConvertFromWei(floorPrice, TOKEN_DECIMALS)} KASI per DIAMOND");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Could not read floor price, using default: {e.Message}");
                }

                faucetContractInitialized = true;
                Debug.Log($"Faucet contract initialized at {faucetAddress}");
                UpdateDiamondToKasiStatus("Ready to swap", true);
                UpdateKasiToDiamondStatus("Ready to swap", true);
                UpdateDiamondToKasiButtonState();
                UpdateKasiToDiamondButtonState();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to initialize faucet contract: {e.Message}");
                UpdateDiamondToKasiStatus($"Swap init failed: {e.Message}", false);
                UpdateKasiToDiamondStatus($"Swap init failed: {e.Message}", false);
            }
        }

        private async Task InitializeKasiContractAsync()
        {
            if (kasiContractInitialized) return;

            try
            {
                kasiContract = await ThirdwebContract.Create(
                    client: ThirdwebManager.Instance.Client,
                    address: kasiTokenAddress,
                    chain: chainId
                );

                kasiContractInitialized = true;
                Debug.Log($"KASI contract initialized at {kasiTokenAddress}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to initialize KASI contract: {e.Message}");
            }
        }

        private async Task InitializeDiamondContractAsync()
        {
            if (diamondContractInitialized) return;

            try
            {
                diamondContract = await ThirdwebContract.Create(
                    client: ThirdwebManager.Instance.Client,
                    address: diamondTokenAddress,
                    chain: chainId
                );

                diamondContractInitialized = true;
                Debug.Log($"Diamond contract initialized at {diamondTokenAddress}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to initialize Diamond contract: {e.Message}");
            }
        }

        #endregion

        #region Panel Management

        public void ShowDiamondToKasiPanel()
        {
            if (diamondToKasiPanel != null)
            {
                diamondToKasiPanel.SetActive(true);
                ResetDiamondToKasiUI();
                _ = UpdateDiamondToKasiBalances();
            }
        }

        public void HideDiamondToKasiPanel()
        {
            if (diamondToKasiPanel != null)
            {
                diamondToKasiPanel.SetActive(false);
                ResetDiamondToKasiUI();
            }
        }

        public void ShowKasiToDiamondPanel()
        {
            if (kasiToDiamondPanel != null)
            {
                kasiToDiamondPanel.SetActive(true);
                ResetKasiToDiamondUI();
                _ = UpdateKasiToDiamondBalances();
            }
        }

        public void HideKasiToDiamondPanel()
        {
            if (kasiToDiamondPanel != null)
            {
                kasiToDiamondPanel.SetActive(false);
                ResetKasiToDiamondUI();
            }
        }

        #endregion

        #region Diamond to KASI Swap

        private void ResetDiamondToKasiUI()
        {
            if (diamondToKasiAmountInput != null) diamondToKasiAmountInput.text = "";
            if (diamondToKasiStatusText != null)
            {
                diamondToKasiStatusText.text = faucetContractInitialized ? "Enter Diamond amount" : "Initializing...";
                diamondToKasiStatusText.color = Color.white;
            }
            if (diamondToKasiFeeText != null) diamondToKasiFeeText.text = "Gas: ~0.002-0.008 POL";
            if (diamondToKasiProgressSlider != null) diamondToKasiProgressSlider.value = 0;
            if (diamondToKasiOutputText != null) diamondToKasiOutputText.text = "0.0000";
            UpdateDiamondToKasiButtonState();
        }

        private void OnDiamondToKasiInputChanged()
        {
            UpdateDiamondToKasiButtonState();
            UpdateDiamondToKasiFeeEstimate();
            _ = CalculateAndDisplayDiamondToKasiOutput();
        }

        private void UpdateDiamondToKasiButtonState()
        {
            if (diamondToKasiSwapButton == null) return;

            bool isValid = IsValidAmount(diamondToKasiAmountInput?.text);
            diamondToKasiSwapButton.interactable = isValid && faucetContractInitialized;

            var buttonText = diamondToKasiSwapButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                buttonText.text = !faucetContractInitialized ? "INITIALIZING..." : "SWAP DIAMOND FOR KASI";
            }
        }

        private void UpdateDiamondToKasiFeeEstimate()
        {
            if (diamondToKasiFeeText == null || !IsValidAmount(diamondToKasiAmountInput?.text) || !faucetContractInitialized)
                return;

            diamondToKasiFeeText.text = "Gas: ~0.002-0.008 POL";
        }

        private async Task CalculateAndDisplayDiamondToKasiOutput()
        {
            if (!faucetContractInitialized || !IsValidAmount(diamondToKasiAmountInput?.text))
            {
                if (diamondToKasiOutputText != null) diamondToKasiOutputText.text = "0.0000";
                return;
            }

            try
            {
                decimal inputAmount = decimal.Parse(diamondToKasiAmountInput.text);
                BigInteger inputAmountWei = ConvertToWei(inputAmount, TOKEN_DECIMALS);

                var reserves = await ThirdwebContract.Read<BigInteger[]>(
                    faucetContract,
                    "getReserves"
                );

                BigInteger diamondReserve = reserves[0];
                BigInteger kasiReserve = reserves[1];

                // Calculate expected output using contract formula
                BigInteger k = diamondReserve * kasiReserve;
                BigInteger newDiamond = diamondReserve + inputAmountWei;
                BigInteger newKasiReserve = k / newDiamond;
                BigInteger expectedOutput = kasiReserve - newKasiReserve;

                // Check floor price
                BigInteger effectivePrice = expectedOutput * BigInteger.Parse("1000000000000000000") / inputAmountWei;
                if (effectivePrice < floorPrice)
                {
                    if (diamondToKasiOutputText != null)
                        diamondToKasiOutputText.text = "Below floor price";
                    UpdateDiamondToKasiStatus("Price below minimum floor", false);
                    return;
                }

                decimal outputAmount = ConvertFromWei(expectedOutput, TOKEN_DECIMALS);

                if (diamondToKasiOutputText != null)
                    diamondToKasiOutputText.text = $"{outputAmount:F4}";

                UpdateDiamondToKasiStatus("Ready to swap", true);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error calculating Diamond to KASI output: {e.Message}");
                if (diamondToKasiOutputText != null) diamondToKasiOutputText.text = "Error";
            }
        }

        private async Task ExecuteDiamondToKasiSwapAsync()
        {
            if (!faucetContractInitialized || !IsValidAmount(diamondToKasiAmountInput?.text))
            {
                UpdateDiamondToKasiStatus("Invalid swap parameters", false);
                return;
            }

            try
            {
                if (diamondToKasiSwapButton != null) diamondToKasiSwapButton.interactable = false;

                var wallet = ThirdwebManager.Instance.GetActiveWallet();
                if (wallet == null)
                {
                    UpdateDiamondToKasiStatus("Wallet not connected", false);
                    return;
                }

                decimal inputAmount = decimal.Parse(diamondToKasiAmountInput.text);
                BigInteger inputAmountWei = ConvertToWei(inputAmount, TOKEN_DECIMALS);

                // Get reserves and calculate expected output
                UpdateDiamondToKasiStatus("Fetching reserves...", true);
                if (diamondToKasiProgressSlider != null) diamondToKasiProgressSlider.value = 0.1f;

                var reserves = await ThirdwebContract.Read<BigInteger[]>(
                    faucetContract,
                    "getReserves"
                );

                BigInteger diamondReserve = reserves[0];
                BigInteger kasiReserve = reserves[1];

                // Calculate expected output using contract formula
                BigInteger k = diamondReserve * kasiReserve;
                BigInteger newDiamond = diamondReserve + inputAmountWei;
                BigInteger newKasiReserve = k / newDiamond;
                BigInteger expectedOutput = kasiReserve - newKasiReserve;

                // Apply slippage
                BigInteger minOutput = ApplySlippage(expectedOutput, slippageTolerance);
                Debug.Log($"Swapping {inputAmount} DIAMOND for minimum {ConvertFromWei(minOutput, TOKEN_DECIMALS)} KASI");

                // Check Diamond balance
                UpdateDiamondToKasiStatus("Checking Diamond balance...", true);
                if (diamondToKasiProgressSlider != null) diamondToKasiProgressSlider.value = 0.2f;

                string userAddress = await wallet.GetAddress();
                var balance = await ThirdwebContract.Read<BigInteger>(diamondContract, "balanceOf", userAddress);

                if (balance < inputAmountWei)
                {
                    UpdateDiamondToKasiStatus($"Insufficient Diamond. You have {ConvertFromWei(balance, TOKEN_DECIMALS)}", false);
                    return;
                }

                // Check allowance
                UpdateDiamondToKasiStatus("Checking allowance...", true);
                if (diamondToKasiProgressSlider != null) diamondToKasiProgressSlider.value = 0.3f;

                var allowance = await ThirdwebContract.Read<BigInteger>(
                    diamondContract,
                    "allowance",
                    userAddress,
                    faucetAddress
                );

                // Approve if needed
                if (allowance < inputAmountWei)
                {
                    UpdateDiamondToKasiStatus("Approving Diamond...", true);
                    if (diamondToKasiProgressSlider != null) diamondToKasiProgressSlider.value = 0.4f;

                    var approveTxHash = await ThirdwebContract.Write(
                        wallet,
                        diamondContract,
                        "approve",
                        0,
                        faucetAddress,
                        inputAmountWei
                    );

                    Debug.Log($"Approval transaction: {approveTxHash}");
                    await Task.Delay(2000);
                }

                // Execute swap using contract function
                UpdateDiamondToKasiStatus("Executing swap...", true);
                if (diamondToKasiProgressSlider != null) diamondToKasiProgressSlider.value = 0.6f;

                var swapTxHash = await ThirdwebContract.Write(
                    wallet,
                    faucetContract,
                    "swapDiamondForKasi",
                    0,
                    inputAmountWei,
                    minOutput
                );

                Debug.Log($"Diamond to KASI swap transaction hash: {swapTxHash}");

                // Wait for confirmation
                UpdateDiamondToKasiStatus("Waiting for confirmation...", true);
                if (diamondToKasiProgressSlider != null) diamondToKasiProgressSlider.value = 0.8f;
                await Task.Delay(3000);

                UpdateDiamondToKasiStatus("Swap confirmed!", true);
                if (diamondToKasiProgressSlider != null) diamondToKasiProgressSlider.value = 1f;

                // Refresh balances
                if (userDetails != null)
                {
                    await Task.Delay(1000);
                    userDetails.RefreshWalletBalance();
                }

                await Task.Delay(2000);
                decimal outputAmount = ConvertFromWei(expectedOutput, TOKEN_DECIMALS);
                UpdateDiamondToKasiStatus($"Successfully swapped {inputAmount} DIAMOND for ~{outputAmount:F4} KASI!", true);
                await Task.Delay(2000);

                HideDiamondToKasiPanel();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Diamond to KASI swap error: {e.Message}");
                UpdateDiamondToKasiStatus($"Swap failed: {ParseErrorMessage(e.Message)}", false);
            }
            finally
            {
                if (diamondToKasiSwapButton != null) diamondToKasiSwapButton.interactable = true;
                if (diamondToKasiProgressSlider != null) diamondToKasiProgressSlider.value = 0;
            }
        }

        private void UpdateDiamondToKasiStatus(string message, bool isSuccess)
        {
            if (diamondToKasiStatusText != null)
            {
                diamondToKasiStatusText.text = message;
                diamondToKasiStatusText.color = isSuccess ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.2f, 0.2f);
            }
        }

        #endregion

        #region KASI to Diamond Swap

        private void ResetKasiToDiamondUI()
        {
            if (kasiToDiamondAmountInput != null) kasiToDiamondAmountInput.text = "";
            if (kasiToDiamondStatusText != null)
            {
                kasiToDiamondStatusText.text = faucetContractInitialized ? "Enter KASI amount" : "Initializing...";
                kasiToDiamondStatusText.color = Color.white;
            }
            if (kasiToDiamondFeeText != null) kasiToDiamondFeeText.text = "Gas: ~0.002-0.008 POL";
            if (kasiToDiamondProgressSlider != null) kasiToDiamondProgressSlider.value = 0;
            if (kasiToDiamondOutputText != null) kasiToDiamondOutputText.text = "0.0000";
            UpdateKasiToDiamondButtonState();
        }

        private void OnKasiToDiamondInputChanged()
        {
            UpdateKasiToDiamondButtonState();
            UpdateKasiToDiamondFeeEstimate();
            _ = CalculateAndDisplayKasiToDiamondOutput();
        }

        private void UpdateKasiToDiamondButtonState()
        {
            if (kasiToDiamondSwapButton == null) return;

            bool isValid = IsValidAmount(kasiToDiamondAmountInput?.text);
            kasiToDiamondSwapButton.interactable = isValid && faucetContractInitialized;

            var buttonText = kasiToDiamondSwapButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                buttonText.text = !faucetContractInitialized ? "INITIALIZING..." : "SWAP KASI FOR DIAMOND";
            }
        }

        private void UpdateKasiToDiamondFeeEstimate()
        {
            if (kasiToDiamondFeeText == null || !IsValidAmount(kasiToDiamondAmountInput?.text) || !faucetContractInitialized)
                return;

            kasiToDiamondFeeText.text = "Gas: ~0.002-0.008 POL";
        }

        private async Task CalculateAndDisplayKasiToDiamondOutput()
        {
            if (!faucetContractInitialized || !IsValidAmount(kasiToDiamondAmountInput?.text))
            {
                if (kasiToDiamondOutputText != null) kasiToDiamondOutputText.text = "0.0000";
                return;
            }

            try
            {
                decimal inputAmount = decimal.Parse(kasiToDiamondAmountInput.text);
                BigInteger inputAmountWei = ConvertToWei(inputAmount, TOKEN_DECIMALS);

                var reserves = await ThirdwebContract.Read<BigInteger[]>(
                    faucetContract,
                    "getReserves"
                );

                BigInteger diamondReserve = reserves[0];
                BigInteger kasiReserve = reserves[1];

                // Calculate expected output using contract formula
                BigInteger k = diamondReserve * kasiReserve;
                BigInteger newKasi = kasiReserve + inputAmountWei;
                BigInteger newDiamondReserve = k / newKasi;
                BigInteger expectedOutput = diamondReserve - newDiamondReserve;

                decimal outputAmount = ConvertFromWei(expectedOutput, TOKEN_DECIMALS);

                if (kasiToDiamondOutputText != null)
                    kasiToDiamondOutputText.text = $"{outputAmount:F4}";

                UpdateKasiToDiamondStatus("Ready to swap", true);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error calculating KASI to Diamond output: {e.Message}");
                if (kasiToDiamondOutputText != null) kasiToDiamondOutputText.text = "Error";
            }
        }

        private async Task ExecuteKasiToDiamondSwapAsync()
        {
            if (!faucetContractInitialized || !IsValidAmount(kasiToDiamondAmountInput?.text))
            {
                UpdateKasiToDiamondStatus("Invalid swap parameters", false);
                return;
            }

            try
            {
                if (kasiToDiamondSwapButton != null) kasiToDiamondSwapButton.interactable = false;

                var wallet = ThirdwebManager.Instance.GetActiveWallet();
                if (wallet == null)
                {
                    UpdateKasiToDiamondStatus("Wallet not connected", false);
                    return;
                }

                decimal inputAmount = decimal.Parse(kasiToDiamondAmountInput.text);
                BigInteger inputAmountWei = ConvertToWei(inputAmount, TOKEN_DECIMALS);

                // Get reserves and calculate expected output
                UpdateKasiToDiamondStatus("Fetching reserves...", true);
                if (kasiToDiamondProgressSlider != null) kasiToDiamondProgressSlider.value = 0.1f;

                var reserves = await ThirdwebContract.Read<BigInteger[]>(
                    faucetContract,
                    "getReserves"
                );

                BigInteger diamondReserve = reserves[0];
                BigInteger kasiReserve = reserves[1];

                // Calculate expected output using contract formula
                BigInteger k = diamondReserve * kasiReserve;
                BigInteger newKasi = kasiReserve + inputAmountWei;
                BigInteger newDiamondReserve = k / newKasi;
                BigInteger expectedOutput = diamondReserve - newDiamondReserve;

                // Apply slippage tolerance to get minimum output
                BigInteger minOutput = ApplySlippage(expectedOutput, slippageTolerance);

                Debug.Log($"Swapping {inputAmount} KASI for minimum {ConvertFromWei(minOutput, TOKEN_DECIMALS)} DIAMOND");

                // Check KASI balance
                UpdateKasiToDiamondStatus("Checking KASI balance...", true);
                if (kasiToDiamondProgressSlider != null) kasiToDiamondProgressSlider.value = 0.2f;

                string userAddress = await wallet.GetAddress();
                var balance = await ThirdwebContract.Read<BigInteger>(kasiContract, "balanceOf", userAddress);

                if (balance < inputAmountWei)
                {
                    UpdateKasiToDiamondStatus($"Insufficient KASI. You have {ConvertFromWei(balance, TOKEN_DECIMALS)}", false);
                    return;
                }

                // Check allowance
                UpdateKasiToDiamondStatus("Checking allowance...", true);
                if (kasiToDiamondProgressSlider != null) kasiToDiamondProgressSlider.value = 0.3f;

                var allowance = await ThirdwebContract.Read<BigInteger>(
                    kasiContract,
                    "allowance",
                    userAddress,
                    faucetAddress
                );

                // Approve if needed
                if (allowance < inputAmountWei)
                {
                    UpdateKasiToDiamondStatus("Approving KASI...", true);
                    if (kasiToDiamondProgressSlider != null) kasiToDiamondProgressSlider.value = 0.4f;

                    var approveTxHash = await ThirdwebContract.Write(
                        wallet,
                        kasiContract,
                        "approve",
                        0,
                        faucetAddress,
                        inputAmountWei
                    );

                    Debug.Log($"Approval transaction: {approveTxHash}");
                    await Task.Delay(2000);
                }

                // Execute swap using contract function
                UpdateKasiToDiamondStatus("Executing swap...", true);
                if (kasiToDiamondProgressSlider != null) kasiToDiamondProgressSlider.value = 0.6f;

                var swapTxHash = await ThirdwebContract.Write(
                    wallet,
                    faucetContract,
                    "swapKasiForDiamond",
                    0,
                    inputAmountWei,
                    minOutput
                );

                Debug.Log($"KASI to Diamond swap transaction hash: {swapTxHash}");

                // Wait for confirmation
                UpdateKasiToDiamondStatus("Waiting for confirmation...", true);
                if (kasiToDiamondProgressSlider != null) kasiToDiamondProgressSlider.value = 0.8f;
                await Task.Delay(3000);

                UpdateKasiToDiamondStatus("Swap confirmed!", true);
                if (kasiToDiamondProgressSlider != null) kasiToDiamondProgressSlider.value = 1f;

                // Refresh balances
                if (userDetails != null)
                {
                    await Task.Delay(1000);
                    userDetails.RefreshWalletBalance();
                }

                await Task.Delay(2000);
                decimal outputAmount = ConvertFromWei(expectedOutput, TOKEN_DECIMALS);
                UpdateKasiToDiamondStatus($"Successfully swapped {inputAmount} KASI for ~{outputAmount:F4} DIAMOND!", true);
                await Task.Delay(2000);

                HideKasiToDiamondPanel();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"KASI to Diamond swap error: {e.Message}");
                UpdateKasiToDiamondStatus($"Swap failed: {ParseErrorMessage(e.Message)}", false);
            }
            finally
            {
                if (kasiToDiamondSwapButton != null) kasiToDiamondSwapButton.interactable = true;
                if (kasiToDiamondProgressSlider != null) kasiToDiamondProgressSlider.value = 0;
            }
        }

        private void UpdateKasiToDiamondStatus(string message, bool isSuccess)
        {
            if (kasiToDiamondStatusText != null)
            {
                kasiToDiamondStatusText.text = message;
                kasiToDiamondStatusText.color = isSuccess ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.2f, 0.2f);
            }
        }

        #endregion

        #region Balance Updates

        private async Task UpdateDiamondToKasiBalances()
        {
            if (!faucetContractInitialized) return;

            try
            {
                var wallet = ThirdwebManager.Instance.GetActiveWallet();
                if (wallet == null) return;

                string userAddress = await wallet.GetAddress();

                var diamondBalance = await ThirdwebContract.Read<BigInteger>(
                    diamondContract,
                    "balanceOf",
                    userAddress
                );
                var kasiBalance = await ThirdwebContract.Read<BigInteger>(
                    kasiContract,
                    "balanceOf",
                    userAddress
                );

                if (diamondToKasiDiamondBalance != null)
                    diamondToKasiDiamondBalance.text = $"Balance: {ConvertFromWei(diamondBalance, TOKEN_DECIMALS):F4} DIAMOND";
                if (diamondToKasiKasiBalance != null)
                    diamondToKasiKasiBalance.text = $"Balance: {ConvertFromWei(kasiBalance, TOKEN_DECIMALS):F4} KASI";
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error updating Diamond to KASI balances: {e.Message}");
            }
        }

        private async Task UpdateKasiToDiamondBalances()
        {
            if (!faucetContractInitialized) return;

            try
            {
                var wallet = ThirdwebManager.Instance.GetActiveWallet();
                if (wallet == null) return;

                string userAddress = await wallet.GetAddress();

                var kasiBalance = await ThirdwebContract.Read<BigInteger>(
                    kasiContract,
                    "balanceOf",
                    userAddress
                );
                var diamondBalance = await ThirdwebContract.Read<BigInteger>(
                    diamondContract,
                    "balanceOf",
                    userAddress
                );

                if (kasiToDiamondKasiBalance != null)
                    kasiToDiamondKasiBalance.text = $"Balance: {ConvertFromWei(kasiBalance, TOKEN_DECIMALS):F4} KASI";
                if (kasiToDiamondDiamondBalance != null)
                    kasiToDiamondDiamondBalance.text = $"Balance: {ConvertFromWei(diamondBalance, TOKEN_DECIMALS):F4} DIAMOND";
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error updating KASI to Diamond balances: {e.Message}");
            }
        }

        #endregion

        #region Utility Methods

        private BigInteger ApplySlippage(BigInteger amount, float slippagePercent)
        {
            BigInteger slippageAmount = amount * (BigInteger)(slippagePercent * 100) / 10000;
            return amount - slippageAmount;
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
            if (error.Contains("Slippage exceeded")) return "Price changed too much, try increasing slippage";
            if (error.Contains("Price below floor")) return "Price below minimum floor price";
            if (error.Contains("Insufficient liquidity")) return "Not enough liquidity in pool";

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

        public void PrepareDiamondToKasiSwap(decimal amount)
        {
            ShowDiamondToKasiPanel();
            if (diamondToKasiAmountInput != null) diamondToKasiAmountInput.text = amount.ToString();
            OnDiamondToKasiInputChanged();
        }

        public void PrepareKasiToDiamondSwap(decimal amount)
        {
            ShowKasiToDiamondPanel();
            if (kasiToDiamondAmountInput != null) kasiToDiamondAmountInput.text = amount.ToString();
            OnKasiToDiamondInputChanged();
        }

        public void SetSlippageTolerance(float percent)
        {
            slippageTolerance = Mathf.Clamp(percent, 0.1f, 5f);
            Debug.Log($"Slippage tolerance set to {slippageTolerance}%");
        }

        #endregion
    }
}