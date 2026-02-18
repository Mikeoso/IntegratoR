using System.Linq.Expressions;
using FluentAssertions;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Common.CQRS.Queries;
using IntegratoR.Application.Features.Common.Validators;
using IntegratoR.TestKit.Doubles.Entities;
using Xunit;

namespace IntegratoR.Application.Tests.Features.Common.Validators;

/// <summary>
/// Tests for all 8 generic validator classes in the Application layer.
/// </summary>
public class ValidatorTests
{
    // -------------------------------------------------------------------------
    // CreateCommandValidator<T>
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateCommandValidator_Validate_ValidEntity_NoErrors()
    {
        // Arrange
        var validator = new CreateCommandValidator<TestEntity>();
        var entity = new TestEntity { Id = "1", PartitionKey = "pk", Name = "Test" };
        var command = new CreateCommand<TestEntity>(entity);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateCommandValidator_Validate_NullEntity_HasError()
    {
        // Arrange
        var validator = new CreateCommandValidator<TestEntity>();
        var command = new CreateCommand<TestEntity>(null!);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Entity");
    }

    // -------------------------------------------------------------------------
    // CreateBatchCommandValidator<T>
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateBatchCommandValidator_Validate_ValidEntities_NoErrors()
    {
        // Arrange
        var validator = new CreateBatchCommandValidator<TestEntity>();
        var entities = new List<TestEntity>
        {
            new() { Id = "1", PartitionKey = "pk", Name = "Test" }
        };
        var command = new CreateBatchCommand<TestEntity>(entities);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateBatchCommandValidator_Validate_NullEntities_HasError()
    {
        // Arrange
        var validator = new CreateBatchCommandValidator<TestEntity>();
        var command = new CreateBatchCommand<TestEntity>(null!);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Entities");
    }

    [Fact]
    public void CreateBatchCommandValidator_Validate_EmptyEntities_HasError()
    {
        // Arrange
        var validator = new CreateBatchCommandValidator<TestEntity>();
        var command = new CreateBatchCommand<TestEntity>(Enumerable.Empty<TestEntity>());

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    // -------------------------------------------------------------------------
    // UpdateCommandValidator<T>
    // -------------------------------------------------------------------------

    [Fact]
    public void UpdateCommandValidator_Validate_ValidEntity_NoErrors()
    {
        // Arrange
        var validator = new UpdateCommandValidator<TestEntity>();
        var entity = new TestEntity { Id = "1", PartitionKey = "pk", Name = "Updated" };
        var command = new UpdateCommand<TestEntity>(entity);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateCommandValidator_Validate_NullEntity_HasError()
    {
        // Arrange
        var validator = new UpdateCommandValidator<TestEntity>();
        var command = new UpdateCommand<TestEntity>(null!);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Entity");
    }

    // -------------------------------------------------------------------------
    // UpdateBatchCommandValidator<T>
    // -------------------------------------------------------------------------

    [Fact]
    public void UpdateBatchCommandValidator_Validate_ValidEntities_NoErrors()
    {
        // Arrange
        var validator = new UpdateBatchCommandValidator<TestEntity>();
        var entities = new List<TestEntity>
        {
            new() { Id = "1", PartitionKey = "pk", Name = "Updated" }
        };
        var command = new UpdateBatchCommand<TestEntity>(entities);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateBatchCommandValidator_Validate_NullEntities_HasError()
    {
        // Arrange
        var validator = new UpdateBatchCommandValidator<TestEntity>();
        var command = new UpdateBatchCommand<TestEntity>(null!);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Entities");
    }

    [Fact]
    public void UpdateBatchCommandValidator_Validate_EmptyEntities_HasError()
    {
        // Arrange
        var validator = new UpdateBatchCommandValidator<TestEntity>();
        var command = new UpdateBatchCommand<TestEntity>(Enumerable.Empty<TestEntity>());

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    // -------------------------------------------------------------------------
    // DeleteCommandValidator<T>
    // -------------------------------------------------------------------------

    [Fact]
    public void DeleteCommandValidator_Validate_ValidEntity_NoErrors()
    {
        // Arrange
        var validator = new DeleteCommandValidator<TestEntity>();
        var entity = new TestEntity { Id = "1", PartitionKey = "pk", Name = "ToDelete" };
        var command = new DeleteCommand<TestEntity>(entity);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void DeleteCommandValidator_Validate_NullEntity_HasError()
    {
        // Arrange
        var validator = new DeleteCommandValidator<TestEntity>();
        var command = new DeleteCommand<TestEntity>(null!);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Entity");
    }

    // -------------------------------------------------------------------------
    // DeleteBatchCommandValidator<T>
    // -------------------------------------------------------------------------

    [Fact]
    public void DeleteBatchCommandValidator_Validate_ValidEntities_NoErrors()
    {
        // Arrange
        var validator = new DeleteBatchCommandValidator<TestEntity>();
        var entities = new List<TestEntity>
        {
            new() { Id = "1", PartitionKey = "pk", Name = "ToDelete" }
        };
        var command = new DeleteBatchCommand<TestEntity>(entities);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void DeleteBatchCommandValidator_Validate_NullEntities_HasError()
    {
        // Arrange
        var validator = new DeleteBatchCommandValidator<TestEntity>();
        var command = new DeleteBatchCommand<TestEntity>(null!);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Entities");
    }

    [Fact]
    public void DeleteBatchCommandValidator_Validate_EmptyEntities_HasError()
    {
        // Arrange
        var validator = new DeleteBatchCommandValidator<TestEntity>();
        var command = new DeleteBatchCommand<TestEntity>(Enumerable.Empty<TestEntity>());

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    // -------------------------------------------------------------------------
    // GetByKeyQueryValidator<T>
    // -------------------------------------------------------------------------

    [Fact]
    public void GetByKeyQueryValidator_Validate_ValidKey_NoErrors()
    {
        // Arrange
        var validator = new GetByKeyQueryValidator<TestEntity>();
        var query = new GetByKeyQuery<TestEntity>(new object[] { "1", "pk" });

        // Act
        var result = validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GetByKeyQueryValidator_Validate_NullKey_HasError()
    {
        // Arrange
        var validator = new GetByKeyQueryValidator<TestEntity>();
        var query = new GetByKeyQuery<TestEntity>(null!);

        // Act
        var result = validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "CompositeKey");
    }

    [Fact]
    public void GetByKeyQueryValidator_Validate_EmptyKey_HasError()
    {
        // Arrange
        var validator = new GetByKeyQueryValidator<TestEntity>();
        var query = new GetByKeyQuery<TestEntity>(Array.Empty<object>());

        // Act
        var result = validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void GetByKeyQueryValidator_Validate_KeyWithNullElement_HasError()
    {
        // Arrange
        var validator = new GetByKeyQueryValidator<TestEntity>();
        var query = new GetByKeyQuery<TestEntity>(new object[] { "valid", null! });

        // Act
        var result = validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    // -------------------------------------------------------------------------
    // GetByFilterQueryValidator<T>
    // -------------------------------------------------------------------------

    [Fact]
    public void GetByFilterQueryValidator_Validate_ValidFilter_NoErrors()
    {
        // Arrange
        var validator = new GetByFilterQueryValidator<TestEntity>();
        Expression<Func<TestEntity, bool>> filter = e => e.Id == "1";
        var query = new GetByFilterQuery<TestEntity>(filter);

        // Act
        var result = validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GetByFilterQueryValidator_Validate_NullFilter_HasError()
    {
        // Arrange
        var validator = new GetByFilterQueryValidator<TestEntity>();
        var query = new GetByFilterQuery<TestEntity>(null!);

        // Act
        var result = validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Filter");
    }
}
