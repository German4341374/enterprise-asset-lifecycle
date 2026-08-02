## Summary

Describe the change and the operational problem it solves.

## Verification

- [ ] `dotnet format EnterpriseAssetLifecycle.slnx --verify-no-changes`
- [ ] `dotnet build EnterpriseAssetLifecycle.slnx --configuration Release`
- [ ] Unit tests pass
- [ ] Testcontainers integration tests pass when database behavior changed
- [ ] Docker image builds when runtime behavior changed

## Database and lifecycle impact

Describe migrations, state transitions, concurrency behavior, rollback steps, and retention implications. Write `None` where not applicable.

## Security

- [ ] No secrets or personal data are included
- [ ] Inputs are validated and logs contain no sensitive payloads

