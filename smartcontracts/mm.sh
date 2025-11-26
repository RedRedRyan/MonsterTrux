#!/bin/bash


FAUCET="0xBe3C9052649B770b686226E876409FEE28157b34"
RPC="https://rpc-amoy.polygon.technology"

echo "🔍 Checking Faucet AMM Configuration..."
echo ""

# Get the token addresses the Faucet was deployed with
DIAMOND_ADDR=$(cast call $FAUCET "diamondToken()(address)" --rpc-url $RPC)
KASI_ADDR=$(cast call $FAUCET "gameToken()(address)" --rpc-url $RPC)

echo "Token Addresses in Faucet:"
echo "DIAMOND Token: $DIAMOND_ADDR"
echo "KASI Token:    $KASI_ADDR"
echo ""

# Check if they're the same
if [ "$DIAMOND_ADDR" == "$KASI_ADDR" ]; then
    echo "❌ PROBLEM FOUND: Both tokens point to the same address!"
    echo ""
    echo "This means when you called addLiquidity(1000, 99000):"
    echo "  - First transferFrom() took 1,000 tokens from this address"
    echo "  - Second transferFrom() took 99,000 MORE tokens from SAME address"
    echo "  - Total: 100,000 tokens transferred (all from one token)"
    echo ""
else
    echo "✅ Tokens are different addresses"
    echo ""
fi

# Get token details
echo "Token Details:"
echo ""
echo "DIAMOND Token:"
DIAMOND_SYMBOL=$(cast call $DIAMOND_ADDR "symbol()(string)" --rpc-url $RPC 2>/dev/null || echo "N/A")
DIAMOND_NAME=$(cast call $DIAMOND_ADDR "name()(string)" --rpc-url $RPC 2>/dev/null || echo "N/A")
echo "  Symbol: $DIAMOND_SYMBOL"
echo "  Name:   $DIAMOND_NAME"
echo ""

echo "KASI Token:"
KASI_SYMBOL=$(cast call $KASI_ADDR "symbol()(string)" --rpc-url $RPC 2>/dev/null || echo "N/A")
KASI_NAME=$(cast call $KASI_ADDR "name()(string)" --rpc-url $RPC 2>/dev/null || echo "N/A")
echo "  Symbol: $KASI_SYMBOL"
echo "  Name:   $KASI_NAME"
echo ""

# Check current reserves
echo "Current Reserves in Faucet:"
RESERVES=$(cast call $FAUCET "getReserves()(uint256,uint256)" --rpc-url $RPC)
echo "$RESERVES"
echo ""

# Check actual token balances
echo "Actual Token Balances in Faucet:"
DIAMOND_BAL=$(cast call $DIAMOND_ADDR "balanceOf(address)(uint256)" $FAUCET --rpc-url $RPC)
KASI_BAL=$(cast call $KASI_ADDR "balanceOf(address)(uint256)" $FAUCET --rpc-url $RPC)
echo "DIAMOND balance: $DIAMOND_BAL"
echo "KASI balance:    $KASI_BAL"
