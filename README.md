# 🚀 IntegratoR

**Enterprise integrations for D365 F&O — Robust, Extensible, and Accelerated.**

`IntegratoR` is an opinionated .NET framework designed to standardize and accelerate the development of complex, reliable, and maintainable integration solutions built on Azure Functions. It is specifically tailored to address the challenges of integrating with **Microsoft Dynamics 365 Finance & Operations** and other enterprise backend systems.

This framework is more than just a collection of utilities; it's a complete architectural blueprint based on modern best practices. It empowers developers to focus on business logic instead of reinventing the wheel for foundational infrastructure.

---
## ✨ Key Features

- **Clean Architecture:** Enforces a strict separation of concerns, ensuring maximum **testability**, **maintainability**, and **reusability** of your business logic.
- **CQRS & MediatR:** Implements a clear segregation between read (**Queries**) and write (**Commands**) operations, leading to a highly organized and scalable codebase.
- **Robust Error Handling:** Utilizes a built-in `Result` pattern to handle expected failures gracefully, eliminating exceptions for predictable outcomes (e.g., "Not Found") and ensuring stable error handling.
- **Extensible by Design:**
    - **Entities:** Standard entities (e.g., for F&O) can be easily extended with project-specific fields through simple **inheritance**.
    - **Logic:** Standard Commands and Queries are built with **generics**, allowing them to be reused with extended entities without writing boilerplate code.
- **Flexible Data Access:** A generic **repository pattern** with a powerful and highly configurable implementation for **OData (F&O)** and a clear architecture for connecting to any other system.
- **Modern Authentication:** Features a configurable, per-client authentication pipeline (supporting **OAuth & API Key**) using `HttpMessageHandlers` to securely communicate with diverse endpoints.
- **Cross-Cutting Concerns via Pipeline:** Provides out-of-the-box support for:
    - **Logging:** Structured logging for every processed Command and Query.
    - **Validation:** Automatic request validation using `FluentValidation`.
    - **Caching:** Optional caching of query results to boost performance.

---
## 🚀 Getting Started: A Practical Example

The framework encapsulates complexity, making its application simple and clean. Here is a realistic example of how to create a new `LedgerJournalHeader` within an Activity Function.

```csharp
/*
 * This activity function is responsible for creating a new journal header in F&O.
 * It demonstrates how developers interact with the framework by focusing purely
 * on the business data, not the integration complexity.
*/
[Function(nameof(CreateJournalHeaderActivity))]
public async Task<Result<LedgerJournalHeader>> CreateJournalHeaderActivity([ActivityTrigger] string company)
{
    _logger.LogInformation("Creating journal header for company {Company}...", company);

    // 1. Assemble the business entity.
    // The developer creates a standard, strongly-typed F&O entity object.
    // There's no need to manually handle JSON or HTTP clients.
    var newLedgerJournalHeader = new LedgerJournalHeader
    {
        DataAreaId = company,
        JournalName = "SomeJournal",
        Description = $"Imported on - {DateTime.UtcNow:yyyy-MM-dd HH:mm}"
    };

    // 2. Wrap the entity in a generic command.
    // This command signals the intent to create a record.
    // We use the generic CreateCommand<TEntity> provided by the framework.
    var command = new CreateLedgerJournalHeaderCommand<LedgerJournalHeader>(newLedgerJournalHeader);

    // 3. Send the command through the MediatR pipeline.
    // THIS IS THE CORE OF THE FRAMEWORK'S POWER.
    // When Send() is called, the request automatically flows through:
    //      - LoggingBehaviour (entry/exit logs)
    //      - ValidationBehaviour (if a validator is defined)
    //      - CachingBehaviour (invalidates related cache entries)
    //      - The specific CreateLedgerJournalHeaderHandler
    // The handler, in turn, uses the generic repository to perform the OData call,
    // which is automatically authenticated by the ODataAuthenticationHandler.
    var createHeaderResult = await _mediator.Send(command);

    // 4. Handle the robust Result object.
    // No try-catch blocks are needed for predictable outcomes like validation errors
    // or records not being found. The Result object makes this flow explicit and safe.
    if (createHeaderResult.IsFailure)
    {
        _logger.LogError(
            "Failed to create journal header for company {Company}: {Error}", 
            company, 
            createHeaderResult.Error);
    }
    else 
    {
        _logger.LogInformation(
            "Successfully created journal header with Batch Number: {BatchNumber}", 
            createHeaderResult.Value?.JournalBatchNumber);
    }
    
    return createHeaderResult;
}