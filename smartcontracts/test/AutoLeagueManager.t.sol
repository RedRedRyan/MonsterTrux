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
 * - Unlimited players per league (solo play game)
 * - Leagues automatically start when first player submits score
 * - Leagues last 3 minutes from first submission
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
        uint256 endTime; // startTime + LEAGUE_DURATION
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

        // Auto-start league on first score submission (or when threshold met)
        if (league.status == LeagueStatus.WaitingForPlayers) {
            if (league.participantCount >= tierConfigs[_tier].autoStartThreshold) {
                league.status = LeagueStatus.Active;
                league.startTime = block.timestamp;
                league.endTime = block.timestamp + LEAGUE_DURATION;
                emit LeagueStarted(leagueId, _tier, league.startTime, league.endTime);
            }
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
        isPlay                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       