 #!/bin/bash
 cast send 0x73a0Ce2918B2771b7f10F61444c9D726bDCd8dea "addLiquidity(uint256,uint256)" 100y0000000000000000000  100000000000000000000000 --private-key $PRIVATEKEY --rpc-url https://rpc-amoy.polygon.technology  --legacy 
    echo "Liquidity Added"
    exit 0