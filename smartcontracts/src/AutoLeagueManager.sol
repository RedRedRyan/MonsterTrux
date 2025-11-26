// SPDX-License-Identifier: UNLICENSED
pragma solidity ^0.8.13;

import {IERC20} from "forge-std/interfaces/IERC20.sol";
import {Ownable} from "openzeppelin-contracts/contracts/access/Ownable.sol";
import {Pausable} from "openzeppelin-contracts/contracts/utils/Pausable.sol";
import {ReentrancyGuard} from "openzeppelin-contracts/contracts/utils/ReentrancyGuard.sol";

/**
 * @title AutoLeagueManager
 * @dev Automated 3-minute league system with three stake tiers
 *
 * Features:
 * - Three automatic stake tiers: Low (2 SPEED), Mid (4 SPEED), High (8 SPEED)
 * - Players just submit scores - no manual league creation needed
 * - Auto-spawning leagues when previous ones fill or timeout
 * - Automatic prize distribution to top 10% of players
 * - Real-time leaderboard tracking across all tiers
 */
contract AutoLeagueManager is Ownable, Pausable, ReentrancyGuard {
    IERC20 public immutable gameToken;

    // Stake tier enumeration
    enum StakeTier {
        Low,
        Mid,
        High
    }

    // League status enumeration
    enum LeagueStatus {
        WaitingForPlayers,
        Active,
        Finished
    }

    // Stake tier configuration
    struct TierConfig {
        uint256 entryFee; // Entry fee in SPEED tokens
        uint256 autoStartThreshold; // Min players to auto-start league (can be 1 for immediate start)
        bool isActive; // Whether this tier is accepting players
    }

    // League structure
    struct League {
        uint256 id;
        StakeTier tier;
        uint256 entryFee;
        uint256 prizePool;
        uint256 startTime; // When league started (first score submitted)
        uint256 endTime; // startTime + 3 minutes
        LeagueStatus status;
        uint256 participantCount;
        uint256 scoringPlayerCount;
        bool prizesDistributed;
        address[] participants;
        address[] scoringPlayers;
    }

    // Player score in a league
    struct PlayerScore {
        address player;
        uint256 score;
        uint256 submissionTime;
        bool hasSubmitted;
    }

    // State variables
    uint256 public nextLeagueId = 1;
    uint256 public platformFeePercentage = 500; // 5% in basis points
    address public platformFeeRecipient;
    uint256 public constant LEAGUE_DURATION = 3 minutes;
    uint256 public constant MIN_PLAYERS_FOR_PRIZES = 2;
    uint256 public constant AUTO_SPAWN_TIMEOUT = 5 minutes; // Create new league if current one stalls

    uint256 private constant BPS_DENOMINATOR = 10_000;
    uint256 private constant PODIUM_FIRST_BPS = 3000; // 30% of net pool
    uint256 private constant PODIUM_SECOND_BPS = 1400; // 14% of net pool
    uint256 private constant PODIUM_THIRD_BPS = 1100; // 11% of net pool
    uint256 private constant COMPETITIVE_POOL_BPS = 2500; // 25% of net pool
    uint256 private constant PARTICIPATION_POOL_BPS = 2000; // 20% of net pool

    // Tier configurations
    mapping(StakeTier => TierConfig) public tierConfigs;

    // Current active league per tier (only one active league per tier at a time)
    mapping(StakeTier => uint256) public currentLeagueForTier;

    // League storage
    mapping(uint256 => League) public leagues;
    mapping(uint256 => mapping(address => PlayerScore)) public playerScores;
    mapping(uint256 => mapping(address => bool)) public isPlayerRegistered;

    // Player tracking
    mapping(address => mapping(StakeTier => uint256)) public playerLastLeague; // Last league player joined for each tier
    mapping(address => mapping(StakeTier => bool)) public playerInActiveTier; // Is player currently in an active league for tier

    // Events
    event TierConfigured(StakeTier indexed tier, uint256 entryFee, uint256 autoStartThreshold);
    event LeagueCreated(uint256 indexed leagueId, StakeTier indexed tier, uint256 entryFee);
    event PlayerJoinedLeague(uint256 indexed leagueId, StakeTier indexed tier, address indexed player);
    event LeagueStarted(uint256 indexed leagueId, StakeTier indexed tier, uint256 startTime, uint256 endTime);
    event ScoreSubmitted(uint256 indexed leagueId, StakeTier indexed tier, address indexed player, uint256 score);
    event LeagueFinished(uint256 indexed leagueId, StakeTier indexed tier, uint256 scoringPlayers);
    event PrizesDistributed(uint256 indexed leagueId, StakeTier indexed tier, uint256 totalPrizes, uint256 winners);
    event NewLeagueSpawned(uint256 indexed newLeagueId, StakeTier indexed tier, string reason);

    constructor(address _gameToken, address _owner, address _platformFeeRecipient) Ownable(_owner) {
        gameToken = IERC20(_gameToken);
        platformFeeRecipient = _platformFeeRecipient;

        // Initialize default tier configurations
        _initializeDefaultTiers();
    }

    /**
     * @dev Initialize default tier configurations
     */
    function _initializeDefaultTiers() internal {
        // Low Stakes: 2 SPEED tokens - starts immediately with first player
        tierConfigs[StakeTier.Low] = TierConfig({
            entryFee: 2 * 10 ** 18,
            autoStartThreshold: 1, // Start immediately when first player joins
            isActive: true
        });

        // Mid Stakes: 4 SPEED tokens
        tierConfigs[StakeTier.Mid] = TierConfig({entryFee: 4 * 10 ** 18, autoStartThreshold: 1, isActive: true});

        // High Stakes: 8 SPEED tokens
        tierConfigs[StakeTier.High] = TierConfig({entryFee: 8 * 10 ** 18, autoStartThreshold: 1, isActive: true});
        // Create initial leagues for each tier
        
        _createNewLeague(StakeTier.Low);
        _createNewLeague(StakeTier.Mid);
        _createNewLeague(StakeTier.High);
    }

    /**
     * @dev Submit score for a specific tier - automatically handles league joining
     */
    function submitScore(StakeTier _tier, uint256 _score) external whenNotPaused nonReentrant {
        require(_score > 0, "Score must be greater than 0");
        require(tierConfigs[_tier].isActive, "Tier is not active");

        uint256 leagueId = currentLeagueForTier[_tier];
        require(leagueId != 0, "No active league for tier");

        League storage league = leagues[leagueId];

        // Auto-join player to current league if not already registered
        if (!isPlayerRegistered[leagueId][msg.sender]) {
            _joinCurrentLeague(_tier, leagueId);
        }

        // Auto-start league on first score submission
        if (league.status == LeagueStatus.WaitingForPlayers) {
            league.status = LeagueStatus.Active;
            league.startTime = block.timestamp;
            league.endTime = block.timestamp + LEAGUE_DURATION;
            emit LeagueStarted(leagueId, _tier, league.startTime, league.endTime);
        }

        require(league.status == LeagueStatus.Active, "League not active");
        require(block.timestamp <= league.endTime, "League ended");

        PlayerScore storage playerScore = playerScores[leagueId][msg.sender];

        // Only allow score updates if new score is higher
        require(!playerScore.hasSubmitted || _score > playerScore.score, "Score must be higher than previous");

        // If first submission for this player, add to scoring players
        if (!playerScore.hasSubmitted) {
            league.scoringPlayers.push(msg.sender);
            league.scoringPlayerCount++;
        }

        playerScore.score = _score;
        playerScore.submissionTime = block.timestamp;
        playerScore.hasSubmitted = true;

        emit ScoreSubmitted(leagueId, _tier, msg.sender, _score);

        // Check if league should auto-finish
        if (block.timestamp >= league.endTime) {
            _finishLeague(leagueId, _tier);
        }
    }

    /**
     * @dev Join current league for a tier (internal)
     */
    function _joinCurrentLeague(StakeTier _tier, uint256 _leagueId) internal {
        League storage league = leagues[_leagueId];
        require(
            league.status == LeagueStatus.WaitingForPlayers || league.status == LeagueStatus.Active,
            "League not accepting players"
        );
        require(league.status == LeagueStatus.WaitingForPlayers || block.timestamp <= league.endTime, "League ended");
        require(!playerInActiveTier[msg.sender][_tier], "Already in active league for this tier");

        uint256 entryFee = tierConfigs[_tier].entryFee;

        // Collect entry fee
        require(gameToken.transferFrom(msg.sender, address(this), entryFee), "Entry fee transfer failed");

    // Register player
    isPlayerRegistered[_leagueId][msg.sender] = true;
        league.participants.push(msg.sender);
        league.participantCount++;
        league.prizePool += entryFee;

        playerScores[_leagueId][msg.sender] =
            PlayerScore({player: msg.sender, score: 0, submissionTime: 0, hasSubmitted: false});

        playerLastLeague[msg.sender][_tier] = _leagueId;
        playerInActiveTier[msg.sender][_tier] = true;

        emit PlayerJoinedLeague(_leagueId, _tier, msg.sender);
    }

    /**
     * @dev Create a new league for a tier
     */
    function _createNewLeague(StakeTier _tier) internal returns (uint256) {
        uint256 leagueId = nextLeagueId++;
        TierConfig memory config = tierConfigs[_tier];

        leagues[leagueId] = League({
            id: leagueId,
            tier: _tier,
            entryFee: config.entryFee,
            prizePool: 0,
            startTime: 0,
            endTime: 0,
            status: LeagueStatus.WaitingForPlayers,
            participantCount: 0,
            scoringPlayerCount: 0,
            prizesDistributed: false,
            participants: new address[](0),
            scoringPlayers: new address[](0)
        });

        currentLeagueForTier[_tier] = leagueId;

        emit LeagueCreated(leagueId, _tier, config.entryFee);
        return leagueId;
    }

    /**
     * @dev Spawn new league and mark old one for cleanup if needed
     */
    function _spawnNewLeague(StakeTier _tier, string memory _reason) internal {
        uint256 newLeagueId = _createNewLeague(_tier);
        emit NewLeagueSpawned(newLeagueId, _tier, _reason);
    }

    /**
     * @dev Finish a league and distribute prizes
     */
    function _finishLeague(uint256 _leagueId, StakeTier _tier) internal {
        League storage league = leagues[_leagueId];
        league.status = LeagueStatus.Finished;

        // Mark all players as no longer in active league for this tier
        for (uint256 i = 0; i < league.participants.length; i++) {
            playerInActiveTier[league.participants[i]][_tier] = false;
        }

        emit LeagueFinished(_leagueId, _tier, league.scoringPlayerCount);

        // Auto-distribute prizes if there are enough players
        if (league.scoringPlayerCount >= MIN_PLAYERS_FOR_PRIZES && league.prizePool > 0) {
            _distributePrizes(_leagueId, _tier);
        } else if (league.prizePool > 0) {
            _refundEntryFees(_leagueId);
        }

        // Spawn new league for this tier
        _spawnNewLeague(_tier, "Previous league finished");
    }

    /**
     * @dev Distribute prizes using podium + competitive + participation brackets
     */
    function _distributePrizes(uint256 _leagueId, StakeTier _tier) internal {
        League storage league = leagues[_leagueId];

        address[] memory sortedPlayers = _getSortedLeaderboard(_leagueId);
        if (sortedPlayers.length == 0) {
            _refundEntryFees(_leagueId);
            return;
        }

        uint256 platformFee = (league.prizePool * platformFeePercentage) / BPS_DENOMINATOR;
        uint256 netPrizePool = league.prizePool - platformFee;

        if (platformFee > 0) {
            require(gameToken.transfer(platformFeeRecipient, platformFee), "Platform fee transfer failed");
        }

        uint256 distributed;
        uint256 winnersPaid;
        uint256 payoutIndex;

        uint256[3] memory podiumBps = [PODIUM_FIRST_BPS, PODIUM_SECOND_BPS, PODIUM_THIRD_BPS];
        uint256 podiumCount = sortedPlayers.length < podiumBps.length ? sortedPlayers.length : podiumBps.length;

        for (uint256 i = 0; i < podiumCount; i++) {
            uint256 payout = (netPrizePool * podiumBps[i]) / BPS_DENOMINATOR;
            if (payout > 0) {
                require(gameToken.transfer(sortedPlayers[payoutIndex], payout), "Prize transfer failed");
                distributed += payout;
            }
            payoutIndex++;
            winnersPaid++;
        }

        uint256 bracketTarget = _ceilDiv(league.scoringPlayerCount, 5); // ceil(20%)
        uint256 remainingPlayers = sortedPlayers.length > payoutIndex ? sortedPlayers.length - payoutIndex : 0;

        uint256 competitiveCount = bracketTarget < remainingPlayers ? bracketTarget : remainingPlayers;
        if (competitiveCount > 0) {
            uint256 competitivePool = (netPrizePool * COMPETITIVE_POOL_BPS) / BPS_DENOMINATOR;
            uint256 competitivePayout = competitivePool / competitiveCount;
            for (uint256 i = 0; i < competitiveCount; i++) {
                if (competitivePayout > 0) {
                    require(gameToken.transfer(sortedPlayers[payoutIndex], competitivePayout), "Prize transfer failed");
                    distributed += competitivePayout;
                }
                payoutIndex++;
                winnersPaid++;
            }
        }

        remainingPlayers = sortedPlayers.length > payoutIndex ? sortedPlayers.length - payoutIndex : 0;
        uint256 participationCount = bracketTarget < remainingPlayers ? bracketTarget : remainingPlayers;
        if (participationCount > 0) {
            uint256 participationPool = (netPrizePool * PARTICIPATION_POOL_BPS) / BPS_DENOMINATOR;
            uint256 participationPayout = participationPool / participationCount;
            for (uint256 i = 0; i < participationCount; i++) {
                if (participationPayout > 0) {
                    require(gameToken.transfer(sortedPlayers[payoutIndex], participationPayout), "Prize transfer failed");
                    distributed += participationPayout;
                }
                payoutIndex++;
                winnersPaid++;
            }
        }

        league.prizesDistributed = true;
        emit PrizesDistributed(_leagueId, _tier, netPrizePool, winnersPaid);
    }

    /**
     * @dev Check and finish expired leagues for all tiers
     */
    function checkAndFinishExpiredLeagues() external {
        StakeTier[3] memory tiers = [StakeTier.Low, StakeTier.Mid, StakeTier.High];

        for (uint256 i = 0; i < tiers.length; i++) {
            uint256 leagueId = currentLeagueForTier[tiers[i]];
            if (leagueId != 0) {
                League storage league = leagues[leagueId];

                // Finish if timer expired
                if (league.status == LeagueStatus.Active && block.timestamp >= league.endTime) {
                    _finishLeague(leagueId, tiers[i]);
                }

                // Spawn new league if current one is stalled
                if (
                    league.status == LeagueStatus.WaitingForPlayers
                        && block.timestamp >= (block.timestamp + AUTO_SPAWN_TIMEOUT)
                ) {
                    _spawnNewLeague(tiers[i], "League timeout");
                }
            }
        }
    }

    /**
     * @dev Get leaderboard for a league
     */
    function getLeaderboard(uint256 _leagueId, uint256 _limit)
        external
        view
        returns (address[] memory players, uint256[] memory scores, uint256[] memory submissionTimes)
    {
        address[] memory sortedPlayers = _getSortedLeaderboard(_leagueId);
        uint256 length = sortedPlayers.length < _limit ? sortedPlayers.length : _limit;

        players = new address[](length);
        scores = new uint256[](length);
        submissionTimes = new uint256[](length);

        for (uint256 i = 0; i < length; i++) {
            players[i] = sortedPlayers[i];
            PlayerScore memory playerScore = playerScores[_leagueId][sortedPlayers[i]];
            scores[i] = playerScore.score;
            submissionTimes[i] = playerScore.submissionTime;
        }
    }

    /**
     * @dev Get current league info for a tier
     */
    function getCurrentLeagueInfo(StakeTier _tier)
        external
        view
        returns (
            uint256 leagueId,
            uint256 participantCount,
            uint256 prizePool,
            LeagueStatus status,
            uint256 timeRemaining
        )
    {
        leagueId = currentLeagueForTier[_tier];
        if (leagueId == 0) return (0, 0, 0, LeagueStatus.WaitingForPlayers, 0);

        League memory league = leagues[leagueId];

        participantCount = league.participantCount;
        prizePool = league.prizePool;
        status = league.status;

        if (league.status == LeagueStatus.Active && block.timestamp < league.endTime) {
            timeRemaining = league.endTime - block.timestamp;
        } else {
            timeRemaining = 0;
        }
    }

    /**
     * @dev Get tier configuration
     */
    function getTierConfig(StakeTier _tier) external view returns (TierConfig memory) {
        return tierConfigs[_tier];
    }

    /**
     * @dev Get all tier configurations
     */
    function getAllTierConfigs()
        external
        view
        returns (TierConfig memory lowTier, TierConfig memory midTier, TierConfig memory highTier)
    {
        lowTier = tierConfigs[StakeTier.Low];
        midTier = tierConfigs[StakeTier.Mid];
        highTier = tierConfigs[StakeTier.High];
    }

    // Owner functions
    function configureTier(StakeTier _tier, uint256 _entryFee, uint256 _autoStartThreshold, bool _isActive)
        external
        onlyOwner
    {
        tierConfigs[_tier] =
            TierConfig({entryFee: _entryFee, autoStartThreshold: _autoStartThreshold, isActive: _isActive});

        emit TierConfigured(_tier, _entryFee, _autoStartThreshold);
    }

    function setPlatformFee(uint256 _feePercentage) external onlyOwner {
        require(_feePercentage <= 1000, "Fee cannot exceed 10%");
        platformFeePercentage = _feePercentage;
    }

    function setPlatformFeeRecipient(address _recipient) external onlyOwner {
        require(_recipient != address(0), "Invalid recipient");
        platformFeeRecipient = _recipient;
    }

    function pause() external onlyOwner {
        _pause();
    }

    function unpause() external onlyOwner {
        _unpause();
    }

    function emergencyWithdraw() external onlyOwner {
        uint256 balance = gameToken.balanceOf(address(this));
        require(gameToken.transfer(owner(), balance), "Emergency withdrawal failed");
    }

    function _ceilDiv(uint256 a, uint256 b) internal pure returns (uint256) {
        if (a == 0) {
            return 0;
        }
        return (a + b - 1) / b;
    }

    // Internal functions
    function _getSortedLeaderboard(uint256 _leagueId) internal view returns (address[] memory) {
        League memory league = leagues[_leagueId];
        address[] memory players = new address[](league.scoringPlayers.length);

        // Copy scoring players
        for (uint256 i = 0; i < league.scoringPlayers.length; i++) {
            players[i] = league.scoringPlayers[i];
        }

        // Simple bubble sort by score (descending)
        for (uint256 i = 0; i < players.length; i++) {
            for (uint256 j = 0; j < players.length - 1 - i; j++) {
                uint256 score1 = playerScores[_leagueId][players[j]].score;
                uint256 score2 = playerScores[_leagueId][players[j + 1]].score;

                if (score1 < score2) {
                    address temp = players[j];
                    players[j] = players[j + 1];
                    players[j + 1] = temp;
                }
            }
        }

        return players;
    }

    function _refundEntryFees(uint256 _leagueId) internal {
        League storage league = leagues[_leagueId];

        for (uint256 i = 0; i < league.participants.length; i++) {
            require(gameToken.transfer(league.participants[i], league.entryFee), "Refund failed");
        }

        league.prizesDistributed = true;
    }
}
