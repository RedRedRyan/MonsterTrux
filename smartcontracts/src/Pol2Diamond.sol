// SPDX-License-Identifier: UNLICENSED
pragma solidity ^0.8.13;

import "@openzeppelin/contracts/access/Ownable.sol";
import "@openzeppelin/contracts/utils/ReentrancyGuard.sol";
import "@openzeppelin/contracts/utils/Pausable.sol";
import "./DiamondToken.sol";

/**
 * @title PolDiamondPool
 * @dev Constant-product AMM for POL (native) <-> DIAMOND swaps with LP shares
 *
 * Features:
 * - Constant product formula: x * y = k
 * - Native POL handling (no WPOL wrapper needed)
 * - Floor price enforcement: 1 DIAMOND = minimum POL
 * - LP shares minted/burned on add/remove liquidity
 * - Owner can pause/unpause, set floor price
 *
 * NOTE: This pool handles native POL (MATIC) and DIAMOND (ERC20)
 */
contract PolDiamondPool is Ownable, ReentrancyGuard, Pausable {
    DiamondToken public immutable diamondToken;

    // AMM reserves
    uint256 public polReserves;      // Native POL (wei)
    uint256 public diamondReserves;  // DIAMOND token (wei)

    // LP shares
    uint256 public totalLpShares;
    mapping(address => uint256) public lpShares;

    // Floor price: POL per 1 DIAMOND (scaled by 1e18)
    // Example: 0.1 POL per 1 DIAMOND = 0.1 * 1e18 = 1e17
    uint256 public floorPrice;

    // Stats
    uint256 public totalSwaps;
    uint256 public totalVolumePol;
    uint256 public totalVolumeDiamond;

    // Events
    event LiquidityAdded(
        address indexed provider,
        uint256 polAmount,
        uint256 diamondAmount,
        uint256 sharesMinted
    );
    event LiquidityRemoved(
        address indexed provider,
        uint256 polAmount,
        uint256 diamondAmount,
        uint256 sharesBurned
    );
    event SwapPolForDiamond(
        address indexed user,
        uint256 polIn,
        uint256 diamondOut
    );
    event SwapDiamondForPol(
        address indexed user,
        uint256 diamondIn,
        uint256 polOut
    );
    event FloorPriceUpdated(uint256 oldPrice, uint256 newPrice);
    event PoolPaused(address indexed by);
    event PoolUnpaused(address indexed by);

    uint256 internal constant DECIMALS = 1e18;
    uint256 internal constant DEFAULT_FLOOR = 1e17; // 0.1 POL per 1 DIAMOND

    /**
     * @dev Constructor
     * @param _diamondToken Address of the DiamondToken contract
     * @param _initialOwner Address that will own the pool
     */
    constructor(
        address _diamondToken,
        address _initialOwner
    ) Ownable(_initialOwner) {
        require(_diamondToken != address(0), "Pool: Invalid diamond token");
        require(_initialOwner != address(0), "Pool: Invalid owner");

        diamondToken = DiamondToken(_diamondToken);
        floorPrice = DEFAULT_FLOOR;
    }

    // Allow contract to receive POL
    receive() external payable {}

    // ========================
    // Liquidity Management
    // ========================

    /**
     * @notice Add liquidity by depositing POL and DIAMOND tokens
     * @param diamondAmount Amount of DIAMOND to deposit (must be approved)
     * @dev Send POL as msg.value. Mints LP shares proportional to liquidity
     */
    function addLiquidity(uint256 diamondAmount)
        external
        payable
        nonReentrant
        whenNotPaused
    {
        require(msg.value > 0, "Pool: POL amount must be > 0");
        require(diamondAmount > 0, "Pool: DIAMOND amount must be > 0");

        // Transfer DIAMOND from sender
        require(
            diamondToken.transferFrom(msg.sender, address(this), diamondAmount),
            "Pool: DIAMOND transfer failed"
        );

        uint256 sharesToMint;

        if (totalLpShares == 0) {
            // Initial liquidity: mint sqrt(pol * diamond) shares
            sharesToMint = _sqrt(msg.value * diamondAmount);
            require(sharesToMint > 0, "Pool: Insufficient liquidity");
        } else {
            // Proportional minting based on reserves
            uint256 shareFromPol = (msg.value * totalLpShares) / polReserves;
            uint256 shareFromDiamond = (diamondAmount * totalLpShares) / diamondReserves;
            // Mint the minimum to preserve ratio
            sharesToMint = shareFromPol < shareFromDiamond
                ? shareFromPol
                : shareFromDiamond;
            require(sharesToMint > 0, "Pool: Insufficient share amount");
        }

        // Update reserves and LP accounting
        polReserves += msg.value;
        diamondReserves += diamondAmount;
        totalLpShares += sharesToMint;
        lpShares[msg.sender] += sharesToMint;

        emit LiquidityAdded(msg.sender, msg.value, diamondAmount, sharesToMint);
    }

    /**
     * @notice Remove liquidity by burning LP shares
     * @param shares Amount of LP shares to burn
     */
    function removeLiquidity(uint256 shares) external nonReentrant {
        require(shares > 0, "Pool: Shares must be > 0");
        require(lpShares[msg.sender] >= shares, "Pool: Insufficient shares");
        require(totalLpShares > 0, "Pool: No liquidity");

        uint256 polOut = (shares * polReserves) / totalLpShares;
        uint256 diamondOut = (shares * diamondReserves) / totalLpShares;

        // Update state first (CEI pattern)
        lpShares[msg.sender] -= shares;
        totalLpShares -= shares;
        polReserves -= polOut;
        diamondReserves -= diamondOut;

        // Transfer tokens
        require(
            diamondToken.transfer(msg.sender, diamondOut),
            "Pool: DIAMOND transfer failed"
        );
        (bool success, ) = payable(msg.sender).call{value: polOut}("");
        require(success, "Pool: POL transfer failed");

        emit LiquidityRemoved(msg.sender, polOut, diamondOut, shares);
    }

    // ========================
    // Swaps
    // ========================

    /**
     * @notice Swap POL for DIAMOND using constant product formula
     * @param minDiamondOut Minimum acceptable DIAMOND out (slippage protection)
     * @dev Send POL as msg.value
     */
    function swapPolForDiamond(uint256 minDiamondOut)
        external
        payable
        nonReentrant
        whenNotPaused
    {
        require(msg.value > 0, "Pool: POL amount must be > 0");
        require(
            polReserves > 0 && diamondReserves > 0,
            "Pool: Insufficient liquidity"
        );

        // Constant product: diamondOut = y - (k / (x + dx))
        uint256 k = polReserves * diamondReserves;
        uint256 newPol = polReserves + msg.value;
        uint256 newDiamondReserve = k / newPol;
        uint256 diamondOut = diamondReserves - newDiamondReserve;

        require(diamondOut > 0, "Pool: DiamondOut == 0");
        require(diamondOut >= minDiamondOut, "Pool: Slippage exceeded");

        // Update reserves
        polReserves = newPol;
        diamondReserves = newDiamondReserve;

        // Update stats
        totalSwaps++;
        totalVolumePol += msg.value;
        totalVolumeDiamond += diamondOut;

        // Transfer DIAMOND to user
        require(
            diamondToken.transfer(msg.sender, diamondOut),
            "Pool: DIAMOND transfer failed"
        );

        emit SwapPolForDiamond(msg.sender, msg.value, diamondOut);
    }

    /**
     * @notice Swap DIAMOND for POL using constant product formula
     * @param diamondIn Amount of DIAMOND to swap (must be approved)
     * @param minPolOut Minimum acceptable POL out (slippage protection)
     * @dev Enforces floor price: (polOut * 1e18) / diamondIn >= floorPrice
     */
    function swapDiamondForPol(uint256 diamondIn, uint256 minPolOut)
        external
        nonReentrant
        whenNotPaused
    {
        require(diamondIn > 0, "Pool: DIAMOND amount must be > 0");
        require(
            polReserves > 0 && diamondReserves > 0,
            "Pool: Insufficient liquidity"
        );

        // Transfer DIAMOND from sender
        require(
            diamondToken.transferFrom(msg.sender, address(this), diamondIn),
            "Pool: DIAMOND transfer failed"
        );

        // Constant product: polOut = x - (k / (y + dy))
        uint256 k = polReserves * diamondReserves;
        uint256 newDiamond = diamondReserves + diamondIn;
        uint256 newPolReserve = k / newDiamond;
        uint256 polOut = polReserves - newPolReserve;

        require(polOut > 0, "Pool: PolOut == 0");
        require(polOut >= minPolOut, "Pool: Slippage exceeded");

        // Enforce floor price: (polOut * 1e18) / diamondIn >= floorPrice
        uint256 effectivePrice = (polOut * DECIMALS) / diamondIn;
        require(effectivePrice >= floorPrice, "Pool: Price below floor");

        // Update reserves
        polReserves = newPolReserve;
        diamondReserves = newDiamond;

        // Update stats
        totalSwaps++;
        totalVolumePol += polOut;
        totalVolumeDiamond += diamondIn;

        // Transfer POL to user
        (bool success, ) = payable(msg.sender).call{value: polOut}("");
        require(success, "Pool: POL transfer failed");

        emit SwapDiamondForPol(msg.sender, diamondIn, polOut);
    }

    // ========================
    // Price Queries
    // ========================

    /**
     * @notice Get amount of DIAMOND out for a given POL input
     * @param polIn Amount of POL to swap
     * @return diamondOut Amount of DIAMOND that would be received
     */
    function getPolToDiamondQuote(uint256 polIn)
        external
        view
        returns (uint256 diamondOut)
    {
        require(polReserves > 0 && diamondReserves > 0, "Pool: No liquidity");
        uint256 k = polReserves * diamondReserves;
        uint256 newPol = polReserves + polIn;
        uint256 newDiamondReserve = k / newPol;
        diamondOut = diamondReserves - newDiamondReserve;
    }

    /**
     * @notice Get amount of POL out for a given DIAMOND input
     * @param diamondIn Amount of DIAMOND to swap
     * @return polOut Amount of POL that would be received
     */
    function getDiamondToPolQuote(uint256 diamondIn)
        external
        view
        returns (uint256 polOut)
    {
        require(polReserves > 0 && diamondReserves > 0, "Pool: No liquidity");
        uint256 k = polReserves * diamondReserves;
        uint256 newDiamond = diamondReserves + diamondIn;
        uint256 newPolReserve = k / newDiamond;
        polOut = polReserves - newPolReserve;
    }

    /**
     * @notice Get current spot price (POL per 1 DIAMOND)
     * @return price POL per DIAMOND (scaled by 1e18)
     */
    function getCurrentPrice() external view returns (uint256 price) {
        require(diamondReserves > 0, "Pool: No liquidity");
        price = (polReserves * DECIMALS) / diamondReserves;
    }

    // ========================
    // Owner Functions
    // ========================

    /**
     * @notice Update the floor price (POL per 1 DIAMOND, scaled by 1e18)
     * @param newFloor New floor price (e.g., 1e17 for 0.1 POL per DIAMOND)
     */
    function setFloorPrice(uint256 newFloor) external onlyOwner {
        require(newFloor > 0, "Pool: Invalid floor");
        uint256 old = floorPrice;
        floorPrice = newFloor;
        emit FloorPriceUpdated(old, newFloor);
    }

    /**
     * @notice Pause swaps and liquidity operations
     */
    function pause() external onlyOwner {
        _pause();
        emit PoolPaused(msg.sender);
    }

    /**
     * @notice Unpause swaps and liquidity operations
     */
    function unpause() external onlyOwner {
        _unpause();
        emit PoolUnpaused(msg.sender);
    }

    /**
     * @notice Emergency withdraw POL (owner only, for emergencies)
     * @param amount Amount of POL to withdraw
     */
    function emergencyWithdrawPol(uint256 amount) external onlyOwner {
        require(amount <= address(this).balance, "Pool: Insufficient balance");
        (bool success, ) = payable(owner()).call{value: amount}("");
        require(success, "Pool: Transfer failed");
    }

    /**
     * @notice Emergency withdraw DIAMOND (owner only, for emergencies)
     * @param amount Amount of DIAMOND to withdraw
     */
    function emergencyWithdrawDiamond(uint256 amount) external onlyOwner {
        require(
            diamondToken.transfer(owner(), amount),
            "Pool: Transfer failed"
        );
    }

    // ========================
    // View Functions
    // ========================

    /**
     * @notice Get current reserves
     * @return polReserve Amount of POL in reserves
     * @return diamondReserve Amount of DIAMOND in reserves
     */
    function getReserves()
        external
        view
        returns (uint256 polReserve, uint256 diamondReserve)
    {
        return (polReserves, diamondReserves);
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
     * @notice Get pool statistics
     * @return polBalance Current POL balance
     * @return diamondBalance Current DIAMOND balance
     * @return totalShares Total LP shares
     * @return swapCount Total number of swaps
     * @return volumePol Total POL volume traded
     * @return volumeDiamond Total DIAMOND volume traded
     */
    function getPoolStats()
        external
        view
        returns (
            uint256 polBalance,
            uint256 diamondBalance,
            uint256 totalShares,
            uint256 swapCount,
            uint256 volumePol,
            uint256 volumeDiamond
        )
    {
        return (
            address(this).balance,
            diamondToken.balanceOf(address(this)),
            totalLpShares,
            totalSwaps,
            totalVolumePol,
            totalVolumeDiamond
        );
    }

    /**
     * @notice Calculate how much liquidity a user can withdraw
     * @param account Address to check
     * @return polAmount POL that would be received
     * @return diamondAmount DIAMOND that would be received
     */
    function calculateWithdrawal(address account)
        external
        view
        returns (uint256 polAmount, uint256 diamondAmount)
    {
        if (totalLpShares == 0) return (0, 0);
        uint256 shares = lpShares[account];
        polAmount = (shares * polReserves) / totalLpShares;
        diamondAmount = (shares * diamondReserves) / totalLpShares;
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