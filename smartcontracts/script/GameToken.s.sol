// SPDX-License-Identifier: UNLICENSED
pragma solidity ^0.8.13;

import {Script} from "forge-std/Script.sol";
import {console} from "forge-std/console.sol";
import {GameToken} from "../src/GameToken.sol";
import {Faucet} from "../src/Faucet.sol";

/**
 * @title GameTokenDeploy
 * @dev Deployment script for GameToken contract
 */
contract GameTokenDeploy is Script {
    GameToken public gameToken;

    function setUp() public {}

    function run() public {
        // Get the deployer's address
        address deployer = vm.envAddress("DEPLOYER_ADDRESS");

        vm.startBroadcast();

        // Deploy GameToken with deployer as initial owner
        gameToken = new GameToken(deployer);

        console.log("GameToken deployed at:", address(gameToken));
        console.log("Owner:", gameToken.owner());
        console.log("Name:", gameToken.name());
        console.log("Symbol:", gameToken.symbol());
        console.log("Total Supply:", gameToken.totalSupply());
        console.log("Max Supply:", gameToken.MAX_SUPPLY());

        vm.stopBroadcast();
    }
}
