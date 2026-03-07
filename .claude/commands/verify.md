Build and test the solution, then report results.

$ARGUMENTS

## Steps

1. Run `dotnet build`. Report any errors.

2. If the build succeeds, run tests:
   - If $ARGUMENTS specifies a test project, run `dotnet test $ARGUMENTS`.
   - Otherwise, run `dotnet test` for all tests.

3. Report a clear summary:
   - Build: PASS or FAIL (with error count)
   - Tests: PASS or FAIL (with pass/fail/skip counts)
