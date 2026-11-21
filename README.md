# LIZO-ResortEmpire

A 2D isometric mobile resort management game developed by **LIZO Project**.

## Project Information

- **Project:** Resort Empire
- **Team:** LIZO Project
- **Game Engine:** Unity 6.3 LTS (6000.3.26f1)
- **Target Platforms:** Android and iOS
- **Orientation:** Landscape
- **Development Status:** Sprint 3 Completed – Extended Gameplay Loop
- **Current Version:** v0.3.0

## Getting Started

1. Clone the repository.
2. Open Unity Hub.
3. Select **Add project from disk** and choose the cloned project folder.
4. Install or select Unity Editor version `6000.3.26f1`.
5. Open the project in Unity.

## Sprint 1 – Project Setup

- Initial Unity project and GitHub repository setup.
- Isometric grid, island scene, and basic environment.
- Initial building assets and locked areas.
- Camera movement with boundaries.
- Mobile landscape orientation and initial UI setup.

## Sprint 2 – Core Gameplay Integration

- New game initialization and main menu.
- Splash screen with transition to the main menu.
- Guest wave management and state transitions.
- Aggregated guest needs processing and Wave Needs Panel.
- Resort status display through TopBar.
- Building selection and interactive building details.
- Worker assignment, unassignment, and reassignment.
- Worker Allocation UI integrated with the existing worker management system.
- Start Wave button with automatic state updates.
- Mobile landscape UI with SafeArea support.

## Sprint 3 – Extended Gameplay Loop

- Extended gameplay phase transitions and wave progression.
- Configuration of five service buildings and at least eight guest waves.
- Wave Forecast Panel displaying expected needs before starting a wave.
- Collapsible forecast UI with visibility based on game phases.
- Dynamic visibility of guest needs based on unlocked service buildings.
- Purchase and unlocking of Pool and Bungalows using Coins.
- Automatic Worker allocation and activation of new guest needs after building purchases.
- Worker reassignment during active waves.
- Reduced but non-zero service performance for buildings without assigned Workers.
- Service bottleneck detection and recovery status indicators.
- Wave completion rating from 1 to 5 stars.
- Wave completion results and Coins rewards.
- Reward claiming and continuation to subsequent waves.
- Game End Panel after completing the configured wave sequence.

**Note:** Save/Load functionality, building upgrades, and upgrade recommendations are planned for later development. The project is still under development.

## Testing

Testing is performed continuously throughout development to verify individual features and their integration.

### Sprint 2

The team performed:

- Functional testing of guest wave management and state transitions.
- Verification of aggregated guest needs processing.
- Worker assignment, unassignment, and reassignment tests.
- Validation of worker assignment persistence between waves.
- Building interaction and UI functionality testing.
- UI layout testing on multiple mobile device resolutions using Unity Device Simulator.
- Automated smoke tests for selected gameplay systems.
- Integration testing in the main game scene.

### Sprint 3

During Sprint 3, the team performed:

- Functional testing of wave progression, game phase transitions, and completion of the configured wave sequence.
- Verification of Wave Forecast visibility, expected guest needs, and updates after building purchases.
- Testing of Pool and Bungalows purchases, including unlock conditions, Coins deduction, and building state updates.
- Verification of Worker reassignment, service performance, and bottleneck detection.
- Testing of wave completion ratings and Coins reward calculation.
- Verification of reward claiming, prevention of duplicate rewards, and continuation to subsequent waves.
- Testing of Wave Complete and Game End Panel functionality.
- Integration testing of the extended gameplay loop in the main game scene.

The project has not yet undergone complete final MVP testing.

## Development

Team members should work on separate branches and submit their changes through Pull Requests.

## Assets & Attribution

The visual assets used in Resort Empire are generated using **OpenAI's ChatGPT** and prepared for integration into Unity by **LIZO Project**.

The project is currently developed for non-commercial educational purposes. No third-party asset packs have been used.

AI-generated assets are used in accordance with [OpenAI's Terms of Use](https://openai.com/policies/terms-of-use/) and applicable copyright laws.

Any third-party assets added in the future will be documented with their respective sources and licenses.

## Git Workflow

The Resort Empire project follows the **classic GitFlow branching model**.

- `main` – stable releases of the project.
- `develop` – main integration branch for ongoing development.
- `feature/*` – individual feature and task development, branched from and merged into `develop`.
- `release/*` – preparation of new releases.
- `hotfix/*` – urgent fixes for released versions.

All new development work is integrated into `develop` through Pull Requests and code review.

### GitFlow Adoption

During the initial phase of Sprint 1, the team used feature branches and Pull Requests targeting `main`.

On **October 19, 2026**, following the clarification of the required branching methodology, the team transitioned to classic GitFlow. The `develop` branch was created from the existing `main` branch, configured as the default branch, and the updated workflow was communicated to all team members.

Previous Pull Requests and commits have been preserved to maintain a transparent project history. All subsequent development follows the GitFlow branching model.

For detailed instructions, see [GitFlow Guide](GITFLOW_GUIDE.md).
