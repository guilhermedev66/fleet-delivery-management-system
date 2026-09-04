using FleetDelivery.BuildingBlocks.Results;
using FluentAssertions;

namespace FleetDelivery.UnitTests.Results;

// Exercises BuildingBlocks' Result/Error pattern. Doubles as a smoke test
// that the UnitTests project is correctly wired to BuildingBlocks (and,
// via FluentAssertions, to the Api project) — real module test suites
// start landing in M1.
public class ResultTests
{
    [Fact]
    public void Success_returns_success_result_with_no_error()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_returns_failure_result_with_error()
    {
        var error = new Error("Test.Failure", "Something went wrong.");

        var result = Result.Failure(error);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Generic_success_carries_value()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Accessing_value_of_failed_result_throws()
    {
        var result = Result.Failure<int>(new Error("Test.Failure", "Something went wrong."));

        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>();
    }
}
