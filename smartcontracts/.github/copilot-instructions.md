# Copilot Instructions for Polygon Game Contracts

## Project Overview
This is a Foundry-based Ethereum smart contract project focused on gaming infrastructure for Polygon. The project uses Solidity ^0.8.13 and follows Foundry's standard project structure for building, testing, and deploying gaming-related smart contracts.

## Architecture & Key Components

### Core Contracts (`src/`)
- **GameToken.sol**: Token contract for in-game economy (currently empty - needs implementation)
- **LeagueManager.sol**: Manages gaming leagues and tournaments (currently empty - needs implementation)  
- **Faucet.sol**: Token distribution for testing/onboarding (currently empty - needs implementation)
- **Counter.sol**: Reference implementation showing basic contract patterns

### Testing Patterns (`test/`)
- Use Foundry's `Test` contract as base class: `import {Test} from "forge-std/Test.sol"`
- Follow naming convention: `ContractName.t.sol` for test files
- Test functions start with `test` prefix (e.g., `test_Increment()`)
- Use `testFuzz_` prefix for fuzz tests with random inputs
- Always include `setUp()` function to initialize contract instances
- Example: See `test/Counter.t.sol` for reference patterns

### Deployment Scripts (`script/`)
- Use Foundry's `Script` contract: `import {Script} from "forge-std/Script.sol"`
- Wrap deployment logic in `vm.startBroadcast()` and `vm.stopBroadcast()`
- Follow naming convention: `ContractName.s.sol` for script files
- Example: See `script/Counter.s.sol` for deployment patterns

## Development Workflow

### Essential Commands
```bash
# Build contracts
forge build

# Run tests with verbose output
forge test -vvv

# Format code (enforced in CI)
forge fmt

# Deploy to network
forge script script/Deploy.s.sol --rpc-url <rpc_url> --private-key <private_key>

# Local development chain
anvil
```

### Testing Strategy
- Write comprehensive unit tests for each contract function
- Use fuzz testing for functions with numeric parameters
- Test both success and failure scenarios
- CI runs `forge test -vvv` for detailed output

### Code Standards
- Use SPDX license identifier: `// SPDX-License-Identifier: UNLICENSED`
- Solidity version: `pragma solidity ^0.8.13;`
- Code formatting enforced via `forge fmt --check` in CI
- Import forge-std utilities: `Test.sol`, `Script.sol`, `console.sol`

## Gaming Contract Considerations
When implementing the empty gaming contracts, consider:
- **GameToken**: ERC-20 standard for in-game currency with potential gaming-specific features
- **LeagueManager**: Tournament creation, player registration, prize distribution logic
- **Faucet**: Rate-limited token distribution for new players and testing
- Gas optimization for Polygon's lower costs but still important for user experience

## CI/CD Pipeline
GitHub Actions workflow (`.github/workflows/test.yml`):
- Runs on push, PR, and manual dispatch
- Checks code formatting, builds contracts, and runs full test suite
- Uses `FOUNDRY_PROFILE: ci` environment variable