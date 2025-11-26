# Deployment Guide

## Over**Default Configuration:**

| Component | Setting | Value |
|-----------|---------|-------|
| **Faucet Funding** | Initial balance | 10,000 SPEED |
| **Claim Amount** | Per claim | 100 SPEED |
| **Cooldown** | Between claims | 1 day (86400 sec) |
| **Max Claims** | Per user | 5 |
| **Platform Fee** | Revenue share | 5% (500 basis points) |

| Tier | Entry Fee | Auto-Start Threshold | Max Players | Status |
|------|-----------|---------------------|-------------|--------|
| LOW | 2 SPEED | 1 player | UNLIMITED | Active |
| MID | 4 SPEED | 1 player | UNLIMITED | Active |
| HIGH | 8 SPEED | 1 player | UNLIMITED | Active |his project provides **one unified deployment script** for the automated multi-tier gaming platform:

**`DeployAutoLeagues.s.sol`** - Deploys complete platform with automated 3-tier leagues

### What Gets Deployed:
1. **GameToken (SPEED)** - ERC-20 game currency
2. **Faucet** - Free token distribution for player onboarding  
3. **AutoLeagueManager** - 3-tier automated leagues (LOW/MID/HIGH)

---

## Quick Start

### Deploy Complete Platform (Automated Multi-Tier)


```bash
# Deploy with default tiers (2/4/8 SPEED)
DEPLOYER_ADDRESS=0xYourAddress \
forge script script/DeployAutoLeagues.s.sol:DeployAutoLeagues \
  --broadcast --rpc-url $RPC_URL
```

**Default Configuration:**

| Tier | Entry Fee | Auto-Start Threshold | Max Players | Status |
|------|-----------|---------------------|-------------|--------|
| LOW | 2 SPEED | 1 player | UNLIMITED | Active |
| MID | 4 SPEED | 1 player | UNLIMITED | Active |
| HIGH | 8 SPEED | 1 player | UNLIMITED | Active |

### Custom Tier Configurations

#### Budget Tiers (0.5/1/2 SPEED)
Perfect for testing or casual play:

```bash
DEPLOYER_ADDRESS=0x... \
LOW_ENTRY_FEE=500000000000000000 \
MID_ENTRY_FEE=1000000000000000000 \
HIGH_ENTRY_FEE=2000000000000000000 \
forge script script/DeployAutoLeagues.s.sol:DeployAutoLeagues \
  --broadcast --rpc-url $RPC_URL
```

#### Standard Tiers (5/10/25 SPEED)
Balanced risk/reward:

```bash
DEPLOYER_ADDRESS=0x... \
LOW_ENTRY_FEE=5000000000000000000 \
MID_ENTRY_FEE=10000000000000000000 \
HIGH_ENTRY_FEE=25000000000000000000 \
forge script script/DeployAutoLeagues.s.sol:DeployAutoLeagues \
  --broadcast --rpc-url $RPC_URL
```

#### Whale Tiers (50/100/500 SPEED)
High-stakes competition:

```bash
DEPLOYER_ADDRESS=0x... \
LOW_ENTRY_FEE=50000000000000000000 \
MID_ENTRY_FEE=100000000000000000000 \
HIGH_ENTRY_FEE=500000000000000000000 \
HIGH_AUTO_START=3 \
PLATFORM_FEE_PCT=1000 \
forge script script/DeployAutoLeagues.s.sol:DeployAutoLeagues \
  --broadcast --rpc-url $RPC_URL
```

#### Testnet Micro Tiers (0.1/0.5/1 SPEED)
For development and testing:

```bash
DEPLOYER_ADDRESS=0x... \
LOW_ENTRY_FEE=100000000000000000 \
MID_ENTRY_FEE=500000000000000000 \
HIGH_ENTRY_FEE=1000000000000000000 \
forge script script/DeployAutoLeagues.s.sol:DeployAutoLeagues \
  --broadcast --rpc-url $MUMBAI_RPC
```

---

## Environment Variables Reference

### Deploy.s.sol (Manual League System)

| Variable | Description | Default | Example |
|----------|-------------|---------|---------|
| `DEPLOYER_ADDRESS` | Contract owner address | **Required** | `0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb` |
| `FAUCET_FUNDING_AMOUNT` | Initial faucet funding (wei) | 10,000 SPEED | `50000000000000000000000` |
| `CLAIM_AMOUNT` | Tokens per claim (wei) | 100 SPEED | `200000000000000000000` |
| `COOLDOWN_PERIOD` | Seconds between claims | 86400 (1 day) | `43200` (12 hours) |
| `MAX_CLAIMS_PER_USER` | Maximum claims allowed | 5 | `10` |
| `PLATFORM_FEE_PERCENTAGE` | Fee in basis points | 500 (5%) | `750` (7.5%) |

### AutoLeagueManager.s.sol (Multi-Tier System)

| Variable | Description | Default | Example |
|----------|-------------|---------|---------|
| `GAME_TOKEN_ADDRESS` | Deployed GameToken address | **Required** | `0x...` |
| `DEPLOYER_ADDRESS` | Contract owner address | **Required** | `0x...` |
| `PLATFORM_FEE_RECIPIENT` | Fee recipient address | `DEPLOYER_ADDRESS` | `0x...` |
| `PLATFORM_FEE_PCT` | Fee in basis points | 500 (5%) | `1000` (10%) |
| **LOW TIER** |
| `LOW_ENTRY_FEE` | Entry fee (wei) | 2 SPEED | `5000000000000000000` |
| `LOW_AUTO_START` | Min players to start | 1 | `3` |
| **MID TIER** |
| `MID_ENTRY_FEE` | Entry fee (wei) | 4 SPEED | `10000000000000000000` |
| `MID_AUTO_START` | Min players to start | 1 | `3` |
| **HIGH TIER** |
| `HIGH_ENTRY_FEE` | Entry fee (wei) | 8 SPEED | `25000000000000000000` |
| `HIGH_AUTO_START` | Min players to start | 1 | `5` |

---

## Token Amount Conversion

| SPEED Tokens | Wei Value | Usage |
|--------------|-----------|-------|
| 0.1 SPEED | `100000000000000000` | Testnet micro tier |
| 0.5 SPEED | `500000000000000000` | Budget tier |
| 1 SPEED | `1000000000000000000` | Basic tier |
| 2 SPEED | `2000000000000000000` | Default LOW tier |
| 4 SPEED | `4000000000000000000` | Default MID tier |
| 5 SPEED | `5000000000000000000` | Standard tier |
| 8 SPEED | `8000000000000000000` | Default HIGH tier |
| 10 SPEED | `10000000000000000000` | Standard tier |
| 25 SPEED | `25000000000000000000` | Premium tier |
| 50 SPEED | `50000000000000000000` | Whale tier |
| 100 SPEED | `100000000000000000000` | Whale tier |
| 500 SPEED | `500000000000000000000` | Ultra whale tier |

**Formula:** `SPEED_AMOUNT * 10^18 = WEI_VALUE`

---

## Post-Deployment Management

### Update Tier Configuration

```bash
# Update LOW tier to 5 SPEED, start with 3 players
cast send $AUTO_LEAGUE_MANAGER \
  "configureTier(uint8,uint256,uint256,bool)" \
  0 \
  5000000000000000000 \
  3 \
  true \
  --private-key $PRIVATE_KEY \
  --rpc-url $RPC_URL
```

### Check Current League Status

```bash
# Get current league info for LOW tier (tier = 0)
cast call $AUTO_LEAGUE_MANAGER \
  "getCurrentLeagueInfo(uint8)" 0 \
  --rpc-url $RPC_URL
```

### View Leaderboard

```bash
# Get top 10 players in league #1
cast call $AUTO_LEAGUE_MANAGER \
  "getLeaderboard(uint256,uint256)" 1 10 \
  --rpc-url $RPC_URL
```

### Get Tier Configuration

```bash
# Get LOW tier config (tier = 0)
cast call $AUTO_LEAGUE_MANAGER \
  "tierConfigs(uint8)(uint256,uint256,bool)" 0 \
  --rpc-url $RPC_URL
```

### Update Platform Fee

```bash
# Set platform fee to 10% (1000 basis points)
cast send $AUTO_LEAGUE_MANAGER \
  "setPlatformFee(uint256)" 1000 \
  --private-key $PRIVATE_KEY \
  --rpc-url $RPC_URL
```

---

## Network-Specific Deployments

### Polygon Mainnet

```bash
# 1. Deploy GameToken + Faucet + Manual LeagueManager
DEPLOYER_ADDRESS=$YOUR_ADDRESS \
forge script script/Deploy.s.sol:Deploy \
  --broadcast \
  --rpc-url $POLYGON_RPC \
  --private-key $PRIVATE_KEY \
  --verify \
  --etherscan-api-key $POLYGONSCAN_API_KEY

# 2. Deploy AutoLeagueManager (after noting GameToken address)
GAME_TOKEN_ADDRESS=<from_step_1> \
DEPLOYER_ADDRESS=$YOUR_ADDRESS \
forge script script/AutoLeagueManager.s.sol:AutoLeagueManagerDeploy \
  --broadcast \
  --rpc-url $POLYGON_RPC \
  --private-key $PRIVATE_KEY \
  --verify \
  --etherscan-api-key $POLYGONSCAN_API_KEY
```

### Polygon Mumbai (Testnet)

```bash
# Use testnet RPC and micro tiers
DEPLOYER_ADDRESS=$YOUR_ADDRESS \
LOW_ENTRY_FEE=100000000000000000 \
MID_ENTRY_FEE=500000000000000000 \
HIGH_ENTRY_FEE=1000000000000000000 \
forge script script/AutoLeagueManager.s.sol:AutoLeagueManagerDeploy \
  --broadcast \
  --rpc-url $MUMBAI_RPC \
  --private-key $PRIVATE_KEY
```

### Local Anvil

```bash
# 1. Start local node
anvil

# 2. Deploy with default anvil account
DEPLOYER_ADDRESS=0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266 \
forge script script/Deploy.s.sol:Deploy \
  --broadcast \
  --rpc-url http://localhost:8545
```

---

## Output Files

After deployment, you'll get:

- **`deployment.env`** - Manual league system addresses
- **`autoleague-deployment.env`** - AutoLeagueManager addresses and tier config

Use these files to configure your frontend:

```javascript
// Load deployment addresses
import fs from 'fs';
const env = fs.readFileSync('autoleague-deployment.env', 'utf8');
const config = Object.fromEntries(
  env.split('\n')
    .filter(line => line && !line.startsWith('#'))
    .map(line => line.split('='))
);

console.log('AutoLeagueManager:', config.AUTO_LEAGUE_MANAGER_ADDRESS);
console.log('Low Tier Entry:', config.LOW_ENTRY_FEE / 1e18, 'SPEED');
```

---

## Verification

After deployment, verify contracts on block explorer:

```bash
forge verify-contract \
  --chain-id 137 \
  --compiler-version v0.8.13 \
  $CONTRACT_ADDRESS \
  src/AutoLeagueManager.sol:AutoLeagueManager \
  --constructor-args $(cast abi-encode "constructor(address,address,address)" $TOKEN $OWNER $FEE_RECIPIENT) \
  --etherscan-api-key $POLYGONSCAN_API_KEY
```

---

## Troubleshooting

### "Insufficient allowance" error
Make sure GameToken owner has approved the faucet for funding amount.

### "Tier not active" error
Check tier configuration: `cast call $AUTO_LEAGUE_MANAGER "tierConfigs(uint8)" 0`

### Gas estimation failed
Increase gas limit or check contract is not paused.

### Wrong tier prices showing
Clear cache and redeploy or update via `configureTier()`.

---

## Summary

**For Manual Leagues:** Use `Deploy.s.sol` - users create their own leagues with custom fees.

**For Automated Multi-Tier:** Use `AutoLeagueManager.s.sol` - 3 predefined tiers, unlimited players, auto-spawning leagues.

**Default Tiers (AutoLeagueManager):**
- 🟢 LOW: 2 SPEED (casual play)
- 🟡 MID: 4 SPEED (balanced risk)
- 🔴 HIGH: 8 SPEED (high stakes)

**Customize via CLI:** Pass environment variables to set any entry fee amount.

**Post-Deploy:** Update tiers anytime with `configureTier()` as contract owner.
