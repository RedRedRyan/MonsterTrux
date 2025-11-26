// SPDX-License-Identifier: UNLICENSED
pragma solidity ^0.8.13;
import {Script} from "forge-std/Script.sol";
import {console} from "forge-std/console.sol";
import {Faucet} from "../src/Faucet.sol";
import {GameToken} from "../src/GameToken.sol";
/**
 * @title FaucetAMMDeploy
 * @dev Deployment script for Faucet AMM contract
 * 
 * Required environment variables:
 * - DIAMOND_TOKEN_ADDRESS: Address of the deployed DiamondToken contract
 * - GAME_TOKEN_ADDRESS: Address of the deployed GameToken (KASI) contract
 * - FAUCET_OWNER_ADDRESS: Address that will own the Faucet contract
 * - DEPLOYER_PRIVATE_KEY: Private key of the deployer account
 * 
 * Optional environment variables:
 * - INITIAL_KASI_FUNDING: Amount of KASI to fund the faucet with upon deployment (default: 10,000 KASI)
 * 
 * Usage:
 * DIAMOND_TOKEN_ADDRESS=0x... \
 * GAME_TOKEN_ADDRESS=0x... \
 * FAUCET_OWNER_ADDRESS=0x... \
 * INITIAL_KASI_FUNDING=10000 \
 * forge script script/DeployAMM.s.sol:FaucetAMMDeploy \
 *   --rpc-url $RPC_URL \
 *   --private-key $DEPLOYER_PRIVATE_KEY \
 *   --broadcast \
 *   --legacy
 */
 
contract FaucetAMMDeploy is Script {
    Faucet public faucet;
    GameToken public gameToken;

    uint256 public constant DEFAULT_INITIAL_KASI_FUNDING = 100_000 * 10**18; // 10,000 KASI

    function run() public {
        address diamondTokenAddress = vm.envAddress("DIAMOND_TOKEN_ADDRESS");
        address gameTokenAddress = vm.envAddress("GAME_TOKEN_ADDRESS");
        address faucetOwner = vm.envAddress("FAUCET_OWNER_ADDRESS");

        uint256 initialFunding = vm.envOr("INITIAL_KASI_FUNDING", DEFAULT_INITIAL_KASI_FUNDING);

        console.log("=== Faucet AMM Deployment ===");
        console.log("Diamond token:"); console.log(diamondTokenAddress);
        console.log("Game token:"); console.log(gameTokenAddress);
        console.log("Faucet owner:"); console.log(faucetOwner);
        console.log("");

        // Deploy Faucet contract using deployer's private key
        uint256 deployerKey = vm.envUint("DEPLOYER_PRIVATE_KEY");
        vm.startBroadcast(deployerKey);
        faucet = new Faucet(diamondTokenAddress, gameTokenAddress, faucetOwner);
        vm.stopBroadcast();

        console.log("Faucet deployed at:"); console.log(address(faucet));

        // Optionally fund faucet with KASI (requires deployer has KASI and approves)
        address deployer = vm.addr(deployerKey);
        gameToken = GameToken(gameTokenAddress);
        uint256 deployerBalance = gameToken.balanceOf(deployer);

        if (initialFunding > 0) {
            if (deployerBalance >= initialFunding) {
                console.log("Funding faucet with KASI:", initialFunding / 10**18);
                vm.startBroadcast(deployerKey);
                // approve and fund
                require(gameToken.approve(address(faucet), initialFunding), "approve failed");
                faucet.fundFaucet(initialFunding);
                vm.stopBroadcast();
                console.log("Faucet funded");
            } else {
                console.log("Warning: deployer has insufficient KASI to auto-fund faucet");
                console.log("Deployer balance:"); console.log(deployerBalance);
                console.log("Requested funding:"); console.log(initialFunding);
            }
        } else {
            console.log("Skipping initial funding (INITIAL_KASI_FUNDING=0)");
        }

        _logDeploymentSummary(deployer);
    }

    function _logDeploymentSummary(address deployer) internal view {
        console.log("");
        console.log("=== Deployment Summary ===");
        console.log("Faucet address:"); console.log(address(faucet));
        console.log("Owner:"); console.log(faucet.owner());
        (uint256 diamondReserve, uint256 kasiReserve) = faucet.getReserves();
        console.log("DIAMOND reserve:"); console.log(diamondReserve);
        console.log("KASI reserve:"); console.log(kasiReserve);
        console.log("Total LP shares for deployer:"); console.log(faucet.lpBalanceOf(deployer));
        console.log("");
        console.log("Next steps:");
        console.log("1) If faucet not funded, mint/approve KASI and call faucet.fundFaucet()");
        console.log("2) Add liquidity by calling faucet.addLiquidity(diamondAmount,kasiAmount) after approving tokens");
    }
}