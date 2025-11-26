// SPDX-License-Identifier: UNLICENSED
pragma solidity ^0.8.13;

import {Script} from "forge-std/Script.sol";
import {console} from "forge-std/console.sol";
import {DiamondToken} from "../src/DiamondToken.sol";

/**
 * @title DiamondTokenDeploy
 * @dev Deployment script for DiamondToken (DMD)
 * 
 * Required environment variables:
 * - DEPLOYER_ADDRESS: Address that will own the DiamondToken
 * 
 * Usage:
 * DEPLOYER_ADDRESS=0x... \
 * forge script script/DiamondToken.s.sol:DiamondTokenDeploy \
 *   --rpc-url $RPC_URL \
 *   --private-key $DEPLOYER_PRIVATE_KEY \
 *   --broadcast \
 *   --legacy
 */
contract DiamondTokenDeploy is Script {
    DiamondToken public diamondToken;

    function run() public {
        address deployer = vm.envAddress("DEPLOYER_ADDRESS");

        console.log("=== DiamondToken Deployment ===");
        console.log("Deployer:", deployer);
        console.log("");

        vm.startBroadcast();

        // Deploy DiamondToken
        diamondToken = new DiamondToken(deployer);

        console.log("DiamondToken deployed at:", address(diamondToken));

        vm.stopBroadcast();

        // Display summary
        _logDeploymentSummary();
    }

    function _logDeploymentSummary() internal view {
        console.log("");
        console.log("=== Deployment Summary ===");
        console.log("Contract Address:", address(diamondToken));
        console.log("Token Name:", diamondToken.name());
        console.log("Token Symbol:", diamondToken.symbol());
        console.log("Decimals:", diamondToken.decimals());
        console.log("Max Supply:", diamondToken.MAX_SUPPLY() / 10**18, "DMD");
        console.log("Initial Supply:", diamondToken.totalSupply() / 10**18, "DMD");
        console.log("Owner:", diamondToken.owner());
        console.log("Remaining Mintable:", diamondToken.remainingMintableSupply() / 10**18, "DMD");
        console.log("");
    }
}