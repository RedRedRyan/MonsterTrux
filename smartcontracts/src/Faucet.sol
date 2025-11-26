// SPDX-License-Identifier: UNLICENSED
pragma solidity ^0.8.13;

import "@openzeppelin/contracts/access/Ownable.sol";
import "@openzeppelin/contracts/utils/ReentrancyGuard.sol";
import "@openzeppelin/contracts/utils/Pausable.sol";
import "@openzeppelin/contracts/token/ERC20/IERC20.sol";
import "./GameToken.sol";
import "./DiamondToken.sol";

/**
 * @title Faucet (AMM)
 * @dev Constant-product AMM for DIAMOND <-> KASI swaps with LP shares
 *
 * Features:
 * - Constant product formula: x * y = k
 * - Floor price enforcement: 1 DIAMOND = 100 KASI minimum
 * - LP shares minted/burned on add/remove liquidity
 * - Owner can pause/unpause, set floor price
 * - Constructor: Faucet(diamondToken, gameToken, owner)
 *
 * NOTE: Both DIAMOND and KASI are ERC20 tokens
 */
contract Faucet is Ownable, ReentrancyGuard, Pausable {
    DiamondToken public immutable diamondToken;
    GameToken public immutable gameToken;

    // AMM reserves
    uint256 public diamondReserves;  // DIAMOND token (wei)
    uint256 public kasiReserves;     // KASI token (wei)

    // LP shares
    uint256 public totalLpShares;
    mapping(address => uint256) public lpShares;

    // Floor price: KASI per 1 DIAMOND (scaled by 1e18)
    // Default: 100 KASI per 1 DIAMOND = 100 * 1e18
    uint256 public floorPrice;

    // Legacy stats (backward compatibility)
    mapping(address => uint256) public totalReceived;
    uint256 public totalDistributed;
    uint256 public totalUsers;

    // Events
    event LiquidityAdded(address indexed provider, uint256 diamondAmount, uint256 kasiAmount, uint256 sharesMinted);
    event LiquidityRemoved(address indexed provider, uint256 diamondAmount, uint256 kasiAmount, uint256 sharesBurned);
    event SwapDiamondForKasi(address indexed user, uint256 diamondIn, uint256 kasiOut);
    event SwapKasiForDiamond(address indexed user, uint256 kasiIn, uint256 diamondOut);
    event FloorPriceUpdated(uint256 oldPrice, uint256 newPrice);
    event FaucetFunded(address indexed funder, uint256 amount);
    event FaucetDrained(address indexed owner, uint256 amount);
    event FaucetPaused(address indexed by);
    event FaucetUnpaused(address indexed by);

    uint256 internal constant DECIMALS = 1e18;
    uint256 internal constant DEFAULT_FLOOR = 100 * DECIMALS; // 100 KASI per 1 DIAMOND

    /**
     * @dev Constructor
     * @param _diamondToken Address of the DiamondToken contract
     * @param _gameToken Address of the GameToken contract
     * @param _initialOwner Address that will own the faucet
     */
    constructor(address _diamondToken, address _gameToken, address _initialOwner) Ownable(_initialOwner) {
        require(_diamondToken != address(0), "Faucet: Invalid diamond token address");
        require(_gameToken != address(0), "Faucet: Invalid game token address");
        require(_initialOwner != address(0), "Faucet: Invalid owner address");

        diamondToken = DiamondToken(_diamondToken);
        gameToken = GameToken(_gameToken);
        floorPrice = DEFAULT_FLOOR;
    }

    // ========================
    // Liquidity Management
    // ========================

    /**
     * @notice Add liquidity by depositing DIAMOND and KASI tokens
     * @param diamondAmount Amount of DIAMOND to deposit (must be approved)
     * @param kasiAmount Amount of KASI to deposit (must be approved)
     * @dev Mints LP shares proportional to provided liquidity
     */
    function addLiquidity(uint256 diamondAmount, uint256 kasiAmount) external nonReentrant whenNotPaused {
        require(diamondAmount > 0, "Faucet: DIAMOND amount must be > 0");
        require(kasiAmount > 0, "Faucet: KASI amount must be > 0");

        // Transfer tokens from sender
        require(
            diamondToken.transferFrom(msg.sender, address(this), diamondAmount),
            "Faucet: DIAMOND transfer failed"
        );
        require(gameToken.transferFrom(msg.sender, address(this), kasiAmount), "Faucet: KASI transfer failed");

        uint256 sharesToMint;

        if (totalLpShares == 0) {
            // Initial liquidity: mint sqrt(diamond * kasi) shares
            sharesToMint = _sqrt(diamondAmount * kasiAmount);
            require(sharesToMint > 0, "Faucet: Insufficient liquidity for shares");
        } else {
            // Proportional minting based on reserves
            uint256 shareFromDiamond = (diamondAmount * totalLpShares) / diamondReserves;
            uint256 shareFromKasi = (kasiAmount * totalLpShares) / kasiReserves;
            // Mint the minimum to preserve ratio
            sharesToMint = shareFromDiamond < shareFromKasi ? shareFromDiamond : shareFromKasi;
            require(sharesToMint > 0, "Faucet: Insufficient share amount");
        }

        // Update reserves and LP accounting
        diamondReserves += diamondAmount;
        kasiReserves += kasiAmount;
        totalLpShares += sharesToMint;
        lpShares[msg.sender] += sharesToMint;

        emit LiquidityAdded(msg.sender, diamondAmount, kasiAmount, sharesToMint);
    }

    /**
     * @notice Remove liquidity by burning LP shares
     * @param shares Amount of LP shares to burn
     */
    function removeLiquidity(uint256 shares) external nonReentrant {
        require(shares > 0, "Faucet: Shares must be > 0");
        require(lpShares[msg.sender] >= shares, "Faucet: Insufficient shares");
        require(totalLpShares > 0, "Faucet: No liquidity");

        uint256 diamondOut = (shares * diamondReserves) / totalLpShares;
        uint256 kasiOut = (shares * kasiReserves) / totalLpShares;

        // Update state first (CEI pattern)
        lpShares[msg.sender] -= shares;
        totalLpShares -= shares;
        diamondReserves -= diamondOut;
        kasiReserves -= kasiOut;

        // Transfer tokens
        require(diamondToken.transfer(msg.sender, diamondOut), "Faucet: DIAMOND transfer failed");
        require(gameToken.transfer(msg.sender, kasiOut), "Faucet: KASI transfer failed");

        emit LiquidityRemoved(msg.sender, diamondOut, kasiOut, shares);
    }

    // ========================
    // Swaps
    // ========================

    /**
     * @notice Swap DIAMOND for KASI using constant product formula
     * @param diamondIn Amount of DIAMOND to swap (must be approved)
     * @param minKasiOut Minimum acceptable KASI out (slippage protection)
     * @dev Enforces floor price: (kasiOut * 1e18) / diamondIn >= floorPrice
     */
    function swapDiamondForKasi(uint256 diamondIn, uint256 minKasiOut) external nonReentrant whenNotPaused {
        require(diamondIn > 0, "Faucet: DIAMOND amount must be > 0");
        require(kasiReserves > 0 && diamondReserves > 0, "Faucet: Insufficient liquidity");

        // Transfer DIAMOND from sender
        require(
            diamondToken.transferFrom(msg.sender, address(this), diamondIn),
            "Faucet: DIAMOND transferFrom failed"
        );

        // Constant product: amountOut = y - (k / (x + dx))
        uint256 k = diamondReserves * kasiReserves;
        uint256 newDiamond = diamondReserves + diamondIn;
        uint256 newKasiReserve = k / newDiamond;
        uint256 kasiOut = kasiReserves - newKasiReserve;

        require(kasiOut > 0, "Faucet: KasiOut == 0");
        require(kasiOut >= minKasiOut, "Faucet: Slippage exceeded");

        // Enforce floor price: (kasiOut * 1e18) / diamondIn >= floorPrice
        uint256 effectivePrice = (kasiOut * DECIMALS) / diamondIn;
        require(effectivePrice >= floorPrice, "Faucet: Price below floor");

        // Update reserves
        diamondReserves = newDiamond;
        kasiReserves = newKasiReserve;

        // Transfer KASI to user
        require(gameToken.transfer(msg.sender, kasiOut), "Faucet: KASI transfer failed");

        // Track stats (backward compatibility)
        if (totalReceived[msg.sender] == 0) {
            totalUsers++;
        }
        totalReceived[msg.sender] += kasiOut;
        totalDistributed += kasiOut;

        emit SwapDiamondForKasi(msg.sender, diamondIn, kasiOut);
    }

    /**
     * @notice Swap KASI for DIAMOND using constant product formula
     * @param kasiIn Amount of KASI to send (must be approved)
     * @param minDiamondOut Minimum acceptable DIAMOND out (slippage protection)
     */
    function swapKasiForDiamond(uint256 kasiIn, uint256 minDiamondOut) external nonReentrant whenNotPaused {
        require(kasiIn > 0, "Faucet: KASI amount must be > 0");
        require(kasiReserves > 0 && diamondReserves > 0, "Faucet: Insufficient liquidity");

        // Transfer KASI from sender
        require(gameToken.transferFrom(msg.sender, address(this), kasiIn), "Faucet: KASI transferFrom failed");

        // Constant product: diamondOut = x - (k / (y + dy))
        uint256 k = diamondReserves * kasiReserves;
        uint256 newKasi = kasiReserves + kasiIn;
        uint256 newDiamondReserve = k / newKasi;
        uint256 diamondOut = diamondReserves - newDiamondReserve;

        require(diamondOut > 0, "Faucet: DiamondOut == 0");
        require(diamondOut >= minDiamondOut, "Faucet: Slippage exceeded");

        // Update reserves
        kasiReserves = newKasi;
        diamondReserves = newDiamondReserve;

        // Transfer DIAMOND to user
        require(diamondToken.transfer(msg.sender, diamondOut), "Faucet: DIAMOND transfer failed");

        emit SwapKasiForDiamond(msg.sender, kasiIn, diamondOut);
    }

    // ========================
    // Owner Functions
    // ========================

    /**
     * @notice Update the floor price (KASI per 1 DIAMOND, scaled by 1e18)
     * @param newFloor New floor price (e.g., 100e18 for 100 KASI per DIAMOND)
     */
    function setFloorPrice(uint256 newFloor) external onlyOwner {
        require(newFloor > 0, "Faucet: Invalid floor");
        uint256 old = floorPrice;
        floorPrice = newFloor;
        emit FloorPriceUpdated(old, newFloor);
    }

    /**
     * @notice Fund faucet with KASI (legacy compatibility, adds to reserves)
     * @param amount Amount of KASI to fund
     */
    function fundFaucet(uint256 amount) external {
        require(amount > 0, "Faucet: Amount must be > 0");
        require(gameToken.transferFrom(msg.sender, address(this), amount), "Faucet: TransferFrom failed");
        kasiReserves += amount;
        emit FaucetFunded(msg.sender, amount);
    }

    /**
     * @notice Drain KASI from reserves to owner
     * @param amount Amount of KASI to drain
     */
    function drainFaucet(uint256 amount) external onlyOwner {
        require(amount > 0, "Faucet: Amount must be > 0");
        require(kasiReserves >= amount, "Faucet: Insufficient KASI reserves");
        kasiReserves -= amount;
        require(gameToken.transfer(owner(), amount), "Faucet: Transfer failed");
        emit FaucetDrained(owner(), amount);
    }

    /**
     * @notice Pause swaps and liquidity operations
     */
    function pause() external onlyOwner {
        _pause();
        emit FaucetPaused(msg.sender);
    }

    /**
     * @notice Unpause swaps and liquidity operations
     */
    function unpause() external onlyOwner {
        _unpause();
        emit FaucetUnpaused(msg.sender);
    }

    /**
     * @notice Recover ERC20 tokens accidentally sent (except DIAMOND and KASI)
     * @param tokenAddress Address of the token to recover
     * @param amount Amount to recover
     */
    function recoverTokens(address tokenAddress, uint256 amount) external onlyOwner {
        require(tokenAddress != address(gameToken), "Faucet: Cannot recover KASI");
        require(tokenAddress != address(diamondToken), "Faucet: Cannot recover DIAMOND");
        require(tokenAddress != address(0), "Faucet: Invalid token address");
        IERC20(tokenAddress).transfer(owner(), amount);
    }

    // ========================
    // View Functions
    // ========================

    /**
     * @notice Get current reserves
     * @return diamondReserve Amount of DIAMOND in reserves
     * @return kasiReserve Amount of KASI in reserves
     */
    function getReserves() external view returns (uint256 diamondReserve, uint256 kasiReserve) {
        return (diamondReserves, kasiReserves);
    }

    /**
     * @notice Get LP share balance for an account
     * @param account Address to check
     * @return LP share balance
     */
    function lpBalanceOf(address account) external view returns (uint256) {
        return lpShares[account];
    }

    /**
     * @notice Get user info (legacy compatibility)
     * @param user Address of the user
     * @return totalReceivedAmount Total KASI received via swaps
     */
    function getUserInfo(address user) external view returns (uint256 totalReceivedAmount) {
        totalReceivedAmount = totalReceived[user];
    }

    /**
     * @notice Get faucet statistics (legacy compatibility)
     * @return tokenBalance Current KASI balance (reserve + any extra)
     * @return totalDistributedAmount Total KASI distributed via swaps
     * @return totalUsersCount Total users who swapped
     */
    function getFaucetInfo()
        external
        view
        returns (
            uint256 tokenBalance,
            uint256 totalDistributedAmount,
            uint256 totalUsersCount
        )
    {
        tokenBalance = gameToken.balanceOf(address(this));
        totalDistributedAmount = totalDistributed;
        totalUsersCount = totalUsers;
    }

    // ========================
    // Internal Helpers
    // ========================

    /**
     * @dev Babylonian square root method
     */
    function _sqrt(uint256 y) internal pure returns (uint256 z) {
        if (y == 0) return 0;
        uint256 x = y / 2 + 1;
        z = y;
        while (x < z) {
            z = x;
            x = (y / x + x) / 2;
        }
    }
}
 