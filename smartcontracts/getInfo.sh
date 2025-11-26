#!/bin/bash
# Get LP (Liquidity Provider) information for the Faucet AMM

set -e

# Configuration
FAUCET_AMM="${FAUCET_AMM:-0x73a0Ce2918B2771b7f10F61444c9D726bDCd8dea}"
RPC_URL="${RPC_URL:-https://rpc-amoy.polygon.technology}"
USER_ADDRESS="${USER_ADDRESS:-}"

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}   Faucet AMM - LP Information${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

# Get reserves
echo -e "${GREEN}📊 Pool Reserves:${NC}"
RESERVES=$(cast call $FAUCET_AMM "getReserves()(uint256,uint256)" --rpc-url $RPC_URL)
DIAMOND_RESERVE=$(echo $RESERVES | cut -d' ' -f1)
KASI_RESERVE=$(echo $RESERVES | cut -d' ' -f2)

echo "  DIAMOND Reserve: $(cast --to-unit $DIAMOND_RESERVE ether) DMD"
echo "  KASI Reserve:    $(cast --to-unit $KASI_RESERVE ether) KASI"
echo ""

# Get total LP shares
echo -e "${GREEN}💎 Total LP Shares:${NC}"
TOTAL_SHARES=$(cast call $FAUCET_AMM "totalLpShares()(uint256)" --rpc-url $RPC_URL)
echo "  Total Shares: $(cast --to-unit $TOTAL_SHARES ether)"
echo ""

# Get user LP shares if address provided
if [ -n "$USER_ADDRESS" ]; then
    echo -e "${GREEN}👤 Your LP Position:${NC}"
    USER_SHARES=$(cast call $FAUCET_AMM "lpBalanceOf(address)(uint256)" $USER_ADDRESS --rpc-url $RPC_URL)
    echo "  Your Shares: $(cast --to-unit $USER_SHARES ether)"
    
    # Calculate user's share percentage
    if [ "$TOTAL_SHARES" != "0" ]; then
        SHARE_PERCENT=$(awk "BEGIN {printf \"%.4f\", ($USER_SHARES * 100) / $TOTAL_SHARES}")
        echo "  Ownership:   ${SHARE_PERCENT}%"
        
        # Calculate claimable amounts
        USER_DIAMOND=$(awk "BEGIN {printf \"%.0f\", ($USER_SHARES * $DIAMOND_RESERVE) / $TOTAL_SHARES}")
        USER_KASI=$(awk "BEGIN {printf \"%.0f\", ($USER_SHARES * $KASI_RESERVE) / $TOTAL_SHARES}")
        
        echo ""
        echo -e "${YELLOW}  💰 Your Claimable Assets (if you withdraw all shares):${NC}"
        echo "     DIAMOND: $(cast --to-unit $USER_DIAMOND ether) DMD"
        echo "     KASI:    $(cast --to-unit $USER_KASI ether) KASI"
    fi
else
    echo -e "${YELLOW}ℹ️  Set USER_ADDRESS to see your LP position${NC}"
fi

echo ""
echo -e "${GREEN}🔧 Pool Configuration:${NC}"
FLOOR_PRICE=$(cast call $FAUCET_AMM "floorPrice()(uint256)" --rpc-url $RPC_URL)
echo "  Floor Price: $(cast --to-unit $FLOOR_PRICE ether) KASI per 1 DMD"

PAUSED=$(cast call $FAUCET_AMM "paused()(bool)" --rpc-url $RPC_URL)
if [ "$PAUSED" == "true" ]; then
    echo -e "  Status:      ${YELLOW}⏸️  PAUSED${NC}"
else
    echo -e "  Status:      ${GREEN}✅ ACTIVE${NC}"
fi

echo ""
echo -e "${BLUE}========================================${NC}"
echo ""
echo "Usage examples:"
echo "  Get your LP info:     USER_ADDRESS=0x... ./get-lp-info.sh"
echo "  Use different pool:   FAUCET_AMM=0x... ./get-lp-info.sh"