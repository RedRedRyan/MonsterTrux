// SPDX-License-Identifier: UNLICENSED
pragma solidity ^0.8.13;

import {Test} from "forge-std/Test.sol";
import {GameToken} from "../src/GameToken.sol";

contract GameTokenTest is Test {
    GameToken public gameToken;
    address public owner;
    address public alice;
    address public bob;

    uint256 public constant MAX_SUPPLY = 100_000_000 * 10 ** 18;
    uint256 public constant INITIAL_SUPPLY = 10_000_000 * 10 ** 18;

    // Events to test
    event TokensMinted(address indexed to, uint256 amount);
    event ContractPaused(address indexed by);
    event ContractUnpaused(address indexed by);

    function setUp() public {
        owner = address(this);
        alice = makeAddr("alice");
        bob = makeAddr("bob");

        gameToken = new GameToken(owner);
    }

    // Constructor tests
    function test_Constructor() public {
        assertEq(gameToken.name(), "Kasi");
        assertEq(gameToken.symbol(), "SPEED");
        assertEq(gameToken.decimals(), 18);
        assertEq(gameToken.totalSupply(), INITIAL_SUPPLY);
        assertEq(gameToken.balanceOf(owner), INITIAL_SUPPLY);
        assertEq(gameToken.owner(), owner);
        assertEq(gameToken.MAX_SUPPLY(), MAX_SUPPLY);
    }

    function test_ConstructorWithZeroAddressFails() public {
        vm.expectRevert(abi.encodeWithSignature("OwnableInvalidOwner(address)", address(0)));
        new GameToken(address(0));
    }

    // Minting tests
    function test_Mint() public {
        uint256 mintAmount = 1000 * 10 ** 18;
        uint256 initialBalance = gameToken.balanceOf(alice);
        uint256 initialSupply = gameToken.totalSupply();

        vm.expectEmit(true, false, false, true);
        emit TokensMinted(alice, mintAmount);

        gameToken.mint(alice, mintAmount);

        assertEq(gameToken.balanceOf(alice), initialBalance + mintAmount);
        assertEq(gameToken.totalSupply(), initialSupply + mintAmount);
    }

    function test_MintOnlyOwner() public {
        uint256 mintAmount = 1000 * 10 ** 18;

        vm.prank(alice);
        vm.expectRevert(abi.encodeWithSignature("OwnableUnauthorizedAccount(address)", alice));
        gameToken.mint(bob, mintAmount);
    }

    function test_MintToZeroAddressFails() public {
        uint256 mintAmount = 1000 * 10 ** 18;

        vm.expectRevert("GameToken: Cannot mint to zero address");
        gameToken.mint(address(0), mintAmount);
    }

    function test_MintExceedsMaxSupplyFails() public {
        uint256 excessiveAmount = MAX_SUPPLY - gameToken.totalSupply() + 1;

        vm.expectRevert("GameToken: Would exceed max supply");
        gameToken.mint(alice, excessiveAmount);
    }

    function test_MintMaxSupplyExactly() public {
        uint256 remainingSupply = gameToken.remainingMintableSupply();

        gameToken.mint(alice, remainingSupply);

        assertEq(gameToken.totalSupply(), MAX_SUPPLY);
        assertEq(gameToken.remainingMintableSupply(), 0);
    }

    // Pause functionality tests
    function test_Pause() public {
        vm.expectEmit(true, false, false, false);
        emit ContractPaused(owner);

        gameToken.pause();

        assertTrue(gameToken.paused());
    }

    function test_Unpause() public {
        gameToken.pause();
        assertTrue(gameToken.paused());

        vm.expectEmit(true, false, false, false);
        emit ContractUnpaused(owner);

        gameToken.unpause();

        assertFalse(gameToken.paused());
    }

    function test_PauseOnlyOwner() public {
        vm.prank(alice);
        vm.expectRevert(abi.encodeWithSignature("OwnableUnauthorizedAccount(address)", alice));
        gameToken.pause();
    }

    function test_UnpauseOnlyOwner() public {
        gameToken.pause();

        vm.prank(alice);
        vm.expectRevert(abi.encodeWithSignature("OwnableUnauthorizedAccount(address)", alice));
        gameToken.unpause();
    }

    function test_TransferWhenPausedFails() public {
        uint256 transferAmount = 1000 * 10 ** 18;

        // First transfer tokens to alice
        gameToken.transfer(alice, transferAmount);

        // Pause the contract
        gameToken.pause();

        // Try to transfer when paused
        vm.prank(alice);
        vm.expectRevert("GameToken: Token transfers are paused");
        gameToken.transfer(bob, transferAmount);
    }

    function test_TransferAfterUnpause() public {
        uint256 transferAmount = 1000 * 10 ** 18;

        // Transfer tokens to alice
        gameToken.transfer(alice, transferAmount);

        // Pause and unpause
        gameToken.pause();
        gameToken.unpause();

        // Should work after unpause
        vm.prank(alice);
        gameToken.transfer(bob, transferAmount);

        assertEq(gameToken.balanceOf(bob), transferAmount);
    }

    // Burning tests
    function test_Burn() public {
        uint256 burnAmount = 1000 * 10 ** 18;
        uint256 initialBalance = gameToken.balanceOf(owner);
        uint256 initialSupply = gameToken.totalSupply();

        gameToken.burn(burnAmount);

        assertEq(gameToken.balanceOf(owner), initialBalance - burnAmount);
        assertEq(gameToken.totalSupply(), initialSupply - burnAmount);
    }

    function test_BurnFrom() public {
        uint256 burnAmount = 1000 * 10 ** 18;

        // Transfer tokens to alice and approve bob to burn them
        gameToken.transfer(alice, burnAmount);
        vm.prank(alice);
        gameToken.approve(bob, burnAmount);

        uint256 initialAliceBalance = gameToken.balanceOf(alice);
        uint256 initialSupply = gameToken.totalSupply();

        vm.prank(bob);
        gameToken.burnFrom(alice, burnAmount);

        assertEq(gameToken.balanceOf(alice), initialAliceBalance - burnAmount);
        assertEq(gameToken.totalSupply(), initialSupply - burnAmount);
    }

    // View function tests
    function test_RemainingMintableSupply() public {
        uint256 expected = MAX_SUPPLY - gameToken.totalSupply();
        assertEq(gameToken.remainingMintableSupply(), expected);

        // After minting, remaining should decrease
        uint256 mintAmount = 1000 * 10 ** 18;
        gameToken.mint(alice, mintAmount);

        assertEq(gameToken.remainingMintableSupply(), expected - mintAmount);
    }

    function test_CanReceiveTokens() public {
        assertTrue(gameToken.canReceiveTokens(alice));
        assertFalse(gameToken.canReceiveTokens(address(0)));

        // Should return false when paused
        gameToken.pause();
        assertFalse(gameToken.canReceiveTokens(alice));

        // Should return true when unpaused
        gameToken.unpause();
        assertTrue(gameToken.canReceiveTokens(alice));
    }

    // Standard ERC20 functionality tests
    function test_Transfer() public {
        uint256 transferAmount = 1000 * 10 ** 18;
        uint256 initialOwnerBalance = gameToken.balanceOf(owner);

        gameToken.transfer(alice, transferAmount);

        assertEq(gameToken.balanceOf(owner), initialOwnerBalance - transferAmount);
        assertEq(gameToken.balanceOf(alice), transferAmount);
    }

    function test_TransferFrom() public {
        uint256 transferAmount = 1000 * 10 ** 18;

        // Approve alice to spend owner's tokens
        gameToken.approve(alice, transferAmount);

        uint256 initialOwnerBalance = gameToken.balanceOf(owner);

        vm.prank(alice);
        gameToken.transferFrom(owner, bob, transferAmount);

        assertEq(gameToken.balanceOf(owner), initialOwnerBalance - transferAmount);
        assertEq(gameToken.balanceOf(bob), transferAmount);
        assertEq(gameToken.allowance(owner, alice), 0);
    }

    function test_Approve() public {
        uint256 approveAmount = 1000 * 10 ** 18;

        gameToken.approve(alice, approveAmount);

        assertEq(gameToken.allowance(owner, alice), approveAmount);
    }

    // Fuzz tests
    function testFuzz_Mint(uint256 amount) public {
        // Bound the amount to valid range
        amount = bound(amount, 1, gameToken.remainingMintableSupply());

        uint256 initialSupply = gameToken.totalSupply();
        gameToken.mint(alice, amount);

        assertEq(gameToken.totalSupply(), initialSupply + amount);
        assertEq(gameToken.balanceOf(alice), amount);
    }

    function testFuzz_Transfer(uint256 amount) public {
        // Bound amount to owner's balance
        amount = bound(amount, 0, gameToken.balanceOf(owner));

        uint256 initialOwnerBalance = gameToken.balanceOf(owner);

        gameToken.transfer(alice, amount);

        assertEq(gameToken.balanceOf(owner), initialOwnerBalance - amount);
        assertEq(gameToken.balanceOf(alice), amount);
    }

    function testFuzz_Burn(uint256 amount) public {
        // Bound amount to owner's balance
        amount = bound(amount, 0, gameToken.balanceOf(owner));

        uint256 initialSupply = gameToken.totalSupply();
        uint256 initialBalance = gameToken.balanceOf(owner);

        gameToken.burn(amount);

        assertEq(gameToken.totalSupply(), initialSupply - amount);
        assertEq(gameToken.balanceOf(owner), initialBalance - amount);
    }
}
