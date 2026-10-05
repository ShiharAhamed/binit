using FluentAssertions;
using SmartWaste.Application.DTOs.Auth;
using SmartWaste.Application.Validators;

namespace SmartWaste.Tests;

public class AuthValidationTests
{
    private readonly RegisterRequestValidator _registerValidator = new();
    private readonly LoginRequestValidator _loginValidator = new();

    [Theory]
    [InlineData("", "valid@example.com", "0771234567", "Password123!", "Full name")]
    [InlineData("Valid Name", "", "0771234567", "Password123!", "Email")]
    [InlineData("Valid Name", "invalid-email", "0771234567", "Password123!", "valid email")]
    [InlineData("Valid Name", "valid@example.com", "", "Password123!", "Phone number")]
    [InlineData("Valid Name", "valid@example.com", "0771234567", "short", "at least 8 characters")]
    [InlineData("Valid Name", "valid@example.com", "0771234567", "nodigitshere!", "digit")]
    [InlineData("Valid Name", "valid@example.com", "0771234567", "NOLOWERCASE123!", "lowercase")]
    [InlineData("Valid Name", "valid@example.com", "0771234567", "nouppercase123!", "uppercase")]
    public void RegisterRequest_InvalidData_ShouldFailValidation(
        string fullName,
        string email,
        string phone,
        string password,
        string expectedErrorSubstring)
    {
        var model = new RegisterRequest
        {
            FullName = fullName,
            Email = email,
            PhoneNumber = phone,
            Password = password
        };

        var result = _registerValidator.Validate(model);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains(expectedErrorSubstring, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RegisterRequest_ValidData_ShouldPassValidation()
    {
        var model = new RegisterRequest
        {
            FullName = "Kamal Silva",
            Email = "kamal@example.com",
            PhoneNumber = "+94771234567",
            Password = "Password123!"
        };

        var result = _registerValidator.Validate(model);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Password123!")]
    [InlineData("invalid-email", "Password123!")]
    [InlineData("valid@example.com", "")]
    public void LoginRequest_InvalidData_ShouldFailValidation(string email, string password)
    {
        var model = new LoginRequest
        {
            Email = email,
            Password = password
        };

        var result = _loginValidator.Validate(model);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void LoginRequest_ValidData_ShouldPassValidation()
    {
        var model = new LoginRequest
        {
            Email = "user@example.com",
            Password = "Password123!"
        };

        var result = _loginValidator.Validate(model);

        result.IsValid.Should().BeTrue();
    }
}
