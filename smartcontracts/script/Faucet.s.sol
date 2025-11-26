// SPDX-License-Identifier: UNLICENSED
pragma solidity ^0.8.13;

import {Script} from "forge-std/Script.sol";
import {console} from "forge-std/console.sol";
import {GameToken} from "../src/GameToken.sol";
import {Faucet} from "../src/Faucet.sol";

/**
 * @title FaucetDeploy
 * @dev Deployment script for Faucet contract
 */
contract FaucetDeploy is Script {
    Faucet public faucet;

    function setUp() public {}

    function run() public {
        // Get deployment parameters from environment
        address gameTokenAddress = vm.envAddress("GAME_TOKEN_ADDRESS");
        address faucetOwner = vm.envAddress("FAUCET_OWNER_ADDRESS");

        vm.startBroadcast();

        // Deploy Faucet
        faucet = new Faucet(gameTokenAddress, faucetOwner);

        console.log("Faucet deployed at:", address(faucet));
        console.log("GameToken address:", address(faucet.gameToken()));
        console.log("Owner:", faucet.owner());

        vm.stopBroadcast();
    }
}
