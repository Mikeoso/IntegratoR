# Testing

## Project Structure

- Mirror the source project structure in the test project
- Place test classes in the same namespace path as the code they test
- One test class per production class

## Test Structure

- Follow the **Arrange-Act-Assert** pattern in every test
- Keep each section clearly separated with blank lines or comments
- Each test should verify exactly one behaviour

## Naming Convention

Use the pattern: `MethodName_Scenario_ExpectedResult`

Examples:
- `Handle_ValidCommand_ReturnsSuccessResult`
- `Handle_MissingEntity_ReturnsNotFoundError`
- `Validate_EmptyName_HasValidationError`

## Coverage Guidelines

- Cover the happy path and all meaningful error paths
- Mock only direct dependencies — do not mock the system under test
- Test edge cases: null inputs, empty collections, boundary values
- For async code, test both success and cancellation paths

## Test Independence

- Tests must not depend on execution order
- Tests must not share mutable state
- Each test should set up its own fixtures
