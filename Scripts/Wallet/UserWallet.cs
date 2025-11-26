using UnityEngine;
using System;
using System.Threading.Tasks;
using UnityEngine.UI;

namespace Thirdweb.Unity
{
    public class UserWallet : MonoBehaviour
    {
        public static UserWallet Instance { get; private set; }

        [Header("Wallet Data")]
        [SerializeField] private string  _walletAddress;
        [SerializeField] private string _email;
        [SerializeField] private string _balance;
        [SerializeField] private string _username;
        [SerializeField] private ulong _chainId = 80002;

        public string WalletAddress => _walletAddress;
        public string Email => _email;
        public string Balance => _balance;
        public string Username => _username;
        public ulong ChainId => _chainId;

        // Events for wallet data changes
        public static event Action<string> OnWalletAddressChanged;
        public static event Action<string> OnEmailChanged;
        public static event Action<string> OnBalanceChanged;
        public static event Action<string> OnUsernameChanged;
        public static event Action<bool> OnWalletConnected;

        private IThirdwebWallet _activeWallet;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeWallet();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private async void InitializeWallet()
        {
            try
            {
                _activeWallet = ThirdwebManager.Instance.GetActiveWallet();
                if (_activeWallet != null)
                {
                    await LoadWalletData();
                    OnWalletConnected?.Invoke(true);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error initializing wallet: {e.Message}");
            }
        }

        public async Task<bool> ConnectWallet(string email = null)
        {
            try
            {
                if (statusText != null)
                    statusText.text = "Connecting...";

                WalletOptions options;
                
                if (!string.IsNullOrEmpty(email))
                {
                    var inAppWalletOptions = new InAppWalletOptions(email: email);
                    options = new WalletOptions(
                        provider: WalletProvider.InAppWallet,
                        chainId: _chainId,
                        inAppWalletOptions: inAppWalletOptions
                    );
                    _email = email;
                }
                else
                {
                    options = new WalletOptions(
                        provider: WalletProvider.InAppWallet,
                        chainId: _chainId
                    );
                }

                _activeWallet = await ThirdwebManager.Instance.ConnectWallet(options);
                
                if (_activeWallet != null)
                {
                    await LoadWalletData();
                    OnWalletConnected?.Invoke(true);
                    return true;
                }
                
                return false;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error connecting wallet: {e.Message}");
                if (statusText != null)
                    statusText.text = $"Connection error: {e.Message}";
                return false;
            }
        }

        private async System.Threading.Tasks.Task LoadWalletData()
        {
            if (_activeWallet == null) return;

            try
            {
                // Load address
                _walletAddress = await _activeWallet.GetAddress();
                OnWalletAddressChanged?.Invoke(_walletAddress);

                // Load balance
                await UpdateBalance();

                // Generate username from email or address
                if (string.IsNullOrEmpty(_username))
                {
                    _username = GenerateUsername();
                    OnUsernameChanged?.Invoke(_username);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error loading wallet data: {e.Message}");
            }
        }

        public async System.Threading.Tasks.Task UpdateBalance()
        {
            if (_activeWallet == null) return;

            try
            {
                var balance = await _activeWallet.GetBalance(chainId: _chainId);
                var chainDetails = await Utils.GetChainMetadata(
                    client: ThirdwebManager.Instance.Client,
                    chainId: _chainId
                );
                var symbol = chainDetails?.NativeCurrency?.Symbol ?? "ETH";
                var balanceEth = Utils.ToEth(
                    wei: balance.ToString(),
                    decimalsToDisplay: 4,
                    addCommas: true
                );
                
                _balance = $"{balanceEth} {symbol}";
                OnBalanceChanged?.Invoke(_balance);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error updating balance: {e.Message}");
                _balance = "Error";
                OnBalanceChanged?.Invoke(_balance);
            }
        }

        public void SetUsername(string username)
        {
            _username = username;
            OnUsernameChanged?.Invoke(_username);
        }

        public void SetEmail(string email)
        {
            _email = email;
            OnEmailChanged?.Invoke(email);
        }

        

        private void ClearWalletData()
        {
            _walletAddress = string.Empty;
            _email = string.Empty;
            _balance = string.Empty;
            _username = string.Empty;

            OnWalletAddressChanged?.Invoke(string.Empty);
            OnEmailChanged?.Invoke(string.Empty);
            OnBalanceChanged?.Invoke(string.Empty);
            OnUsernameChanged?.Invoke(string.Empty);
        }

        private string GenerateUsername()
        {
            if (!string.IsNullOrEmpty(_email))
            {
                return _email.Split('@')[0]; // Use email prefix as username
            }
            else if (!string.IsNullOrEmpty(_walletAddress))
            {
                return $"User{_walletAddress.Substring(2, 6)}"; // User + first 6 chars of address
            }
            
            return "Anonymous";
        }

        public bool IsConnected()
        {
            return _activeWallet != null && !string.IsNullOrEmpty(_walletAddress);
        }

        public string GetFormattedAddress()
        {
            if (string.IsNullOrEmpty(_walletAddress)) return "Not Connected";
            
            return _walletAddress.Length > 10 
                ? $"{_walletAddress.Substring(0, 6)}...{_walletAddress.Substring(_walletAddress.Length - 4)}"
                : _walletAddress;
        }

        public void CopyAddressToClipboard()
        {
            if (!string.IsNullOrEmpty(_walletAddress))
            {
                GUIUtility.systemCopyBuffer = _walletAddress;
            }
        }

        // Helper method for status text (you can remove this if not needed)
        private Text statusText;
        public void SetStatusTextReference(Text statusText)
        {
            this.statusText = statusText;
        }
    }
}