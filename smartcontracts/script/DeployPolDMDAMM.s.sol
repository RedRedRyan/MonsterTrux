// SPDX-License-Identifier: UNLICENSED
pragma solidity ^0.8.13;

import {Script} from "forge-std/Script.sol";
import {DiamondToken} from "../src/DiamondToken.sol";
import {PolDiamondPool} from "../src/Pol2Diamond.sol";
import {console} from "forge-std/console.sol";

contract DeployPolDiamondPool is Script {
    function run() external {
        uint256 deployerPrivateKey = vm.envUint("PRIVATE_KEY");
        address deployer = vm.addr(deployerPrivateKey);
        
        vm.startBroadcast(deployerPrivateKey);
        
        // Deploy DIAMOND token
        DiamondToken diamond = new DiamondToken(deployer);
        console.log("DiamondToken deployed at:", address(diamond));
        
        // Deploy Pool
        PolDiamondPool pool = new PolDiamondPool(
            address(diamond),
            deployer
        );
        console.log("PolDiamondPool deployed at:", address(pool));
        
        // Optional: Set custom floor price
         pool.setFloorPrice(0.5 ether);
        
        vm.stopBroadcast();
    }
}
