// SPDX-License-Identifier: UNLICENSED
pragma solidity ^0.8.13;

import "@openzeppelin/contracts/token/ERC20/ERC20.sol";
import "@openzeppelin/contracts/token/ERC20/extensions/ERC20Burnable.sol";
import "@openzeppelin/contracts/access/Ownable.sol";
import "@openzeppelin/contracts/utils/Pausable.sol";

/**
 * @title GameToken
 * @dev Speed token (KASI) - The core ERC-20 token for the Polygon gaming ecosystem
 *
 * Features:
 * - Standard ERC-20 functionality
 * - Mintable by owner for rewards and game economics
 * - Burnable for deflationary mechanics
 * - Pausable for emergency situations
 * - Owner-controlled for gaming infrastructure
 */
contract GameToken is ERC20, ERC20Burnable, Ownable, Pausable {
    // Maximum total supply (100 million tokens)
    uint256 public constant MAX_SUPPLY = 100_000_000 * 10 ** 18;

    // Events
    event TokensMinted(address indexed to, uint256 amount);
    event ContractPaused(address indexed by);
    event ContractUnpaused(address indexed by);

    /**
     * @dev Constructor that sets up the token with initial parameters
     * @param initialOwner The address that will own the contract
     */
    constructor(address initialOwner) ERC20("Kasi", "SPEED") Ownable(initialOwner) {
        require(initialOwner != address(0), "GameToken: Invalid owner address");

        // Mint initial supply to owner (10% of max supply)
        uint256 initialSupply = MAX_SUPPLY / 10; // 10 million tokens
        _mint(initialOwner, initialSupply);

        emit TokensMinted(initialOwner, initialSupply);
    }

    /**
     * @dev Mint new tokens. Only owner can mint.
     * @param to Address to mint tokens to
     * @param amount Amount of tokens to mint
     */
    function mint(address to, uint256 amount) public onlyOwner {
        require(to != address(0), "GameToken: Cannot mint to zero address");
        require(totalSupply() + amount <= MAX_SUPPLY, "GameToken: Would exceed max supply");

        _mint(to, amount);
        emit TokensMinted(to, amount);
    }

    /**
     * @dev Pause the contract. Only owner can pause.
     */
    function pause() public onlyOwner {
        _pause();
        emit ContractPaused(msg.sender);
    }

    /**
     * @dev Unpause the contract. Only owner can unpause.
     */
    function unpause() public onlyOwner {
        _unpause();
        emit ContractUnpaused(msg.sender);
    }

    /**
     * @dev Override required by Solidity for multiple inheritance and pause functionality
     */
    function _update(address from, address to, uint256 value) internal virtual override {
        super._update(from, to, value);
        require(!paused(), "GameToken: Token transfers are paused");
    }

    /**
     * @dev Get remaining mintable supply
     * @return The amount of tokens that can still be minted
     */
    function remainingMintableSupply() public view returns (uint256) {
        return MAX_SUPPLY - totalSupply();
    }

    /**
     * @dev Check if an address can receive tokens (not zero address and contract is not paused)
     * @param recipient The address to check
     * @return True if the address can receive tokens
     */
    function canReceiveTokens(address recipient) public view returns (bool) {
        return recipient != address(0) && !paused();
    }
}
