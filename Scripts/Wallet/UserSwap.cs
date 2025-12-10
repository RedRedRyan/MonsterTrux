using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Numerics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Thirdweb;

namespace Thirdweb.Unity
{
    public class UserSwap : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject swapPanel;
        [SerializeField] private TMP_Dropdown fromCurrencyDropdown;
        [SerializeField] private TMP_Dropdown toCurrencyDropdown;
        [SerializeField] private TMP_InputField amountInput;
        [SerializeField] private TMP_Text outputText;
        [SerializeField] private TMP_Text gasEstimateText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text fromBalanceText;
        [SerializeField] private TMP_Text toBalanceText;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private Button swapButton;
        [SerializeField] private Button cancelButton;

        [Header("Contract Settings")]
        [SerializeField] private string kasiDmdPoolAddress = "0x73a0Ce2918B2771b7f10F61444c9D726bDCd8dea";
        [SerializeField] private string dmdPolPoolAddress = "0x9856A6f0CE553AB3A7BBcc416C082A519F1821f1";
        [SerializeField] private string kasiTokenAddress = "0x02D5C205B3E4F550a7c6D1432E3E12c106A25a9a";
        [SerializeField] private string diamondTokenAddress = "0x1b0bA94B1F01590E4aeCDa2363A839e99d57fF5b";
        [SerializeField] private ulong chainId = 80002;
        [SerializeField][Range(0.1f, 10f)] private float slippageTolerance = 2.0f; // Increased to 2% default

        private ThirdwebContract kasiDmdPoolContract;
        private ThirdwebContract dmdPolPoolContract;
        private ThirdwebContract kasiContract;
        private ThirdwebContract diamondContract;
        
        private bool poolsInitialized = false;
        private UserDetails userDetails;
        private string userAddress;
        private IThirdwebWallet activeWallet;

        private const int TOKEN_DECIMALS = 18;
        private BigInteger floorPrice = BigInteger.Parse("100000000000000000000"); // Default: 100 KASI per DIAMOND
        
        // Currency definitions
        public enum Currency { KASI, DIAMOND, POL }
        private Dictionary<Currency, string> currencySymbols = new Dictionary<Currency, string>
        {
            { Currency.KASI, "KASI" },
            { Currency.DIAMOND, "DIAMOND" },
            { Currency.POL, "POL" }
        };

        private Currency selectedFromCurrency = Currency.KASI;
        private Currency selectedToCurrency = Currency.DIAMOND;
        private ThirdwebTransactionReceipt swapTxn;

        private void Awake()
        {
            userDetails = FindFirstObjectByType<UserDetails>();
            
            if (ThirdwebManager.Instance != null)
            {
                activeWallet = ThirdwebManager.Instance.GetActiveWallet();
            }
        }

        private void Start()
        {
            InitializeUI();
            _ = InitializeContractsAsync();
        }

        #region UI Initialization

        private void InitializeUI()
        {
            // Initialize dropdowns
            if (fromCurrencyDropdown != null)
            {
                fromCurrencyDropdown.ClearOptions();
                fromCurrencyDropdown.AddOptions(new List<string> { "KASI", "DIAMOND", "POL" });
                fromCurrencyDropdown.onValueChanged.AddListener(OnFromCurrencyChanged);
                fromCurrencyDropdown.value = 0;
            }

            if (toCurrencyDropdown != null)
            {
                toCurrencyDropdown.ClearOptions();
                toCurrencyDropdown.AddOptions(new List<string> { "KASI", "DIAMOND", "POL" });
                toCurrencyDropdown.onValueChanged.AddListener(OnToCurrencyChanged);
                toCurrencyDropdown.value = 1;
            }

            // Initialize buttons
            if (swapButton != null)
            {
                swapButton.onClick.RemoveAllListeners();
                swapButton.onClick.AddListener(() => _ = ExecuteSwapAsync());
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(HideSwapPanel);
            }

            // Initialize input field
            if (amountInput != null)
                amountInput.onValueChanged.AddListener(val => OnAmountInputChanged());

            ResetUI();
        }

        private void OnFromCurrencyChanged(int index)
        {
            selectedFromCurrency = (Currency)index;
            UpdateToCurrencyDropdown();
            _ = UpdateBalancesAsync();
            OnAmountInputChanged();
        }

        private void OnToCurrencyChanged(int index)
        {
            selectedToCurrency = (Currency)index;
            _ = UpdateBalancesAsync();
            OnAmountInputChanged();
        }

        private void UpdateToCurrencyDropdown()
        {
            if (toCurrencyDropdown == null) return;

            List<Currency> availableCurrencies = GetAvailableToCurrencies(selectedFromCurrency);
            
            toCurrencyDropdown.ClearOptions();
            toCurrencyDropdown.AddOptions(availableCurrencies.Select(c => currencySymbols[c]).ToList());
            
            if (availableCurrencies.Count > 0)
            {
                selectedToCurrency = availableCurrencies[0];
                toCurrencyDropdown.value = 0;
            }
        }

        private List<Currency> GetAvailableToCurrencies(Currency fromCurrency)
        {
            switch (fromCurrency)
            {
                case Currency.KASI:
                    return new List<Currency> { Currency.DIAMOND };
                case Currency.DIAMOND:
                    return new List<Currency> { Currency.KASI, Currency.POL };
                case Currency.POL:
                    return new List<Currency> { Currency.DIAMOND };
                default:
                    return new List<Currency> { Currency.KASI };
            }
        }

        private void OnAmountInputChanged()
        {
            UpdateSwapButtonState();
            UpdateGasEstimate();
            _ = CalculateAndDisplayOutputAsync();
        }

        private void UpdateSwapButtonState()
        {
            if (swapButton == null) return;

            bool isValid = IsValidAmount(amountInput?.text);
            swapButton.interactable = isValid && poolsInitialized;

            var buttonText = swapButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                buttonText.text = !poolsInitialized ? "INITIALIZING..." : 
                    $"SWAP {currencySymbols[selectedFromCurrency]} FOR {currencySymbols[selectedToCurrency]}";
            }
        }

        private void UpdateGasEstimate()
        {
            if (gasEstimateText == null || !IsValidAmount(amountInput?.text) || !poolsInitialized)
                return;

            gasEstimateText.text = "Gas: ~0.002-0.008 POL";
        }

        private void ResetUI()
        {
            if (amountInput != null) amountInput.text = "";
            if (statusText != null)
            {
                statusText.text = poolsInitialized ? "Select currencies and enter amount" : "Initializing...";
                statusText.color = Color.white;
            }
            if (gasEstimateText != null) gasEstimateText.text = "Gas: ~0.002-0.008 POL";
            if (progressSlider != null) progressSlider.value = 0;
            if (outputText != null) outputText.text = "0.0000";
            UpdateSwapButtonState();
        }

        #endregion

        #region Contract Initialization

        private async Task InitializeContractsAsync()
        {
            try
            {
                UpdateStatus("Initializing contracts...", true);
                
                var client = ThirdwebManager.Instance.Client;
                var chain = new BigInteger(chainId);
                
                // Initialize KASI-DMD pool
                kasiDmdPoolContract = await ThirdwebContract.Create(
                    client: client,
                    address: kasiDmdPoolAddress,
                    chain: chain,
                    abi: GetKasiDmdPoolABI()
                );

                // Initialize DMD-POL pool
                dmdPolPoolContract = await ThirdwebContract.Create(
                    client: client,
                    address: dmdPolPoolAddress,
                    chain: chain,
                    abi: GetDmdPolPoolABI()
                );

                // Initialize token contracts
                kasiContract = await ThirdwebContract.Create(
                    client: client,
                    address: kasiTokenAddress,
                    chain: chain
                );

                diamondContract = await ThirdwebContract.Create(
                    client: client,
                    address: diamondTokenAddress,
                    chain: chain
                );

                // Get floor price from KASI-DMD pool
                try
                {
                    floorPrice = await ThirdwebContract.Read<BigInteger>(kasiDmdPoolContract, "floorPrice");
                    Debug.Log($"Floor price: {ConvertFromWei(floorPrice, TOKEN_DECIMALS)} KASI per DIAMOND");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Could not read floor price, using default: {e.Message}");
                }

                poolsInitialized = true;
                Debug.Log("All contracts initialized successfully");
                UpdateStatus("Ready to swap", true);
                
                // Get user address
                if (activeWallet != null)
                {
                    userAddress = await activeWallet.GetAddress();
                    Debug.Log($"User address: {userAddress}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to initialize contracts: {e.Message}");
                UpdateStatus($"Initialization failed: {e.Message}", false);
            }
        }

        private string GetKasiDmdPoolABI()
        {
            return @"[
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
            ]";
        }

        private string GetDmdPolPoolABI()
        {
            return @"[
                {
                    ""type"": ""function"",
                    ""name"": ""swapPolForDiamond"",
                    ""inputs"": [
                        { ""name"": ""minDiamondOut"", ""type"": ""uint256"", ""internalType"": ""uint256"" }
                    ],
                    ""outputs"": [],
                    ""stateMutability"": ""payable""
                },
                {
                    ""type"": ""function"",
                    ""name"": ""swapDiamondForPol"",
                    ""inputs"": [
                        { ""name"": ""diamondIn"", ""type"": ""uint256"", ""internalType"": ""uint256"" },
                        { ""name"": ""minPolOut"", ""type"": ""uint256"", ""internalType"": ""uint256"" }
                    ],
                    ""outputs"": [],
                    ""stateMutability"": ""nonpayable""
                },
                {
                    ""type"": ""function"",
                    ""name"": ""getReserves"",
                    ""inputs"": [],
                    ""outputs"": [
                        { ""name"": ""polReserve"", ""type"": ""uint256"", ""internalType"": ""uint256"" },
                        { ""name"": ""diamondReserve"", ""type"": ""uint256"", ""internalType"": ""uint256"" }
                    ],
                    ""stateMutability"": ""view""
                },
                {
                    ""type"": ""function"",
                    ""name"": ""getPolToDiamondQuote"",
                    ""inputs"": [
                        { ""name"": ""polIn"", ""type"": ""uint256"", ""internalType"": ""uint256"" }
                    ],
                    ""outputs"": [
                        { ""name"": ""diamondOut"", ""type"": ""uint256"", ""internalType"": ""uint256"" }
                    ],
                    ""stateMutability"": ""view""
                },
                {
                    ""type"": ""function"",
                    ""name"": ""getDiamondToPolQuote"",
                    ""inputs"": [
                        { ""name"": ""diamondIn"", ""type"": ""uint256"", ""internalType"": ""uint256"" }
                    ],
                    ""outputs"": [
                        { ""name"": ""polOut"", ""type"": ""uint256"", ""internalType"": ""uint256"" }
                    ],
                    ""stateMutability"": ""view""
                }
            ]";
        }

        #endregion

        #region Panel Management

        public void ShowSwapPanel()
        {
            if (swapPanel != null)
            {
                swapPanel.SetActive(true);
                ResetUI();
                _ = UpdateBalancesAsync();
            }
        }

        public void HideSwapPanel()
        {
            if (swapPanel != null)
            {
                swapPanel.SetActive(false);
                ResetUI();
            }
        }

        #endregion

        #region Swap Calculation - CORRECTED VERSION

        private async Task CalculateAndDisplayOutputAsync()
        {
            if (!poolsInitialized || !IsValidAmount(amountInput?.text))
            {
                if (outputText != null) outputText.text = "0.0000";
                return;
            }

            try
            {
                decimal inputAmount = decimal.Parse(amountInput.text);
                BigInteger inputAmountWei = ConvertToWei(inputAmount, TOKEN_DECIMALS);
                BigInteger expectedOutput = BigInteger.Zero;

                // CORRECTED: Based on your actual reserves where DMD is more valuable
                if (selectedFromCurrency == Currency.KASI && selectedToCurrency == Currency.DIAMOND)
                {
                    // KASI -> DMD: Need more KASI to get DMD
                    var reserves = await ThirdwebContract.Read<BigInteger[]>(kasiDmdPoolContract, "getReserves");
                    // Assuming reserves[0] = KASI, reserves[1] = DMD based on your actual pool
                    BigInteger dmdReserve = reserves[0]; // Assuming reserves[0] is DIAMOND
                    BigInteger kasiReserve = reserves[1];   // Assuming reserves[1] is KASI
                    
                    // With DMD being more valuable, we need to invert the calculation
                    // Actually, we should use the same formula but understand that DMD output will be small
                    BigInteger k = kasiReserve * dmdReserve;
                    BigInteger newKasi = kasiReserve + inputAmountWei;
                    BigInteger newDmdReserve = k / newKasi;
                    expectedOutput = dmdReserve - newDmdReserve;
                    
                    Debug.Log($"KASI->DMD: Input {inputAmount} KASI, Expected {ConvertFromWei(expectedOutput, TOKEN_DECIMALS)} DMD");
                }
                else if (selectedFromCurrency == Currency.DIAMOND && selectedToCurrency == Currency.KASI)
                {
                    // DMD -> KASI: Small DMD gives lots of KASI
                    var reserves = await ThirdwebContract.Read<BigInteger[]>(kasiDmdPoolContract, "getReserves");
                    BigInteger dmdReserve = reserves[0]; // Assuming reserves[0] is DIAMOND
                    BigInteger kasiReserve = reserves[1];   // Assuming reserves[1] is KASI
                    
                    BigInteger k = kasiReserve * dmdReserve;
                    BigInteger newDmd = dmdReserve + inputAmountWei;
                    BigInteger newKasiReserve = k / newDmd;
                    expectedOutput = kasiReserve - newKasiReserve;

                    // Check floor price - DMD should be more valuable than floor
                    BigInteger effectivePrice = expectedOutput * BigInteger.Parse("1000000000000000000") / inputAmountWei;
                    if (effectivePrice < floorPrice)
                    {
                        outputText.text = "Below floor price";
                        UpdateStatus("Price below minimum floor", false);
                        return;
                    }
                    
                    Debug.Log($"DMD->KASI: Input {inputAmount} DMD, Expected {ConvertFromWei(expectedOutput, TOKEN_DECIMALS)} KASI");
                }
                else if (selectedFromCurrency == Currency.DIAMOND && selectedToCurrency == Currency.POL)
                {
                    var reserves = await ThirdwebContract.Read<BigInteger[]>(dmdPolPoolContract, "getReserves");
                    // Assuming reserves[0] = POL, reserves[1] = DMD
                    BigInteger polReserve = reserves[0];
                    BigInteger dmdReserve = reserves[1];
                    
                    BigInteger k = polReserve * dmdReserve;
                    BigInteger newDmd = dmdReserve + inputAmountWei;
                    BigInteger newPolReserve = k / newDmd;
                    expectedOutput = polReserve - newPolReserve;
                }
                else if (selectedFromCurrency == Currency.POL && selectedToCurrency == Currency.DIAMOND)
                {
                    var reserves = await ThirdwebContract.Read<BigInteger[]>(dmdPolPoolContract, "getReserves");
                    BigInteger polReserve = reserves[0];
                    BigInteger dmdReserve = reserves[1];
                    
                    BigInteger k = polReserve * dmdReserve;
                    BigInteger newPol = polReserve + inputAmountWei;
                    BigInteger newDmdReserve = k / newPol;
                    expectedOutput = dmdReserve - newDmdReserve;
                }

                decimal outputAmount = ConvertFromWei(expectedOutput, TOKEN_DECIMALS);
                if (outputText != null)
                    outputText.text = $"{outputAmount:F4} {currencySymbols[selectedToCurrency]}";

                UpdateStatus("Ready to swap", true);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error calculating output: {e.Message}");
                if (outputText != null) outputText.text = "Error";
            }
        }

        #endregion

        #region Swap Execution - CORRECTED VERSION

        private async Task ExecuteSwapAsync()
        {
            if (!poolsInitialized || !IsValidAmount(amountInput?.text))
            {
                UpdateStatus("Invalid swap parameters", false);
                return;
            }

            try
            {
                if (swapButton != null) swapButton.interactable = false;
                if (progressSlider != null) progressSlider.value = 0.1f;

                if (activeWallet == null)
                {
                    UpdateStatus("Wallet not connected", false);
                    return;
                }

                decimal inputAmount = decimal.Parse(amountInput.text);
                BigInteger inputAmountWei = ConvertToWei(inputAmount, TOKEN_DECIMALS);

                // Get reserves and calculate expected output
                UpdateStatus("Fetching reserves...", true);
                if (progressSlider != null) progressSlider.value = 0.2f;

                BigInteger expectedOutput = BigInteger.Zero;
                ThirdwebContract poolContract = null;
                ThirdwebContract tokenContract = null;
                string swapMethod = "";
                BigInteger weiValue = BigInteger.Zero;
                object[] parameters = null;

                // Determine which pool and method to use
                if (selectedFromCurrency == Currency.KASI && selectedToCurrency == Currency.DIAMOND)
                {
                    poolContract = kasiDmdPoolContract;
                    tokenContract = kasiContract;
                    swapMethod = "swapKasiForDiamond";

                    var reserves = await ThirdwebContract.Read<BigInteger[]>(poolContract, "getReserves");
                    BigInteger dmdReserve = reserves[0];
                    BigInteger kasiReserve = reserves[1];
                    BigInteger k = kasiReserve * dmdReserve;
                    BigInteger newKasi = kasiReserve + inputAmountWei;
                    BigInteger newDmdReserve = k / newKasi;
                    expectedOutput = dmdReserve - newDmdReserve;
                }
                else if (selectedFromCurrency == Currency.DIAMOND && selectedToCurrency == Currency.KASI)
                {
                    poolContract = kasiDmdPoolContract;
                    tokenContract = diamondContract;
                    swapMethod = "swapDiamondForKasi";

                    var reserves = await ThirdwebContract.Read<BigInteger[]>(poolContract, "getReserves");
                    BigInteger dmdReserve = reserves[0];
                    BigInteger kasiReserve = reserves[1];
                    BigInteger k = kasiReserve * dmdReserve;
                    BigInteger newDmd = dmdReserve + inputAmountWei;
                    BigInteger newKasiReserve = k / newDmd;
                    expectedOutput = kasiReserve - newKasiReserve;
                }
                else if (selectedFromCurrency == Currency.DIAMOND && selectedToCurrency == Currency.POL)
                {
                    poolContract = dmdPolPoolContract;
                    tokenContract = diamondContract;
                    swapMethod = "swapDiamondForPol";

                    var reserves = await ThirdwebContract.Read<BigInteger[]>(poolContract, "getReserves");
                    BigInteger polReserve = reserves[0];
                    BigInteger dmdReserve = reserves[1];
                    BigInteger k = polReserve * dmdReserve;
                    BigInteger newDmd = dmdReserve + inputAmountWei;
                    BigInteger newPolReserve = k / newDmd;
                    expectedOutput = polReserve - newPolReserve;
                }
                else if (selectedFromCurrency == Currency.POL && selectedToCurrency == Currency.DIAMOND)
                {
                    poolContract = dmdPolPoolContract;
                    swapMethod = "swapPolForDiamond";
                    weiValue = inputAmountWei;

                    var reserves = await ThirdwebContract.Read<BigInteger[]>(poolContract, "getReserves");
                    BigInteger polReserve = reserves[0];
                    BigInteger dmdReserve = reserves[1];
                    BigInteger k = polReserve * dmdReserve;
                    BigInteger newPol = polReserve + inputAmountWei;
                    BigInteger newDmdReserve = k / newPol;
                    expectedOutput = dmdReserve - newDmdReserve;
                }

                // Apply slippage tolerance - FIXED calculation
                BigInteger minOutput = ApplySlippage(expectedOutput, slippageTolerance);
                Debug.Log($"Swapping {inputAmount} {currencySymbols[selectedFromCurrency]} for min {ConvertFromWei(minOutput, TOKEN_DECIMALS)} {currencySymbols[selectedToCurrency]} (Expected: {ConvertFromWei(expectedOutput, TOKEN_DECIMALS)})");

                // Check balance
                UpdateStatus($"Checking {currencySymbols[selectedFromCurrency]} balance...", true);
                if (progressSlider != null) progressSlider.value = 0.3f;

                if (selectedFromCurrency != Currency.POL)
                {
                    var balance = await ThirdwebContract.Read<BigInteger>(tokenContract, "balanceOf", userAddress);
                    if (balance < inputAmountWei)
                    {
                        UpdateStatus($"Insufficient {currencySymbols[selectedFromCurrency]}. You have {ConvertFromWei(balance, TOKEN_DECIMALS)}", false);
                        return;
                    }

                    // Check and approve allowance
                    UpdateStatus("Checking allowance...", true);
                    if (progressSlider != null) progressSlider.value = 0.4f;

                    var allowance = await ThirdwebContract.Read<BigInteger>(
                        tokenContract,
                        "allowance",
                        userAddress,
                        poolContract.Address
                    );

                    if (allowance < inputAmountWei)
                    {
                        UpdateStatus($"Approving {currencySymbols[selectedFromCurrency]}...", true);
                        if (progressSlider != null) progressSlider.value = 0.5f;

                        var approveReceipt = await ThirdwebContract.Write(
                            activeWallet,
                            tokenContract,
                            "approve",
                            BigInteger.Zero,
                            poolContract.Address,
                            inputAmountWei
                        );

                        Debug.Log($"Approval transaction: {approveReceipt.TransactionHash}");
                        await Task.Delay(2000);
                    }
                }
                else
                {
                    // Check POL balance
                    var polBalance = await activeWallet.GetBalance(chainId: chainId);
                    if (polBalance < inputAmountWei + ConvertToWei(0.01m, TOKEN_DECIMALS)) // Add gas buffer
                    {
                        UpdateStatus($"Insufficient POL. You have {ConvertFromWei(polBalance, TOKEN_DECIMALS)}", false);
                        return;
                    }
                }

                // Execute swap
                UpdateStatus("Executing swap...", true);
                if (progressSlider != null) progressSlider.value = 0.6f;
                
                // Build parameters based on swap type
                if (selectedFromCurrency == Currency.POL)
                {
                    // POL swap with value - only minOutput parameter
                    parameters = new object[] { minOutput };
                }
                else if (selectedFromCurrency == Currency.KASI && selectedToCurrency == Currency.DIAMOND)
                {
                    // KASI to DMD - inputAmountWei, minOutput
                    parameters = new object[] { inputAmountWei, minOutput };
                }
                else if (selectedFromCurrency == Currency.DIAMOND && selectedToCurrency == Currency.KASI)
                {
                    // DMD to KASI - inputAmountWei, minOutput
                    parameters = new object[] { inputAmountWei, minOutput };
                }
                else if (selectedFromCurrency == Currency.DIAMOND && selectedToCurrency == Currency.POL)
                {
                    // DMD to POL - inputAmountWei, minOutput
                    parameters = new object[] { inputAmountWei, minOutput };
                }

                Debug.Log($"Executing {swapMethod} with value {weiValue} and parameters: {string.Join(", ", parameters)}");

                // Execute the transaction
                swapTxn = await ThirdwebContract.Write(
                    activeWallet,
                    poolContract,
                    swapMethod,
                    weiValue,
                    parameters
                );

                string swapTxHash = swapTxn.TransactionHash;
                Debug.Log($"Swap transaction hash: {swapTxHash}");

                // Wait for confirmation
                UpdateStatus("Waiting for confirmation...", true);
                if (progressSlider != null) progressSlider.value = 0.8f;
                await Task.Delay(3000);

                UpdateStatus("Swap confirmed!", true);
                if (progressSlider != null) progressSlider.value = 1f;

                // Refresh balances
                if (userDetails != null)
                {
                    await Task.Delay(1000);
                    userDetails.RefreshWalletBalance();
                }

                await Task.Delay(2000);
                decimal outputAmount = ConvertFromWei(expectedOutput, TOKEN_DECIMALS);
                UpdateStatus($"Successfully swapped {inputAmount} {currencySymbols[selectedFromCurrency]} for ~{outputAmount:F4} {currencySymbols[selectedToCurrency]}!", true);
                await Task.Delay(2000);

                HideSwapPanel();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Swap error: {e.Message}");
                string errorMsg = ParseErrorMessage(e.Message);
                
                // Special handling for slippage errors
                if (errorMsg.Contains("Slippage exceeded"))
                {
                    errorMsg += ". Try increasing slippage tolerance or reducing swap amount.";
                }
                
                UpdateStatus($"Swap failed: {errorMsg}", false);
            }
            finally
            {
                if (swapButton != null) swapButton.interactable = true;
                if (progressSlider != null) progressSlider.value = 0;
            }
        }

        #endregion

        #region Balance Updates

        private async Task UpdateBalancesAsync()
        {
            if (!poolsInitialized || activeWallet == null) return;

            try
            {
                if (userAddress == null)
                {
                    userAddress = await activeWallet.GetAddress();
                }

                // Get POL balance
                BigInteger polBalance = await activeWallet.GetBalance(chainId: chainId);

                // Get KASI balance
                BigInteger kasiBalance = await ThirdwebContract.Read<BigInteger>(
                    kasiContract,
                    "balanceOf",
                    userAddress
                );

                // Get DIAMOND balance
                BigInteger diamondBalance = await ThirdwebContract.Read<BigInteger>(
                    diamondContract,
                    "balanceOf",
                    userAddress
                );

                // Update UI
                if (fromBalanceText != null)
                {
                    BigInteger fromBalance = selectedFromCurrency == Currency.KASI ? kasiBalance :
                                           selectedFromCurrency == Currency.DIAMOND ? diamondBalance :
                                           polBalance;
                    decimal displayBalance = ConvertFromWei(fromBalance, TOKEN_DECIMALS);
                    fromBalanceText.text = $"{currencySymbols[selectedFromCurrency]}: {displayBalance:F4}";
                }

                if (toBalanceText != null)
                {
                    BigInteger toBalance = selectedToCurrency == Currency.KASI ? kasiBalance :
                                         selectedToCurrency == Currency.DIAMOND ? diamondBalance :
                                         polBalance;
                    decimal displayBalance = ConvertFromWei(toBalance, TOKEN_DECIMALS);
                    toBalanceText.text = $"{currencySymbols[selectedToCurrency]}: {displayBalance:F4}";
                }


            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error updating balances: {e.Message}");
            }
        }

        #endregion

        #region Utility Methods

        private BigInteger ApplySlippage(BigInteger amount, float slippagePercent)
        {
            if (amount == BigInteger.Zero) return BigInteger.Zero;
            
            // Correct slippage calculation: amount * (100 - slippagePercent) / 100
            BigInteger numerator = amount * (BigInteger)(10000 - (slippagePercent * 100)); // Multiply by 100 for percentage
            BigInteger result = numerator / 10000;
            
            // Ensure minimum of 1 wei
            return result < 1 ? BigInteger.One : result;
        }

        private bool IsValidAmount(string amount)
        {
            if (string.IsNullOrEmpty(amount)) return false;
            return decimal.TryParse(amount, out decimal val) && val > 0;
        }

        private string ParseErrorMessage(string error)
        {
            if (error.Contains("user rejected") || error.Contains("User denied"))
                return "Transaction was rejected";
            if (error.Contains("insufficient funds"))
                return "Insufficient funds for gas";
            if (error.Contains("Slippage exceeded") || error.Contains("slippage"))
                return "Slippage exceeded - price changed";
            if (error.Contains("Price below floor"))
                return "Price below minimum floor price";
            if (error.Contains("Insufficient liquidity"))
                return "Not enough liquidity in pool";
            if (error.Contains("allowance"))
                return "Token approval needed";
            if (error.Contains("reverted"))
                return "Transaction reverted";

            var lines = error.Split('\n');
            string firstLine = lines[0].Trim();
            return firstLine.Length > 60 ? firstLine.Substring(0, 60) + "..." : firstLine;
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
            if (wei == BigInteger.Zero) return 0m;
            
            BigInteger divisor = BigInteger.Pow(10, decimals);
            BigInteger wholePart = wei / divisor;
            BigInteger remainder = wei % divisor;

            decimal result = (decimal)wholePart;
            result += (decimal)remainder / (decimal)divisor;

            return result;
        }

        private void UpdateStatus(string message, bool isSuccess)
        {
            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = isSuccess ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.2f, 0.2f);
            }
        }

        #endregion

        #region Public Methods

        public void PrepareSwap(Currency fromCurrency, Currency toCurrency, decimal amount)
        {
            ShowSwapPanel();
            
            // Set currencies
            selectedFromCurrency = fromCurrency;
            selectedToCurrency = toCurrency;
            
            if (fromCurrencyDropdown != null) fromCurrencyDropdown.value = (int)fromCurrency;
            
            // Update to dropdown based on from currency
            UpdateToCurrencyDropdown();
            
            // Set amount
            if (amountInput != null) amountInput.text = amount.ToString();
            
            OnAmountInputChanged();
        }

        public void SetSlippageTolerance(float percent)
        {
            slippageTolerance = Mathf.Clamp(percent, 0.1f, 10f);
            Debug.Log($"Slippage tolerance set to {slippageTolerance}%");
        }

        #endregion
    }
}