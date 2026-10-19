# LIZO-ResortEmpire

A 2D isometric mobile resort management game developed by **LIZO Project**.

## Project Information

- **Project:** Resort Empire
- **Team:** LIZO Project
- **Game Engine:** Unity 6.3 LTS (6000.3.26f1)
- **Target Platforms:** Android and iOS
- **Orientation:** Landscape
- **Development Status:** Sprint 1 – Project Setup

## Getting Started

1. Clone the repository.
2. Open Unity Hub.
3. Select **Add project from disk** and choose the cloned project folder.
4. Install or select Unity Editor version `6000.3.26f1`.
5. Open the project in Unity.

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
