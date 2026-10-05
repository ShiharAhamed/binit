using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartWaste.Api.Authentication.InternalService;
using SmartWaste.Api.Controllers.Internal;
using SmartWaste.Application.Collection.DTOs.Responses;
using SmartWaste.Application.Collection.Interfaces;
using SmartWaste.Application.Collection.Queries;
using SmartWaste.Application.Common.Models;
using Xunit;

namespace SmartWaste.Tests.Collection.Controllers;

public class AiToolsCollectionNeedsControllerUnitTests
{
    private readonly Mock<ICollectionNeedService> _service = new();

    private AiToolsCollectionNeedsController CreateController()
    {
        return new AiToolsCollectionNeedsController(_service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public void Controller_RequiresTheExistingInternalServicePolicy()
    {
        var authorization = typeof(AiToolsCollectionNeedsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Should()
            .ContainSingle()
            .Subject;

        authorization.Policy.Should().Be(InternalServiceDefaults.PolicyName);
    }

    [Fact]
    public async Task GetCollectionNeeds_ValidQuery_ReturnsTheSanitizedPagedResult()
    {
        var query = new GetCollectionNeedsForAiQuery
        {
            TargetType = "Bin",
            CollectionReason = "FullOrBlockedBin",
            TargetDate = new DateOnly(2026, 1, 5),
            Page = 1,
            PageSize = 20
        };
        var expected = new PagedResult<CollectionNeedToolItemDto>
        {
            Items = new List<CollectionNeedToolItemDto>(),
            Page = 1,
            PageSize = 20,
            TotalCount = 0
        };
        _service.Setup(service => service.GetCollectionNeedsForAiAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await CreateController().GetCollectionNeeds(query, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(expected);
        _service.Verify(service => service.GetCollectionNeedsForAiAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCollectionNeeds_InvalidPageSize_ReturnsExistingValidationProblemWithoutCallingTheService()
    {
        var result = await CreateController().GetCollectionNeeds(
            new GetCollectionNeedsForAiQuery { PageSize = 51 },
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeOfType<ValidationProblemDetails>().Which.Status.Should().Be(StatusCodes.Status400BadRequest);
        _service.Verify(service => service.GetCollectionNeedsForAiAsync(
            It.IsAny<GetCollectionNeedsForAiQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
