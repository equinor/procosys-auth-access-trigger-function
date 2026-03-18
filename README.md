# procosys-auth-access-trigger-function

Azure Function App that syncs AAD group memberships to ProCoSys access control via Service Bus triggers.

## CI/CD

CI workflows run on every pull request:

- **Build & run tests** — builds the solution and runs all unit tests
- **Verify formatting** — checks code formatting for all projects
- **Verify PR title** — enforces [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) in PR titles

Deployment workflows:

| Environment | Trigger | Workflow |
|---|---|---|
| `dev` | Pull request + manual | `deploy-dev.yml` |
| `test` | Push to `main` + manual | `deploy-test.yml` |
| `prod` | Push to `main` (automatic) | `deploy-prod.yml` |
| `prod` (rollback) | Manual with ref input | `deploy-prod-rollback.yml` |

> **Important**: Merging to `main` automatically deploys to both **test** and **prod**. There is no manual gate for production — ensure changes are thoroughly reviewed before merging.

## PR and Production Flow

1. Open a PR against `main`
2. CI checks run automatically; dev environment is deployed for validation
3. After review and approval, merge the PR
4. On merge, the app is automatically deployed to **test** and **prod**

## Manual Deployment

- **Dev / Test**: Trigger `deploy-dev.yml` or `deploy-test.yml` via `workflow_dispatch` in the Actions tab
- **Prod rollback**: Trigger `deploy-prod-rollback.yml` with a branch name, commit SHA, or relative ref (e.g., `HEAD~1`)
