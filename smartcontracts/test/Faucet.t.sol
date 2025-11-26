// SPDX-License-Identifier: UNLICENSED
pragma solidity ^0.8.13;

import {Test} from "forge-std/Test.sol";
import {GameToken} from "../src/GameToken.sol";
import {Faucet} from "../src/Faucet.sol";

contract FaucetTest is Test {
    GameToken public gameToken;
    Faucet public faucet;

    address public owner;
    address public alice;
    address public bob;
    address public charlie;

    uint256 public constant CLAIM_AMOUNT = 100 * 10 ** 18; // 100 tokens
    uint256 public constant COOLDOWN_PERIOD = 1 days;
    uint256 public constant MAX_CLAIMS_PER_USER = 5;
    uint256 public constant FAUCET_FUNDING = 10000 * 10 ** 18; // 10,000 tokens

    // Events to test
    event TokensDripped(address indexed user, uint256 amount, uint256 timestamp);
    event FaucetFunded(address indexed funder, uint256 amount);
    event FaucetDrained(address indexed owner, uint256 amount);
    event FaucetPaused(address indexed by);
    event FaucetUnpaused(address indexed by);

    function setUp() public {
        owner = address(this);
        alice = makeAddr("alice");
        bob = makeAddr("bob");
        charlie = makeAddr("charlie");

        // Deploy GameToken
        gameToken = new GameToken(owner);

    // Deploy Faucet (new constructor: gameToken, owner)
    faucet = new Faucet(address(gameToken), owner);

        // Fund the faucet
        gameToken.approve(address(faucet), FAUCET_FUNDING);
        faucet.fundFaucet(FAUCET_FUNDING);
    }

    // Constructor tests
    function test_Constructor() public {
        assertEq(address(faucet.gameToken()), address(gameToken));
        assertEq(faucet.owner(), owner);
        // Faucet refactor: claim parameters removed; check stats instead
        assertEq(faucet.totalDistributed(), 0);
        assertEq(faucet.totalUsers(), 0);
        assertEq(gameToken.balanceOf(address(faucet)), FAUCET_FUNDING);
    }

    function test_ConstructorWithInvalidParameters() public {
    // Invalid token address
    vm.expectRevert("Faucet: Invalid token address");
    new Faucet(address(0), owner);

    // Invalid owner address
    vm.expectRevert("Faucet: Invalid owner address");
    new Faucet(address(gameToken), address(0));
    }

    // Claim tokens tests
    function test_DripTokens() public {
        uint256 initialBalance = gameToken.balanceOf(alice);
        uint256 initialFaucetBalance = gameToken.balanceOf(address(faucet));

        vm.expectEmit(true, false, false, false);
        emit TokensDripped(alice, CLAIM_AMOUNT, block.timestamp);

        // Owner drips tokens to alice
        vm.prank(owner);
        faucet.dripTokens(alice, CLAIM_AMOUNT);

        assertEq(gameToken.balanceOf(alice), initialBalance + CLAIM_AMOUNT);
        assertEq(gameToken.balanceOf(address(faucet)), initialFaucetBalance - CLAIM_AMOUNT);
        assertEq(faucet.totalReceived(alice), CLAIM_AMOUNT);
        assertEq(faucet.totalDistributed(), CLAIM_AMOUNT);
        assertEq(faucet.totalUsers(), 1);
    }

    function test_DripTokensMultipleUsers() public {
        // Owner drips to Alice
        vm.prank(owner);
        faucet.dripTokens(alice, CLAIM_AMOUNT);

        // Owner drips to Bob
        vm.prank(owner);
        faucet.dripTokens(bob, CLAIM_AMOUNT);

        assertEq(gameToken.balanceOf(alice), CLAIM_AMOUNT);
        assertEq(gameToken.balanceOf(bob), CLAIM_AMOUNT);
        assertEq(faucet.totalDistributed(), CLAIM_AMOUNT * 2);
        assertEq(faucet.totalUsers(), 2);
    }

    // Cooldown / per-user limit removed in the refactor — drip model is owner-controlled.

    // Removed: per-user maximum claims.

    function test_DripTokensInsufficientFaucetBalance() public {
        // Drain most of the faucet
        faucet.drainFaucet(FAUCET_FUNDING - CLAIM_AMOUNT + 1);

        vm.prank(owner);
        vm.expectRevert("Faucet: Insufficient tokens in faucet");
        faucet.dripTokens(alice, CLAIM_AMOUNT);
    }

    function test_DripTokensWhenPaused() public {
        faucet.pause();

        vm.prank(owner);
        vm.expectRevert(abi.encodeWithSignature("EnforcedPause()"));
        faucet.dripTokens(alice, CLAIM_AMOUNT);
    }

    // Funding tests
    function test_FundFaucet() public {
        uint256 fundAmount = 1000 * 10 ** 18;
        uint256 initialBalance = gameToken.balanceOf(address(faucet));

        gameToken.approve(address(faucet), fundAmount);

    vm.expectEmit(true, false, false, true);
    emit FaucetFunded(owner, fundAmount);

    faucet.fundFaucet(fundAmount);

        assertEq(gameToken.balanceOf(address(faucet)), initialBalance + fundAmount);
    }

    function test_FundFaucetZeroAmount() public {
        vm.expectRevert("Faucet: Amount must be greater than 0");
        faucet.fundFaucet(0);
    }

    function test_FundFaucetInsufficientAllowance() public {
        uint256 fundAmount = 1000 * 10 ** 18;

        vm.expectRevert(
            abi.encodeWithSignature(
                "ERC20InsufficientAllowance(address,uint256,uint256)", address(faucet), 0, fundAmount
            )
        );
        faucet.fundFaucet(fundAmount);
    }

    // Drain faucet tests
    function test_DrainFaucet() public {
        uint256 drainAmount = 1000 * 10 ** 18;
        uint256 initialFaucetBalance = gameToken.balanceOf(address(faucet));
        uint256 initialOwnerBalance = gameToken.balanceOf(owner);

        vm.expectEmit(true, false, false, true);
        emit FaucetDrained(owner, drainAmount);

        faucet.drainFaucet(drainAmount);

        assertEq(gameToken.balanceOf(address(faucet)), initialFaucetBalance - drainAmount);
        assertEq(gameToken.balanceOf(owner), initialOwnerBalance + drainAmount);
    }

    function test_DrainFaucetOnlyOwner() public {
        vm.prank(alice);
        vm.expectRevert(abi.encodeWithSignature("OwnableUnauthorizedAccount(address)", alice));
        faucet.drainFaucet(1000 * 10 ** 18);
    }

    function test_DrainFaucetZeroAmount() public {
        vm.expectRevert("Faucet: Amount must be greater than 0");
        faucet.drainFaucet(0);
    }

    function test_DrainFaucetInsufficientBalance() public {
        uint256 excessiveAmount = gameToken.balanceOf(address(faucet)) + 1;

        vm.expectRevert("Faucet: Insufficient tokens in faucet");
        faucet.drainFaucet(excessiveAmount);
    }

    // Parameter update tests
    // updateParameters removed in refactor (owner directly controls drip behavior)

    // Pause/Unpause tests
    function test_Pause() public {
        vm.expectEmit(true, false, false, false);
        emit FaucetPaused(owner);

        faucet.pause();

        assertTrue(faucet.paused());
    }

    function test_Unpause() public {
        faucet.pause();

        vm.expectEmit(true, false, false, false);
        emit FaucetUnpaused(owner);

        faucet.unpause();

        assertFalse(faucet.paused());
    }

    function test_PauseOnlyOwner() public {
        vm.prank(alice);
        vm.expectRevert(abi.encodeWithSignature("OwnableUnauthorizedAccount(address)", alice));
        faucet.pause();
    }

    function test_UnpauseOnlyOwner() public {
        faucet.pause();

        vm.prank(alice);
        vm.expectRevert(abi.encodeWithSignature("OwnableUnauthorizedAccount(address)", alice));
        faucet.unpause();
    }

    // View function tests
    function test_GetUserInfo() public {
        // Before any drips
        (uint256 totalReceivedAmount) = faucet.getUserInfo(alice);

        assertEq(totalReceivedAmount, 0);

        // After one drip
        vm.prank(owner);
        faucet.dripTokens(alice, CLAIM_AMOUNT);

        (totalReceivedAmount) = faucet.getUserInfo(alice);
        assertEq(totalReceivedAmount, CLAIM_AMOUNT);
    }

    function test_GetFaucetInfo() public {
        (
            uint256 tokenBalance,
            uint256 totalDistributedAmount,
            uint256 totalUsersCount
        ) = faucet.getFaucetInfo();

        assertEq(tokenBalance, FAUCET_FUNDING);
        assertEq(totalDistributedAmount, 0);
        assertEq(totalUsersCount, 0);

        // After a drip
        vm.prank(owner);
        faucet.dripTokens(alice, CLAIM_AMOUNT);

        (tokenBalance, totalDistributedAmount, totalUsersCount) = faucet.getFaucetInfo();

        assertEq(tokenBalance, FAUCET_FUNDING - CLAIM_AMOUNT);
        assertEq(totalDistributedAmount, CLAIM_AMOUNT);
        assertEq(totalUsersCount, 1);
    }

    // Recovery function tests
    function test_RecoverTokens() public {
        // Deploy a different ERC20 token
        GameToken otherToken = new GameToken(owner);
        uint256 recoverAmount = 1000 * 10 ** 18;

        // Send some tokens to the faucet by mistake
        otherToken.transfer(address(faucet), recoverAmount);

        uint256 initialOwnerBalance = otherToken.balanceOf(owner);

        faucet.recoverTokens(address(otherToken), recoverAmount);

        assertEq(otherToken.balanceOf(owner), initialOwnerBalance + recoverAmount);
        assertEq(otherToken.balanceOf(address(faucet)), 0);
    }

    function test_RecoverTokensOnlyOwner() public {
        GameToken otherToken = new GameToken(owner);

        vm.prank(alice);
        vm.expectRevert(abi.encodeWithSignature("OwnableUnauthorizedAccount(address)", alice));
        faucet.recoverTokens(address(otherToken), 1000 * 10 ** 18);
    }

    function test_RecoverTokensCannotRecoverGameTokens() public {
        vm.expectRevert("Faucet: Cannot recover game tokens");
        faucet.recoverTokens(address(gameToken), 1000 * 10 ** 18);
    }

    function test_RecoverTokensInvalidAddress() public {
        vm.expectRevert("Faucet: Invalid token address");
        faucet.recoverTokens(address(0), 1000 * 10 ** 18);
    }

    // Fuzz tests
    function testFuzz_ClaimTokensMultipleTimes(uint8 claimTimes) public {
        claimTimes = uint8(bound(claimTimes, 1, MAX_CLAIMS_PER_USER));

        for (uint8 i = 0; i < claimTimes; i++) {
            vm.prank(owner);
            faucet.dripTokens(alice, CLAIM_AMOUNT);
            vm.warp(block.timestamp + COOLDOWN_PERIOD);
        }

        assertEq(faucet.totalReceived(alice), CLAIM_AMOUNT * claimTimes);
        assertEq(gameToken.balanceOf(alice), CLAIM_AMOUNT * claimTimes);
    }
    
}
