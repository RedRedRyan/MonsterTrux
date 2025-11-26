# Polygon Gaming Platform - Smart Contract Documentation

## 🎮 Platform Overview

This is a complete gaming ecosystem built on Polygon featuring 3-minute timed competitive leagues. Players compete for SPEED tokens through fast-paced score-based competitions with automatic prize distribution to the top 10% of performers.


## 🪙 GameToken.sol (Kasi Token - SPEED)

### Overview
**Status: ✅ Fully Implemented**

ERC-20 token contract powering the gaming economy on Polygon. Features supply management, pausable functionality, and gaming-optimized mechanics.

### Contract Details
```solidity
Name: "Kasi Token"
Symbol: "SPEED" 
Decimals: 18
Max Supply: 100,000,000 SPEED tokens
Initial Supply: 0 (minted as needed)
```

### Key Features

#### 🏦 Supply Management
- **Capped Supply**: Maximum 100M tokens to prevent inflation
- **Owner-Controlled Minting**: Only contract owner can mint new tokens
- **Burnable**: Players can burn tokens for deflationary pressure
- **Supply Tracking**: Real-time monitoring of circulating supply

#### 🛡️ Security & Controls
- **Pausable**: Emergency pause for all transfers during critical issues
- **Access Control**: OpenZeppelin Ownable for administrative functions
- **Safe Math**: Built-in Solidity 0.8+ overflow protection
- **Standard Compliance**: Full ERC-20 compatibility

#### 🎮 Gaming Integration
- **League Entry Fees**: Primary currency for tournament participation
- **Prize Distributions**: Automatic rewards to competition winners
- **Faucet Support**: Initial token distribution for new players
- **Platform Fees**: Revenue collection for platform sustainability

### Core Functions

```solidity
// Minting (Owner Only)
function mint(address to, uint256 amount) external onlyOwner

// Burning (Anyone)
function burn(uint256 amount) external
function burnFrom(address account, uint256 amount) external

// Emergency Controls (Owner Only)
function pause() external onlyOwner
function unpause() external onlyOwner

// Standard ERC-20
function transfer(address to, uint256 amount) external returns (bool)
function approve(address spender, uint256 amount) external returns (bool)
```

### Economics
- **Entry Fees**: Players pay SPEED to join leagues
- **Prize Pools**: Accumulated entry fees distributed to winners
- **Platform Revenue**: 5% fee on all prize pools
- **Faucet Distribution**: Free tokens for new player onboarding

---

## 🚰 Faucet.sol

### Overview
**Status: ✅ Fully Implemented**

Token distribution system for onboarding new players to the gaming platform. Provides controlled SPEED token distribution with anti-abuse mechanisms.

### Configuration
```solidity
Default Claim Amount: 100 SPEED tokens
Cooldown Period: 24 hours between claims
Max Claims Per User: 5 lifetime claims
Funding: Owner-managed token reserves
```

### Key Features

#### 💧 Distribution Mechanics
- **Controlled Claims**: Users can claim tokens once per cooldown period
- **Lifetime Limits**: Maximum total claims to prevent farming
- **Configurable Amounts**: Owner can adjust claim amounts
- **Auto-Funding**: Owner can deposit tokens to maintain supply

#### 🛡️ Anti-Abuse Systems
- **Cooldown Enforcement**: Strict timing between consecutive claims
- **User Tracking**: Individual claim history and totals
- **Pausable**: Emergency stop for claim functionality
- **Balance Checks**: Ensures sufficient faucet balance before claims

#### 📊 Administrative Controls
- **Dynamic Configuration**: Adjust claim amounts, cooldowns, and limits
- **Fund Management**: Add/remove tokens from faucet reserves
- **Emergency Recovery**: Owner can withdraw remaining tokens
- **Usage Analytics**: Track claims, users, and distribution patterns

### Core Functions

```solidity
// User Functions
function claimTokens() external
function getClaimInfo(address user) external view returns (...)

// Admin Functions (Owner Only)
function fundFaucet(uint256 amount) external onlyOwner
function setClaimAmount(uint256 newAmount) external onlyOwner
function setCooldownPeriod(uint256 newPeriod) external onlyOwner
function pause() external onlyOwner
```

### Usage Flow
1. **New Player**: Visits platform and connects wallet
2. **Claim Check**: System verifies eligibility (cooldown + limit)
3. **Token Distribution**: Faucet transfers SPEED tokens to player
4. **Cooldown Start**: 24-hour timer begins for next claim
5. **Gaming Ready**: Player can now join leagues and compete

---

## 🏆 LeagueManager.sol

### Overview
**Status: ✅ Fully Implemented**

3-minute timed competitive league system with automatic score tracking and prize distribution to top 10% of players.

### League Mechanics
```solidity
Duration: Exactly 3 minutes per league
Entry Method: Pay SPEED tokens to join
Competition: Submit highest score during active period
Winners: Top 10% of players who submitted scores
Distribution: Equal share of net prize pool
Platform Fee: 5% of total prize pool
```

### Key Features

#### ⏱️ Timing System
- **Auto-Start**: League begins when first player submits a score
- **Fixed Duration**: Exactly 180 seconds (3 minutes) from start
- **Real-Time Tracking**: Live countdown and status updates
- **Auto-Finish**: League automatically ends after timer expires

#### 🎯 Competition Mechanics
- **Score Submission**: Players submit their game scores
- **Higher Scores Only**: Can only improve previous score (anti-sandbagging)
- **Live Leaderboard**: Real-time ranking of all participants
- **Multiple Attempts**: Players can submit multiple times within time limit

#### 🏅 Prize Distribution
- **Top 10% Winners**: Dynamic winner count based on participation
- **Minimum Winners**: Always at least 1 winner (even with <10 players)
- **Equal Sharing**: Winners split net prize pool equally
- **Auto-Distribution**: Immediate payout when league ends

#### 🛡️ Security & Fairness
- **Entry Fee Collection**: Secure SPEED token deposits
- **Prize Pool Protection**: Funds locked until competition ends
- **Platform Fee**: 5% deducted before winner distribution
- **Refund Logic**: Entry fees returned if insufficient participation

### Core Functions

```solidity
// League Management
function createLeague(string name, uint256 entryFee, uint256 maxParticipants) external
function registerForLeague(uint256 leagueId) external
function submitScore(uint256 leagueId, uint256 score) external

// Information & Status
function getLeaderboard(uint256 leagueId, uint256 limit) external view
function getTimeRemaining(uint256 leagueId) external view returns (uint256)
function getWinnerCount(uint256 leagueId) external view returns (uint256)

// Prize Distribution
function distributePrizes(uint256 leagueId) external
function checkAndFinishExpiredLeague(uint256 leagueId) external
```

### Competition Flow

#### 1. **Pre-Game Phase**
- League creator sets name, entry fee, and max participants
- Players register and pay SPEED token entry fees
- Prize pool accumulates from all entry fees

#### 2. **Active Competition (3 Minutes)**
- First score submission triggers automatic timer start
- Players compete to achieve highest scores
- Real-time leaderboard updates with each submission
- Timer countdown visible to all participants

#### 3. **Post-Game Distribution**
- League automatically finishes after exactly 3 minutes
- System calculates top 10% of scoring players
- Platform fee (5%) deducted from total prize pool
- Remaining tokens distributed equally among winners

### Example Scenarios

| Players | Scorers | Winners | Prize Split |
|---------|---------|---------|-------------|
| 10 | 8 | 1 (10%) | Winner gets 95% of total entry fees |
| 20 | 15 | 2 (10%) | Each winner gets 47.5% of entry fees |
| 50 | 40 | 4 (10%) | Each winner gets 23.75% of entry fees |
| 5 | 3 | 1 (min) | Winner gets 95% of total entry fees |

---

## 🔗 Contract Integration

### Cross-Contract Communication

#### GameToken ↔ Faucet
- Faucet requests allowance to distribute SPEED tokens
- Owner funds faucet by transferring tokens from main supply
- Claim function transfers tokens directly to users

#### GameToken ↔ LeagueManager  
- League registration requires SPEED token approval and transfer
- Prize distribution transfers tokens from contract to winners
- Platform fees transferred to designated recipient address

#### Faucet → LeagueManager (Indirect)
- New players claim tokens from faucet
- Use claimed tokens to pay league entry fees
- Creates seamless onboarding experience

### Security Model

#### Access Controls
- **GameToken**: Owner can mint, pause, and manage supply
- **Faucet**: Owner can fund, configure, and pause distributions  
- **LeagueManager**: Owner can set fees, pause, and emergency withdraw

#### Economic Safeguards
- **Supply Cap**: GameToken limited to 100M total supply
- **Faucet Limits**: Per-user and per-period claim restrictions
- **Prize Protection**: League funds locked until competition ends

#### Emergency Procedures
- **Pause Functionality**: All contracts can be paused by owner
- **Emergency Withdrawals**: Owner can recover funds if needed
- **Upgrade Path**: Proxy patterns can be implemented for future updates

---

## 📊 Platform Economics

### Token Flow
```
1. Owner mints SPEED tokens
2. Faucet funded with initial token supply
3. New players claim free tokens from faucet
4. Players use tokens to pay league entry fees
5. Entry fees accumulate in league prize pools
6. Platform takes 5% fee from each league
7. Remaining 95% distributed to top 10% of players
8. Winners receive tokens, cycle repeats
```

### Revenue Model
- **Platform Fees**: 5% of every league prize pool
- **Token Appreciation**: SPEED token value growth
- **Premium Features**: Potential paid league types or bonuses
- **Partnerships**: Revenue sharing with game developers

### Growth Incentives
- **New Player Bonus**: Free tokens via faucet
- **Competitive Rewards**: High-skill players earn more
- **Network Effects**: More players = larger prize pools
- **Fast Gameplay**: 3-minute format encourages frequent play

---

## 🚀 Deployment Information

### Contract Addresses
*Addresses will be populated after deployment*

```
GameToken (SPEED): TBD
Faucet: TBD  
LeagueManager: TBD
```

### Network Configuration
- **Blockchain**: Polygon Mainnet
- **Gas Optimization**: All contracts optimized for low-cost transactions
- **Verification**: All contracts will be verified on Polygonscan

### Environment Variables
```bash
# Required
DEPLOYER_ADDRESS=0x...           # Contract owner address
PRIVATE_KEY=0x...                # Deployment wallet private key
RPC_URL=https://polygon-rpc.com  # Polygon RPC endpoint

# Optional
FAUCET_FUNDING_AMOUNT=10000      # Initial faucet funding (SPEED tokens)
CLAIM_AMOUNT=100                 # Tokens per faucet claim
COOLDOWN_PERIOD=86400            # Seconds between claims (24 hours)
MAX_CLAIMS_PER_USER=5            # Maximum lifetime claims
PLATFORM_FEE_PERCENTAGE=500      # Platform fee in basis points (5%)
```

---

## 🧪 Testing Coverage

### Unit Tests
- ✅ **GameToken**: Complete ERC-20 functionality, minting, burning, pausing
- ✅ **Faucet**: Claim mechanics, cooldowns, limits, admin functions
- ✅ **LeagueManager**: League creation, registration, scoring, prize distribution

### Integration Tests  
- ✅ **End-to-End Flow**: Complete player journey from faucet to league winning
- ✅ **Cross-Contract**: Token approvals, transfers, and interactions
- ✅ **Error Handling**: Proper revert messages and edge case handling

### Fuzz Testing
- ✅ **Score Submissions**: Random score values and timing
- ✅ **Prize Calculations**: Various player counts and entry fees  
- ✅ **Token Operations**: Transfer amounts and approval limits

---

## 📋 Current Status

| Component | Implementation | Testing | Deployment | Documentation |
|-----------|---------------|---------|------------|---------------|
| GameToken | ✅ Complete | ✅ Complete | 🔄 Ready | ✅ Complete |
| Faucet | ✅ Complete | ✅ Complete | 🔄 Ready | ✅ Complete |
| LeagueManager | ✅ Complete | ✅ Complete | 🔄 Ready | ✅ Complete |
| Integration | ✅ Complete | ✅ Complete | 🔄 Ready | ✅ Complete |

## 🎯 Future Enhancements

### Planned Features
- **Multiple Game Modes**: Different competition formats beyond 3-minute leagues
- **Tournament Brackets**: Multi-round elimination tournaments
- **Season Play**: Long-term competitive seasons with special rewards
- **NFT Integration**: Collectible achievements and trophies
- **Social Features**: Player profiles, friend systems, team formation

### Technical Improvements
- **Gas Optimization**: Further reduce transaction costs on Polygon
- **Oracle Integration**: Chainlink VRF for provably fair random events
- **Layer 2 Scaling**: Additional scaling solutions if needed
- **Cross-Chain**: Bridge to other networks for expanded player base

---

## 📞 Support & Resources

### Documentation
- Smart contract source code with inline comments
- Deployment scripts with configuration options
- Test suites demonstrating all functionality

### Community
- GitHub repository for issues and feature requests
- Discord server for player community
- Developer documentation for integrations

---

*Last Updated: November 6, 2025*
*Version: 1.0.0*
*Network: Polygon*